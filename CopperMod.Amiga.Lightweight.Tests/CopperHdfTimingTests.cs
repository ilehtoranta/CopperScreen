using Copper68k;
using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed partial class CopperHdfControllerTests
{
    [Fact]
    public void DeferredCompletionServerLoadsSysBaseAndPreservesServerRegisters()
    {
        var path=CreateTempPath();
        try
        {
            File.WriteAllBytes(path,new byte[32768]);
            using var machine=new LightweightA500Machine(new()
            {
                Hardfiles=[new(0,path,readOnly:true)],
                HardfileTimings=new Dictionary<int,LightweightDriveTimingProfile> { [0]=LightweightDriveTimingProfile.Cf }
            });
            var rom=new byte[256*1024]; WriteUInt32(rom,0,0x7000); WriteUInt32(rom,4,0x6000); machine.LoadKickstart(rom);
            var bus=machine.HardfileBus!; SupplyPublicMemory(bus); var controller=bus.CopperHdf;
            var request=new M68kCpuState(); request.A[1]=0x5000; request.A[7]=0x7000;
            bus.WriteWord(0x501C,2); bus.WriteByte(0x501E,1); bus.WriteLong(0x5024,512); bus.WriteLong(0x5028,0x12000);
            var begin=controller.DeviceBase-30; bus.TryInvokeHostGateway(begin,bus.ReadLong(begin+2),request);
            while(!controller.TimedInterrupt) machine.AdvanceHardwareTo(controller.NextTimedCycle);
            bus.WriteLong(4,0x1000);
            // ReplyMsg fixture counts calls. The server must find SysBase itself:
            // Exec interrupt servers receive A6 as scratch, not a library base.
            bus.WriteWord(0x1000-378,0x5279); bus.WriteLong(0x1000-376,0x7100); bus.WriteWord(0x1000-372,0x4E75);
            bus.CopyToMemory(0x6200,LightweightStorageCompletionCode.Image);
            bus.WriteLong(0x7200,controller.ConfiguredBase);
            ushort[] entry=[0x13FC,2,0x00BF,0xE001,0x227C,0,0x7200,0x4EB9,0,0x6200,0x4E72,0x2700];
            for(var i=0;i<entry.Length;i++) bus.WriteWord(0x6000u+(uint)i*2,entry[i]);
            machine.Cpu.A[2]=0x12345678; machine.Cpu.A[5]=0x23456780; machine.Cpu.A[6]=0x34567890;
            machine.Cpu.D[2]=0x45678901;
            machine.ExecuteFrame();
            Assert.True(machine.Cpu.Stopped); Assert.Equal(1,bus.ReadWord(0x7100)); Assert.False(controller.TimedInterrupt);
            Assert.Equal(0x12345678u,machine.Cpu.A[2]); Assert.Equal(0x23456780u,machine.Cpu.A[5]);
            Assert.Equal(0x34567890u,machine.Cpu.A[6]); Assert.Equal(0x45678901u,machine.Cpu.D[2]);
        }
        finally { DeleteTemp(path); }
    }

    [Fact]
    public void ReusedChangeRegistrationRemainsAPendingMessage()
    {
        var path=CreateTempPath();
        try
        {
            File.WriteAllBytes(path,new byte[32768]);
            using var machine=new LightweightA500Machine(new() { Hardfiles=[new(0,path,readOnly:true)] });
            var bus=machine.HardfileBus!; SupplyPublicMemory(bus);
            bus.WriteByte(0x5008,7); bus.WriteByte(0x501E,1); bus.WriteWord(0x501C,20);
            Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus,0x5000));
            Assert.Equal(5,bus.ReadByte(0x5008)); Assert.Equal(0,bus.ReadByte(0x501E)&1);
        }
        finally { DeleteTemp(path); }
    }

    private static void SupplyPublicMemory(LightweightHdfBus bus)
    {
        bus.WriteLong(0x1142,0x8000); bus.WriteLong(0x8000,0x1146); bus.WriteWord(0x800E,1);
        bus.WriteLong(0x8010,0x10000); bus.WriteLong(0x8014,0x10000); bus.WriteLong(0x8018,0x70000);
        bus.WriteLong(0x801C,0x60000); bus.WriteLong(0x10000,0); bus.WriteLong(0x10004,0x60000);
        InvokeBootstrap(bus);
    }

    [Fact]
    public void StorageDeadlinesMatchScalarClockAcrossFramesAndWhileStopped()
    {
        var path=CreateTempPath();
        try
        {
            var bytes=new byte[32768]; bytes.AsSpan(512,512).Fill(0x39); File.WriteAllBytes(path,bytes);
            LightweightA500Machine Create() => new(new()
            {
                Hardfiles=[new(0,path,readOnly:true)], HardfileTimings=new Dictionary<int,LightweightDriveTimingProfile>
                { [0]=LightweightDriveTimingProfile.Cf with { CommandMicroseconds=25001 } }
            });
            using var scalar=Create(); using var batched=Create();
            void Submit(LightweightA500Machine machine)
            {
                var bus=machine.HardfileBus!; SupplyPublicMemory(bus);
                var state=new M68kCpuState(); state.A[1]=0x5000; state.A[7]=0x7000;
                bus.WriteWord(0x501C,2); bus.WriteByte(0x501E,1); bus.WriteLong(0x5024,512); bus.WriteLong(0x5028,0x12000); bus.WriteLong(0x502C,512);
                var begin=bus.CopperHdf.DeviceBase-30; bus.TryInvokeHostGateway(begin,bus.ReadLong(begin+2),state);
                machine.Cpu.Stopped=true;
                machine.WriteCustomRegisterFromCopper(0x09A,0xC028,machine.Cycle); // master, VBL and PORTS
            }
            Submit(scalar); Submit(batched);
            var first=batched.HardfileBus!.CopperHdf.NextTimedCycle;
            Assert.True(first>LightweightClock.PalLongFieldCycles);
            // Before the first odd deadline no payload or completion is visible.
            for(long cycle=1;cycle<first;cycle++) scalar.AdvanceHardwareTo(cycle);
            batched.AdvanceHardwareTo(first-1);
            Assert.Equal(0u,batched.HardfileBus.ReadLong(0x5020));
            var deadline=first;
            while(!batched.HardfileBus.CopperHdf.TimedInterrupt)
            {
                batched.AdvanceHardwareTo(deadline);
                while(scalar.Cycle<deadline) scalar.AdvanceHardwareTo(scalar.Cycle+1);
                deadline=batched.HardfileBus.CopperHdf.NextTimedCycle;
            }
            Assert.True(scalar.HardfileBus!.CopperHdf.TimedInterrupt);
            Assert.Equal(scalar.Cycle,batched.Cycle); Assert.Equal(512u,batched.HardfileBus.ReadLong(0x5020));
            Assert.Equal(0x39,batched.HardfileBus.ReadByte(0x12000));
            Assert.Equal(scalar.GetCustomRegister(0x01E),batched.GetCustomRegister(0x01E));
            Assert.Equal(0x28,batched.GetCustomRegister(0x01E)&0x28); // VBL and storage both survive
            Assert.True(batched.Cpu.Stopped); // hardware completes independently of instruction execution
        }
        finally { DeleteTemp(path); }
    }

    private sealed class PrefixFailure : IAmigaHardfileIoHook
    {
        internal bool Enabled;
        public bool FlushOnDispose=>false;
        public void Read(FileStream stream,long offset,Span<byte> destination)
        { if(Enabled && offset>=512) throw new IOException("Synthetic medium fault."); stream.Position=offset; stream.ReadExactly(destination); }
        public void Write(FileStream stream,long offset,ReadOnlySpan<byte> source)=>throw new InvalidOperationException();
        public void Flush(FileStream stream) { }
    }

    [Fact]
    public void TimingIsPerUnitAndLeavesAnUnconfiguredUnitSynchronous()
    {
        var path=CreateTempPath();
        try
        {
            var image=new byte[32768]; image.AsSpan(512,512).Fill(0x41); image.AsSpan(1024,512).Fill(0x42); File.WriteAllBytes(path,image);
            using var machine=new LightweightA500Machine(new()
            {
                Hardfiles=[new(0,path,readOnly:true),new(1,path,readOnly:true)],
                HardfileTimings=new Dictionary<int,LightweightDriveTimingProfile> { [0]=LightweightDriveTimingProfile.Cf }
            });
            var bus=machine.HardfileBus!; SupplyPublicMemory(bus); var controller=bus.CopperHdf;
            for(uint unit=0;unit<2;unit++)
            {
                var request=0x5000+unit*256; var state=new M68kCpuState(); state.A[1]=request; state.A[7]=0x7000; state.D[0]=unit;
                var open=controller.DeviceBase-6; bus.TryInvokeHostGateway(open,bus.ReadLong(open+2),state); Assert.Equal(0u,state.D[0]);
                bus.WriteWord(request+28,2); bus.WriteByte(request+30,1); bus.WriteLong(request+36,512);
                bus.WriteLong(request+40,0x12000+unit*512); bus.WriteLong(request+44,(unit+1)*512);
                var begin=controller.DeviceBase-30; bus.TryInvokeHostGateway(begin,bus.ReadLong(begin+2),state);
            }
            Assert.Equal(0,bus.ReadByte(0x12000)); Assert.Equal(0x42,bus.ReadByte(0x12200));
            Assert.Equal(0,bus.ReadByte(0x501E)); Assert.Equal(1,bus.ReadByte(0x511E)); Assert.Equal(512u,bus.ReadLong(0x5120));
            while(!controller.TimedInterrupt) machine.AdvanceHardwareTo(controller.NextTimedCycle);
            Assert.Equal(0x41,bus.ReadByte(0x12000)); Assert.Equal(512u,bus.ReadLong(0x5020));
        }
        finally { DeleteTemp(path); }
    }

    [Fact]
    public void TimedTdRead64PreservesSectorBitsAbove32()
    {
        var path=CreateTempPath();
        try
        {
            const long offset=(1L<<32)*512;
            using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
            {
                Assert.True(AmigaSparseFile.TryEnable(file),"This wide-address fixture requires sparse backing storage.");
                file.SetLength(offset+512); file.Position=offset;
                var bytes=new byte[512]; Array.Fill(bytes,(byte)0x6E); file.Write(bytes);
            }
            using var machine=new LightweightA500Machine(new()
            {
                Hardfiles=[new(0,path,readOnly:true) { ExposeBootNode=false }],
                HardfileTimings=new Dictionary<int,LightweightDriveTimingProfile> { [0]=LightweightDriveTimingProfile.Cf }
            });
            var bus=machine.HardfileBus!; SupplyPublicMemory(bus); var controller=bus.CopperHdf;
            var state=new M68kCpuState(); state.A[1]=0x5000; state.A[7]=0x7000;
            bus.WriteWord(0x501C,24); bus.WriteByte(0x501E,1); bus.WriteLong(0x5020,(uint)(offset>>32));
            bus.WriteLong(0x5024,512); bus.WriteLong(0x5028,0x12000); bus.WriteLong(0x502C,(uint)(offset&uint.MaxValue));
            var begin=controller.DeviceBase-30; bus.TryInvokeHostGateway(begin,bus.ReadLong(begin+2),state);
            while(!controller.TimedInterrupt) machine.AdvanceHardwareTo(controller.NextTimedCycle);
            Assert.Equal(512u,bus.ReadLong(0x5020)); Assert.Equal(0,bus.ReadByte(0x501F));
            Assert.Equal(0x6E,bus.ReadByte(0x12000)); Assert.Equal(0x6E,bus.ReadByte(0x121FF));
        }
        finally { DeleteTemp(path); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TimedCompletionPublishesOnlyAtTheExactBoundaryIncludingAnErrorPrefix(bool fail)
    {
        var path=CreateTempPath();
        try
        {
            var bytes=new byte[32768]; bytes.AsSpan(0,1536).Fill(0x39); File.WriteAllBytes(path,bytes);
            var fault=new PrefixFailure();
            using var machine=new LightweightA500Machine(new()
            {
                Hardfiles=[new(0,path,readOnly:true) { IoHook=fail ? fault : null }],
                HardfileTimings=new Dictionary<int,LightweightDriveTimingProfile> { [0]=LightweightDriveTimingProfile.Cf }
            });
            var bus=machine.HardfileBus!; SupplyPublicMemory(bus); var controller=bus.CopperHdf;
            fault.Enabled=true;
            var state=new M68kCpuState(); state.A[1]=0x5000; state.A[7]=0x7000;
            bus.WriteWord(0x501C,2); bus.WriteByte(0x501E,1); bus.WriteLong(0x5024,fail ? 1536u : 512u);
            bus.WriteLong(0x5028,0x12000); bus.WriteLong(0x502C,0);
            var begin=controller.DeviceBase-30; bus.TryInvokeHostGateway(begin,bus.ReadLong(begin+2),state);
            var command=LightweightStorageDrive.Microseconds(50);
            var sector=(512*LightweightStorageDrive.ClockHz+8*1024*1024-1)/(8*1024*1024);
            var completion=command+sector;
            machine.AdvanceHardwareTo(completion-1);
            Assert.False(controller.TimedInterrupt); Assert.Equal(0u,bus.ReadLong(0x5020)); Assert.Equal(0,bus.ReadByte(0x12000));
            machine.AdvanceHardwareTo(completion);
            Assert.True(controller.TimedInterrupt); Assert.Equal(512u,bus.ReadLong(0x5020));
            Assert.Equal(fail ? 20 : 0,bus.ReadByte(0x501F)); Assert.Equal(0x39,bus.ReadByte(0x12000));
            Assert.Equal(0,bus.ReadByte(0x12200));
        }
        finally { DeleteTemp(path); }
    }

    [Fact]
    public void TimedQueueAbortOverflowResetAndSteadyExecutionAreBounded()
    {
        var path=CreateTempPath();
        try
        {
            File.WriteAllBytes(path,new byte[32768]);
            using var machine=new LightweightA500Machine(new()
            {
                Hardfiles=[new(0,path,readOnly:true)],
                HardfileTimings=new Dictionary<int,LightweightDriveTimingProfile> { [0]=LightweightDriveTimingProfile.Cf }
            });
            var bus=machine.HardfileBus!; SupplyPublicMemory(bus); var controller=bus.CopperHdf;
            var state=new M68kCpuState();
            void Submit(uint address, bool abort=false)
            {
                state.A[1]=address; state.A[7]=0x7000;
                if (!abort) { bus.WriteWord(address+28,2); bus.WriteByte(address+30,1); bus.WriteLong(address+36,512); bus.WriteLong(address+40,0x12000); bus.WriteLong(address+44,512); }
                var vector=controller.DeviceBase-(abort ? 36u : 30u);
                Assert.True(bus.TryInvokeHostGateway(vector,bus.ReadLong(vector+2),state));
            }
            uint Pop()
            {
                var register=controller.ConfiguredBase+0x6000;
                controller.TryReadCompletionWord(register,out var high); controller.TryReadCompletionWord(register+2,out var low);
                return (uint)high<<16|low;
            }
            Submit(0x5000); Submit(0x5100); Submit(0x5100,true);
            Assert.Equal(0xFE,bus.ReadByte(0x511F)); Assert.Equal(0x5100u,Pop()); Assert.False(controller.TimedInterrupt);
            Submit(0x5000,true); Submit(0x5000,true);
            Assert.Equal(0xFE,bus.ReadByte(0x501F)); Assert.Equal(0x5000u,Pop()); Assert.Equal(0u,Pop());
            for (uint i=0;i<64;i++) Submit(0x2000+i*64);
            Submit(0x4000); Assert.Equal(0xFA,bus.ReadByte(0x401F)); Assert.Equal(1,bus.ReadByte(0x401E));
            controller.ResetTimedStorage(); Assert.False(controller.TimedInterrupt); Assert.Equal(long.MaxValue,controller.NextTimedCycle);
            // Warm both host file I/O and the fixed request/completion machinery.
            for (var iteration=0;iteration<24;iteration++)
            {
                Submit(0x5000);
                while (!controller.TimedInterrupt) { var cycle=controller.NextTimedCycle; machine.ReadWord(LightweightA500Machine.RomBase,ref cycle,M68kBusAccessKind.CpuDataRead); }
                Assert.Equal(0x5000u,Pop());
            }
            var allocated=GC.GetAllocatedBytesForCurrentThread();
            for (var iteration=0;iteration<100;iteration++)
            {
                Submit(0x5000);
                while (!controller.TimedInterrupt) { var cycle=controller.NextTimedCycle; machine.ReadWord(LightweightA500Machine.RomBase,ref cycle,M68kBusAccessKind.CpuDataRead); }
                Pop();
            }
            Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-allocated);
        }
        finally { DeleteTemp(path); }
    }
    [Fact]
    public void TimedRequestsDeferVisibilityAndReplyExactlyOnceInFifoOrder()
    {
        var path = CreateTempPath();
        try
        {
            var image = new byte[32768]; image.AsSpan(512, 512).Fill(0x41); image.AsSpan(1024, 512).Fill(0x42); File.WriteAllBytes(path, image);
            using var machine = new LightweightA500Machine(new()
            {
                Hardfiles = [new(0, path, readOnly:true)],
                HardfileTimings = new Dictionary<int, LightweightDriveTimingProfile> { [0] = LightweightDriveTimingProfile.Cf }
            });
            var bus = machine.HardfileBus!;
            // Public free-list storage used by the existing native bootstrap allocator.
            bus.WriteLong(0x1142, 0x8000); bus.WriteLong(0x8000, 0x1146); bus.WriteWord(0x800E, 1);
            bus.WriteLong(0x8010, 0x10000); bus.WriteLong(0x8014, 0x10000); bus.WriteLong(0x8018, 0x70000);
            bus.WriteLong(0x801C, 0x60000); bus.WriteLong(0x10000, 0); bus.WriteLong(0x10004, 0x60000);
            InvokeBootstrap(bus);
            var begin = bus.CopperHdf.DeviceBase - 30;
            void Queue(uint request, uint sector, uint target)
            {
                var state = new M68kCpuState(); state.A[1] = request; state.A[7] = 0x7000;
                bus.WriteWord(request+28, 2); bus.WriteByte(request+30, 1); bus.WriteLong(request+36, 512);
                bus.WriteByte(request+8,7); // previously replied request reused by DoIO
                bus.WriteLong(request+40, target); bus.WriteLong(request+44, sector*512);
                Assert.True(bus.TryInvokeHostGateway(begin,bus.ReadLong(begin+2),state)); Assert.Equal(0, bus.ReadByte(request+30));
                Assert.Equal(5,bus.ReadByte(request+8));
            }
            Queue(0x5000,1,0x12000); Queue(0x5100,2,0x12400);
            Assert.Equal(0u,bus.ReadLong(0x5020)); Assert.Equal(0,bus.ReadByte(0x12000));
            var controller=bus.CopperHdf; var first=controller.NextTimedCycle;
            long cycle=first-1; machine.ReadWord(LightweightA500Machine.RomBase,ref cycle,M68kBusAccessKind.CpuDataRead);
            Assert.Equal(0u,bus.ReadLong(0x5020));
            for(var events=0; !controller.TimedInterrupt && events<100; events++)
            { cycle=controller.NextTimedCycle; machine.ReadWord(LightweightA500Machine.RomBase,ref cycle,M68kBusAccessKind.CpuDataRead); }
            Assert.True(controller.TimedInterrupt); Assert.Equal(512u,bus.ReadLong(0x5020)); Assert.Equal(0x41,bus.ReadByte(0x12000));
            var register=controller.ConfiguredBase+0x6000;
            Assert.True(controller.TryReadCompletionWord(register,out var high)); Assert.Equal(0,high);
            Assert.True(controller.TryReadCompletionWord(register+2,out var low)); Assert.Equal(0x5000,low);
            Assert.False(controller.TimedInterrupt);
            for(var events=0; !controller.TimedInterrupt && events<100; events++)
            { cycle=controller.NextTimedCycle; machine.ReadWord(LightweightA500Machine.RomBase,ref cycle,M68kBusAccessKind.CpuDataRead); }
            Assert.Equal(0x42,bus.ReadByte(0x12400)); controller.TryReadCompletionWord(register,out _); controller.TryReadCompletionWord(register+2,out low);
            Assert.Equal(0x5100,low); controller.TryReadCompletionWord(register,out _); controller.TryReadCompletionWord(register+2,out low);
            Assert.Equal(0,low); Assert.False(controller.TimedInterrupt);
        }
        finally { DeleteTemp(path); }
    }
}
