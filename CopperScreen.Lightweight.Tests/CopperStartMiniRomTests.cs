using System.Buffers.Binary;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperStartMiniRomTests
{
    [Theory]
    [InlineData((int)CopperScreenMachineModel.Amiga500, 512)]
    [InlineData((int)CopperScreenMachineModel.Amiga500Ecs, 1024)]
    [InlineData((int)CopperScreenMachineModel.Amiga1200, 2048)]
    public void ExternalRomSelectionSurvivesSaveAndReset(int model, int chipKiB)
    {
        var directory = Directory.CreateTempSubdirectory("copperscreen-mini-");
        try
        {
            var rom = new byte[524288];
            BinaryPrimitives.WriteUInt32BigEndian(rom, 0x8000);
            BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xF80010);
            "CSMI\0\u0001\0\0"u8.CopyTo(rom.AsSpan(8));
            BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(16), 0x60FE);
            var path = Path.Combine(directory.FullName, "CopperStart-Mini.rom");
            File.WriteAllBytes(path, rom);

            var draft = CopperScreenMachinePresets.Apply(new CopperScreenSettingsDraft(), (CopperScreenMachineModel)model);
            draft.KickstartRomPath = path;
            draft.RomVersion = CopperScreenKickstartRomArchive.IdentifyNativeVersion(rom);
            Assert.Equal(KickstartVersion.CopperStartMini, draft.RomVersion);
            var saved = CopperScreenProfileStore.Save(draft, directory.FullName, directory.FullName);
            Assert.True(CopperScreenProfile.TryLoad(saved, directory.FullName, out var profile, out var error), error);
            var restored = CopperScreenSettingsDraft.FromProfile(profile);
            Assert.Equal(KickstartVersion.CopperStartMini, restored.RomVersion);
            Assert.Equal(CopperScreenKickstartSource.KickstartRom, restored.KickstartSource);

            using var session = new CopperScreenLightweightSession(restored.ToStartupOptions(directory.FullName));
            Assert.Equal(chipKiB * 1024, session.Machine.ChipRam.Length);
            session.RenderNextFrame(session.Framebuffer);
            Assert.Null(session.FaultMessage);
            session.Reset();
            Assert.Equal(0xF80010u, session.Machine.Cpu.ProgramCounter);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
