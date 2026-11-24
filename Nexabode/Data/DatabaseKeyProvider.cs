using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Nexabode.Data;

public static class DatabaseKeyProvider
{
    private const int KeyLength = 32;
    private static readonly object KeyLock = new();
    private static string? _cachedKey;

    public static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexabode");

    public static string DbPath => Path.Combine(AppFolder, "nexabode.db");

    private static string KeyPath => Path.Combine(AppFolder, "db.key");
    private static string KeyBackupPath => KeyPath + ".bak";

    // Returns the database key (base64). The key is read and decrypted once, then kept in memory.
    // A new key is created only on a first run. If the database already exists but the key cannot be
    // read, this stops with a clear message instead of quietly making a new key the database can't use.
    public static string GetOrCreateKey()
    {
        lock (KeyLock)
        {
            if (_cachedKey != null) return _cachedKey;

            Directory.CreateDirectory(AppFolder);

            var key = TryReadKey(KeyPath);

            // The main key file is missing or damaged: recover from the backup copy.
            if (key == null)
            {
                key = TryReadKey(KeyBackupPath);
                if (key != null) WriteKeyFile(KeyPath, key);
            }

            if (key != null)
            {
                RefreshBackup(key);
                return _cachedKey = Convert.ToBase64String(key);
            }

            if (DatabaseExists())
                throw new InvalidOperationException(
                    "Nexabode could not read its encryption key (db.key), and no usable backup copy of the key was found. " +
                    "Your data cannot be opened without it. The key only works for the Windows account that created it, " +
                    "so check that you are signed in to the same Windows account. " +
                    "If the key is lost, restore a .nexabak backup. Do not delete the Nexabode data folder.");

            // First run: no database yet, so a new key is safe.
            var newKey = RandomNumberGenerator.GetBytes(KeyLength);
            WriteKey(newKey);
            return _cachedKey = Convert.ToBase64String(newKey);
        }
    }

    // Used when restoring a backup: stores the backup's key, protected for this Windows user.
    public static void SetKey(byte[] key)
    {
        lock (KeyLock)
        {
            Directory.CreateDirectory(AppFolder);
            WriteKey(key);
            _cachedKey = Convert.ToBase64String(key);
        }
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

    // ---------- helpers ----------

    private static bool DatabaseExists() =>
        File.Exists(DbPath) && new FileInfo(DbPath).Length > 0;

    // Writes the key file and its backup copy. The backup always matches the current key.
    private static void WriteKey(byte[] key)
    {
        WriteKeyFile(KeyPath, key);
        WriteKeyFile(KeyBackupPath, key);
    }

    // Writes to a temporary file first and swaps it in, so a crash or power cut can never leave a
    // half-written key file. The old file stays intact until the new one is complete.
    private static void WriteKeyFile(string path, byte[] key)
    {
        var protectedBytes = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
        var temp = path + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(protectedBytes);
            stream.Flush(true);   // push it to disk before swapping
        }

        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }

    // Returns the key in the file, or null if the file is missing, damaged, or was made by another Windows account.
    private static byte[]? TryReadKey(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var key = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            return key.Length == KeyLength ? key : null;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Keeps the backup copy in step with the key in use. Not critical, so a failure here is ignored.
    private static void RefreshBackup(byte[] key)
    {
        try
        {
            var existing = TryReadKey(KeyBackupPath);
            if (existing != null && existing.AsSpan().SequenceEqual(key)) return;
            WriteKeyFile(KeyBackupPath, key);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
        }
    }
}
