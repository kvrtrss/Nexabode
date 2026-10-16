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
