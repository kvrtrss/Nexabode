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
  <img src="docs/specs.svg" width="50%" alt="Nexabode specs: version 1.0.0, Windows 10 and 11, .NET 10, SQLCipher 4, MIT license">
</p>

## Screenshots

<table>
  <tr>
    <td width="50%"><sub><b>01 / DASHBOARD</b></sub><br><img src="docs/screenshots/dashboard.png" alt="Dashboard"></td>
    <td width="50%"><sub><b>02 / ROOMS AND BEDS</b></sub><br><img src="docs/screenshots/rooms.png" alt="Rooms and beds"></td>
  </tr>
  <tr>
    <td width="50%"><sub><b>03 / RENT BILLING</b></sub><br><img src="docs/screenshots/rent-billing.png" alt="Rent billing"></td>
    <td width="50%"><sub><b>04 / PAYMENTS</b></sub><br><img src="docs/screenshots/payments.png" alt="Payments"></td>
  </tr>
  <tr>
    <td width="50%"><sub><b>05 / BILLING CHARGES</b></sub><br><img src="docs/screenshots/billing.png" alt="Billing charges"></td>
    <td width="50%"><sub><b>06 / REPORTS</b></sub><br><img src="docs/screenshots/reports.png" alt="Reports"></td>
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

**Tenant Management**

- Search and manage tenant records, occupancy status, and balances.
- Record move-ins and move-outs and review tenant stay history.
- See each tenant's credit balance (the deposit still held), apply it to unpaid charges, refund what is left, correct it with a recorded reason, and undo a refund recorded by mistake.
- Correct any stay (bed, dates, rent, deposit) with a recorded reason.
- Tenants show as Staying, Moving out (on their last day) or Moved out.
- Overlapping stays are blocked on new move-ins and flagged as warnings when correcting history.
- An Owner can remove a stay that was entered by mistake (a duplicate, the wrong tenant or bed) with "Remove this stay" under Stay history. It is allowed only when nothing was ever billed on the stay, needs the Owner's password and a reason, and a record of what was removed is kept in the change history. A stay that already has charges is ended or corrected instead, never deleted.
- Archive settled, moved-out tenants and flag potentially duplicate names.

**Billing and Payments**

- Generate monthly rent charges with optional day-based proration.
- Record electricity, water, and other utility bills and split eligible expenses among tenants.
- Review, edit, void, archive, and restore billing records.
- Record payments and allocate them to outstanding charges using different methods. A payment larger than the balance is accepted, and the extra is shown as change to return.
- Bill utilities for periods that span months, and keep rent and utility charges in step when stay dates are corrected. Paid charges are never altered; differences become adjustment charges.
- Review exactly which records a change affects before you apply it.
- Apply a tenant's credit to unpaid balances, and refund the credit that is left in full or in part.
- Edit a generated rent charge, with a recorded reason.
- Record money given back when a payment is voided. Charges that were refunded are marked Refunded.
- Tenants have an optional Gender (Male, Female, Other, or Not set), chosen when adding or editing a tenant. It shows in the tenant details and list and beside the name in the Rooms & Beds list of who is staying. Existing tenants start as Not set.
- Every confirmation in the app uses one window (Back and Confirm, Enter confirms, the confirm button names the action: Archive, Delete, Record payment...). The rule is simple: an action that needs typing and already says what it does (Void charge, Void payment, Undo split, Reset password, Restore charge) is one window that states the facts and consequences, with no second prompt. Edits (tenant, room, bed, charge, payment, rent, utility bill, credit) show a short "Save these changes?" list of old to new, and do nothing when nothing changed. Credit actions (Apply, Refund, Undo refund, Edit) show the credit before and after. Generate rent, Archive all settled, Record payment, Log out and the other yes/no questions use the same window. Archiving or unarchiving a single row does not ask, because it can be undone at once.
- Change history labels are colour-coded: Stay indigo, Billing amber, Payment green, Account teal, Security rose. Each card also has a coloured edge, and the word stays on the label so colour is never the only clue. Stays are described in plain words ("Room 101 / Bed 2, Nov 9, 2026 to Nov 9, 2026, rent 1,500.00") instead of the stored record.
- Changes ask "Are you sure?" first, with Confirm and Back: saving edits to a tenant, room or bed (the window lists exactly what will change, and saving with no changes does nothing), activating or deactivating a room or bed, starting or ending maintenance, creating an Owner account from the sign-in screen, and changing a password (Forgot password, first sign-in and Settings). Change role now opens a role choice box first, like Add account, and still shows its message about what the role can do.
- Clashes are flagged in two places. Rooms & Beds shows a red banner on a room where two stays share a bed on the same days (naming the beds and tenants and what to do, with an Open Tenants button), and each "Overlaps with..." line there has Edit this stay and Remove this stay (Owners only) buttons that work exactly like the ones on the Tenants page, and the Tenants page shows the same kind of red Check line for the tenant, plus a warning mark beside the name in the list.
- A voided rent charge counts as already generated, so rent is not created again for it. If the void was a mistake, or the stay changed afterwards (for example a move-out was undone), select the voided row on Rent Billing (tick Show voided) and use "Restore charge": the same charge becomes active again, updated to what the stay would be billed now, with a reason saved in the change history. Editing or ending a stay tells you when a voided month gets days back. Voiding a charge is now also written to the change history.
- Removing a stay that was entered by mistake: "Remove this stay" deletes it when nothing was ever billed on it. If it has billing records it cannot be deleted, so it is cancelled instead (only once its charges are voided and it has no deposit): it stays in Stay history marked Cancelled, but no longer holds a bed, causes no clash and is not billed. Both need an Owner's password and a reason.
- Review changes (shown before a stay edit or move-out that affects billing) is written in plain language: what is changing, what happens to the bed, and what happens to billing, with a one-sentence summary such as "The tenant will owe 400.00 more in rent". When the full month's rent is charged for a part-month stay it says so, instead of looking like a mistake.
- Change history reads like a timeline: one card per action in plain words (for example "November 2026 rent was restored"), with what else changed with it and the reason, grouped by day, and filterable by kind of change. "Technical view" shows every stored record in a table as before. Every reason must have at least three letters or digits, so "." or "j" are no longer accepted.
- Print a payment receipt (receipt no., tenant, room and bed, what was paid for, amount received, change and the balance left) from the Payments page, in the window that appears right after a payment is recorded or later from the payment history. A reprint is marked "DUPLICATE COPY", every printing is written to the change history, and a voided payment cannot be printed.
- Search payment history and manage archived or voided transactions.

**Reports and Printing**

- Generate tenant billing statements, occupancy reports, and collection reports.
- Print reports or save them as PDFs using the Windows print dialog.
- Include the boarding house name and address in report headers.

**Notifications**

- A bell in the title bar with an unread count and a drop-down panel, and a Notifications page.
- Overdue and upcoming payments, move-ins and move-outs, rent not yet generated, utility bills not yet split, credit left with moved-out tenants, and backup reminders.
- Your own reminders, with a warning window you can set.

**Security and Data Protection**

- Locally stored, encrypted SQLite database using the SQLCipher 4 file format.
- Password hashing using BCrypt and a required password change on first sign-in.
- Separate sign-in accounts with two roles: Owner (everything) and Staff (records only, with no account management or backup restore). New and reset accounts must choose their own password.
- Receipt numbers: every payment gets a numbered receipt automatically (like RCT-2026-00042: the year, then a running number that starts again each year). Nobody types it, and a number is never reused, even when a payment is voided. The optional "Ref. no." box is only for a reference from outside, such as a GCash reference.
- Forgot password: each account has a one-time recovery code, shown once and kept only as a BCrypt hash. Use it on the sign-in screen to choose a new password. Using it makes a new code.
- Emergency access: each Owner is shown an emergency access key once. If no account can be signed in to, "Locked out of every account?" on the sign-in screen uses that key to create a new Owner account. It never changes or removes existing accounts, and using it makes a new key.
- Reset phrase: an Owner can choose a phrase (at least 3 different words and 16 characters) in Settings. If no account can be signed in to, "Locked out of every account?" accepts it instead of the emergency access key and creates a new Owner account. It can be used once, then it is removed. It is easier to remember than the key, and so easier to guess: it only works for someone at this computer, wrong tries count toward the sign-in lock, and it is optional. Nexabode tells Owners about it in the first-time recovery window, in a Dashboard reminder (until it is set or dismissed), under the "Forgot password?" window, below the "Locked out of every account?" link, and in Settings.
- Recovery sheet: when a recovery code or emergency access key is made, it can be printed on a sheet that says how to use it. Nothing is saved as a file by Nexabode.
- Two Owners: Nexabode warns Owners (on the Dashboard, in Settings and in Notifications) while there is only one Owner account, because a second Owner can reset the first one's password.
- Sign-in lock: 3 failed attempts in a row lock sign-in for 5 minutes. Each repeat lock lasts longer (15, 30, then 60 minutes) until a correct sign-in. The lock is saved in the encrypted database, so closing the app or logging out does not clear it.
- Password-protected database backup and restore, with automatic backup reminders.
- Database safeguards against duplicate rent charges and utility splits.
- Protection intended to prevent multiple application instances from running simultaneously.
- Change history recording the original value, new value, administrator, time and reason of corrections.
- An automatic safety copy of the database before an upgrade.

**Customization**
- Light and dark themes.

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

**Example:** for `Nexabode-1.0.0-win-x64.zip`, run:

```powershell
Get-FileHash .\Nexabode-1.0.0-win-x64.zip -Algorithm SHA256
```

Compare the resulting hash with the SHA-256 value published for that exact release asset. A matching hash verifies that the file matches the published checksum; it does not independently prove that the software is free from vulnerabilities.

</details>

### First sign-in

<p>
  <img src="docs/first-signin.svg" width="50%" alt="First sign-in for v1.0.0: username admin, password admin123, password change required on first login">
</p>

The application requires the default password to be changed on first sign-in. At the same step you can choose your own username instead of `admin`, and you can change it later in **Settings**. Change the password immediately and do not reuse the default credentials elsewhere.

An Owner can add more accounts in **Settings**. A new account signs in with a temporary password and must choose its own the first time.

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
- The sign-in screen deliberately has no open "create account" button: anyone at the computer could use it to get full access. Creating an account without signing in needs the emergency access key.
- A forgotten password can be reset with the account's recovery code, or by an Owner in **Settings**. Write the recovery code down and keep it somewhere private, away from the computer. If the only Owner loses the password, the recovery code, the emergency access key and the reset phrase, Nexabode cannot reset it. Keep the emergency access key written down somewhere secure, and consider a second Owner account.
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
