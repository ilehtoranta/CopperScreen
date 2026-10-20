#if COPPERSCREEN_INSTALLED_TESTS
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace CopperScreen;

// Compiled only into uniquely identified diagnostic packages, never Store builds.
internal static class CopperScreenInstalledProbe
{
    internal static CopperScreenRecentSession? ActiveSession { get; set; }
    private static bool _windowOpened;
    private static object? _launcherLayout;
    private static string? _layoutError;
    private static string _layoutOutput = "";

    internal static void Observe(Window window)
    {
        if (ActiveSession == null) return;
        window.Opened += (_, _) => DispatcherTimer.RunOnce(async () =>
        {
            MainWindow? launcher = null;
            try
            {
                _windowOpened = window.IsVisible && window.ClientSize.Width > 0 && window.ClientSize.Height > 0;
                // Keep the persistence fixture untouched when changing the launcher's boot choice.
                launcher = new MainWindow([], new CopperScreenRecentSession(Path.Combine(ActiveSession.DataDirectory, "LauncherLayout")));
                launcher.Show();
                launcher.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Boot method").SelectedIndex = 3;
                var cases = new List<object>();
                foreach (var (width, height) in new[] { (800, 600), (1280, 720), (960, 640) })
                {
                    launcher.SizeToContent = SizeToContent.Manual;
                    launcher.Width = width;
                    launcher.Height = height;
                    await Task.Delay(250); // Settle real platform resize and scroll notifications.
                    launcher.UpdateLayout();
                    var scroll = launcher.GetLogicalDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LauncherScroll");
                    var viewport = scroll.TranslatePoint(default, launcher)!.Value;
                    Require(Math.Abs(launcher.ClientSize.Width - width) <= 1 && Math.Abs(launcher.ClientSize.Height - height) <= 1,
                        "The native window did not accept the requested bounded client size.");
                    Require(scroll.Viewport.Width > 0 && scroll.Viewport.Height > 0 && scroll.Extent.Width <= scroll.Viewport.Width + 1,
                        "The launcher needs horizontal scrolling or has an empty viewport.");
                    Require(viewport.Y >= 0 && viewport.Y + scroll.Bounds.Height <= launcher.ClientSize.Height + 1,
                        "Launcher scroll viewport extends outside the actual client area.");
                    var save = launcher.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Save launch profile");
                    Require(save.IsEffectivelyVisible && save.Bounds.Width > 0 && save.Bounds.Height > 0, "Profile action has no visible hit target.");
                    save.BringIntoView();
                    await Task.Delay(250);
                    launcher.UpdateLayout();
                    var position = save.TranslatePoint(default, launcher)!.Value;
                    var inScroll = save.TranslatePoint(default, scroll)!.Value;
                    Require(position.X >= 0 && position.Y >= 0 && position.X + save.Bounds.Width <= launcher.ClientSize.Width + 1 &&
                        position.Y + save.Bounds.Height <= launcher.ClientSize.Height + 1 && inScroll.Y >= -1 &&
                        inScroll.Y + save.Bounds.Height <= scroll.Bounds.Height + 1, "Save launch profile is outside the visible client/scroll area.");
                    var capture = Path.Combine(_layoutOutput, $"launcher-{width}x{height}.png");
                    var scale = launcher.RenderScaling;
                    using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(launcher.ClientSize.Width * scale),
                        (int)Math.Ceiling(launcher.ClientSize.Height * scale)), new Vector(96 * scale, 96 * scale)))
                    { bitmap.Render(launcher); bitmap.Save(capture); }
                    cases.Add(new { requestedWidth = width, requestedHeight = height, clientWidth = launcher.ClientSize.Width,
                        clientHeight = launcher.ClientSize.Height, renderScaling = scale, viewportX = viewport.X, viewportY = viewport.Y,
                        viewportWidth = scroll.Bounds.Width, viewportHeight = scroll.Bounds.Height, extentWidth = scroll.Extent.Width,
                        extentHeight = scroll.Extent.Height, offsetY = scroll.Offset.Y, targetX = position.X, targetY = position.Y,
                        targetWidth = save.Bounds.Width, targetHeight = save.Bounds.Height, targetInScrollY = inScroll.Y, capture });
                }
                _launcherLayout = new { protocol = "copperscreen-installed-launcher-layout-v1", passed = true, bootMethod = "CopperOS", cases };
            }
            catch (Exception ex) { _layoutError = ex.ToString(); }
            finally { launcher?.Close(); window.Close(); }
        }, TimeSpan.FromSeconds(1));
    }

    internal static void Run(string[] args, Action<CopperScreenRecentSession> showWindow)
    {
        if (args.Length != 4 || !Guid.TryParseExact(args[2], "N", out var run) || !Path.IsPathFullyQualified(args[3]))
        { Environment.ExitCode = 2; return; }
        var report = new Dictionary<string, object?> { ["SchemaVersion"] = 1, ["Phase"] = args[1], ["RunId"] = args[2], ["Passed"] = false };
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Installed package diagnostics require Windows.");
            var media = args[1].StartsWith("media-", StringComparison.Ordinal);
            var phase = media ? args[1][6..] : args[1];
            if (phase is not ("seed" or "verify" or "fresh") && !(media && phase is "recovery" or "recovered")) throw new ArgumentException("Unknown installed-test phase.");
            report["MediaMode"] = media;
            var package = PackageFullName(); // An unpackaged executable cannot produce passing evidence.
            var manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppxManifest.xml"));
            var identity = manifest.Root!.Elements().Single(e => e.Name.LocalName == "Identity");
            var name = (string)identity.Attribute("Name")!;
            Require(name == "CopperScreen.Test." + run.ToString("N"), "Diagnostic package identity does not match the run.");
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            Require(!principal.IsInRole(WindowsBuiltInRole.Administrator), "Installed app must run as a standard, non-elevated process.");
            var directory = Path.Combine(CopperScreenUserData.DefaultDirectory, "InstalledTests", run.ToString("N"));
            var paths = new[] { Path.Combine(directory, "Profiles", CopperScreenProfile.DefaultProfileId + ".json"),
                Path.Combine(directory, "last-machine.json"), Path.Combine(directory, "recent-session.json") };
            var presets = PresetHash();
            report["PackageFullName"] = package;
            report["PackageFamilyName"] = CopperScreenUserData.CurrentPackageFamily();
            report["PackageVersion"] = (string?)identity.Attribute("Version");
            report["UserDataRoot"] = CopperScreenUserData.DefaultDirectory;
            report["UserDataDirectory"] = directory;
            report["InstallDirectory"] = AppContext.BaseDirectory;
            report["Elevated"] = false;

            if (phase is "seed" or "fresh")
                Require(paths.All(p => !File.Exists(p)), "Expected clean package-private profile/settings data.");
            if (phase == "seed")
            {
                Directory.CreateDirectory(directory);
                var disk = Path.Combine(directory, "synthetic-empty.adf");
                File.WriteAllBytes(disk, []); // Host storage fixture, not native-media replay.
                var draft = new CopperScreenSettingsDraft { DisplayName = "Installed profile å " + run.ToString("N"), RtcEnabled = true,
                    Input = CopperScreenInputOptions.Default.WithPortAssignment(2, "wasd-joystick") };
                draft.DriveDiskPaths[0] = disk;
                draft.DriveWriteProtected[0] = false;
                CopperScreenProfileStore.Save(draft, AppContext.BaseDirectory, directory);
                var recent = new CopperScreenRecentSession(directory);
                recent.RememberDisk(disk);
                recent.Save(draft, 37, true, AppContext.BaseDirectory);
                Require(recent.LastError == null, recent.LastError ?? "Preferences save failed.");
            }
            if (phase != "fresh") Verify(directory, run);
            var guestRoot = Path.Combine(AppContext.BaseDirectory, "Diagnostics", "GuestProof");
            var guestSeed = Path.Combine(guestRoot, "System.adf");
            var guestRom = Path.Combine(guestRoot, "CopperStart-DiskBoot.rom");
            var output = Path.GetDirectoryName(args[3])!;
            if (media) Require(File.Exists(guestSeed) && File.Exists(guestRom), "P1-06 installed qualification requires the exact qualified guest pair.");
            var directImage = media ? CopperScreenInstalledMediaProof.Prepare(phase, directory, output) : null;
            var nativeResult = File.Exists(guestSeed) && File.Exists(guestRom)
                ? phase == "fresh" && !media
                    ? new { status = "fresh-after-uninstall", present = Directory.Exists(Path.Combine(directory,"CopperOS","Workspaces","persistence")) }
                    : CopperOsNativeStorageProof.Run(guestSeed, guestRom, phase == "recovery" ? Path.Combine(directory, "RecoveryNative") : directory,
                        Path.Combine(output, "native-storage-" + Path.GetFileNameWithoutExtension(args[3])), phase is "verify" or "recovered" or "fresh", AppContext.BaseDirectory,
                        denyHostSaves: phase == "recovery", directImage: directImage)
                : new { status = "UNAVAILABLE: qualified native storage pair not supplied" };
            report["CopperOsStorage"] = nativeResult;
            if (media) report["MediaPersistence"] = CopperScreenInstalledMediaProof.Complete(phase, directory, output, nativeResult);
            if (phase == "fresh" && File.Exists(guestSeed))
                Require(!Directory.Exists(Path.Combine(directory,"CopperOS","Workspaces","persistence")), "Uninstall/reinstall retained package-private native workspace unexpectedly.");
            var before = paths.Where(File.Exists).ToDictionary(p => Path.GetFileName(p)!, p => Hash(p));
            _layoutOutput = Path.Combine(output, "launcher-layout-" + Path.GetFileNameWithoutExtension(args[3]));
            Directory.CreateDirectory(_layoutOutput);
            showWindow(new CopperScreenRecentSession(directory));
            Require(_windowOpened, "Packaged desktop window did not open.");
            report["LauncherLayout"] = _launcherLayout;
            Require(_layoutError == null && _launcherLayout != null, "Installed launcher layout failed: " + _layoutError);
            if (phase != "fresh")
            {
                Verify(directory, run);
                // The standard close path may normalize JSON, but must preserve its contents.
                Require(Hash(paths[0]) == before[Path.GetFileName(paths[0])], "Opening/closing changed the saved personal profile.");
            }
            Require(presets == PresetHash(), "Saving user data changed bundled presets.");
            report["Files"] = paths.Where(File.Exists).ToDictionary(p => Path.GetFileName(p)!, p => Hash(p));
            report["PresetHash"] = presets;
            report["WindowOpened"] = _windowOpened;
            report["Passed"] = true;
        }
        catch (Exception ex)
        {
            report["Error"] = ex.ToString();
            Environment.ExitCode = 1;
        }
        finally
        {
            CopperScreenUserData.WriteAtomically(args[3], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void Verify(string directory, Guid run)
    {
        var profile = CopperScreenProfile.LoadDefault(AppContext.BaseDirectory, out var error, userDataDirectory: directory);
        Require(error == null && profile.DisplayName == "Installed profile å " + run.ToString("N") && profile.RtcEnabled,
            "Personal profile was not preserved: " + error);
        Require(profile.Input.Port2ProfileId == "wasd-joystick", "Controller preference was not preserved.");
        var recent = new CopperScreenRecentSession(directory);
        Require(recent.LastError == null && recent.Volume == 37 && recent.Muted, "Volume/mute preferences were not preserved.");
        var machine = recent.LoadMachine(AppContext.BaseDirectory);
        Require(machine != null && machine.RtcEnabled && machine.DisplayName == profile.DisplayName, "Last machine was not preserved.");
        Require(machine!.DriveDiskPaths[0] == Path.Combine(directory, "synthetic-empty.adf") && machine.DriveWriteProtected[0] == false,
            "Media settings were not preserved.");
        Require(recent.RecentDisks.SequenceEqual([machine.DriveDiskPaths[0]!]), "Recent media history was not preserved.");
    }

    private static string PresetHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Presets"), "*.json").Order(StringComparer.Ordinal)
            .Select(path => Path.GetFileName(path) + ":" + Hash(path))))));
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static string PackageFullName()
    {
        uint length = 0;
        var status = GetCurrentPackageFullName(ref length, null);
        if (status != 122) throw new Win32Exception(status, "Activate the installed package through its application identity.");
        var name = new StringBuilder((int)length);
        status = GetCurrentPackageFullName(ref length, name);
        if (status != 0) throw new Win32Exception(status);
        return name.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}
#endif
