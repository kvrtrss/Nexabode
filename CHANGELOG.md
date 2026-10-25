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

## [1.2.0] - Stay corrections, billing adjustments and payment change

### Added
- Edit any stay from a tenant's stay history: bed, move-in date, move-out date, rent and deposit, with a required reason. Past, present and future dates are allowed, and the move-in date is no longer locked once billing exists
- The move-out date is now the tenant's last day in the bed. Leave it blank for an ongoing stay, or set today or a future date. A bed is free from the next day
- Overlap rules: a new move-in that overlaps another stay is blocked. When correcting an existing stay, an overlap that the change creates or lengthens shows a warning naming the other stay and the exact days, and can be saved only as an explicit exception with a reason. A move-out before the move-in is never accepted
- A "Review changes" window before any change that affects billing, listing the affected rent, utility shares, deposit and bed availability
- Rent corrections after a date change: unpaid charges are updated or voided, extra rent on a paid charge becomes a linked adjustment charge, and an overpaid charge is flagged and left as it is
- Utility bills can have a billing period that spans months. Tenants share a bill if they have at least one day in its period, and an overlap warning stops the same days being billed twice
- Utility shares follow stay changes under the same safe rules as rent, and "Recalculate shares" brings a split bill in line with the actual stay dates
- Change history window (per tenant, and in Settings) recording the original value, new value, administrator, time and reason of every stay, rent and utility correction. Overlap exceptions are marked
- Payments can be more than the balance: the charges are paid in full and the extra is shown as change to return, live while typing, on the confirmation, and in the payment history (Received, Applied, Change)
- Settings has a read-only "Check stay records" button that lists overlapping or invalid stays
- A test project with unit and scenario tests for the stay, rent, utility and payment rules

### Changed
- Bed availability and tenant status are decided by dates. A tenant whose move-out is today still shows as Staying until tomorrow
- Ending or shortening a stay is never blocked by an overlap that already exists, and an edit that does not lengthen an overlap does not ask for an exception again. Move out ends all of a tenant's current stays on the chosen date
- Rooms, the Dashboard and the Occupancy report show overlapping stays instead of hiding one of them, and the Dashboard counts a bed once
- The move-in bed list shows every usable bed, with notes such as "occupied" or "free from <date>", so past stays can be recorded
- Payment totals count only what was applied to charges. Change is not counted as collected
- "Charge full rent for partial months" is remembered for the property
- The Rent Billing, Utility Bills and Billing Charges month lists reach back to the earliest move-in
- A safety copy of the encrypted database is saved to the SafetyCopies folder automatically before the first start of this version
- The database gets one new table and four new columns on first start (automatic). Back up before upgrading

### Notes
- Changing a stay's agreed rent applies to rent generated from then on. It does not rewrite past charges
- Paid charges and shares are never altered. When someone has paid more than is now due, the item is flagged for you to decide
- Months with no rent charge yet are listed in the review window, not generated automatically
- Utility shares remain equal among tenants
- Nexabode still does not hold advance payments: a payment needs at least one unpaid charge to be applied to
