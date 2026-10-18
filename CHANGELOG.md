# Changelog

All notable changes to this project are listed here.

## [1.0.0] - first public release

### Added
- Rooms and beds with five bed statuses (Available, Occupied, Reserved, Maintenance, Inactive), edit, activate and deactivate
- Tenants with stay history, move in and move out, search, and filters
- Rent Billing page to generate monthly rent, with optional day-based proration and automatic day-based billing for tenants with more than one stay in a month
- Utility Bills page with equal splitting among tenants, edit, and undo split
- Billing Charges page showing every charge, who has paid it, and a per-item summary for each tenant
- Payments with a "what is the tenant paying for" choice (oldest first, one item, or one specific charge), edit, and void
- Dashboard with stat cards, calendar, and recent activity
- Billing statement, occupancy report, and collection report, with printing and a configurable report header
- Encrypted database, forced change of the default password, password-protected backup and restore
- Database rules against duplicate rent charges and duplicate utility splits, and a single-instance guard
- Light and dark themes

## [1.0.1] - fixes

### Added
- Tenants list has a "Show archived" toggle; archived tenants (moved out, nothing owed) are hidden by default
- Rent Billing has a "Show voided" toggle
- A tenant's deposit is now shown in their stay history

### Changed
- Partial months are billed by days by default; the checkbox is now "Charge full rent for partial months"
- Voided payments and voided rent charges are hidden by default (tick "Show voided" to see them)
- Dashboard and Occupancy report now say that reserved beds are included in occupancy
- The Settings page shows the version from the app itself
- Smaller download: unused language folders and an unused package were removed

### Fixed
- Utility bills are split per tenant, so a tenant who changed beds pays one share
- Cancelling a reservation removes it instead of billing it as a one-day stay
- Move-in dates earlier than a bed's last move-out are rejected
- A failed backup restore is rolled back, keeping the data and its key matched
- "100% collected" and 100% occupancy no longer show while something is outstanding
- Room names are checked without regard to capitalisation
- Error messages no longer blame "another window"

## [1.1.0] - New features and changes

### Added
- Archive: hide settled payments, charges, rent and utility bills from the lists, with Archive and Unarchive buttons, multi-select, "Archive all settled" for a month, and a "Show archived" toggle that also reveals older months. Archiving never changes totals, reports or statements
- Apply deposit: use a tenant's deposit to pay their oldest unpaid charges
- Deposit refunds: record a full or partial refund; the billing statement shows deposit paid, used, refunded and held
- Edit stay: change a stay's rent, deposit and (before billing exists) move-in date
- Backup reminder on the Dashboard and in Settings after 7 days, and a prompt when closing with a backup overdue

### Changed
- A payment larger than the tenant's balance is refused with a clearer message
- Switching theme keeps the window's size, position and state
- The database gets seven new columns on first start (automatic). Back up before upgrading

## [1.1.1] Summary

- Update the application version from 1.1.0 to 1.1.1.
- Prepare the project for the v1.1.1 Windows x64 release.

## Release packaging

- Prepare a refreshed Windows x64 release package.
- Publish the release ZIP separately through GitHub Releases after the source change is merged.
