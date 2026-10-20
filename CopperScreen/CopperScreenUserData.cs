using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CopperScreen;

internal static class CopperScreenUserData
{
    public static string DefaultDirectory
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local))
                throw new IOException("The Windows user-data folder is unavailable. Check your user profile and permissions.");
            // Do not rely on AppData write virtualization: packaged desktop writes
            // can reach an existing shared folder and survive package removal.
            // The family stays stable across updates; Windows owns LocalState.
            var family = OperatingSystem.IsWindows() ? CurrentPackageFamily() : null;
            return family == null
                ? Path.Combine(local, "CopperScreen")
                : Path.Combine(local, "Packages", family, "LocalState", "CopperScreen");
        }
    }

    internal static string? CurrentPackageFamily()
    {
        uint length = 0;
        var status = GetCurrentPackageFamilyName(ref length, null);
        if (status == 15700) return null; // APPMODEL_ERROR_NO_PACKAGE: portable/development app.
        if (status != 122) throw new Win32Exception(status, "Cannot determine the app's package-owned data folder.");
        var family = new StringBuilder(checked((int)length));
        status = GetCurrentPackageFamilyName(ref length, family);
        if (status != 0) throw new Win32Exception(status, "Cannot determine the app's package-owned data folder.");
        return family.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, StringBuilder? packageFamilyName);

    public static string GetDirectory(string? directory = null) => Path.GetFullPath(directory ?? DefaultDirectory);

    // Publish only a complete file. A failed write leaves the previous file intact.
    internal static void WriteAtomically(string path, string contents, bool overwrite = true)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(contents);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
