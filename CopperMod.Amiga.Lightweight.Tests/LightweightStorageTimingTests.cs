using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightStorageTimingTests
{
    private sealed class SectorObserver : ILightweightStorageObserver
    {
        internal ulong LastSector;
        internal int Fetches;
        public void ReadCommand(bool ide, ulong sector, uint bytes, long cycle) { }
        public void MediaRead(ulong sector, long cycle) { LastSector = sector; Fetches++; }
        public void PioRead(int bytes, long cycle) { }
        public void Completed(bool ide, long cycle) { }
    }

    [Fact]
    public void ReturningToCachedDataRedirectsPrefetchAndSkipsProtectedCachedSuccessors()
    {
        using var media = new Media(); using var backing = AmigaHardfile.Open(media.Configuration);
        var observer = new SectorObserver();
        var drive = new LightweightStorageDrive(backing, LightweightDriveTimingProfile.Hdd with { ReadAheadBytes = 2048 }, observer);
        Span<byte> bytes = stackalloc byte[512];
        drive.Request(10, 0); var cycle = drive.NextCycle; drive.Step(cycle); drive.CopySector(bytes);
        while (drive.NextCycle != long.MaxValue) { cycle = drive.NextCycle; drive.Step(cycle); }
        drive.Request(1000, cycle); cycle = drive.NextCycle; drive.Step(cycle); drive.CopySector(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        drive.Request(12, cycle); var ready = drive.Ready; drive.CopySector(bytes);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, allocated); Assert.True(ready); Assert.Equal(12, bytes[0]);
        var next = drive.NextCycle; Assert.NotEqual(long.MaxValue, next);
        var fetches = observer.Fetches; drive.Step(next - 1); Assert.Equal(fetches, observer.Fetches);
        drive.Step(next); Assert.Equal(15u, observer.LastSector);
        foreach (var sector in new ulong[] { 13, 14 })
        { drive.Request(sector, next); Assert.True(drive.Ready); drive.CopySector(bytes); Assert.Equal((byte)sector, bytes[0]); }
    }
    private sealed class Media : IDisposable
    {
        internal readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "copper-storage-" + Guid.NewGuid().ToString("N") + ".hdf");
        internal AmigaHardfileConfiguration Configuration => new(0, Path, readOnly: true) { ExposeBootNode = false };
        internal Media()
        {
            var bytes = new byte[1024 * 1024];
            for (var sector = 0; sector < bytes.Length / 512; sector++) bytes.AsSpan(sector * 512, 512).Fill((byte)sector);
            File.WriteAllBytes(Path, bytes);
        }
        public void Dispose() => File.Delete(Path);
    }

    [Fact]
    public void RotationalEdgesDoNotLoseATurnWhenRoundedToMachineCycles()
    {
        using var media = new Media(); using var backing = AmigaHardfile.Open(media.Configuration);
        var drive = new LightweightStorageDrive(backing, LightweightDriveTimingProfile.Hdd with
        { Heads = 1, SectorsPerTrack = 4, RevolutionsPerMinute = 60, TrackSkewSectors = 0 }, null);
        drive.Request(0, 0); Assert.False(drive.Ready);
        Assert.Equal((LightweightStorageDrive.ClockHz + 3) / 4, drive.NextCycle);
        var first = drive.NextCycle; drive.Step(first);
        Span<byte> data = stackalloc byte[512]; drive.CopySector(data); Assert.Equal(0, data[0]);
        drive.Request(1, first);
        Assert.Equal((LightweightStorageDrive.ClockHz + 1) / 2, drive.NextCycle);
        drive.Step(drive.NextCycle); drive.CopySector(data); Assert.Equal(1, data[0]);
    }

    [Fact]
    public void BackgroundReadAheadStopsAtCapacityAndProtectsTheRequestedSector()
    {
        using var media = new Media(); using var backing = AmigaHardfile.Open(media.Configuration);
        var drive = new LightweightStorageDrive(backing, LightweightDriveTimingProfile.Hdd with { ReadAheadBytes = 2048 }, null);
        drive.Request(20, 0); drive.Step(drive.NextCycle);
        var events = 0; long cycle = 0;
        while (drive.NextCycle != long.MaxValue && events++ < 20) { cycle = drive.NextCycle; drive.Step(cycle); }
        Assert.True(events <= 5); Assert.Equal(long.MaxValue, drive.NextCycle);
        Span<byte> data = stackalloc byte[512]; drive.CopySector(data); Assert.Equal(20, data[0]);
        drive.Request(21, cycle); Assert.True(drive.Ready); drive.CopySector(data); Assert.Equal(21, data[0]);
        Assert.NotEqual(long.MaxValue, drive.NextCycle);
    }

    [Fact]
    public void RedirectedStreamCanUseTheEntireReadAheadCache()
    {
        using var media=new Media(); using var backing=AmigaHardfile.Open(media.Configuration);
        var drive=new LightweightStorageDrive(backing,LightweightDriveTimingProfile.Hdd with { ReadAheadBytes=2048 },null);
        Span<byte> data=stackalloc byte[512]; long cycle=0;
        drive.Request(0,cycle); cycle=drive.NextCycle; drive.Step(cycle); drive.CopySector(data);
        while(drive.NextCycle!=long.MaxValue) { cycle=drive.NextCycle; drive.Step(cycle); }
        drive.Request(1000,cycle); cycle=drive.NextCycle; drive.Step(cycle);
        var prefetched=0;
        while(drive.NextCycle!=long.MaxValue && prefetched<8)
        { cycle=drive.NextCycle; drive.Step(cycle); prefetched++; }
        Assert.Equal(4,prefetched);
        // Speculative replacement cannot overwrite the unread foreground sector.
        drive.CopySector(data); Assert.Equal(unchecked((byte)1000),data[0]);
        for(ulong sector=1001;sector<=1004;sector++)
        { drive.Request(sector,cycle); Assert.True(drive.Ready); drive.CopySector(data); Assert.Equal((byte)sector,data[0]); }
    }

    [Fact]
    public void SolidStateMediaHasNoMechanicalDelayOrSteadyStateAllocations()
    {
        using var media = new Media(); using var backing = AmigaHardfile.Open(media.Configuration);
        var drive = new LightweightStorageDrive(backing, LightweightDriveTimingProfile.Cf, null);
        Span<byte> data = stackalloc byte[512]; long cycle = 0;
        for (var i = 0; i < 20; i++) { drive.Request((uint)i, cycle); Assert.True(drive.Ready); drive.CopySector(data); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) { drive.Request((uint)(i * 17 % 1024), cycle); drive.CopySector(data); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, cycle); Assert.Equal(long.MaxValue,drive.NextCycle);
    }

    [Theory]
    [InlineData(0x20)] [InlineData(0xC4)]
    public void AtaPioReadsRealSectorsAndOnlyDataStrobesConsumeThem(int command)
    {
        using var media = new Media(); using var ata = new LightweightAtaDrive(new(media.Configuration) { Timing = LightweightDriveTimingProfile.Cf }, null);
        long cycle = 0;
        void Ready() { for (var i = 0; i < 100 && (ata.ReadRegister(7, true, false) & 0x80) != 0; i++) { cycle = ata.NextCycle; ata.Step(cycle); } }
        ata.WriteRegister(6, 0xE0, cycle); ata.WriteRegister(2, 2, cycle); ata.WriteRegister(3, 7, cycle);
        ata.WriteRegister(7, (byte)command, cycle); Ready();
        Assert.True(ata.InterruptAsserted); Assert.Equal(0x58, ata.ReadRegister(7, true, false));
        Assert.True(ata.InterruptAsserted); ata.ReadRegister(7, false, true); Assert.False(ata.InterruptAsserted);
        for (var sector = 7; sector <= 8; sector++)
        {
            Assert.Equal((ushort)(sector * 0x101), ata.ReadData(cycle, false));
            for (var word = 0; word < 256; word++) Assert.Equal((ushort)(sector * 0x101), ata.ReadData(cycle++, true));
            Ready();
        }
        Assert.Equal(0x50, ata.ReadRegister(7, true, false)); Assert.Equal(0xFFFF, ata.ReadData(cycle, true));
    }

    [Fact]
    public void AtaErrorsResetSlaveAndInterruptMaskAreObservable()
    {
        using var media = new Media(); using var ata = new LightweightAtaDrive(new(media.Configuration) { Timing = LightweightDriveTimingProfile.Cf }, null);
        ata.WriteControl(2, 0); ata.WriteRegister(7, 0x30, 0); ata.Step(ata.NextCycle);
        Assert.Equal(0x51, ata.ReadRegister(7, true, false)); Assert.Equal(4, ata.ReadRegister(1, false, false)); Assert.False(ata.InterruptAsserted);
        ata.WriteControl(0, 1000); Assert.True(ata.InterruptAsserted);
        ata.WriteControl(4, 2000); Assert.Equal(0x80, ata.ReadRegister(7, true, false));
        ata.WriteControl(0, 2010); ata.Step(ata.NextCycle); Assert.Equal(0x50, ata.ReadRegister(7, true, false));
        ata.WriteRegister(6, 0xB0, 3000); Assert.Equal(0xFF, ata.ReadRegister(7, false, true));
    }

    [Fact]
    public void IdentifyUsesAtaByteOrderAndGayleFastRegistersAcknowledgeIrq()
    {
        using var media = new Media(); using var io = new LightweightA1200Io();
        io.Attach(new(media.Configuration) { Timing = LightweightDriveTimingProfile.Cf }, null);
        io.WriteByte(0xDAA000,0x80);
        io.WriteByte(0xDA2018, 0xA0); io.WriteByte(0xDA201C, 0xEC);
        Assert.Equal(0x80, io.ReadByte(0xDA2000)); // BSY before IDENTIFY is ready
        io.Step(io.NextCycle);
        Assert.Equal(0x80, io.ReadByte(0xDA9000));
        Assert.Equal(0x58, io.ReadByte(0xDA3018)); Assert.True(io.InterruptAsserted);
        // Wrong byte lane is floating and cannot acknowledge ATA INTRQ.
        Assert.Equal(0xFF, io.ReadByte(0xDA201D)); Assert.True(io.InterruptAsserted);
        Assert.Equal(0x58, io.ReadByte(0xDA201C)); Assert.True(io.InterruptAsserted);
        Assert.Equal(0,io.ReadByte(0xDA8000)); // live ATA line is now low
        io.WriteByte(0xDA9000,0x7C); Assert.False(io.InterruptAsserted);
        Assert.Equal(0xFF,io.ReadByte(0xDA401C)); // neither chip select
        Assert.Equal(0, io.ReadByte(0xDA2000));
        Assert.Equal(0, io.ReadByte(0xDA2001));
        Assert.Equal(0x4000, io.ReadWord(0xDA2000, 1000));
        Assert.Equal(0x0800, io.ReadWord(0xDA2000, 1000)); // eight cylinders
        io.ReadWord(0xDA2000, 1000);
        Assert.Equal(0x0400, io.ReadWord(0xDA2000, 1000));
        io.ReadWord(0xDA2000, 1000); io.ReadWord(0xDA2000, 1000);
        Assert.Equal(0x4000, io.ReadWord(0xDA2000, 1000));
        io.WriteByte(0xDA3018, 4, 1000); io.WriteByte(0xDA3018, 0, 1010); io.Step(io.NextCycle);
        io.WriteByte(0xDA000E, 3, 2000); // slow-bank sector-number lane
        Assert.Equal(3, io.ReadByte(0xDA200C));
        io.WriteByte(0xDAA000,0); io.WriteByte(0xDA201C,0xEC,3000); io.Step(io.NextCycle);
        Assert.False(io.InterruptAsserted); Assert.Equal(0x80,io.ReadByte(0xDA9000));
        io.WriteByte(0xDAA000,0x80); Assert.True(io.InterruptAsserted);
        io.WriteByte(0xDA9000,0x7C); Assert.False(io.InterruptAsserted);
        io.ReadByte(0xDA3018); Assert.False(io.InterruptAsserted); // ack before ATA status also works
        io.ReadByte(0xDA201C); Assert.False(io.InterruptAsserted);
    }

    [Fact]
    public void ChsReadMultipleDrainsEverySectorAndRejectsOutOfRange()
    {
        using var media = new Media(); using var ata = new LightweightAtaDrive(new(media.Configuration) { Timing = LightweightDriveTimingProfile.Cf }, null);
        long cycle = 0;
        void Ready() { while ((ata.ReadRegister(7, true, false) & 0x80) != 0) { cycle=ata.NextCycle; ata.Step(cycle); } }
        ata.WriteRegister(2,4,cycle); ata.WriteRegister(7,0xC6,cycle); Ready();
        ata.WriteRegister(6,0xA1,cycle); ata.WriteRegister(3,64,cycle); ata.WriteRegister(2,5,cycle); ata.WriteRegister(7,0xC4,cycle); Ready();
        // Cylinder 0, head 1, sector 64 is LBA 127; block crosses a track.
        for (var sector=127;sector<132;sector++)
        {
            for (var word=0;word<256;word++) Assert.Equal((ushort)(sector*0x101),ata.ReadData(cycle++,true));
            if (sector==130) { Assert.Equal(0x80,ata.ReadRegister(7,true,false)); Ready(); }
        }
        Assert.Equal(0x50,ata.ReadRegister(7,true,false)); Assert.Equal(0,ata.ReadRegister(2,false,false));
        ata.WriteRegister(3,0,cycle); ata.WriteRegister(2,1,cycle); ata.WriteRegister(7,0x20,cycle); Ready();
        Assert.Equal(0x51,ata.ReadRegister(7,true,false)); Assert.Equal(0x10,ata.ReadRegister(1,false,false));
    }

    [Fact]
    public void RandomRedirectCancelsPrefetchWithoutPublishingItsSector()
    {
        using var media = new Media(); using var backing = AmigaHardfile.Open(media.Configuration);
        var drive=new LightweightStorageDrive(backing,LightweightDriveTimingProfile.Hdd,null);
        Span<byte> data=stackalloc byte[512];
        drive.Request(10,0); var first=drive.NextCycle; drive.Step(first); drive.CopySector(data);
        var adjacent=drive.NextCycle;
        drive.Request(1600,first); var random=drive.NextCycle;
        Assert.True(random>adjacent);
        drive.CancelForeground(first+1); drive.Reset();
        Assert.False(drive.Ready); Assert.Equal(long.MaxValue,drive.NextCycle);
        drive.Request(10,0); Assert.Equal(first,drive.NextCycle);
    }

    [Fact]
    public void AtaCommandAndPioSteadyStateAllocateNothing()
    {
        using var media=new Media(); using var ata=new LightweightAtaDrive(new(media.Configuration) { Timing=LightweightDriveTimingProfile.Cf },null);
        long cycle=0; uint checksum=0;
        void Read(int sector)
        {
            ata.WriteRegister(6,0xE0,cycle); ata.WriteRegister(2,1,cycle); ata.WriteRegister(3,(byte)sector,cycle);
            ata.WriteRegister(7,0x20,cycle);
            while((ata.ReadRegister(7,true,false)&0x80)!=0) { cycle=ata.NextCycle; ata.Step(cycle); }
            ata.ReadRegister(7,false,true);
            for(var word=0;word<256;word++) checksum+=ata.ReadData(cycle++,true);
        }
        for(var i=0;i<20;i++) Read(i);
        var allocated=GC.GetAllocatedBytesForCurrentThread();
        for(var i=20;i<120;i++) Read(i);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-allocated); Assert.NotEqual(0u,checksum);
    }

    [Fact]
    public void MechanicalSeekCacheAndPrefetchSteadyStateAllocateNothing()
    {
        using var media=new Media(); using var backing=AmigaHardfile.Open(media.Configuration);
        var drive=new LightweightStorageDrive(backing,LightweightDriveTimingProfile.Hdd,null);
        var data=new byte[512]; long cycle=0;
        void Read(int index)
        {
            drive.Request((uint)(index*71%2048),cycle);
            while(!drive.Ready) { cycle=drive.NextCycle; drive.Step(cycle); }
            drive.CopySector(data);
        }
        for(var i=0;i<20;i++) Read(i);
        var allocated=GC.GetAllocatedBytesForCurrentThread();
        for(var i=20;i<120;i++) Read(i);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-allocated);
    }

    [Fact]
    public void AtaZeroSectorCountMeans256AndAdvancesTheTaskFile()
    {
        using var media=new Media(); using var ata=new LightweightAtaDrive(new(media.Configuration) { Timing=LightweightDriveTimingProfile.Cf },null);
        long cycle=0; ata.WriteRegister(6,0xE0,cycle); ata.WriteRegister(2,0,cycle); ata.WriteRegister(3,1,cycle); ata.WriteRegister(7,0x20,cycle);
        for(var sector=1;sector<=256;sector++)
        {
            while((ata.ReadRegister(7,true,false)&0x80)!=0) { cycle=ata.NextCycle; ata.Step(cycle); }
            Assert.Equal(0x58,ata.ReadRegister(7,false,true));
            for(var word=0;word<256;word++) Assert.Equal((ushort)((byte)sector*0x101),ata.ReadData(cycle++,true));
        }
        Assert.Equal(0x50,ata.ReadRegister(7,true,false)); Assert.Equal(0,ata.ReadRegister(2,false,false));
        Assert.Equal(1,ata.ReadRegister(3,false,false)); Assert.Equal(1,ata.ReadRegister(4,false,false));
    }
}
