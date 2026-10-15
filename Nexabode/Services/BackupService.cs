using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;

namespace Nexabode.Services;

public static class BackupService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("NXBK1");
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;

    // Where the app keeps an automatic copy of your data just before a restore.
    public static string SafetyFolder => Path.Combine(DatabaseKeyProvider.AppFolder, "SafetyCopies");

    // Makes sure the database file on disk is complete and not held open by a leftover connection.
    private static void FlushDatabase()
    {
        try
        {
            using var db = new AppDbContext();
            db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
        }
        catch { /* the copy below still works if no checkpoint was needed */ }

        SqliteConnection.ClearAllPools();
    }

    private static byte[] ReadAllShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void DeleteQuietly(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static byte[] BuildHeader(int iterations, byte[] salt)
    {
        var header = new byte[Magic.Length + 4 + SaltSize];
        Magic.CopyTo(header, 0);
        BitConverter.TryWriteBytes(header.AsSpan(Magic.Length, 4), iterations);
        salt.CopyTo(header, Magic.Length + 4);
        return header;
    }

    // ---------- Create ----------

    public static void CreateBackup(string path, string password)
    {
        FlushDatabase();

        var key = Convert.FromBase64String(DatabaseKeyProvider.GetOrCreateKey());
        var database = ReadAllShared(DatabaseKeyProvider.DbPath);

        // Payload: [key length][key][database file]
        var plain = new byte[4 + key.Length + database.Length];
        BitConverter.TryWriteBytes(plain.AsSpan(0, 4), key.Length);
        key.CopyTo(plain, 4);
        database.CopyTo(plain, 4 + key.Length);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var header = BuildHeader(Iterations, salt);
        var aesKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(aesKey, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag, header);

        // Write to a temporary file first, so a failure never leaves a half-written backup.
        var temp = path + ".tmp";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write))
        {
            file.Write(header);
            file.Write(nonce);
            file.Write(tag);
            file.Write(cipher);
        }
        File.Move(temp, path, overwrite: true);

        // Read it back to be sure the backup can really be opened.
        Decrypt(path, password);
    }

    // ---------- Open ----------

    private static (byte[] Key, byte[] Database) Decrypt(string path, string password)
    {
        var data = File.ReadAllBytes(path);
        var headerLength = Magic.Length + 4 + SaltSize;

        if (data.Length < headerLength + NonceSize + TagSize + 4 ||
            !data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("This file is not a Nexabode backup.");

        var iterations = BitConverter.ToInt32(data, Magic.Length);
        if (iterations < 100_000 || iterations > 10_000_000)
            throw new InvalidDataException("This backup file is damaged.");

        var header = data.AsSpan(0, headerLength).ToArray();
        var salt = data.AsSpan(Magic.Length + 4, SaltSize).ToArray();
        var aesKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);

        var nonce = data.AsSpan(headerLength, NonceSize).ToArray();
        var tag = data.AsSpan(headerLength + NonceSize, TagSize).ToArray();
        var cipher = data.AsSpan(headerLength + NonceSize + TagSize).ToArray();
        var plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(aesKey, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, header);
        }
        catch (CryptographicException)
        {
            throw new InvalidDataException("Wrong backup password, or the backup file is damaged.");
        }

        var keyLength = BitConverter.ToInt32(plain, 0);
        if (keyLength != KeySize || plain.Length <= 4 + keyLength)
            throw new InvalidDataException("This backup file is damaged.");

        return (plain.AsSpan(4, keyLength).ToArray(), plain.AsSpan(4 + keyLength).ToArray());
    }

    // ---------- Restore ----------

    // Returns the path of the safety copy of the data that was replaced.
    public static string Restore(string path, string password)
    {
        var (key, database) = Decrypt(path, password);

        // 1. Check that the data inside really opens with its key before touching anything.
        Directory.CreateDirectory(DatabaseKeyProvider.AppFolder);
        var temp = Path.Combine(DatabaseKeyProvider.AppFolder, "restore-check.tmp");
        File.WriteAllBytes(temp, database);

        try
        {
            var connection = DatabaseKeyProvider.BuildConnectionString(temp, Convert.ToBase64String(key), pooling: false);

            using var check = new SqliteConnection(connection);
            check.Open();
            using var command = check.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AdminUsers";
            command.ExecuteScalar();
        }
        catch (Exception)
        {
            SqliteConnection.ClearAllPools();
            DeleteQuietly(temp);
            DeleteQuietly(temp + "-wal");
            DeleteQuietly(temp + "-shm");
            throw new InvalidDataException("The backup could not be opened. It may be damaged.");
        }

        DeleteQuietly(temp + "-wal");
        DeleteQuietly(temp + "-shm");

        // 2. Save a safety copy of the current data, locked with the same password.
        Directory.CreateDirectory(SafetyFolder);
        var safety = Path.Combine(SafetyFolder, $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.nexabak");
        try
        {
            CreateBackup(safety, password);
        }
        catch (Exception)
        {
            DeleteQuietly(temp);
            throw new IOException("A safety copy of your current data could not be saved, so nothing was changed.");
        }

        // 3. Swap in the restored data and its key.
        SqliteConnection.ClearAllPools();
        File.Move(temp, DatabaseKeyProvider.DbPath, overwrite: true);
        DeleteQuietly(DatabaseKeyProvider.DbPath + "-wal");
        DeleteQuietly(DatabaseKeyProvider.DbPath + "-shm");
        DatabaseKeyProvider.SetKey(key);

        return safety;
    }
}