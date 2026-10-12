using System.IO;
using System.Security.Cryptography;

namespace Nexabode.Data;

public static class DatabaseKeyProvider
{
    public static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexabode");

    public static string DbPath => Path.Combine(AppFolder, "nexabode.db");

    // Creates a random 256-bit key on first run and stores it nice and neatly (protected) in windows
    public static string GetOrCreateKey()
    {
        Directory.CreateDirectory(AppFolder);
        var keyFile = Path.Combine(AppFolder, "db.key");

        if (File.Exists(keyFile))
        {
            var protectedBytes = File.ReadAllBytes(keyFile);
            var key = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(key);
        }

        var newKey = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(keyFile, ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser));
        return Convert.ToBase64String(newKey);
    }
}