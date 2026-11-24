<div align="center">
  <img src="docs/logo.png" width="96" alt="Nexabode logo">
</div>

<div align="center">
  <img src="docs/banner.svg" width="100%" alt="Nexabode, a boarding house bed occupancy and payment monitoring system for Windows">
</div>

<br>

<table>
  <tr>
    <td width="50%"><a href="https://github.com/kvrtrss/Nexabode/releases/latest"><img src="docs/download.svg" width="100%" alt="Download the latest Nexabode release for Windows x64"></a></td>
    <td width="50%"><img src="docs/requires.svg" width="100%" alt="Requires Windows 10 or 11"></td>
  </tr>
</table>

<div align="center">
  <a href="#about">About</a> &nbsp;|&nbsp;
  <a href="#screenshots">Screenshots</a> &nbsp;|&nbsp;
  <a href="#features">Features</a> &nbsp;|&nbsp;
  <a href="#how-it-works">How It Works</a> &nbsp;|&nbsp;
  <a href="#download">Download</a> &nbsp;|&nbsp;
  <a href="#data-storage-and-backups">Data and Backups</a> &nbsp;|&nbsp;
  <a href="#build-from-source">Build from Source</a> &nbsp;|&nbsp;
  <a href="#contributing">Contributing</a>
  <br><br>
  <a href="https://github.com/kvrtrss/Nexabode/actions/workflows/build.yml"><img src="https://github.com/kvrtrss/Nexabode/actions/workflows/build.yml/badge.svg" alt="Build status"></a>
</div>

---

## About

Nexabode is a free, open-source Windows desktop application developed as an **academic project** to apply software development concepts to a practical boarding house management scenario. It brings rooms and beds, tenant occupancy, monthly rent, utility charges, payments, and outstanding balances into one place, as an alternative to notebooks and spreadsheets.

It is designed for local use. Data is stored on your computer in an encrypted database, with no online account required. Review the [security notes](#security-notes) and [disclaimer](#disclaimer) before storing real tenant or financial information.

<p align="center">
  <img src="docs/specs.svg" width="50%" alt="Nexabode specs: version 1.5.0, Windows 10 and 11, .NET 10, SQLCipher 4, MIT license">
</p>

## Screenshots

<table>
  <tr>
    <td width="50%"><sub><b>01 / DASHBOARD</b></sub><br><img src="docs/screenshots/dashboard.png" alt="Dashboard"></td>
    <td width="50%"><sub><b>02 / ROOMS AND BEDS</b></sub><br><img src="docs/screenshots/rooms.png" alt="Rooms and beds"></td>
  </tr>
  <tr>
    <td width="50%"><sub><b>03 / TENANTS</b></sub><br><img src="docs/screenshots/tenants.png" alt="Tenants"></td>
    <td width="50%"><sub><b>04 / RENT BILLING</b></sub><br><img src="docs/screenshots/rent-billing.png" alt="Rent billing"></td>
  </tr>
  <tr>
    <td width="50%"><sub><b>05 / UTILITY BILLS</b></sub><br><img src="docs/screenshots/utility-bills.png" alt="Utility bills"></td>
    <td width="50%"><sub><b>06 / BILLING CHARGES</b></sub><br><img src="docs/screenshots/billing.png" alt="Billing charges"></td>
  </tr>
  <tr>
    <td width="50%"><sub><b>07 / REPORTS</b></sub><br><img src="docs/screenshots/reports.png" alt="Reports"></td>
    <td width="50%"></td>
  </tr>
</table>

## Features

<table>
  <tr>
    <td width="33%"><img src="docs/feature-01.svg" width="100%" alt="Dashboard: occupancy and collections, due-date calendar, recent activity and quick actions"></td>
    <td width="33%"><img src="docs/feature-02.svg" width="100%" alt="Rooms and beds: available, occupied and reserved status, search and filters, deactivate while keeping history"></td>
    <td width="33%"><img src="docs/feature-03.svg" width="100%" alt="Tenants: move-in and move-out records, stay history and balances, duplicate name flags"></td>
  </tr>
  <tr>
    <td width="33%"><img src="docs/feature-04.svg" width="100%" alt="Billing and payments: monthly and prorated rent, split utilities, allocate, edit and void payments"></td>
    <td width="33%"><img src="docs/feature-05.svg" width="100%" alt="Reports: billing statements, occupancy and collection reports, print or save as PDF"></td>
    <td width="33%"><img src="docs/feature-06.svg" width="100%" alt="Security: SQLCipher 4 encrypted database, BCrypt password hashing, password-protected backups"></td>
  </tr>
</table>

<details>
<summary><b>Full feature list</b></summary>

<br>

**Dashboard**

- Overview of occupancy, monthly collections, and outstanding balances.
- Calendar for rent due dates, payments, utility bills, and tenant move-ins and move-outs.
- Recent activity, quick actions, and backup reminders.

**Rooms and Beds**

- Manage rooms and beds with search, filters, and visual status indicators.
- Rooms are grouped under a heading for each floor; the floor is required for every room.
- Track available, occupied, reserved, maintenance, and inactive beds.
- Activate or deactivate rooms and beds while preserving historical records.

**Tenant management**

- Add tenants and record move-ins and move-outs. See each tenant's bed, balance, status (Staying, Moving out on the last day, Moved out) and full stay history.
- Correct any stay (bed, dates, rent, deposit) with a recorded reason. Choosing another bed fills in that bed's price, which can still be changed.
- Overlapping stays are blocked on new move-ins and flagged as warnings when history is corrected. An Owner can remove a stay that was entered by mistake, but only when nothing was ever billed on it.
- Keep a deposit for each tenant: apply it to unpaid charges (oldest first), refund what is left, correct it, or undo a refund.
- Archive settled, moved-out tenants and flag possible duplicate names.

**Billing and payments**

- Generate monthly rent in one window: choose the due date, see every tenant and amount, untick anyone, then generate. Partial months are billed by days, or at full rent if you choose. Rent is due on the day you set in Settings, but never before a tenant's move-in day.
- Record electricity, water and other utility bills, for a whole month or a custom period, and split them among tenants.
- Review, edit, void, archive and restore charges. Paid charges are never altered: differences become adjustment charges. Before a change is applied, Nexabode lists exactly which records it affects.
- Record payments in their own window and choose what they pay for (oldest charges first, one item, or one specific charge). A payment larger than the balance is accepted and the extra is shown as change to return. Edit or void payments, and record money given back.
- Every payment gets a numbered receipt automatically (like RCT-2026-00042: the year, then a running number that starts again each year). A number is never reused, even when a payment is voided.
- Print a receipt after a preview. A reprint is marked "DUPLICATE COPY", every printing is written to the change history, and a voided payment cannot be printed.
- Right-click a row in the payment, charge, rent and utility tables for the actions that apply to it.

**Reports and printing**

- Tenant billing statements, occupancy reports and collection reports, with the boarding house name and address in the header.
- Everything that prints opens one print window with a page-by-page preview next to the printer, copies and orientation. Print on paper, or choose Microsoft Print to PDF to save a PDF.

**Notifications**

- A bell in the title bar with an unread count and a drop-down panel, plus a Notifications page: overdue and upcoming payments, move-ins and move-outs, rent not yet generated, utility bills not yet split, deposit left with moved-out tenants, and backup reminders. You can add your own reminders.
- Optional tips (such as adding a second Owner) are listed separately and never counted on the bell.
- A banner on the Dashboard reminds you to make your first backup and to save your Owner recovery key, and goes away once done.

**Security and data protection**

- Locally stored, encrypted SQLite database using the SQLCipher 4 file format. Its key is protected by the Windows Data Protection API, saved safely, and kept with a spare copy.
- **No default account.** On the first start you create the Owner account yourself and are shown an Owner recovery key once.
- Passwords are hashed with BCrypt. Very weak passwords (very common, repeated characters, or containing the username) are refused whenever a new password is chosen, including for backups.
- Two roles: Owner (everything) and Staff (records only, with no account management or backup restore). New and reset accounts sign in with a temporary password and must choose their own.
- Owner recovery key: shown once, printable on a sheet, and kept only as a BCrypt hash. "Forgot password?" accepts it to set a new password for an Owner, or to create a new Owner account. Using it makes a new key. Staff who forget their password ask an Owner to reset it in Settings.
- Sign-in lock: 3 failed attempts in a row lock sign-in for 5 minutes, and each repeat lock is longer (15, 30, then 60 minutes) until a correct sign-in. The lock is saved in the encrypted database, so closing the app or logging out does not clear it.
- Password-protected backup and restore. A restore checks the backup first (it must open, pass an integrity check and not come from a newer version), and backup reminders appear automatically.
- An automatic safety copy of the database before an upgrade (the newest five are kept).
- Database safeguards against duplicate rent charges and utility splits, and protection against running more than one copy of the app.
- A change history records the original value, the new value, who made the change, when, and why. A change and its history entry are saved together or not at all.

**Customization and accessibility**

- Light and dark themes, or follow the Windows setting.
- Keyboard shortcuts: Ctrl+1 to Ctrl+9 and Ctrl+0 open the pages, Ctrl+F searches, Ctrl+N does the page's main action. The side bar folds to icons on small screens.
- See [ACCESSIBILITY.md](ACCESSIBILITY.md) for what has and has not been checked.

These features provide safeguards, but they should not be interpreted as a guarantee that the application is immune to unauthorized access, data loss, or other security issues.

</details>

## How It Works

<table>
  <tr>
    <td width="33%"><img src="docs/step-01.svg" width="100%" alt="Step 01, set up rooms: create rooms, add beds, and assign monthly rent"></td>
    <td width="33%"><img src="docs/step-02.svg" width="100%" alt="Step 02, register tenants: add tenant records and assign them to open beds"></td>
    <td width="33%"><img src="docs/step-03.svg" width="100%" alt="Step 03, generate rent: create monthly charges for the billing period"></td>
  </tr>
  <tr>
    <td width="33%"><img src="docs/step-04.svg" width="100%" alt="Step 04, manage utilities: record electricity, water and other bills and split eligible ones"></td>
    <td width="33%"><img src="docs/step-05.svg" width="100%" alt="Step 05, record payments: enter payments and allocate them to outstanding charges"></td>
    <td width="33%"><img src="docs/step-06.svg" width="100%" alt="Step 06, monitor and review: track collections and balances, then print reports"></td>
  </tr>
</table>

## Download

1. Visit the [**latest release**](https://github.com/kvrtrss/Nexabode/releases/latest).
2. Download the Windows x64 ZIP asset attached to the release.
3. Extract the ZIP to a folder, such as `C:\Nexabode`.
4. Open `Nexabode.exe`.

The self-contained release includes the .NET runtime required by the application. No separate .NET installation should be necessary for the published Windows x64 build.

> [!WARNING]
> **Windows may show "Windows protected your PC".** The release is not code-signed, so Windows cannot verify the publisher. Do not bypass the warning automatically. Confirm the ZIP came from the official [Nexabode GitHub Releases page](https://github.com/kvrtrss/Nexabode/releases) and compare its SHA-256 checksum with the value in the release notes.

<details>
<summary><b>Verify the download (SHA-256)</b></summary>

<br>

Open PowerShell in the folder containing the downloaded file and run:

```powershell
Get-FileHash .\Nexabode-x.y.z-win-x64.zip -Algorithm SHA256
```

**Example:** for `Nexabode-1.5.0-win-x64.zip`, run:

```powershell
Get-FileHash .\Nexabode-1.5.0-win-x64.zip -Algorithm SHA256
```

Compare the resulting hash with the SHA-256 value published for that exact release asset. A matching hash verifies that the file matches the published checksum; it does not independently prove that the software is free from vulnerabilities.

</details>

### First start

<table>
  <tr>
    <td width="33%"><sub><b>1 / CREATE YOUR OWNER ACCOUNT</b></sub><br><img src="docs/screenshots/first-run.png" alt="Welcome screen: create your Owner account"></td>
    <td width="33%"><sub><b>2 / SAVE YOUR RECOVERY KEY</b></sub><br><img src="docs/screenshots/recovery-key.png" alt="The Owner recovery key is shown once"></td>
    <td width="33%"><sub><b>3 / SIGN IN</b></sub><br><img src="docs/screenshots/sign-in.png" alt="Sign-in screen"></td>
  </tr>
</table>

The first time Nexabode starts, it asks you to create your Owner account: choose a username and a password (a very weak password is refused). It then shows your **Owner recovery key** once. Write it down or print it, keep it somewhere safe and away from the computer, and tick the box to continue. **There is no default account or password.**

An Owner can add more accounts in **Settings**. A new account signs in with a temporary password and must choose its own the first time.

**Upgrading from an earlier version?** Your accounts keep working. Per-account recovery codes and the reset phrase no longer exist, and the emergency access key you already have now works as the Owner recovery key. If you still have the original `admin` account with its default password, change it in **Settings**.

## Data Storage and Backups

Nexabode keeps everything on your computer, in `%LocalAppData%\Nexabode`.

<p>
  <img src="docs/data-files.svg" width="60%" alt="Nexabode data files: nexabode.db encrypted database, db.key key protected by Windows DPAPI, settings.json settings, SafetyCopies folder with safety copies created before a restore or an upgrade">
  
Before the first start after an upgrade that changes the database, Nexabode also saves a safety copy of it in the `SafetyCopies` folder. A normal backup from **Settings** is still the copy to rely on.
</p>

> [!IMPORTANT]
> The database key is protected using the Windows Data Protection API (DPAPI) under the Windows account used by the application.
>
> **Do not assume that copying the database alone is enough to preserve your records.** If the computer is lost or reformatted, the Windows profile is deleted, or the encryption key becomes unavailable, the database may not be recoverable without a valid backup.

Anyone who gains access to your Windows account may also be able to access information available to that account. Use a strong Windows password, lock your computer when unattended, and consider enabling BitLocker where supported.

### Create a Backup

1. Open **Settings** and select **Create backup**.
2. Choose a backup password of at least eight characters.
3. Save the resulting `.nexabak` file.
4. Store a copy somewhere separate from your computer, such as an external drive or trusted cloud storage.

The backup password is separate from your Nexabode sign-in password. **Keep it safe.** If it is forgotten and cannot be recovered by the application, the backup may be unusable. Back up regularly and verify that your backups can be restored before depending on them.

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
- Nexabode supports separate accounts with two roles, Owner and Staff. This is not a full permission system: Staff can still change or void any record, and every account uses the same database on the same computer. Nexabode does not sync between computers.
- The sign-in screen deliberately has no open "create account" button: anyone at the computer could use it to get full access. Without signing in, an account can only be made or reset with the Owner recovery key.
- A forgotten Owner password can be reset with the Owner recovery key (**Forgot password?** on the sign-in screen). A forgotten Staff password is reset by an Owner in **Settings**. Write the recovery key down and keep it somewhere private, away from the computer. If the only Owner loses both the password and the recovery key, Nexabode cannot reset it, so keep both safe, and consider a second Owner account.
- The sign-in lock slows down guessing, but it is not a defence against someone who controls the computer: they can change the system clock or use the Windows account to reach the files. Lock Windows when you step away.
- Unavailable encryption keys may result in inaccessible data.
- Keep backups separate from the computer and protect them with a strong backup password.
- Do not commit databases, encryption keys, backups, real tenant information, or credentials to the repository.
- Review the [security policy](SECURITY.md) for information about reporting potential vulnerabilities.

## Disclaimer

Nexabode is provided **"AS IS"**, without warranties of any kind, express or implied, to the fullest extent permitted by applicable law. The authors and copyright holders do not guarantee that the software will be error-free, uninterrupted, secure, or suitable for a particular purpose.

Users are responsible for verifying records, maintaining appropriate backups, protecting access to their computers, and evaluating the software before relying on it for important operations.

Nexabode should be tested carefully before being used to manage real tenant records, financial information, or other sensitive data. Encryption and other security measures reduce certain risks but do not eliminate every possible risk.

Nothing in this disclaimer excludes or limits liability that cannot lawfully be excluded or limited under applicable law.

## Build from Source

**Requirements:** Windows 10 or 11, Git, and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (or Visual Studio 2026 with the **.NET desktop development** workload).

```powershell
git clone https://github.com/kvrtrss/Nexabode.git
cd Nexabode/Nexabode
dotnet run
```

Run the tests from the repository root:

```powershell
dotnet test Nexabode.Tests/Nexabode.Tests.csproj
```

Alternatively, open `Nexabode/Nexabode.slnx` in Visual Studio and run the application using **F5**.

<details>
<summary><b>Build and publish a release</b></summary>

<br>

From the application project directory:

```powershell
dotnet build .\Nexabode.csproj -c Release
```

To publish a self-contained Windows x64 build:

```powershell
dotnet publish .\Nexabode.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

</details>

<details>
<summary><b>Project layout</b></summary>

<br>

```text
Nexabode/
├── .github/             GitHub workflows and templates
├── docs/                Banner, badges, and screenshots
├── Nexabode/            Application project and solution
│   ├── Controls/        Reusable interface components
│   ├── Data/            Database, encryption, and startup logic
│   ├── Models/          Database entities
│   ├── Services/        Business rules, settings, and utilities
│   └── Views/           Application pages and dialogs
├── Nexabode.Tests/      Unit and scenario tests for the billing, stay and payment rules
├── ACCESSIBILITY.md     Accessibility goals and reporting
├── CHANGELOG.md         Release history
├── CODE_OF_CONDUCT.md   Community behavior guidelines
├── CONTRIBUTING.md      Contribution instructions
├── LICENSE              MIT License
├── README.md            Project overview and documentation
└── SECURITY.md          Security policy
```

The layout above describes the intended repository organization. Directory names may change as the project evolves.

</details>

## Contributing

Bug reports, documentation improvements, feature suggestions, and code contributions are welcome. Before making a substantial change, consider opening an issue to discuss the proposal.

- **Use fictional data.** Never publish real tenant information, financial records, passwords, or encryption keys in issues, screenshots, commits, or pull requests.
- **Protect sensitive files.** Never commit `.db`, `.key`, or `.nexabak` files.
- **Verify your changes.** Build the application in Release configuration and manually test the affected functionality.
- **Describe your work.** Explain the change, its purpose, and the verification you performed.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the contribution process and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community expectations. Accessibility feedback is also welcome; see [ACCESSIBILITY.md](ACCESSIBILITY.md).

## Built With

| Component | Role |
|---|---|
| **C# and .NET 10** | Application logic and runtime |
| **Windows Presentation Foundation (WPF)** | Desktop user interface |
| [WPF-UI](https://github.com/lepoco/wpfui) | Modern WPF controls and styling |
| [Entity Framework Core](https://learn.microsoft.com/ef/core/) with SQLite | Data access and database management |
| [SQLite3 Multiple Ciphers](https://github.com/utelle/SQLite3MultipleCiphers-NuGet) | Encrypted database support |
| [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net) | Password hashing |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM utilities |

These projects are maintained by their respective authors and communities. Refer to the applicable package licenses and notices for details about third-party software included with Nexabode.

## AI Assistance

AI tools were used during development as aids for programming, debugging, documentation, and technical problem-solving:

- **ChatGPT by OpenAI** — development discussions, code analysis, troubleshooting, documentation, and release preparation.
- **Claude by Anthropic** — development tasks, code review, and technical problem-solving.

AI-generated suggestions and outputs were evaluated and adapted as appropriate. The project maintainer remains responsible for the project's implementation, testing, final decisions, and maintenance. AI tools served as development assistants; they are not project maintainers or substitutes for human review.

## License

Nexabode is released under the [MIT License](LICENSE).

<br>

<div align="center">
  <a href="https://github.com/kvrtrss"><img src="docs/footer.svg" width="100%" alt="Nexabode: smarter occupancy, structured management"></a>
</div>
