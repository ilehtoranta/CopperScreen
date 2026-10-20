using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CopperScreen;

// Diagnostic workflow linked into tests and diagnostic MSIX builds only.
// The standard session owns all execution; this observer only reads pixels.
internal static class CopperOsNativeStorageProof
{
    public static object Run(string seed, string rom, string userData, string output, bool reopen, string baseDirectory, string? expectedFailure = null, bool readOnly = false, bool denyHostSaves = false, string? directImage = null)
    {
        Directory.CreateDirectory(output);
        var arguments = directImage == null ? new[] {"--copperos", seed, "--copperos-workspace", "persistence", "--rom", rom}
            : new[] {"--copperstart", "--rom", rom, directImage};
        var options = CopperScreenStartupOptions.Parse(arguments, baseDirectory, userDataDirectory: userData);
        if (options.Error != null) throw new InvalidOperationException(options.Error);
        if (readOnly) options.DriveWriteProtected[0] = true;
        using var created = CopperScreenSession.Create(options);
        var session = (CopperScreenLightweightSession)created;
        if (denyHostSaves) File.SetAttributes(session.DiskPath!, FileAttributes.ReadOnly);
        try
        {
            var slow = (byte[])session.Machine.GetType().GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Machine)!;
            byte Read(uint a) => a < session.Machine.ChipRam.Length ? session.Machine.ChipRam.Span[(int)a] : a >= 0xC00000 && a - 0xC00000 < slow.Length ? slow[a - 0xC00000] : (byte)0;
            uint Long(uint a) => ((uint)Read(a) << 24) | ((uint)Read(a+1) << 16) | ((uint)Read(a+2) << 8) | Read(a+3);
            string Screen()
            {
                var control = Long(0x880); var console = Long(control+32); var bitmap = Long(console+128);
                if (control == 0 || console == 0 || bitmap == 0 || bitmap + 16000 > session.Machine.ChipRam.Length) return "";
                var text = new StringBuilder();
                for (var row=0;row<25;row++)
                {
                    for (var col=0;col<80;col++)
                    {
                        ulong glyph=0; for(var y=0;y<8;y++) glyph=(glyph<<8)|Read(bitmap+(uint)((row*8+y)*80+col));
                        text.Append(Glyphs.GetValueOrDefault(glyph,'?'));
                    }
                    text.Append('\n');
                }
                return text.ToString();
            }
            var before = File.ReadAllBytes(session.DiskPath!);
            var fields=0;
            void Run(int count)
            {
                for(var i=0;i<count;i++)
                {
                    session.RenderNextFrame(session.Framebuffer);fields++;
                    if(session.FaultMessage!=null || session.IsPaused) throw new InvalidOperationException(session.FaultMessage ?? "Native session paused unexpectedly.");
                }
            }
            void Capture(string name)
            {
                File.WriteAllText(Path.Combine(output,name+".txt"),Screen());
                File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(new {fields,pc=session.Machine.Cpu.ProgramCounter,cycle=session.Machine.Cycle,drive=session.Machine.DriveState,data=session.Machine.Cpu.D,address=session.Machine.Cpu.A}));
                File.WriteAllBytes(Path.Combine(output,name+".chipram"),session.Machine.ChipRam.ToArray());
                File.WriteAllBytes(Path.Combine(output,name+".slowram"),slow);
            }
            void Wait(string phrase,int limit)
            {
                for(var i=0;i<limit;i+=100)
                {
                    if(Screen().Contains(phrase,StringComparison.Ordinal)) return;
                    if(Screen().Contains("FAILED",StringComparison.Ordinal)) {Capture("failed");throw new InvalidOperationException("Native CopperOS storage failed: "+Screen());}
                    Run(100);
                    if ((i+100)%5000==0) Capture("progress-"+fields);
                }
                Capture("timeout");throw new InvalidOperationException("Native console timeout waiting for "+phrase+": "+Screen());
            }
            Wait("2.>",15000);Capture("boot");
            foreach(var c in "storage")
            {
                var key=Enum.Parse<AmigaRawKey>(char.ToUpperInvariant(c).ToString());
                session.KeyDown(key);Run(4);session.KeyUp(key);Run(4);
            }
            session.KeyDown(AmigaRawKey.Return);Run(4);session.KeyUp(AmigaRawKey.Return);
            Wait(expectedFailure ?? (reopen?"COPPEROS STORAGE REOPEN VERIFIED":"COPPEROS STORAGE CREATED AND VERIFIED"),25000);
            for (var wait=0;wait<3000 && Screen().Split("2.>").Length < 3;wait+=100) Run(100);
            if (Screen().Split("2.>").Length < 3) throw new InvalidOperationException("Storage command did not return to the native Shell.");
            Run(200);Capture("storage");
            string? recoveredExport = null;
            string? recoveredBackup = null, recoveredWorkspace = null;
            byte[] image;
            if (denyHostSaves)
            {
                try
                {
                    var failed = false;
                    try { session.SavePersistentStorage(); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed = true; }
                    if (!failed) throw new IOException("Expected the primary workspace checkpoint to fail.");
                    using var runtime = CopperScreenRuntime.CreateForTests(session);
                    runtime.Start();
                    recoveredExport = Path.Combine(output,"recovered.adf");
                    var exported = runtime.SaveAdfAsync(0,recoveredExport).GetAwaiter().GetResult();
                    if (!exported.Success || !exported.State.Drives[0].HasUnsavedChanges) throw new IOException("Recovery export failed or cleared unsaved primary data: "+exported.Message);
                    image = File.ReadAllBytes(recoveredExport);
                    recoveredBackup = Path.Combine(output, "recovered.copperos");
                    var bundle = runtime.ExportWorkspaceAsync(recoveredBackup).GetAwaiter().GetResult();
                    if (!bundle.Success || !bundle.State.Drives[0].HasUnsavedChanges) throw new IOException("Workspace backup failed or acknowledged unsaved primary data: " + bundle.Message);
                    recoveredWorkspace = CopperOsWorkspace.ImportBackup(Path.Combine(output, "imported-user"), "imported", recoveredBackup);
                    if (!File.ReadAllBytes(recoveredWorkspace).AsSpan().SequenceEqual(image)) throw new IOException("Imported workspace backup differs from the native recovery export.");
                    if (!File.ReadAllBytes(session.DiskPath!).AsSpan().SequenceEqual(before)) throw new IOException("Failed checkpoint changed the primary workspace.");
                    runtime.DiscardPersistentStorageAsync().GetAwaiter().GetResult();
                }
                finally
                {
                    session.DiscardPersistentStorage(); // explicit diagnostic cleanup, not automatic product recovery
                    File.SetAttributes(session.DiskPath!,FileAttributes.Normal);
                }
            }
            else
            {
                session.SavePersistentStorage();image=File.ReadAllBytes(session.DiskPath!);
                if(session.Machine.IsDriveDirty(0)) throw new IOException("Workspace checkpoint did not clear the dirty state.");
            }
            CopperOsWorkspace.ValidateImage(image);
            if (expectedFailure != null && !image.AsSpan().SequenceEqual(before)) throw new IOException("Failed native operation changed the workspace.");
            var result=new {protocol="copperos-native-storage-v2",passed=true,reopen,expectedFailure,readOnly,denyHostSaves,recoveredExport,recoveredBackup,recoveredWorkspace,fields,workspace=session.DiskPath,workspacePairId=session.WorkspaceSeedPairId,
                imageSha256=Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant(),
                seedSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(seed))).ToLowerInvariant(),
                romSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(rom))).ToLowerInvariant(),
                engine="Lightweight",legacy=false,hostDosCalls=false,
                binaries = new[] {typeof(CopperScreenSession).Assembly.Location,session.Machine.GetType().Assembly.Location,typeof(CopperOsNativeStorageProof).Assembly.Location}
                    .Select(path=>new {file=Path.GetFileName(path),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()})};
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            return result;
        }
        finally
        {
            if (denyHostSaves)
            {
                session.DiscardPersistentStorage(); // Explicit diagnostic cleanup, including early guest failures.
                File.SetAttributes(session.DiskPath!, FileAttributes.Normal);
            }
        }
    }

    private static readonly Dictionary<ulong, char> Glyphs = new()
    {
        [0x1c22262a32221c00UL]='0',[0x0818080808081c00UL]='1',[0x1c22020408103e00UL]='2',
        [0x3c02021c02023c00UL]='3',[0x040c14243e040400UL]='4',[0x3e20203c02023c00UL]='5',
        [0x1c20203c22221c00UL]='6',[0x3e02040810101000UL]='7',[0x1c22221c22221c00UL]='8',
        [0x1c22221e02021c00UL]='9',[0x0000000000000000UL]=' ',[0x1c22223e22222200UL]='A',
        [0x3c22223c22223c00UL]='B',[0x1e20202020201e00UL]='C',[0x3c22222222223c00UL]='D',
        [0x3e20203c20203e00UL]='E',[0x3e20203c20202000UL]='F',[0x1e20202e22221e00UL]='G',
        [0x2222223e22222200UL]='H',[0x3e08080808083e00UL]='I',[0x0e04040424241800UL]='J',
        [0x2224283028242200UL]='K',[0x2020202020203e00UL]='L',[0x22362a2a22222200UL]='M',
        [0x22322a2622222200UL]='N',[0x1c22222222221c00UL]='O',[0x3c22223c20202000UL]='P',
        [0x1c2222222a241a00UL]='Q',[0x3c22223c28242200UL]='R',[0x1e20201c02023c00UL]='S',
        [0x3e08080808080800UL]='T',[0x2222222222221c00UL]='U',[0x2222222222140800UL]='V',
        [0x2222222a2a2a1400UL]='W',[0x2222140814222200UL]='X',[0x2222140808080800UL]='Y',
        [0x3e02040810203e00UL]='Z',[0x00000000000c0c00UL]='.',[0x000c0c000c0c0000UL]=':',
        [0x0000003e00000000UL]='-',[0x0202040810202000UL]='/',[0x2010080408102000UL]='>'
    };
}
