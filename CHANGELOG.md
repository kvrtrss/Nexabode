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
- At first sign-in you can choose your own username along with your new password. Settings has a "Change username" button (it asks for your password), the menu card at the bottom left shows the signed-in username, and sign-in no longer cares about capitals in the username. Username changes are recorded in the change history

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

## [1.3.0] - Credit, notifications and accounts

### Added
- Credit: the Tenants page shows each tenant's Credit balance (the deposit still held), with Credit used and Credit refunded when they apply
- Edit credit corrects a credit that was entered wrongly, and Undo refund puts refunded money back into the credit. Both need a reason and are saved in the change history
- Billing Charges has a Refunded status for a cancelled charge whose money was given back, a Refunded filter, and a note on any charge that had money given back
- Void payment can record the amount given back to the tenant
- Edit rent on the Rent Billing page changes a generated rent charge's details, due date and (while nothing is paid) amount, with a required reason
- Notifications: a bell in the title bar with an unread count and a drop-down panel, and a Notifications page. They cover overdue and upcoming payments, move-ins and move-outs, rent not generated, utility bills not split, credit left with moved-out tenants, backup reminders, and your own reminders. Notifications can be marked as read, and the warning window (1 to 30 days) can be chosen
- Accounts: Owners can add Owner and Staff accounts in Settings, reset passwords, change roles and remove accounts. A new or reset account chooses its own password at the next sign-in
- A Log out button under the account card
- A Moving out status for a tenant on their last day

### Changed
- "Apply deposit" is now "Apply credit", and "Deposit refund" is now "Refund credit". New payments made from credit use the method "Credit"
- Refund credit asks for the amount to give back now, and only the credit that is left can be refunded. It asks for confirmation
- A tenant with a move-out date is Staying until that day, Moving out on that day, and Moved out the day after. Pressing Move out with today's date, or an earlier one, marks the tenant as moved out at once. A later date only schedules the move-out
- A moved-out tenant who still has credit left stays in the default Tenants list
- The billing statement shows "Credit" and "CREDIT BALANCE" in place of "Deposit" and "DEPOSIT HELD"
- Staff accounts cannot manage accounts or restore a backup
- The database gets five new columns and one new table on first start (automatic). A safety copy is saved first. Existing accounts become Owners

### Fixed
- Settings acted on the first account in the database. It now acts on the account that is signed in
- Pressing Move out on a tenant whose move-out date was already set left them as Staying

### Notes
- Payments made from credit before this version keep the method "Deposit" in the history
- Nexabode still keeps its data on one computer. Accounts share that computer's database, so people take turns signing in. They do not sync between computers
- Restoring a backup replaces all accounts with the ones in the backup
