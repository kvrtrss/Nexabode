# Nexabode

<p align="center">
  <img src="docs/banner.svg" alt="Nexabode - Boarding House Bed Occupancy and Payment Monitoring System" width="100%">
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
  <a href="#how-it-works">How It Works</a> &nbsp;|&nbsp;
  <a href="#data-storage-and-backups">Data and Backups</a> &nbsp;|&nbsp;
  <a href="#build-from-source">Build from Source</a> &nbsp;|&nbsp;
  <a href="#contributing">Contributing</a>
</p>

---

Nexabode is a free, open-source Windows desktop application for managing a boarding house. It helps organize rooms and beds, tenant occupancy, monthly rent, utility bills, payments, and outstanding balances in one place.

Instead of relying entirely on notebooks and spreadsheets, you can use Nexabode to keep your occupancy and billing records organized on your computer.

**Nexabode is designed for local use.** Its application data is stored on your computer in an encrypted database, without requiring an online account. Review the application's security and data-handling limitations before using it with real tenant or financial information.

## Why Nexabode?

Managing a boarding house involves keeping track of available beds, tenant move-ins and move-outs, rent due dates, utility expenses, and payments. Handling these records manually can make it difficult to maintain consistent and accurate information.

Nexabode brings these tasks together in a single desktop application, helping boarding house operators monitor occupancy, organize charges, record payments, and review outstanding balances.

## Disclaimer

Nexabode is provided **"AS IS"**, without warranties of any kind, express or implied, to the fullest extent permitted by applicable law. The authors and copyright holders do not guarantee that the software will be error-free, uninterrupted, secure, or suitable for a particular purpose.

Users are responsible for verifying records, maintaining appropriate backups, protecting access to their computers, and evaluating the software before relying on it for important operations.

Nexabode should be tested carefully before being used to manage real tenant records, financial information, or other sensitive data. Encryption and other security measures reduce certain risks but do not eliminate every possible risk.

Nothing in this disclaimer excludes or limits liability that cannot lawfully be excluded or limited under applicable law.

## Download

1. Visit the [**latest release**](https://github.com/kvrtrss/Nexabode/releases/latest).
2. Download the Windows x64 ZIP asset attached to the release.
3. Extract the ZIP to a folder, such as `C:\Nexabode`.
4. Open `Nexabode.exe`.

The self-contained release includes the .NET runtime required by the application. No separate .NET installation should be necessary for the published Windows x64 build.

### Windows security warning

Windows may display a message such as **"Windows protected your PC"** when opening Nexabode. The release is not code-signed, so Windows may be unable to verify the publisher.

Do not bypass a security warning automatically. First, verify that the ZIP came from the official [Nexabode GitHub Releases page](https://github.com/kvrtrss/Nexabode/releases) and, where provided, compare its SHA-256 checksum with the value published in the release notes.

For the `v1.0.0` release, after downloading the ZIP, run PowerShell in the folder containing the downloaded file:

```powershell
Get-FileHash .\Nexabode-v1.0.0-win-x64.zip -Algorithm SHA256
```

Compare the resulting hash with the SHA-256 value published for that exact release asset. A matching hash verifies that the file matches the published checksum; it does not independently prove that the software is free from vulnerabilities.

### First sign-in

The initial credentials for the `v1.0.0` release are:

- **Username:** `admin`
- **Password:** `admin123`

The application requires the default password to be changed on first sign-in. Change it immediately and do not reuse the default credentials elsewhere.

## Screenshots

| Dashboard | Rooms and Beds |
|---|---|
| ![Dashboard](docs/screenshots/dashboard.png) | ![Rooms and beds](docs/screenshots/rooms.png) |

| Rent Billing | Payments |
|---|---|
| ![Rent billing](docs/screenshots/rent-billing.png) | ![Payments](docs/screenshots/payments.png) |

| Billing Charges | Reports |
|---|---|
| ![Billing charges](docs/screenshots/billing.png) | ![Reports](docs/screenshots/reports.png) |

<sub>Screenshots use fictional sample data. Light and dark themes are included.</sub>

## Features

### Dashboard

- Occupancy rate and occupied bed counts.
- Monthly collections compared with the previous month.
- Outstanding balance overview.
- Calendar for rent due dates, payments, utility bills, and tenant move-ins and move-outs.
- Recent activity and quick actions.

### Rooms and beds

- Room cards with colored bed indicators, filters, and search.
- Bed statuses: Available, Occupied, Reserved, Maintenance, and Inactive.
- Add and edit rooms and beds.
- Deactivate rooms and beds instead of deleting them, preserving historical records.

### Tenant management

- Searchable tenant list with status, current bed, move-in date, and balance.
- Edit tenant details and record move-ins and move-outs.
- View tenant stay history.
- Flag duplicate tenant names.

### Billing

- **Rent Billing:** Review monthly billing status and generate rent charges in one operation.
- **Prorated rent:** Optional day-based rent calculation for applicable stays.
- **Multiple stays:** Calculate rent by stay duration when a tenant has more than one stay during a month.
- **Utility Bills:** Enter electricity, water, and other utility expenses for the whole house or a room, then split eligible bills equally among tenants staying there.
- **Billing Charges:** Review charges by tenant, date, payment details, or category, including Rent, Electricity, and Water.
- **Charge corrections:** Edit charges, void them with a reason, and undo utility splits. Voided items remain visible and are excluded from totals.

### Payments

- Record payments and choose how they are allocated: oldest charges first, rent only, electricity only, water only, or a specific charge.
- Review payment history and search for records.
- Edit payment details or void a payment with a reason.
- Block overpayments and future-dated payments.

### Reports and printing

- Tenant billing statements covering all time or a selected month.
- Occupancy reports and collection reports.
- Print reports or save them as PDFs through the Windows print dialog.
- Include the boarding house name and address in report headers.

### Security and data protection

- Encrypted SQLite database using the SQLCipher 4 file format.
- Sign-in passwords hashed using BCrypt.
- Required change of the default password on first sign-in.
- Password-protected database backup and restore.
- Database rules designed to prevent duplicate rent charges and utility splits.
- A guard intended to prevent multiple instances of the application from running simultaneously.
- Light and dark themes.

These features provide safeguards, but they should not be interpreted as a guarantee that the application is immune to unauthorized access, data loss, or other security issues.

## How It Works

1. **Set up rooms and beds.** Create your rooms and assign a monthly rent to each bed.
2. **Register tenants.** Add tenant records and move tenants into available beds.
3. **Generate rent charges.** Open Rent Billing and generate the charges for the applicable month.
4. **Enter utility bills.** Record electricity, water, and other applicable expenses, then split them among eligible tenants.
5. **Record payments.** Enter payments as they arrive and select the charges they should cover.
6. **Review records.** Use the Dashboard and Billing Charges pages to monitor collections and outstanding balances. Print reports when needed.

## Data Storage and Backups

Nexabode stores its application files in:

```text
%LocalAppData%\Nexabode
```

The directory contains files such as:

| File or folder | Purpose |
|---|---|
| `nexabode.db` | Encrypted application database |
| `db.key` | Database encryption key protected by Windows |
| `settings.json` | Application settings, such as theme and report-header details |
| `SafetyCopies\` | Safety copies created before a restore operation |

### Important: Protect Your Data

The database key is protected using Windows Data Protection API (DPAPI) under the Windows account used by the application.

**Do not assume that copying the database alone is enough to preserve your records.** If the computer is lost or reformatted, the Windows profile is deleted, or the encryption key becomes unavailable, the database may not be recoverable without a valid backup.

Anyone who gains access to your Windows account may also be able to access information available to that account. Use a strong Windows password, lock your computer when unattended, and consider enabling BitLocker where supported.

### Create a Backup

1. Open **Settings** and select **Create backup**.
2. Choose a backup password of at least eight characters.
3. Save the resulting `.nexabak` file.
4. Store a copy somewhere separate from your computer, such as an external drive or trusted cloud storage.

The backup password is separate from your Nexabode sign-in password.

**Keep your backup password safe.** If it is forgotten and cannot be recovered by the application, the backup may be unusable. Back up regularly and verify that your backups can be restored before depending on them.

### Restore or Move to Another Computer

1. Create a backup on the original computer using **Settings → Create backup**.
2. Transfer the `.nexabak` file to the new computer.
3. Open Nexabode on the new computer and follow the initial sign-in process if it is a fresh installation.
4. Select **Settings → Restore from backup**.
5. Choose the backup file and enter its backup password.
6. Reopen the application and sign in with the credentials associated with the restored data.

Test the restored database and verify important records before relying on it. Keep an independent copy of the backup until you have confirmed that the restored data is correct.

## Security Notes

- The database is encrypted, but encryption does not protect against every threat, including all forms of access through a compromised Windows account.
- Anyone with sufficient access to your Windows account may be able to access application data. Keep Windows secured and up to date.
- Nexabode currently uses a single administrator account rather than a multi-user role system.
- There is no guaranteed password-recovery mechanism. Forgotten passwords or unavailable encryption keys may result in inaccessible data.
- Keep backups separate from the computer and protect them with a strong backup password.
- Do not commit databases, encryption keys, backups, real tenant information, or credentials to the repository.
- Review the [security policy](SECURITY.md) for information about reporting potential vulnerabilities.

## Known Limitations and Future Ideas

Nexabode is an independently maintained project. Features, compatibility, and security behavior may change between releases.

Ideas for future development include:

- One-off charges.
- Advance payments.
- Automatic locking after inactivity.
- Global search.
- An audit log.

These are possible future improvements, not promises of implementation. Suggestions are welcome through [GitHub Issues](https://github.com/kvrtrss/Nexabode/issues).

## Build from Source

### Requirements

- Windows 10 or Windows 11.
- Git.
- [.NET 10 SDK](https://dotnet.microsoft.com/download), or Visual Studio 2026 with the **.NET desktop development** workload.

### Clone and run

```powershell
git clone https://github.com/kvrtrss/Nexabode.git
cd Nexabode/Nexabode
dotnet run
```

Alternatively, open `Nexabode/Nexabode.slnx` in Visual Studio and run the application using **F5**.

### Build a Release

From the application project directory:

```powershell
dotnet build .\Nexabode.csproj -c Release
```

To publish a self-contained Windows x64 build:

```powershell
dotnet publish .\Nexabode.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

### Project Layout

```text
Nexabode/
├── .github/             GitHub workflows and templates
├── docs/                Banner and screenshots
├── Nexabode/            Application project and solution
│   ├── Controls/        Reusable interface components
│   ├── Data/            Database, encryption, and startup logic
│   ├── Models/          Database entities
│   ├── Services/        Business rules, settings, and utilities
│   └── Views/           Application pages and dialogs
├── ACCESSIBILITY.md     Accessibility goals and reporting
├── CHANGELOG.md         Release history
├── CODE_OF_CONDUCT.md   Community behavior guidelines
├── CONTRIBUTING.md      Contribution instructions
├── LICENSE              MIT License
├── README.md            Project overview and documentation
└── SECURITY.md          Security policy
```

The layout above describes the intended repository organization. Directory names may change as the project evolves.

## Contributing

Bug reports, documentation improvements, feature suggestions, and code contributions are welcome.

Before making a substantial change, consider opening an issue to discuss the proposal.

- **Use fictional data.** Never publish real tenant information, financial records, passwords, or encryption keys in issues, screenshots, commits, or pull requests.
- **Protect sensitive files.** Never commit `.db`, `.key`, or `.nexabak` files.
- **Verify your changes.** Build the application in Release configuration and manually test the affected functionality.
- **Describe your work.** Explain the change, its purpose, and the verification you performed.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the contribution process and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community expectations.

Accessibility feedback is also welcome. See [ACCESSIBILITY.md](ACCESSIBILITY.md) for the project's accessibility goals and reporting guidance.

## Built With

- **C# and .NET 10** — application logic and runtime.
- **Windows Presentation Foundation (WPF)** — desktop user interface.
- [WPF-UI](https://github.com/lepoco/wpfui) — modern WPF controls and styling.
- [Entity Framework Core](https://learn.microsoft.com/ef/core/) with SQLite — data access and database management.
- [SQLite3 Multiple Ciphers](https://github.com/utelle/SQLite3MultipleCiphers-NuGet) — encrypted database support.
- [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net) — password hashing.
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM utilities.

These projects are maintained by their respective authors and communities. Refer to the applicable package licenses and notices for details about third-party software included with Nexabode.

## License

Nexabode is released under the [MIT License](LICENSE).

---

<p align="center"><sub>Nexabode — smarter occupancy, structured management.</sub></p>
