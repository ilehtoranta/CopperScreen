using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Copper68k;
using CopperDisk;
using CopperMod.Amiga.Lightweight;

// Diagnostic reader for development muimaster.library 4.1 and 12.1 ABIs.
// Guest memory is observed only. All interaction uses ordinary machine input.
// This is a correctness workload, never a throughput acceptance measurement.
if (args.Length is < 5 or > 6) throw new ArgumentException("ROM HDF new-output hdf-ro|hdf-ffs max-fields [menu-fixture|fault-stop|string-fixture|join-requester-fixture]");
if (args.Length == 6 && args[5] is not ("menu-fixture" or "fault-stop" or "string-fixture" or "join-requester-fixture" or "join-picker-fixture" or "join-write-fixture" or "join-prefs-fixture" or "popasl-fixture" or "icon-update-fixture" or "requester-probe-fixture"))
    throw new ArgumentException("Unknown interaction fixture");
var output = Path.GetFullPath(args[2]);
if (Directory.Exists(output)) throw new ArgumentException("Use a new output directory");
Directory.CreateDirectory(output);
var rom = File.ReadAllBytes(args[0]);
var mediaHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1])));
var media = args[3] switch
{
    "hdf-ro" => new AmigaHardfileConfiguration(0, args[1], true),
    "hdf-ffs" => new AmigaHardfileConfiguration(0, args[1], true,
        mountMode: AmigaHardfileMountMode.Partition,
        partition: AmigaFfsPartition.ParseGeometry("DH0:1:32:0:1023")),
    _ => throw new ArgumentException("Unknown media mode")
};
using var machine = new LightweightA500Machine(configuration: new LightweightA500Configuration
{
    ChipRamBytes = 524288, SlowRamBytes = 524288, FastRamBytes = 4194304,
    FramebufferWidth = 908,
    Hardfiles = [media], CpuModel = M68kCpuModel.M68000,
    AgnusModel = LightweightAgnusModel.Mos8371, DeniseModel = LightweightDeniseModel.Mos8362,
}); // Public constructor uses the production conservative CPU loop batch.
machine.LoadKickstart(rom);
var traceFileAcceptance = Environment.GetEnvironmentVariable("MUI_TRACE_FILE_ACCEPTANCE") == "1";
var tracePrefsUse = Environment.GetEnvironmentVariable("MUI_TRACE_PREFS_USE") == "1";
uint? traceGlobal = Environment.GetEnvironmentVariable("MUI_TRACE_GLOBAL_LONG") is { Length: > 0 } globalText
    ? Convert.ToUInt32(globalText, 16) : null;
uint? traceGlobalExpected = traceGlobal.HasValue
    ? Convert.ToUInt32(Environment.GetEnvironmentVariable("MUI_TRACE_GLOBAL_EXPECTED") ?? throw new ArgumentException("Trace expected value required"), 16) : null;
var faultTrace = traceFileAcceptance || traceGlobal.HasValue || tracePrefsUse ? new NativeFaultTrace(machine, output, traceGlobal, traceGlobalExpected) : null;
var slow = (byte[])typeof(LightweightA500Machine).GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(machine)!;
var memory = new Reader(machine, slow);
var inspectPicker = args.Length == 6 && args[5] == "join-picker-fixture";
var joinWriteFixture = args.Length == 6 && args[5] == "join-write-fixture";
bool nativeTyping = false;
uint nativeField = 0;
string nativeExpected = "", nativeStage = "";
int selectionRow = 0;
var joinRequesterFixture = inspectPicker || args.Length == 6 && args[5] == "join-requester-fixture";
var joinPrefsFixture = args.Length == 6 && args[5] == "join-prefs-fixture";
var popaslFixture = args.Length == 6 && args[5] == "popasl-fixture";
var iconFixture = args.Length == 6 && args[5] == "icon-update-fixture";
var requesterProbe = args.Length == 6 && args[5] == "requester-probe-fixture";
const int IconResultWaitFields = 120000;
var textStep = 0;
var expectedText = iconFixture ? "ram:src" : "test";
uint mainWindowObject = 0, resultWindow = 0;
var resultDialogClick = false;
uint popupObject = 0;
var actions = requesterProbe ? new[] { "OK" } : joinWriteFixture ? new[] { "Split", "Join", "Quit" } : iconFixture ? new[] { "Start" } : joinPrefsFixture ? new[] { "Prefs", "Use", "Quit" } : joinRequesterFixture ? new[] { "Split" } : args.Length == 6 && args[5] == "menu-fixture" ? new[] { "Test Utility", "Quit" } : Array.Empty<string>();
uint requesterWindow = 0;
var requesterCancellationPassed = false;
var stringFixture = iconFixture || args.Length == 6 && args[5] == "string-fixture";
var stringInputPassed = false;
var rawKeys = iconFixture ? new[] { 0x13, 0x20, 0x37, 0x129, 0x21, 0x13, 0x33, 0x44 } : new[] { 0x14, 0x12, 0x21, 0x14, 0x44 };
var keyIndex = 0;
var events = new List<object>();
var cpuDiagnosticProperties = typeof(M68kCpuState).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToDictionary(p => p.Name);
uint CpuValue(string name) => unchecked((uint)Convert.ToInt64(cpuDiagnosticProperties[name].GetValue(machine.Cpu)));
uint exceptionSequence = 0;
var faultSites = new HashSet<(uint Vector, uint Pc)>();
var phase = "startup"; var action = 0; var entered = 0; var lastInput = -100;
uint library = 0, windowObject = 0, target = 0;
uint originalNativeWindow = 0, originalScreen = 0;
bool? nativeWindowUnlinked = null;
var unavailableActions = new List<string>();
int targetX = 0, targetY = 0;
var reason = "MAX_FIELDS"; var success = false;
long? cleanupObservedFrame = null;
State? state = null;
for (var frame = 0; frame < int.Parse(args[4]); frame++)
{
    if (faultTrace is not null &&
        (joinWriteFixture && (traceGlobal.HasValue && action == 0 && phase is not ("startup" or "observe" or "aim") ||
         traceFileAcceptance && nativeStage == "file" && phase == "file-enter-up") ||
         tracePrefsUse && joinPrefsFixture && action == 1))
    {
        if (tracePrefsUse) faultTrace.WatchLibraryClose(library);
        faultTrace.ExecuteFrame();
        if (faultTrace.StoppedAtInvalidPc)
        { reason = faultTrace.Reason; Capture("trace-stopped"); break; }
    }
    else machine.ExecuteFrame();
    if (machine.UnsupportedActiveFeature is { } unsupported)
    { reason = "UNSUPPORTED: " + unsupported; Capture("unsupported"); break; }
    if (CpuValue("ExceptionSequence") != exceptionSequence)
    {
        exceptionSequence = CpuValue("ExceptionSequence");
        if (CpuValue("LastExceptionVector") is 3 or 4 or 5 or 8 or 10 or 11 && faultSites.Add((CpuValue("LastExceptionVector"), CpuValue("LastExceptionInstructionProgramCounter"))))
        {
            events.Add(new { frame, exceptionVector = CpuValue("LastExceptionVector"),
                pc = CpuValue("LastExceptionInstructionProgramCounter"), sp = CpuValue("LastExceptionA7") });
            Capture("exception-" + exceptionSequence);
            // Never continue a bad return into vectors or outside the 24-bit
            // address space. Preserve one decisive capture instead of producing
            // a fault-storm replay before the utility has even loaded.
            if (CpuValue("LastExceptionVector") != 8 &&
                (CpuValue("LastExceptionInstructionProgramCounter") < 0x400 ||
                 CpuValue("LastExceptionInstructionProgramCounter") >= 0x1000000))
            { reason = state?.Objects.Count > 0 ? "INVALID_GUEST_PC_DURING_UTILITY" : "INVALID_GUEST_PC_BEFORE_UTILITY_PREFLIGHT"; break; }
            if (args.Length == 6 && args[5] == "fault-stop" && state?.Objects.Count > 0 && CpuValue("LastExceptionVector") != 8 && CpuValue("LastExceptionInstructionProgramCounter") < 0xF80000)
            { reason = "GUEST_EXCEPTION_DURING_MUI_STARTUP"; break; }
        }
    }
    // A raw key is a physical down/up pair, not a character injection. Release
    // promptly so the input device's ordinary key repeat cannot alter the text.
    if (phase == "shift-down" && machine.CompletedFrames - entered >= 2)
    { Key((byte)rawKeys[keyIndex]); Transition("key-down"); }
    else if (phase == "key-down" && machine.CompletedFrames - entered >= 2)
    { Key((byte)(rawKeys[keyIndex] | 0x80)); Transition((rawKeys[keyIndex] & 0x100) != 0 ? "shift-release" : "key-up"); }
    else if (phase == "shift-release" && machine.CompletedFrames - entered >= 2)
    { Key(0xE0); Transition("key-up"); }
    else if (phase == "file-enter-down" && machine.CompletedFrames - entered >= 2)
    { Key(0xC4); Transition("file-enter-up"); }
    if (frame % 50 != 0) continue;
    if (library == 0) library = memory.FindLibrary();
    state = library == 0 ? null : memory.ReadState(library);
    if (frame % 25000 == 0)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { frame, phase, action, objects = state?.Objects.Count, depth = state?.Depth }));
        Capture("progress");
    }
    if (phase == "quit-settle")
    {
        if (state is not null && (state.Depth != 0 || state.ActiveOperations != 0 || state.Objects.Count != 0 || state.Classes != 0))
        { reason = "REGISTRY_ACTIVITY_AFTER_QUIT"; Capture("quit-activity"); break; }
        // A completed Dispose may precede the client's CloseLibrary/CLI return.
        // Keep executing ordinary guest frames; an expunged library is allowed
        // only after the captured empty-registry observation above.
        if (frame - entered >= 5000)
        {
            nativeWindowUnlinked = !memory.ScreenContainsWindow(originalScreen, originalNativeWindow);
            if (iconFixture && !memory.IconSnapshotReady())
            {
                if (frame - entered > 60000) { reason = "ICON_AFTER_SNAPSHOT_UNAVAILABLE"; Capture("icon-after-unavailable"); break; }
                continue;
            }
            if (joinWriteFixture && !memory.JoinSnapshotReady())
            {
                if (frame - entered > 60000) { reason = "JOIN_AFTER_SNAPSHOT_UNAVAILABLE"; Capture("join-after-unavailable"); break; }
                continue;
            }
            Capture("quit-settled");
            success = nativeWindowUnlinked == true;
            reason = success ? "EMPTY_REGISTRIES_AND_NATIVE_WINDOW_UNLINKED_AFTER_QUIT_AND_SETTLE"
                : "NATIVE_WINDOW_STILL_LINKED_AFTER_QUIT";
            break;
        }
        continue;
    }
    if (state is null) continue;
    if (joinWriteFixture && phase.StartsWith("file-", StringComparison.Ordinal))
    { if (!HandleJoinFilePhase(frame)) break; continue; }
    var window = state.Objects.FirstOrDefault(o => o.Class == "Window.mui" && o.NativeWindow != 0);
    if (requesterProbe && phase == "startup")
        window = state.Objects.FirstOrDefault(o => o.Class == "Window.mui" && o.NativeWindow != 0 &&
            state.Objects.Any(text => text.Visible && text.ContainingWindow == o.Object && text.Text == "Update Successful."));
    if (phase == "startup")
    {
        // A synchronous MUI_RequestA deliberately keeps its entry pinned while
        // pumping the dialog. The standalone probe must reach its real OK
        // control without requiring the enclosing call to have returned first.
        if (window is null || (!requesterProbe && state.Depth != 0)) continue;
        if (requesterProbe && !state.Objects.Any(o => o.Visible &&
            o.ContainingWindow == window.Object && o.Text == "Update Successful.")) continue;
        windowObject = window.Object;
        mainWindowObject = windowObject;
        originalNativeWindow = window.NativeWindow;
        originalScreen = memory.U32(originalNativeWindow + 46);
        Capture("opened");
        Transition("observe");
    }
    else if (phase == "observe" && frame - entered >= 5000)
    {
        Capture("observed");
        if (stringFixture)
        {
            if (!SelectEmptyString()) break;
        }
        else if (popaslFixture)
        {
            var popup = state.Objects.Where(o => o.Class == "Popasl.mui" && o.Visible && o.ContainingWindow == windowObject)
                .OrderBy(o => o.Top).ThenBy(o => o.Left).FirstOrDefault();
            var button = state.Objects.FirstOrDefault(o => o.Object == popup?.PopButton && o.Visible && o.Width >= 6 && o.Height >= 6);
            if (popup is null || button is null) { reason = "POPASL_BUTTON_UNAVAILABLE"; break; }
            popupObject = popup.Object; target = button.Object;
            targetX = button.Left + button.Width / 2; targetY = button.Top + button.Height / 2;
            Transition("aim");
        }
        else if (actions.Length > 0) { if (!SelectButton()) break; }
        else { targetX = 5; targetY = 5; Transition("close-aim"); }
    }
    else if (phase == "aim" || phase == "close-aim" || phase == "string-aim")
    {
        window = state.Objects.FirstOrDefault(o => o.Object == windowObject);
        if (window is null || window.NativeWindow == 0) { reason = "WINDOW_LOST_DURING_AIM"; break; }
        if (frame - entered > 10000) { reason = "POINTER_AIM_TIMEOUT"; Capture("aim-timeout"); break; }
        var x = memory.I16(window.NativeWindow + 14); var y = memory.I16(window.NativeWindow + 12);
        var dx = targetX - x; var dy = targetY - y;
        if (Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2)
        {
            Input(1, 0, 0); Transition(phase == "aim" ? "press" : phase == "string-aim" ? "string-press" : "close-press");
        }
        else Input(0, Delta(dx), Delta(dy));
    }
    else if (phase == "string-press" && frame - entered >= 1000)
    { Input(0, 0, 0); Transition("string-focused"); }
    else if (phase == "string-focused" && frame - entered >= 2000)
    { PressKey(); }
    else if (phase == "key-up" && frame - entered >= 1000)
    {
        if (++keyIndex < rawKeys.Length) PressKey();
        else Transition(nativeTyping ? "file-string-verify" : "string-verify");
    }
    else if (phase == "string-verify" && state.Depth == 0 && frame - entered >= 5000)
    {
        var field = state.Objects.FirstOrDefault(o => o.Object == target);
        Capture("string-input");
        stringInputPassed = field?.Text == expectedText;
        events.Add(new { frame = machine.CompletedFrames, operation = "hardware-string-input", expected = expectedText, actual = field?.Text, passed = stringInputPassed });
        if (!stringInputPassed) { reason = "STRING_INPUT_MISMATCH"; break; }
        if (iconFixture && textStep == 0)
        {
            textStep++; expectedText = "ram:dst";
            rawKeys = [0x13, 0x20, 0x37, 0x129, 0x22, 0x21, 0x14, 0x44]; keyIndex = 0;
            if (!SelectEmptyString()) break;
        }
        else if (iconFixture) { if (!SelectButton()) break; }
        else { targetX = 5; targetY = 5; Transition("close-aim"); }
    }
    else if (phase == "press")
    {
        var button = state.Objects.FirstOrDefault(o => o.Object == target);
        if (button?.Pressed == 1)
        { Input(0, 0, 0); Transition(requesterProbe ? "cleanup" : joinWriteFixture && action < 2 ? "file-requester-wait" : iconFixture ? resultDialogClick ? "icon-result-close" : "icon-result-wait" : joinRequesterFixture || popaslFixture ? "requester-wait" : "release"); }
        else if (frame - entered > 60000) { reason = "BUTTON_PRESS_TIMEOUT"; Capture("press-timeout"); break; }
    }
    else if (phase == "icon-result-wait")
    {
        var dialog = state.Objects.FirstOrDefault(o => o.Class == "Window.mui" && o.NativeWindow != 0 && o.Object != mainWindowObject);
        if (dialog is not null)
        {
            var text = string.Join("\n", state.Objects.Where(o => o.Visible && o.ContainingWindow == dialog.Object && o.Class == "Text.mui" && o.InputMode == 0).Select(o => Normalize(o.Text)));
            var ok = state.Objects.Where(o => o.Visible && o.ContainingWindow == dialog.Object && o.InputMode is >= 1 and <= 3 && Normalize(o.Text).Equals("OK", StringComparison.OrdinalIgnoreCase)).ToArray();
            // Native OpenWindow precedes completion of MUI's first layout.
            // An allocated OK object with zero geometry is not a click target.
            // Keep the ordinary guest running until a real hit rectangle exists.
            if (ok.Length == 1 && ok[0].Width >= 6 && ok[0].Height >= 6 && text.Length != 0)
            {
                if (!text.Contains("Update Successful.", StringComparison.OrdinalIgnoreCase))
                { reason = "ICON_UPDATE_RESULT: " + text; Capture("icon-result-error"); break; }
                Capture("icon-update-success-dialog");
                resultWindow = dialog.NativeWindow; windowObject = dialog.Object; resultDialogClick = true;
                target = ok[0].Object; targetX = ok[0].Left + ok[0].Width / 2; targetY = ok[0].Top + ok[0].Height / 2;
                Transition("aim");
            }
        }
        if (phase == "icon-result-wait" && frame - entered > IconResultWaitFields)
        { reason = "ICON_RESULT_DIALOG_TIMEOUT"; Capture("icon-result-timeout"); break; }
    }
    else if (phase == "icon-result-close")
    {
        // CloseWindow can finish before requester object disposal and its
        // enclosing service call. Do not address the main window during that
        // modal teardown merely because the native dialog was unlinked.
        if (state.Depth == 0 && state.ActiveOperations == 0 &&
            state.Objects.All(o => o.Object != windowObject) &&
            !memory.ScreenContainsWindow(originalScreen, resultWindow))
        { windowObject = mainWindowObject; targetX = 5; targetY = 5; Transition("close-aim"); }
        else if (frame - entered > IconResultWaitFields) { reason = "ICON_RESULT_DIALOG_CLOSE_TIMEOUT"; Capture("icon-result-close-timeout"); break; }
    }
    else if (phase == "requester-wait")
    {
        requesterWindow = memory.U32(originalScreen + 4);
        if (requesterWindow != 0 && requesterWindow != originalNativeWindow &&
            (!inspectPicker || memory.StringInputs(requesterWindow).Count >= 2) &&
            (!popaslFixture || state.Objects.Any(o => o.Object == popupObject && o.PopActive == 1)) &&
            memory.TryCancelGadget(requesterWindow, out targetX, out targetY))
        { Capture("asl-requester-opened"); Transition("requester-aim"); }
        else if (frame - entered > 60000) { reason = "ASL_REQUESTER_CANCEL_GADGET_UNAVAILABLE"; Capture("requester-unavailable"); break; }
    }
    else if (phase == "requester-aim")
    {
        if (frame - entered > 10000) { reason = "ASL_CANCEL_AIM_TIMEOUT"; break; }
        var dx = targetX - memory.I16(requesterWindow + 14);
        var dy = targetY - memory.I16(requesterWindow + 12);
        if (Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2)
        { Input(1, 0, 0); Transition("requester-cancel-down"); }
        else Input(0, Delta(dx), Delta(dy));
    }
    else if (phase == "requester-cancel-down" && frame - entered >= 1000)
    { Input(0, 0, 0); Transition("requester-cancel-wait"); }
    else if (phase == "requester-cancel-wait")
    {
        if (state.Depth == 0 && !memory.ScreenContainsWindow(originalScreen, requesterWindow) &&
            (!popaslFixture || state.Objects.Any(o => o.Object == popupObject && o.PopActive == 0)) &&
            memory.ScreenContainsWindow(originalScreen, originalNativeWindow))
        {
            requesterCancellationPassed = true; Capture("asl-cancelled");
            targetX = 5; targetY = 5; Transition("close-aim");
        }
        else if (frame - entered > 60000) { reason = "ASL_REQUESTER_CANCEL_TIMEOUT"; Capture("requester-cancel-timeout"); break; }
    }
    else if (phase == "release")
    {
        var button = state.Objects.FirstOrDefault(o => o.Object == target);
        if (action == actions.Length - 1 && state.Objects.Count == 0 && state.Classes == 0 &&
            state.Depth == 0 && state.ActiveOperations == 0)
        { cleanupObservedFrame = machine.CompletedFrames; Capture("cleaned"); Transition("quit-settle"); }
        else if (button?.Pressed == 0 && state.Depth == 0 && frame - entered >= 1000)
        {
            Capture("after-" + action); action++;
            if (action < actions.Length) { if (!SelectButton()) break; }
            else Transition("cleanup");
        }
        else if (frame - entered > 60000) { reason = "BUTTON_RELEASE_TIMEOUT"; Capture("release-timeout"); break; }
    }
    else if (phase == "close-press" && frame - entered >= 500)
    { Input(0, 0, 0); Transition("cleanup"); }
    else if (phase == "cleanup" && state.Depth == 0 && state.ActiveOperations == 0 && state.Objects.Count == 0 && state.Classes == 0)
    { cleanupObservedFrame = machine.CompletedFrames; Capture("cleaned"); Transition("quit-settle"); }
}
machine.SubmitInput(new(0, 0, 0, 0, 0, 0));
Capture("final");
var result = new
{
    schemaVersion = 29, protocol = "mui-utility-native-input-v30", profile = "a500-ocs-5m-diagnostic-v1",
    iconResultWaitFields = IconResultWaitFields,
    traceFileAcceptance, tracePrefsUse, traceGlobal, traceGlobalExpected,
    libraryVersionRevision = library == 0 ? 0 : memory.U32(library + 20),
    buttonPressTimeoutFields = 60000, buttonReleaseTimeoutFields = 60000,
    quitSettleFields = 5000, cleanupObservedFrame,
    romSha256 = Convert.ToHexString(SHA256.HashData(rom)), imageSha256 = mediaHash,
    engineSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LightweightA500Machine).Assembly.Location))),
    cpuSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(M68kCpuModel).Assembly.Location))),
    machine.CompletedFrames, reason, inputAndCleanup = success ? "PASS" : "INCOMPLETE_OR_FAILED",
    scope = requesterProbe ? "STANDALONE_GUEST_MUI_REQUESTA_HARDWARE_OK_AND_CLEANUP_REQUIRES_RESOURCE_INSPECTOR" : joinWriteFixture ? "MUIJOIN_SPLIT_REJOIN_GUI_AND_NATIVE_FILES_REQUIRES_INDEPENDENT_BYTE_INSPECTOR" : joinPrefsFixture ? "MUIJOIN_PREFS_USE_DEFAULTS_AND_QUIT" : iconFixture ? "ICON_UPDATE_GUI_AND_NATIVE_FILE_SNAPSHOT_REQUIRES_INDEPENDENT_INSPECTOR" : popaslFixture ? "POPASL_ASYNC_ACTIVE_REQUESTER_CANCEL_AND_CLOSE" : joinRequesterFixture ? "MUIJOIN_SPLIT_REQUESTER_CANCEL_AND_CLOSE" : stringFixture ? "HARDWARE_STRING_TEST_RETURN_AND_CLOSE" : actions.Length > 0 ? "CONFIGURED_MENU_LAUNCH_AND_QUIT" : "OPEN_OBSERVE_5000_FIELDS_NATIVE_CLOSE_ONLY", stringInputPassed, requesterCancellationPassed, requesterWindow, unavailableActions, events,
    phase, action, finalObjects = state?.Objects.Count, finalClasses = state?.Classes, finalDepth = state?.Depth,
    finalActiveOperations = state?.ActiveOperations,
    originalNativeWindow, originalScreen, nativeWindowUnlinked,
};
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(result));
return success ? 0 : 2;

short Delta(int difference) => (short)Math.Clamp(difference == 0 ? 0 : difference / 2 == 0 ? Math.Sign(difference) : difference / 2, -16, 16);
void Key(byte raw) => machine.SubmitInput(new(0, 0, 0, 0, 0, (ushort)(0x100 | raw)));
void PressKey()
{
    Key((rawKeys[keyIndex] & 0x100) != 0 ? (byte)0x60 : (byte)rawKeys[keyIndex]);
    Transition((rawKeys[keyIndex] & 0x100) != 0 ? "shift-down" : "key-down");
}
bool NativeField(uint id, string text)
{
    var field = memory.StringInputs(requesterWindow).SingleOrDefault(g => g.Id == id);
    if (field is null || field.Text.Length > 128) { reason = "NATIVE_STRING_CONTROL_UNAVAILABLE"; return false; }
    nativeTyping = true; nativeField = field.Gadget; nativeExpected = text;
    // Native Amiga string gadgets support Right/Backspace. Walk to the end
    // before clearing; do not depend on a host keyboard's End shortcut.
    rawKeys = Enumerable.Repeat(0x4E, field.Text.Length).Concat(Enumerable.Repeat(0x41, field.Text.Length))
        .Concat(TextKeys(text)).ToArray(); keyIndex = 0;
    targetX = field.Left + field.Width / 2; targetY = field.Top + field.Height / 2;
    Transition("file-string-aim"); return true;
}
IEnumerable<int> TextKeys(string text)
{
    const string row1 = "qwertyuiop", row2 = "asdfghjkl", row3 = "zxcvbnm";
    foreach (var c in text)
    {
        if (c == ':') yield return 0x129;
        else if (c == '/') yield return 0x3A;
        else if (row1.IndexOf(c) is var a && a >= 0) yield return 0x10 + a;
        else if (row2.IndexOf(c) is var b && b >= 0) yield return 0x20 + b;
        else if (row3.IndexOf(c) is var d && d >= 0) yield return 0x31 + d;
        else throw new InvalidDataException("Unsupported fixture character");
    }
}
bool NativePoint(uint id, bool row)
{
    if (!memory.TryFilePickerPoint(requesterWindow, id, row ? selectionRow : -1, out targetX, out targetY))
    { reason = "NATIVE_FILE_PICKER_GEOMETRY_UNAVAILABLE"; return false; }
    Transition(row ? "file-list-aim" : "file-ok-aim"); return true;
}
bool HandleJoinFilePhase(int frame)
{
    if (frame - entered > 120000) { reason = "JOIN_FILE_PHASE_TIMEOUT: " + phase; Capture("file-timeout"); return false; }
    if (phase == "file-requester-wait")
    {
        requesterWindow = memory.U32(originalScreen + 4);
        if (requesterWindow != 0 && requesterWindow != originalNativeWindow &&
            memory.StringInputs(requesterWindow).Count == 2 &&
            memory.Text(memory.U32(requesterWindow + 32)).Contains(action == 0 ? "split" : "join", StringComparison.OrdinalIgnoreCase))
        { Capture("file-requester-ready"); nativeStage = "drawer"; return NativeField(9, "ram:muijoin"); }
    }
    else if (phase is "file-string-aim" or "file-list-aim" or "file-ok-aim")
    {
        var dx = targetX - memory.I16(requesterWindow + 14); var dy = targetY - memory.I16(requesterWindow + 12);
        if (Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2)
        { Input(1, 0, 0); Transition(phase == "file-string-aim" ? "file-string-down" : phase == "file-list-aim" ? "file-list-down" : "file-ok-down"); }
        else Input(0, Delta(dx), Delta(dy));
    }
    else if (phase == "file-string-down" && frame - entered >= 1000)
    { Input(0, 0, 0); Transition("file-string-focus"); }
    else if (phase == "file-string-focus" && frame - entered >= 2000) PressKey();
    else if (phase == "file-string-verify" && frame - entered >= 1000)
    {
        var field = memory.StringInputs(requesterWindow).FirstOrDefault(g => g.Gadget == nativeField);
        Capture("file-typed-" + nativeStage);
        events.Add(new { frame = machine.CompletedFrames, operation = "hardware-native-string", expected = nativeExpected, actual = field?.Text });
        if (field?.Text != nativeExpected) { reason = "NATIVE_STRING_INPUT_MISMATCH"; return false; }
        Key(0x44); Transition("file-enter-down");
    }
    else if (phase == "file-enter-up" && frame - entered >= (nativeStage == "drawer" ? 10000 : 1000))
    {
        nativeTyping = false;
        if (nativeStage == "drawer")
        {
            var drawer = memory.StringInputs(requesterWindow).FirstOrDefault(g => g.Id == 9);
            if (drawer is null || !drawer.Text.Contains("muijoin", StringComparison.OrdinalIgnoreCase))
            { reason = "OWNED_DRAWER_NOT_SELECTED"; return false; }
            Capture(action == 0 ? "split-drawer-ready" : "join-five-file-drawer-ready");
            if (action == 0) { nativeStage = "file"; return NativeField(10, "muifix"); }
            // This private fixture contains only muifix and its four parts.
            // First click clears the default selection; Shift adds the rest.
            selectionRow = 1; return NativePoint(12, true);
        }
        Transition("file-operation-wait");
    }
    else if (phase == "file-list-down" && frame - entered >= 1000)
    { Input(0, 0, 0); Transition("file-list-up"); }
    else if (phase == "file-list-up" && frame - entered >= 1000)
    {
        if (selectionRow == 1) { Key(0x60); Transition("file-shift-ready"); }
        else if (selectionRow < 4) { selectionRow++; return NativePoint(12, true); }
        else { Key(0xE0); Transition("file-shift-released"); }
    }
    else if (phase == "file-shift-ready" && frame - entered >= 1000)
    { selectionRow = 2; return NativePoint(12, true); }
    else if (phase == "file-shift-released" && frame - entered >= 1000)
    { Capture("join-four-parts-selected"); return NativePoint(1, false); }
    else if (phase == "file-ok-down" && frame - entered >= 1000)
    { Input(0, 0, 0); Transition("file-operation-wait"); }
    else if (phase == "file-operation-wait")
    {
        if (state!.Objects.Any(o => o.Class == "Window.mui" && o.NativeWindow != 0 && o.NativeWindow != originalNativeWindow))
        { reason = "UNEXPECTED_MUI_FILE_OPERATION_DIALOG"; Capture("file-operation-dialog"); return false; }
        if (frame - entered >= 1000 && state.Depth == 0 &&
            !state.Objects.Any(o => o.ApplicationSleep != 0) &&
            !memory.ScreenContainsWindow(originalScreen, requesterWindow))
        { Capture("file-operation-returned-" + action); action++; return SelectButton(); }
    }
    return true;
}
bool SelectEmptyString()
{
    var field = state!.Objects.Where(o => o.Class == "String.mui" && o.Visible &&
        o.ContainingWindow == windowObject && o.Width >= 6 && o.Height >= 6 && o.Text == "")
        .OrderBy(o => o.Top).ThenBy(o => o.Left).FirstOrDefault();
    if (field is null) { reason = "EMPTY_STRING_FIELD_UNAVAILABLE"; return false; }
    target = field.Object; targetX = field.Left + field.Width / 2; targetY = field.Top + field.Height / 2;
    Transition("string-aim"); return true;
}
void Input(byte buttons, short dx, short dy)
{
    if (machine.CompletedFrames - lastInput < 50) return;
    machine.SubmitInput(new(0, 0, buttons, dx, dy, 0)); lastInput = (int)machine.CompletedFrames;
}
void Transition(string next)
{
    phase = next; entered = (int)machine.CompletedFrames;
    events.Add(new { frame = machine.CompletedFrames, phase, action, target, targetX, targetY });
    Console.WriteLine(JsonSerializer.Serialize(events[^1]));
}
bool SelectButton()
{
    var matches = state!.Objects.Where(o => o.Class == "Text.mui" && o.InputMode is >= 1 and <= 3 &&
        o.Visible && o.Width >= 6 && o.Height >= 6 &&
        (joinPrefsFixture ? state.Objects.Any(w => w.Object == o.ContainingWindow && w.NativeWindow != 0) : o.ContainingWindow == windowObject) &&
        string.Equals(Normalize(o.Text), actions[action], StringComparison.OrdinalIgnoreCase)).ToArray();
    if (matches.Length != 1 || matches[0].Width < 6 || matches[0].Height < 6)
    {
        unavailableActions.Add(actions[action]); Capture("button-unavailable");
        if (joinWriteFixture) { reason = "REQUIRED_JOIN_ACTION_UNAVAILABLE"; return false; }
        events.Add(new { frame = machine.CompletedFrames, operation = actions[action], result = "UNAVAILABLE_NOT_UNIQUELY_VISIBLE" });
        if (++action < actions.Length) return SelectButton();
        reason = "BUTTON_NOT_UNIQUELY_VISIBLE"; return false;
    }
    var button = matches[0]; target = button.Object;
    if (joinPrefsFixture) windowObject = button.ContainingWindow;
    targetX = button.Left + button.Width / 2; targetY = button.Top + button.Height / 2;
    Transition("aim"); return true;
}
string Normalize(string text)
{
    var result = new StringBuilder();
    for (var i = 0; i < text.Length; i++) { if (text[i] == '\x1b') { i++; continue; } result.Append(text[i]); }
    return result.ToString().Trim();
}
void Capture(string label)
{
    var stem = Path.Combine(output, $"frame-{machine.CompletedFrames:D6}-{label}");
    File.WriteAllText(stem + ".json", JsonSerializer.Serialize(new
    {
        machine.CompletedFrames, phase, action, state,
        FirstExceptionVector = CpuValue("FirstExceptionVector"), FirstExceptionInstructionProgramCounter = CpuValue("FirstExceptionInstructionProgramCounter"),
        FirstExceptionStackedProgramCounter = CpuValue("FirstExceptionStackedProgramCounter"), FirstExceptionOpcode = CpuValue("FirstExceptionOpcode"),
        FirstExceptionA7 = CpuValue("FirstExceptionA7"), LastExceptionVector = CpuValue("LastExceptionVector"),
        LastExceptionInstructionProgramCounter = CpuValue("LastExceptionInstructionProgramCounter"), LastExceptionStackedProgramCounter = CpuValue("LastExceptionStackedProgramCounter"),
        LastExceptionOpcode = CpuValue("LastExceptionOpcode"), LastExceptionA0 = CpuValue("LastExceptionA0"), LastExceptionA6 = CpuValue("LastExceptionA6"),
        LastExceptionA7 = CpuValue("LastExceptionA7"), ExceptionSequence = CpuValue("ExceptionSequence"),
        nativeRequesterInputs = requesterWindow == 0 ? new List<NativeStringInput>() : memory.StringInputs(requesterWindow),
        pc = machine.Cpu.ProgramCounter.ToString("X8"),
        userStackPointer = CpuValue("UserStackPointer").ToString("X8"),
        supervisorStackPointer = CpuValue("SupervisorStackPointer").ToString("X8"),
        status = machine.Cpu.StatusRegister.ToString("X4"),
        data = machine.Cpu.D.ToArray().Select(v => v.ToString("X8")).ToArray(),
        address = machine.Cpu.A.ToArray().Select(v => v.ToString("X8")).ToArray(),
    }, new JsonSerializerOptions { WriteIndented = true }));
    using (var writer = new BinaryWriter(File.Create(stem + ".bmp")))
    {
        var pixels = machine.Framebuffer.Span;
        writer.Write((ushort)0x4D42); writer.Write(54 + pixels.Length * 4); writer.Write(0); writer.Write(54); writer.Write(40);
        writer.Write(machine.FramebufferWidth); writer.Write(-machine.FramebufferHeight); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(0); writer.Write(pixels.Length * 4); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        foreach (var pixel in pixels) writer.Write(pixel);
    }
    File.WriteAllBytes(stem + ".fastram", machine.FastRam.ToArray());
    File.WriteAllBytes(stem + ".chipram", machine.ChipRam.ToArray());
    File.WriteAllBytes(stem + ".slowram", slow);
    File.WriteAllText(stem + ".memory.json", JsonSerializer.Serialize(new { fastRamBytes = machine.FastRam.Length, fastRamBase = machine.FastRamBase }));
}

sealed record GuestObject(uint Object, string Class, string Text, uint InputMode, uint Pressed,
    int Left, int Top, int Width, int Height, bool Visible, uint NativeWindow, uint ContainingWindow,
    uint PopButton, uint PopActive, uint ChoiceActive, uint Selected, uint ApplicationSleep);
sealed record State(uint Library, uint Root, uint Depth, uint ActiveOperations, int Classes, List<GuestObject> Objects);
sealed record NativeStringInput(uint Gadget, uint Id, uint Flags, int Left, int Top,
    int Width, int Height, uint Buffer, int MaxChars, string Text);
sealed class Reader(LightweightA500Machine machine, byte[] slow)
{
    public bool JoinSnapshotReady()
    {
        if (machine.FastRamBase is not uint b) return false;
        var data = machine.FastRam.Span;
        for (var i = 16; i + 72 < data.Length; i += 2)
            if (BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i, 4)) == 0x4A574132 &&
                U8(b + (uint)i - 8) == 8 && Text(U32(b + (uint)i - 6)) == "MUI_JoinAfter2" && U32(b + (uint)i + 4) == 1 &&
                U32(b + (uint)i + 8) == 12345 && U32(b + (uint)i + 12) == 4096 && U32(b + (uint)i + 16) == 4096 &&
                U32(b + (uint)i + 20) == 4096 && U32(b + (uint)i + 24) == 57 && U32(b + (uint)i + 28) == 12345) return true;
        return false;
    }
    public bool TryFilePickerPoint(uint window, uint id, int row, out int x, out int y)
    {
        x = y = 0;
        if (!Mapped(window, 136)) return false;
        var gadget = U32(window + 62);
        for (var i = 0; gadget != 0 && i < 256; i++, gadget = U32(gadget))
        {
            if (!Mapped(gadget, 44)) return false;
            if ((ushort)I16(gadget + 38) != id || ((ushort)I16(gadget + 16) & 0x8000) != 0) continue;
            var left = I16(gadget + 4); var top = I16(gadget + 6); var width = I16(gadget + 8); var height = I16(gadget + 10);
            if (left < 0 || top < 11 || width < 20 || height < 8 || left + width > I16(window + 8) || top + height > I16(window + 10)) return false;
            if (row >= 0)
            {
                // Captured ASL 40.6 list ID 12: Topaz-8 glyphs use a 9-pixel
                // row pitch. Reject other fonts/geometry; inspect the captured
                // five-file drawer and selected rows before any byte verdict.
                var rp = U32(window + 50); var font = U32(rp + 52);
                if (!Mapped(font, 22) || I16(font + 20) != 8 || height < 49 || row is < 1 or > 4) return false;
                x = left + 20; y = top + 2 + row * 9 + 4;
            }
            else { x = left + width / 2; y = top + height / 2; }
            return true;
        }
        return false;
    }
    public List<NativeStringInput> StringInputs(uint window)
    {
        var inputs = new List<NativeStringInput>();
        if (!Mapped(window, 136)) return inputs;
        var gadget = U32(window + 62);
        for (var i = 0; gadget != 0 && i < 256; i++, gadget = U32(gadget))
        {
            if (!Mapped(gadget, 44)) break;
            var kind = (ushort)I16(gadget + 16) & 7;
            var info = U32(gadget + 34);
            if (kind is not (4 or 5) || !Mapped(info, 36)) continue;
            var buffer = U32(info); var max = I16(info + 10); var count = I16(info + 14);
            if (max < 2 || max > 4096 || count < 0 || count >= max || !Mapped(buffer, (uint)max)) continue;
            inputs.Add(new(gadget, (ushort)I16(gadget + 38), (ushort)I16(gadget + 12),
                I16(gadget + 4), I16(gadget + 6), I16(gadget + 8), I16(gadget + 10),
                buffer, max, Text(buffer)));
        }
        return inputs;
    }
    public bool IconSnapshotReady()
    {
        if (machine.FastRamBase is not uint b) return false;
        var data = machine.FastRam.Span;
        for (var i = 16; i + 40 < data.Length; i += 2)
            if (BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i, 4)) == 0x49554131 &&
                U8(b + (uint)i - 8) == 8 && Text(U32(b + (uint)i - 6)) == "MUI_IconAfter" &&
                U32(b + (uint)i + 4) == 1 && U32(b + (uint)i + 8) is > 0 and <= 512 &&
                U32(b + (uint)i + 12) is > 0 and <= 512 &&
                U32(b + (uint)i + 24) == 1 && U32(b + (uint)i + 28) == 1) return true;
        return false;
    }
    bool Mapped(uint a, uint length) => a != 0 && ((ulong)a + length <= (ulong)machine.ChipRam.Length ||
        a >= 0xC00000 && (ulong)a - 0xC00000 + length <= (ulong)slow.Length ||
        machine.FastRamBase is uint b && a >= b && (ulong)a - b + length <= (ulong)machine.FastRam.Length);
    public byte U8(uint a) => a < machine.ChipRam.Length ? machine.ChipRam.Span[(int)a] :
        a >= 0xC00000 && a - 0xC00000 < slow.Length ? slow[a - 0xC00000] :
        machine.FastRamBase is uint b && a >= b && a - b < machine.FastRam.Length ? machine.FastRam.Span[(int)(a - b)] : (byte)0;
    public uint U32(uint a) => (uint)(U8(a) << 24 | U8(a + 1) << 16 | U8(a + 2) << 8 | U8(a + 3));
    public short I16(uint a) => unchecked((short)(U8(a) << 8 | U8(a + 1)));
    public bool ScreenContainsWindow(uint screen, uint window)
    {
        // Unknown or malformed capture state cannot establish successful quit.
        if (window == 0 || !Mapped(screen, 8)) return true;
        var current = U32(screen + 4);
        for (var i = 0; current != 0 && i < 256; i++)
        {
            if (current == window || !Mapped(current, 4)) return true;
            current = U32(current);
        }
        return current != 0;
    }
    public bool TryCancelGadget(uint window, out int x, out int y)
    {
        x = y = 0;
        if (!Mapped(window, 136)) return false;
        var gadget = U32(window + 62);
        for (var i = 0; gadget != 0 && i < 256; i++, gadget = U32(gadget))
        {
            if (!Mapped(gadget, 44)) return false;
            // Classic Intuition's system close gadget cancels an ASL file
            // requester. GadTools button labels need not use GadgetText.
            var kind = (ushort)I16(gadget + 16);
            var systemClose = (kind & 0x80F0) == 0x8080;
            var text = U32(gadget + 26);
            var label = Mapped(text, 20) ? Text(U32(text + 12)).Replace("_", "").Trim() : "";
            if (!systemClose && !label.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) continue;
            // This fixture admits ordinary positive-size, absolute gadgets.
            // Relative geometry needs its own explicitly checked protocol.
            if ((I16(gadget + 12) & 0xF0) != 0 || I16(gadget + 8) <= 0 || I16(gadget + 10) <= 0) return false;
            x = I16(gadget + 4) + I16(gadget + 8) / 2;
            y = I16(gadget + 6) + I16(gadget + 10) / 2;
            return true;
        }
        // CopperOS Intuition handles its drawn system gadgets directly and
        // does not expose them as application Gadget nodes. Admit its public
        // CloseGadget flag and header geometry in this bounded A500 profile.
        if ((U32(window + 24) & 8) != 0 && U8(window + 55) >= 8)
        { x = 4; y = U8(window + 55) / 2; return true; }
        return false;
    }
    public string Text(uint a)
    {
        var b = new List<byte>(); for (uint i = 0; a != 0 && i < 512 && U8(a + i) != 0; i++) b.Add(U8(a + i));
        return Encoding.Latin1.GetString(b.ToArray());
    }
    public uint FindLibrary()
    {
        if (machine.FastRamBase is not uint b) return 0;
        var d = machine.FastRam.Span;
        for (var i = 16; i + 32 < d.Length; i += 2)
            if (BinaryPrimitives.ReadUInt32BigEndian(d.Slice(i, 4)) == 0x02F40030 &&
                BinaryPrimitives.ReadUInt32BigEndian(d.Slice(i + 4, 4)) is 0x00040001 or 0x000C0001 && Text(U32(b + (uint)i - 6)) == "muimaster.library")
                return b + (uint)i - 16;
        return 0;
    }
    uint Attribute(uint record, uint key, uint fallback = 0)
    {
        var a = U32(record + 12);
        for (var i = 0; a != 0 && i < 4096; i++, a = U32(a)) if (U32(a + 4) == key) return U32(a + 8);
        return fallback;
    }
    uint Geometry(uint record)
    {
        var a = U32(record + 32);
        for (var i = 0; a != 0 && i < 1024; i++, a = U32(a))
            if (U32(a + 4) == 0x7F070035 && U32(a + 12) == 28 && U32(U32(a + 8)) == 0x4D414745) return U32(a + 8);
        return 0;
    }
    public State? ReadState(uint library)
    {
        if (Text(U32(library + 10)) != "muimaster.library" ||
            U32(library + 16) != 0x02F40030 || U32(library + 20) is not (0x00040001 or 0x000C0001)) return null;
        var root = U32(library + 42); var registry = U32(root);
        if (!Mapped(root, 48) || !Mapped(registry, 32)) return null;
        var owner = U32(root + 24);
        if (U32(registry) != 0x4D554934 || U32(registry + 4) != 1 || !Mapped(owner, 56) ||
            U32(owner + 8) != 0x4D554F31 || U32(owner + 12) != 6) return null;
        var objects = new List<GuestObject>(); var rec = U32(registry + 12);
        for (var i = 0; rec != 0 && i < 1024; i++, rec = U32(rec))
        {
            if (!Mapped(rec, 64)) return null;
            var geometry = Geometry(rec); var visible = Attribute(rec, 0x80429BA8, 1) != 0;
            uint containingWindow = 0;
            for (uint parent = U32(rec + 28), n = 0; parent != 0 && n < 64; parent = U32(parent + 28), n++)
            {
                visible &= Attribute(parent, 0x80429BA8, 1) != 0;
                if (Text(U32(U32(parent + 8) + 4)) == "Window.mui") containingWindow = U32(parent + 4);
            }
            var className = Text(U32(U32(rec + 8) + 4));
            objects.Add(new(U32(rec + 4), className, Text(Attribute(rec, className == "String.mui" ? 0x80428FFD : 0x8042F8DC)),
                Attribute(rec, 0x8042FB04), Attribute(rec, 0x80423535),
                geometry == 0 ? 0 : unchecked((int)U32(geometry + 4)), geometry == 0 ? 0 : unchecked((int)U32(geometry + 8)),
                geometry == 0 ? 0 : unchecked((int)U32(geometry + 12)), geometry == 0 ? 0 : unchecked((int)U32(geometry + 16)),
                visible, Attribute(rec, 0x7FFE0011), containingWindow,
                Attribute(rec, 0x8042D0B9), Attribute(rec, 0x80421B37),
                Attribute(rec, className == "Radio.mui" ? 0x80429B41 : 0x80421788), Attribute(rec, 0x8042654B), Attribute(rec, 0x80425711)));
        }
        var classes = 0;
        for (var c = U32(registry + 8); c != 0 && classes < 64; c = U32(c)) classes++;
        return new(library, root, U32(root + 32) + U32(root + 36), U32(owner + 28), classes, objects);
    }
}
