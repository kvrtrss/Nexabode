# Advanced: Opening the Nexabode Database

> **Unsupported.** This guide is for advanced users who want to inspect their own
> data outside the app. Most people never need it. To move or back up your data,
> use **Settings → Create backup / Restore backup** instead. To read your data,
> use the app's export feature if available.

## How the database is protected

- Your data lives in `nexabode.db`, encrypted with SQLCipher 4.
- Both files are in `%LOCALAPPDATA%\Nexabode\`:
  - `nexabode.db` is the encrypted database.
  - `db.key` is the database key, encrypted with Windows (DPAPI) for **your
    Windows account on this PC**.
- The database password is **not** your Nexabode sign-in password. It is a random
  key stored in `db.key`.

Because of this:

- The database can only be opened by the **same Windows user on the same PC**.
- Copying `nexabode.db` and `db.key` to another PC will **not** work. Use a
  `.nexabak` backup instead.
- If Windows is reinstalled or your user profile is lost, the key cannot be
  recovered. Keep regular backups.

## Before you start

1. **Close Nexabode** completely.
2. **Work on a copy.** Copy `nexabode.db` (and `nexabode.db-wal` / `nexabode.db-shm`
   if they exist) to another folder, for example your Desktop. Never open or edit
   the original.
3. Install [DB Browser for SQLite (SQLCipher edition)](https://sqlitebrowser.org/)
   or the `sqlcipher` command-line tool.
4. Delete the copy when you are finished. It contains your tenants' data.

## Step 1: Get the key

Open **Windows PowerShell** (not as administrator) using the same Windows account
that runs Nexabode, then run:

```powershell
Add-Type -AssemblyName System.Security
$f = "$env:LOCALAPPDATA\Nexabode\db.key"
$p = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($f), $null, 'CurrentUser')
$k = [Convert]::ToBase64String($p)
Set-Clipboard $k
"Key copied to clipboard ($($k.Length) characters)"
```

The key is copied to your clipboard and is not shown on screen. It should be
44 characters long, ending in `=`. When you are done, copy something else to
clear the clipboard.

## Step 2: Open the copy

### DB Browser for SQLite

1. Open your **copy** of `nexabode.db`.
2. In the encryption dialog, leave the dropdown on **Passphrase**.
3. Paste the key.
4. Select **SQLCipher 4 defaults** (page size 4096, 256000 iterations, SHA512).
5. Click **OK**.

### sqlcipher command line

```
sqlcipher nexabode-copy.db
PRAGMA key = 'PASTE_KEY_HERE';
PRAGMA cipher_compatibility = 4;
SELECT count(*) FROM sqlite_master;
```

## Troubleshooting

| Problem | Likely cause |
|---|---|
| "Key not valid for use in specified state" | Wrong Windows account or PC. The key only works for the account that created it. |
| "File is not a database" / wrong key | The `.db` and `db.key` come from different installs, the key was pasted with extra spaces, or the SQLCipher settings are not "SQLCipher 4 defaults". |
| `db.key` not found | Nexabode has not been run yet, or the data folder was moved or deleted. |

## Safety notes

- **Do not share your key or a decrypted copy of the database.**
- Do not save the key in a plain text file on the same PC. This defeats the
  Windows protection. If you must store it, use a password manager.
- Do not edit the database. Changes made outside the app can corrupt your data
  or break the app's change history.
- A restored `.nexabak` backup replaces the key, so any key you saved earlier
  stops working.
