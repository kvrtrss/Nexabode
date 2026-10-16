<p align="center">
  <img src="docs/banner.svg" alt="Nexabode - Boarding House Bed Occupancy & Payment Monitoring System" width="100%">
</p>

<p align="center">
  <a href="https://github.com/kvrtrss/Nexabode/releases/latest"><img src="https://img.shields.io/github/v/release/kvrtrss/Nexabode?color=4F46E5" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-4F46E5" alt="MIT license"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4" alt="Windows 10 and 11">
  <a href="https://github.com/kvrtrss/Nexabode/actions/workflows/build.yml"><img src="https://github.com/kvrtrss/Nexabode/actions/workflows/build.yml/badge.svg" alt="Build status"></a>
</p>

<p align="center">
  <a href="#download">Download</a> &nbsp;|&nbsp;
  <a href="#screenshots">Screenshots</a> &nbsp;|&nbsp;
  <a href="#features">Features</a> &nbsp;|&nbsp;
  <a href="#how-it-works">How it works</a> &nbsp;|&nbsp;
  <a href="#your-data-and-backups">Your data</a> &nbsp;|&nbsp;
  <a href="#build-from-source">Build from source</a> &nbsp;|&nbsp;
  <a href="#contributing">Contributing</a>
</p>

---

**Nexabode** is a free, open-source Windows app for running a single boarding house. It keeps track of rooms and beds, who is staying where, monthly rent and utility bills, payments, and who still owes money, so you can stop relying on notebooks and spreadsheets.

Everything is stored **on your own computer** in an encrypted database. There is no account to create and nothing is sent over the internet.

## Why Nexabode

Boarding house owners often track occupied beds, unpaid rent, and utility bills by hand. It is easy to lose track of empty beds, miscalculate charges, and miss overdue payments, especially when tenants move in and out during the school year. Nexabode puts all of this in one place and shows, at a glance, which beds are taken and what is still owed.

## Download

1. Go to the [**latest release**](https://github.com/kvrtrss/Nexabode/releases/latest) and download `Nexabode-x.y.z-win-x64.zip`.
2. Extract the zip to a folder, for example `C:\Nexabode`.
3. Open `Nexabode.exe`.

No installer and no extra software are needed. The download already includes everything the app requires.

> **Windows may say "Windows protected your PC".** Nexabode is not code-signed, because certificates cost money. Click **More info**, then **Run anyway**. To check that your download is genuine, compare its SHA256 checksum with the one in the release notes:
> `Get-FileHash .\Nexabode-1.0.0-win-x64.zip -Algorithm SHA256`

**First sign-in:** username `admin`, password `admin123`. Nexabode will make you choose your own password straight away.

## Screenshots

| Dashboard | Rooms & Beds |
|---|---|
| ![Dashboard](docs/screenshots/dashboard.png) | ![Rooms and beds](docs/screenshots/rooms.png) |

| Rent Billing | Payments |
|---|---|
| ![Rent billing](docs/screenshots/rent-billing.png) | ![Payments](docs/screenshots/payments.png) |

| Billing Charges | Reports |
|---|---|
| ![Billing charges](docs/screenshots/billing.png) | ![Reports](docs/screenshots/reports.png) |

<sub>Screenshots use made-up sample data. Light and dark themes are both included.</sub>

## Features

**Dashboard**
- Occupancy rate, occupied beds, money collected this month (compared with last month), and outstanding balance
- A calendar marking rent due dates, payments, utility bills, and move-ins and move-outs
- Recent activity and quick actions

**Rooms and beds**
- Room cards with colored bed chips, filters, and search
- Bed statuses: Available, Occupied, Reserved (a tenant moving in on a future date), Maintenance, and Inactive
- Add and edit rooms and beds. Nothing is ever deleted; rooms and beds are deactivated instead

**Tenants**
- Searchable list with status, current bed, move-in date, and balance
- Edit details, move in, and move out, with the full stay history
- Duplicate names are flagged

**Billing**
- **Rent Billing** page: see who has and has not been billed for a month, then generate rent for everyone in one click, with optional day-based proration
- Tenants with more than one stay in a month are billed by days, so they never pay full rent twice
- **Utility Bills** page: enter electricity, water, and other bills for the whole house or one room, and split them equally among the tenants staying there
- **Billing Charges** page: every charge with who paid it, when, and how, filtered by item (Rent, Electricity, Water, and so on)
- Edit charges, void them with a reason, or undo a split. Voided items stay visible and never count in totals

**Payments**
- Record a payment and choose what it is for: oldest charges first, rent only, electricity only, water only, or one specific charge
- Payment history with search, edit of details, and void with a reason
- Overpayments and future-dated payments are blocked

**Reports and printing**
- Billing statement per tenant (all time or one month), occupancy report, and collection report
- Print or save as PDF through the Windows print dialog
- Your boarding house name and address in the report header

**Security and safety**
- Encrypted database (SQLCipher 4 file format)
- Sign-in passwords are hashed with BCrypt, and the default password must be changed on first sign-in
- Password-protected backup and restore, which is also how you move to another computer
- Database rules that stop duplicate rent charges and duplicate utility splits, and a guard so only one copy of the app runs at a time
- Light and dark themes

## How it works

1. **Set up** your rooms and beds, with a monthly rent for each bed.
2. **Add tenants** and move them into a bed.
3. Each month, open **Rent Billing** and generate rent, then add the **Utility Bills** and split them.
4. **Record payments** as they arrive and choose what each one is for.
5. Check the **Dashboard** and **Billing Charges** to see who has paid and who is overdue, and print **Reports** when you need them.

## Your data and backups

Nexabode keeps its files in `%LocalAppData%\Nexabode`:

| File | What it is |
|---|---|
| `nexabode.db` | The encrypted database |
| `db.key` | The database key, protected by your Windows account |
| `settings.json` | Theme, report header, and last backup date (nothing sensitive) |
| `SafetyCopies\` | Automatic copies of your data made just before a restore |

### Make backups
The database key is tied to your Windows account. **If the computer is lost or reformatted, or your Windows profile is deleted, the data cannot be recovered without a backup.**

1. Open **Settings, then Create backup**.
2. Choose a backup password (at least 8 characters). It is separate from your sign-in password.
3. Save the `.nexabak` file on a USB drive or cloud storage, not only on the same computer.

**The backup password cannot be recovered or reset. Write it down somewhere safe.** Back up regularly, and always before restoring or upgrading.

### Moving to another computer
1. On the old computer: **Settings, then Create backup**, and copy the file across.
2. On the new computer: open Nexabode and sign in once with `admin` / `admin123` (then choose a temporary password).
3. **Settings, then Restore from backup**, pick the file, and enter the backup password.
4. Reopen Nexabode and sign in with the password that was in use when the backup was made.

## Security notes

- The database file is encrypted, so a copied `nexabode.db` cannot be read without its key.
- Anyone who can sign in to your Windows account can open Nexabode, so lock your computer when you step away. Using BitLocker and a strong Windows password is recommended.
- There is no password reset. A forgotten sign-in password or backup password cannot be recovered.
- Nexabode has a single administrator account.
- To report a security problem, see [SECURITY.md](SECURITY.md).

## Scope and limitations

- One boarding house and one admin account
- No online or digital payment integration. Payments such as cash or GCash are recorded by hand
- Utility bills are split equally among the tenants staying in the room or house that month (no sub-metering)
- No advance payments or credit balance
- No one-off charges (such as a damage fee) yet
- No tenant login or tenant view
- Windows only

## Ideas for the future

These are ideas, not promises: one-off charges, advance payments, automatic lock after inactivity, a global search bar, and an audit log. Suggestions are welcome as [issues](https://github.com/kvrtrss/Nexabode/issues).

## Build from source

You need Windows 10 or 11, Git, and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (or Visual Studio 2026 with the ".NET desktop development" workload).

```
git clone https://github.com/kvrtrss/Nexabode.git
cd Nexabode/Nexabode
dotnet run
```

Or open `Nexabode/Nexabode.slnx` in Visual Studio and press **F5**.

<!-- Uncomment after the test project is added:
### Run the tests
```
dotnet test Nexabode.Tests
```
-->

### Project layout
```
Nexabode/            The app
  Controls/          Reusable interface pieces (page header, stat card)
  Data/              Database context, encryption key handling, startup and upgrades
  Models/            Database entities
  Services/          Billing rules, theme, settings, backup, and formatting helpers
  Views/             Pages and dialogs
docs/                Banner and screenshots
```

## Contributing

Ideas and fixes are welcome. Please open an issue first for larger changes.

- **Never commit real tenant data.** Use made-up names in test data, screenshots, and issues.
- Never commit `.db`, `.key`, or `.nexabak` files. `.gitignore` blocks them, but check your changes before every commit.
- Test the main flow before opening a pull request: add a room, add a tenant, move in, generate rent, record a payment, and create a backup.

## Built with

- C# and .NET 10, with WPF for the interface
- [WPF-UI](https://github.com/lepoco/wpfui) for the modern look
- [Entity Framework Core](https://learn.microsoft.com/ef/core/) with SQLite
- [SQLite3 Multiple Ciphers](https://github.com/utelle/SQLite3MultipleCiphers-NuGet) for database encryption
- [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net) for password hashing

Thanks to the authors of these projects. See each project for its own license.

## License

Released under the [MIT License](LICENSE).

<p align="center"><sub>Nexabode: smarter occupancy, structured management.</sub></p>