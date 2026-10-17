# Nexabode

<p align="center">
  <img src="docs/banner.svg" alt="Nexabode - Boarding House Bed Occupancy and Payment Monitoring System" width="100%">
</p>

<p align="center">
  <a href="https://github.com/kvrtrss/Nexabode/releases/tag/v1.0.0"><img src="https://img.shields.io/badge/Download-Nexabode-4F46E5" alt="Download Nexabode v1.0.0"></a>
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
## Nexabode

Nexabode is a free, open-source Windows desktop application developed as an **academic project** to apply software development concepts to a practical boarding house management scenario. It provides a centralized way to manage rooms and beds, tenant occupancy, monthly rent, utility charges, payments, and outstanding balances.

Designed to simplify everyday boarding house operations, Nexabode helps operators organize occupancy records, track tenant move-ins and move-outs, monitor bills and payments, and review unpaid balances. By bringing these tasks together in one application, it offers an alternative to managing records manually through notebooks and spreadsheets.

Nexabode is designed for local use, storing application data on the user's computer in an encrypted database without requiring an online account. Users should review the application's security measures and data-handling limitations before storing real tenant or financial information.

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

## Features

### Dashboard
- Overview of occupancy, monthly collections, and outstanding balances.
- Calendar for rent due dates, payments, utility bills, and tenant move-ins and move-outs.
- Recent activity and quick actions.

### Rooms and Beds
- Manage rooms and beds with search, filters, and visual status indicators.
- Track available, occupied, reserved, maintenance, and inactive beds.
- Deactivate rooms and beds while preserving historical records.

### Tenant Management
- Search and manage tenant records, occupancy status, and balances.
- Record move-ins and move-outs and review tenant stay history.
- Flag potentially duplicate tenant names.

### Billing and Payments
- Generate monthly rent charges, with optional prorated rent calculations.
- Record electricity, water, and other utility bills and split eligible expenses among tenants.
- Review, correct, or void billing charges while retaining historical records.
- Record payments and allocate them to outstanding charges using different allocation methods.
- Search payment history, edit payment details, and void payments with a reason.

### Reports and Printing
- Generate tenant billing statements, occupancy reports, and collection reports.
- Print reports or save them as PDFs using the Windows print dialog.
- Include the boarding house name and address in report headers.

### Security and Data Protection
- Locally stored, encrypted SQLite database using the SQLCipher 4 file format.
- Password hashing using BCrypt and a required password change on first sign-in.
- Password-protected database backup and restore.
- Database safeguards against duplicate rent charges and utility splits.
- Protection intended to prevent multiple application instances from running simultaneously.

### Customization
- Light and dark themes.

These features provide safeguards, but they should not be interpreted as a guarantee that the application is immune to unauthorized access, data loss, or other security issues.

## How It Works

1. **Set up rooms and beds.** Create rooms, add beds, and assign monthly rent rates.
2. **Register tenants.** Add tenant records and assign tenants to available beds.
3. **Generate rent charges.** Generate monthly rent charges for the applicable billing period.
4. **Manage utility bills.** Record electricity, water, and other applicable expenses, then split eligible bills among tenants.
5. **Record payments.** Enter payments and allocate them to the appropriate outstanding charges.
6. **Monitor and review.** Use the Dashboard, Billing Charges, and Reports sections to track collections, review outstanding balances, and print reports.

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

## AI Assistance

AI tools were used during the development of Nexabode as aids for programming, debugging, documentation, and technical problem-solving.

Tools used include:

- **ChatGPT by OpenAI** — assistance with development discussions, code analysis, troubleshooting, documentation, and release preparation.
- **Claude by Anthropic** — assistance with development tasks, code review, and technical problem-solving.

AI-generated suggestions and outputs were evaluated and adapted as appropriate during development. The project maintainer remains responsible for the project's implementation, testing, final decisions, and maintenance.

AI tools served as development assistants; they are not project maintainers or substitutes for human review.

## License

Nexabode is released under the [MIT License](LICENSE).

---

<p align="center"><sub>Nexabode — smarter occupancy, structured management.</sub></p>
