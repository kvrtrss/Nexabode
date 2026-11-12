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

## [1.4.0] - Clearer, more professional wording

### Changed
- Messages across the whole app were rewritten to read formally and clearly: what happened, why, and what to do next
- When dates clash while moving in, moving out or editing a stay, the message now names the bed and the other tenant, the exact overlapping days, and how to fix it
- Billing, payment, void, undo and over-payment messages state the reason and the next step; confirmations say whether the action can be undone
- Notifications, reports, sign-in and recovery screens use consistent terms (charge, payment, outstanding, move-in date, sign in) and plain language instead of technical phrases
- The Dashboard reminder buttons now use the card's own amber (or blue for tips) instead of the app's indigo: a solid main button and a quiet outlined "Remind me later"

## [1.4.0] - A calmer first sign-in

### Changed
- A new Owner now sees one pop-up at first sign-in showing the recovery code and the emergency access key together (other accounts see just the recovery code). "Not now" is fine; nothing is saved until they confirm they have written it down
- Everything still to do is gathered into a single reminder on the Dashboard, replacing the separate backup and single-Owner warnings. While first-time setup is unfinished it is amber, headed "A few important steps are still waiting", with the line "Finish setting up now for peace of mind and to keep your data safe". One main button does the next step; "Remind me later" hides it until the app is next opened
- Once setup is done, later reminders (an old backup) use a plain blue "A quick reminder" card instead
- The "add a second Owner" suggestion is optional: it appears only after everything else is done, can be dismissed for good with "Not needed", and its notification is informational instead of a warning
- The recovery code and emergency key wording now describes a "spare key" rather than a warning

## [1.4.0] - Rooms grouped by floor

### Added
- Rooms and Beds now shows a heading for each floor ("Floor 1", "Floor 2", "Ground floor", ...) with that floor's rooms underneath, plus a count of rooms and beds
- Floors are ordered by number (Floor 2 comes before Floor 10); basement and ground come first
- Searching rooms also matches the floor, for example "floor 2"

### Changed
- Floor is now required when adding or editing a room. "Floor 1", "01" and "1" are all saved as the same floor
- Rooms created before this change that have no floor appear under "No floor set" at the bottom, and ask for a floor the next time they are edited. Nothing is guessed or changed automatically

## [1.4.0] - Getting back in when locked out

### Added
- Two-Owner warning: while there is only one Owner account, Owners see a notice on the Dashboard, in Settings > Accounts and in Notifications, with a button to add a second Owner. Staff are not shown it, since they can't add accounts
- Print recovery sheet: when a recovery code or an emergency access key is made, a "Print recovery sheet" button prints it on a page that says which button to use on the sign-in screen and what to type, with a line to write where it is kept. It goes straight to the printer; Nexabode saves nothing and puts nothing on the clipboard
- Reset phrase: an Owner can choose a phrase in Settings > Accounts (asks for the password first). On the sign-in screen, "Locked out of every account?" now has two choices, the emergency access key or the reset phrase. Either one only adds a new Owner account. Capitals and extra spaces don't matter. The phrase can be changed or removed in Settings
- Settings > Accounts shows whether the emergency access key and the reset phrase are ready

### Changed
- The sign-in lock message now lists what to do when it ends: sign in with another Owner, use "Forgot password?" with the recovery code, or use "Locked out of every account?" with the emergency key or reset phrase
- The recovery code and emergency key screens now say "written down or printed"
- The database gets one new column (SignInGuards.ResetPhraseHash) on first start (automatic). A safety copy is saved first

### Security
- The reset phrase must be at least 3 different words and 16 characters, and must not contain the username. Only a BCrypt hash is kept; the phrase is never written to the change history
- It can be used once: using it removes it, so the next lockout needs a new one. Wrong phrases count toward the same sign-in lock as wrong passwords and keys, and nothing is checked while sign-in is locked
- Using it, setting it, changing it and removing it are written to the change history as "Security" entries
- A reset phrase is weaker than the emergency key, because an Owner chooses it. It only helps someone who is at this computer and knows it. Don't use it if you would rather rely on the emergency key alone: it is optional, and it can be removed at any time

### Notes
- A recovery code or key is shown once, so it can only be printed when it is made. To print another, make a new one in Settings (the old one stops working)

## [1.4.0] - Independent scrolling, automatic receipt number

### Changed
- Payment history no longer has "Ref. no." and "Details" columns, so the Tenant column is readable again and the table is less crowded
- Every table, list and panel scrolls on its own, and only while the pointer is over it. At its first or last row it no longer passes the wheel on to the page, so the page and the other panels stay still
- A table, list or panel with nothing to scroll hands the wheel to the page, so the page is never stuck under it
- Mouse wheels, trackpads and smooth scrolling keep working as normal: the app now only steps in for an area with nothing to scroll, and leaves the scrolling itself to Windows

- Scroll bars sit in a free strip beside the content instead of on top of it, in the tenant, room, payment, report and charge panels, the notifications page and list, and the report dialog

### Added
- Dashboard "Recent activity" now has an icon for each kind of event (money received, moved in, moved out, overdue), coloured like its title
- Payment history shows the reference no., remarks and void reason of the selected payment in one line under the buttons
- The payment form shows the receipt number the payment will get, in a field that can't be edited. It follows the payment date's year and moves on after each payment

### Fixed
- A bed is now Available as soon as its tenant is moved out. Before, a tenant who left on their last day was already Moved out, but the bed stayed Occupied until the next day. This also fixes the Dashboard bed count, the occupancy report, the Rooms & Beds details, and lets a bed be deactivated, put under maintenance, or given to a new tenant the same day. The last day is still billed exactly as before
- The reference number box is switched off for cash payments. It is only for GCash, bank or check references, and it is cleared if the method is changed to cash

## [1.4.0] - Forgot password and sign-in lock

### Added
- Forgot password: a link on the sign-in screen lets someone choose a new password with their recovery code. The same message is shown for a wrong username and a wrong code, so it doesn't reveal which usernames exist
- Recovery codes: each account gets a 16-character code (shown once, written as XXXX-XXXX-XXXX-XXXX). Only a BCrypt hash is stored, never the code itself. Using a code makes a new one. Existing accounts are shown their first code the next time they sign in
- New recovery code in Settings (asks for your password first). The old code stops working when the new one is saved
- Sign-in lock: 3 failed attempts in a row lock sign-in for 5 minutes, with a countdown. Each repeat lock is longer (15, 30, then 60 minutes), and a correct sign-in starts again. Wrong recovery codes count toward the same lock
- Locks are written to the change history as "Security" entries

### Changed
- The lock is saved in the encrypted database, so closing the app, reopening it or logging out does not clear it
- A wrong username and a wrong password now take the same time to answer, and show the same message
- The password dialog no longer says there is no password reset
- The database gets one new column and one new table on first start (automatic). A safety copy is saved first. Back up before upgrading

### Security
- While sign-in is locked, nothing is checked, so even a correct password is refused until the lock ends
- Passwords, recovery codes and the text typed in the username box are never written to the change history
- The recovery code is not placed on the clipboard

### Notes
- If the only Owner loses both the password and the recovery code, Nexabode cannot reset it. A second Owner account can reset the first one in Settings
- The lock depends on the computer's clock. It slows down guessing but is not a defence against someone who controls the computer

### Interface (sign-in and recovery screens)
- "Forgot password?" is now a link in the app's primary color, on the same line as the Password label, instead of a plain button under Sign in
- Messages on the sign-in screen use the same tinted banners as the rest of the app: red for a wrong password (with the attempts remaining), amber for the lock (with the countdown), green after a password change
- The recovery code is shown in a larger, clearly separated box with a "shown only once" notice
- The recovery code box accepts lowercase, and shows an example of the format
- Clearer, more formal wording on the sign-in, reset and recovery screens
- Controls on the new screens have accessible names for screen readers

### Interface (buttons, scrolling, accounts)
- Add account: the role is now chosen from a drop-down (Staff or Owner) instead of typed. Other forms can use the same drop-down
- Scrolling: the mouse wheel now scrolls the page wherever the pointer is, including over tables and empty space, not only over the scroll bar. A table that has reached its top or bottom passes the wheel on to the page
- Tenants, Billing, Rent billing and Utility bills pages now scroll as a whole when the window is short, instead of squeezing their tables
- Buttons are organised the same way across the app: filters and actions are on separate rows, actions are grouped with a divider between groups, and every button has the same size and gap
- Tenant and Room side panels group their actions under small headings (Tenant, Stay, Credit, Room, Selected bed) in an even two-column grid
- Settings buttons use the same spacing and grouping
- "Forgot password?" has more space around it, so it no longer sits close to the show/hide password icon, and it no longer has a box around it

## [1.4.0] - Emergency access, error banners and password strength

### Added
- Emergency access: Owners are shown an emergency access key once (written as five groups of four characters). "Locked out of every account?" on the sign-in screen uses it to create a new Owner account if no one can sign in. It never changes or removes existing accounts, and using it makes a new key. Only a BCrypt hash is stored. Using it, and making a new key, are written to the change history
- New emergency key in Settings (Owners only, asks for your password first)
- Password strength hint under new-password boxes (Change password, Reset password, Emergency access, and new backup passwords): four bars and a line of advice. It warns about short, common, repeated or sequential passwords and passwords that contain the username. It only advises: the existing rules still decide what is accepted

### Changed
- Errors in every dialog now use the same red banner as the sign-in screen
- A wrong emergency key counts toward the same sign-in lock as other failed attempts
- The sign-in screen is slightly taller to fit the new link

### Security
- There is deliberately no open "create account" button on the sign-in screen, because anyone at the computer could use it to get full access. The emergency key is the safe alternative
- The username is only checked (and so only revealed as taken) after the emergency key is correct

### Notes
- Password boxes already include the show/hide (eye) button supplied by the interface library, so no change was needed there
- Existing Owners are asked to save their emergency key the next time they sign in. "Skip for now" is allowed, and they are asked again at the following sign-in

## [1.4.0] - Colour-coded history and confirmations everywhere they make sense

### Added
- Change history: the label of each kind of change is colour-coded (Stay indigo, Billing amber, Payment green, Account teal, Security rose) and each card has a coloured edge. Light and dark versions, with the word kept on the label
- Credit confirmations: Apply credit, Edit credit, Refund credit and Undo refund each show the credit (and for Apply, the unpaid balance) before and after, with a button named after the action
- A "Save these changes?" review, listing old to new and skipped when nothing changed, for Edit charge, Edit payment, Edit rent and Edit bill

### Changed
- Every confirmation now uses the same window instead of the grey Windows message box: Archive all settled (charges, rent and utility bills), Generate rent, Record payment, Cancel reservation, Remove account, Change role, Restore backup, Remove reset phrase, Delete bill, Split bill, Delete reminder, Log out and the Utility Bills prompts. The messages are unchanged. The confirm button says what it does, Enter confirms, and the window opens over the screen in use
- Void charge, Void payment, Undo split, Reset password and Restore charge keep a single window each: it already lists the facts and consequences and has a button named after the action, so there is no second prompt
- Stays in the Change history are described in plain words, not as the stored line with raw dates and amounts
- "Closing the app" still asks whether to back up first with Yes and No, because No means "close now"

## [1.4.0] - Cancel stay, plain-language review, readable history

### Added
- Remove this stay on a stay that already has billing now works: it cannot be deleted, so it is cancelled. It needs every charge voided and no deposit on the stay, an Owner's password and a reason. A cancelled stay is kept with its records and shown greyed and struck through in Stay history, but it no longer holds a bed, no longer shows as a clash and is not billed. A stay that was never billed is still deleted, as before
- Change history is now a timeline of cards: one for each action, in plain words, with the other things that changed alongside it, the reason, who and when, grouped under Today, Yesterday or the date. Records saved together by one action are shown as one. Filter by kind (Stay, Billing, Payment, Account, Security) and search the wording. "Technical view" brings back the table of every stored record
- Every reason box (void, restore, remove, edit stay, credit and others) now requires at least three letters or digits

### Changed
- Review changes: sections "What is changing", "The bed" and "Billing", a highlighted one-sentence summary of what the change means for the rent, bullets with a hanging indent, small grey notes, and a window that fits its content. The confusing "more rent is due" message for a shorter stay now says what the new month's rent is and why, when the full month is charged for part-month stays. The buttons read "Go back" and "Save changes" ("Save the stay" or "Save move-out" where that is clearer)
- A reason of "." or "j" that was saved earlier is shown in the history as "No reason given"

### Database
- Three optional columns on stays (IsCancelled, CancelReason, CancelledOn). They are added when the app starts, existing data is kept, and the first upgrade makes the usual safety copy

## [1.4.0] - Buttons, back to the Windows look

### Changed
- Buttons keep the native Windows (WPF-UI) look in light and dark mode. A first attempt at a custom button style (stronger borders, bold text, grey fills, red outlines) made the screens busy and was removed
- Dialogs follow the Windows dialog pattern: the message on top, and the buttons in a slightly different command area set off by a hairline, as two equal-width buttons with the main action in the accent color on the right
- "Payment recorded": OK on the left, and Print receipt, the likely next step, on the right in the accent color with a printer icon
- Buttons that void or remove something (Void payment, Void charge, Remove account, Remove this stay) are normal buttons with red text, the same idea as Log out

## [1.4.0] - Tenant gender

### Added
- An optional Gender field (Male, Female, Other, or Not set) in Add tenant and Edit tenant. It shows in the tenant details, as a Gender column in the tenant list, and beside the tenant in Rooms & Beds under "Staying in". Existing tenants start as Not set. Edit tenant lists a gender change in its confirmation like any other field
- The database gets a new optional Tenants.Gender column when the app starts. Existing data is kept, and the first upgrade makes the usual safety copy first
- Changing your username (Settings) now asks "Are you sure?" and shows the old and new name

## [1.4.0] - Confirmation steps

### Added
- A reusable "Are you sure?" window with Confirm and Back (Esc also goes back)
- It now appears before: saving an edited tenant, room or bed (it lists each field that changes, as "Contact number: (empty) to 0917...", and does nothing when nothing changed); activating or deactivating a room or a bed; starting or ending bed maintenance; creating an Owner account from "Locked out of every account?"; changing a password in Forgot password, in the first sign-in set-up and in Settings
- Change role opens a role choice box first, like Add account, starting on the other role. Choosing the role the account already has is refused. The existing message about what the new role can and cannot do still follows

### Changed
- On the sign-in screens, blank fields and mismatched passwords are now reported before the confirmation, so nobody is asked to confirm something that cannot work

## [1.4.0] - Clash warnings on Rooms & Beds and Tenants

### Added
- Rooms & Beds: "Edit this stay" and "Remove this stay" (Owners only) under each "Overlaps with..." line, so a clash can be fixed without leaving the page. They are the same actions as on the Tenants page, with the same rules: a stay can only be removed when nothing was billed on it, and removal asks for a reason and the Owner's password
- Rooms & Beds: a red banner at the top of a room's details when two stays share a bed on the same days. It names each bed and the tenants involved, says what to do (and, for Staff, to ask an Owner to remove a stay entered by mistake), and has an "Open Tenants" button. The small warning mark on the bed chip now explains itself in its tooltip
- Tenants: a red "Check" line when another tenant is booked on the same bed on some of the same days (until now only a tenant's own overlapping stays were flagged), and a warning mark beside the name in the tenant list. Only stays that still hold their bed are checked, the same set Rooms & Beds uses

## [1.4.0] - Restoring a voided rent charge

### Added
- "Restore charge" on Rent Billing for a voided rent charge. It reuses the same charge record, updates it to what the stay would be billed today (days, amount, details), moves a past due date to today so it is not overdue at once, asks for a reason, and writes every change to the change history. It is refused when the charge has payments or adjustment charges, or the stay has no days in that month
- When a stay is edited or a move-out is saved and that gives days back to a month whose rent was voided, the message now says so and points to Restore charge

### Fixed
- After a move-out was undone, rent could not be billed again because the voided charge counted as already generated and there was no way to bring it back
- When every rent row of a month was voided, Rent Billing said "No stays this month". It now says how many are voided and how to see and restore them
- Voiding a charge on Billing Charges is now written to the change history (it was not recorded before)

## [1.4.0] - Removing a stay entered by mistake

### Added
- "Remove this stay" under Stay history (Owners only), for a stay that was entered by mistake: a duplicate, the wrong tenant or the wrong bed. It only works when no rent or utility charge was ever made for the stay (voided charges count too) and no deposit refund is recorded on it. It asks for a reason and the Owner's password, warns that it cannot be undone (and that a recorded deposit goes with the stay), and writes what was removed to the change history

### Changed
- The red "Check" line for overlapping stays now says when two stays are exactly the same and points to Remove this stay, instead of only pointing to Edit this stay. Staff are told to ask an Owner

## [1.4.0] - Printable payment receipts

### Added
- Print receipt: a one-page receipt for any recorded payment, from the "Payment recorded" window that now appears after every payment, or from the "Print receipt" button in the payment history. It shows the house name and address (from Reports settings), receipt no., date, tenant, room and bed, what was paid for, amount received, change returned, payment method and reference, and the balance left as of the day of printing, with a line for the person who received the money
- A reprint is marked "DUPLICATE COPY (print no. N)", and each printing is written to the change history ("Receipt printed"), so a second paper receipt can be traced
- A voided payment cannot be printed

### Fixed
- The printed receipt squeezed its right-hand column into a sliver, so the values were stacked letter by letter and the receipt ran onto three pages. The table columns are now sized from the real printable width

### Changed
- The "Print receipt" button was removed from the green banner under the payment form. The window shown after recording a payment now has it, next to OK, and is shown for every payment (it used to appear only when there was change to return)

## [1.4.0] - Making the reset phrase easier to find

### Added
- The first-time recovery window (recovery code and emergency key) now has an optional "Reset phrase" section with a button to set one
- The Dashboard reminds an Owner, as a gentle tip, that a reset phrase can be set. "Not needed" hides the tip for good, and it disappears once a phrase is set
- An optional "Set a reset phrase" notification (under the bell) for Owners, shown until a phrase is set or "Not needed" is chosen on the Dashboard
- A short line under "Locked out of every account?" on the sign-in screen says the emergency access key or reset phrase can be used there
- Settings explains what the emergency key and the reset phrase are for, and the status line suggests setting a phrase when none exists

### Changed
- The "Forgot password?" window no longer says nothing can be done when the only Owner has lost the recovery code: it now points to the emergency access key and the reset phrase
- The printed emergency key sheet mentions that a reset phrase can be typed instead of the key (the phrase itself is never printed)

## [1.4.0] - Receipt numbers and small-screen fixes

### Added
- Automatic receipt numbers: every payment is given a receipt number when it is saved, such as RCT-2026-00042 (the year of the payment, then a running number that starts again each year). Nobody has to type it. Numbers are never reused, even when a payment is voided or edited
- Payments recorded before this update are numbered automatically on first start, oldest first. A safety copy of the database is made first, as for every upgrade
- The receipt number is shown in the payment history (and can be searched), in the charge details on Billing, in the Edit payment window, in the payment reports, and in a confirmation after a payment is recorded

### Changed
- The typed "Reference no." is now "Ref. no." and is for a reference from outside only (a GCash, bank or check number). It stays optional
- Reports that list payments have a Receipt no. column

### Fixed
- Rooms & Beds: the page had no scrolling, so the room details and the bed buttons were cut off on a small window. The details panel now scrolls by itself, and so does the whole page
- Tenants: the details panel (stay, credit and history) was cut off when the window was short. It now scrolls by itself
- Payments and Reports pages now scroll as a whole when the window is short, and every page scrolls sideways instead of squeezing or cutting off its content when the window is narrow
- The main window no longer opens larger than the screen's usable area (it used to open 760 pixels tall, which does not fit a 768-pixel screen once the taskbar is counted), and it can now be made smaller (minimum 820 by 540)
- The sign-in window fits small screens, and scrolls if it must
- Dialogs can no longer run off the bottom or side of a small screen, and a tall dialog scrolls

