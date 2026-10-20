using CopperScreen;

// Linked production snapshot code, with publication barriers compiled only here.
// The parent test kills this process; no app CLI switch or shipping test hook.
if (args.Length != 4) return 2;
var failPartial = args[2] == "FailPartial";
if (!Enum.TryParse<CopperScreenAtomicFile.Stage>(args[2], out var point) && !failPartial) return 2;
CopperScreenAtomicFile.TestObserver = stage =>
{
    if (failPartial && stage == CopperScreenAtomicFile.Stage.Partial) throw new IOException("Injected host disk-full error after a partial temporary write.");
    if (stage != point) return;
    File.WriteAllText(args[3], stage.ToString());
    Thread.Sleep(Timeout.Infinite);
};
try { using var workspace = CopperOsWorkspace.Open(args[0]); workspace.Save(File.ReadAllBytes(args[1])); return 0; }
catch (IOException ex) { Console.Error.WriteLine(ex.Message); return 3; }
