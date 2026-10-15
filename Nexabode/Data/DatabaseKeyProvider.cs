using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Nexabode.Data;

public static class DatabaseKeyProvider
{
    public static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexabode");

    public static string DbPath => Path.Combine(AppFolder, "nexabode.db");

    // Creates a random 256-bit key on first run and stores it protected by Windows (DPAPI).
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

    // Used when restoring a backup: stores the backup's key, protected for this Windows user.
    public static void SetKey(byte[] key)
    {
        Directory.CreateDirectory(AppFolder);
        var keyFile = Path.Combine(AppFolder, "db.key");
        File.WriteAllBytes(keyFile, ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser));
    }

    // One place that builds the encrypted connection. The cipher settings keep the SQLCipher 4
    // file format, so databases and backups made before the package swap keep working.
    public static string BuildConnectionString(string path, string base64Key, bool pooling = true)
    {
        var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri + "?cipher=sqlcipher&legacy=4";

        return new SqliteConnectionStringBuilder
        {
            DataSource = uri,
            Password = base64Key,
            Pooling = pooling
        }.ToString();
    }
}