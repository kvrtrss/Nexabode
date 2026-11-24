# Security Policy

## Supported versions
Only the latest release receives fixes.

## Reporting a vulnerability
Please **do not** open a public issue for a security problem.

Use the **Report a vulnerability** button on the repository's Security tab to send a private report. Include the version, what you did, and what happened.

**Never include real tenant data** (names, phone numbers, amounts) in a report. Made-up examples are enough.

## About your data
Nexabode stores data only on your own computer, in an encrypted database. There is no server and nothing is sent over the internet.

A forgotten Staff password is reset by an Owner in Settings. A forgotten Owner password is reset with the Owner recovery key, which is shown once when the Owner account is created and kept only as a hash. Using the key sets a new password for an Owner (or creates a new Owner account) and replaces the key with a new one; the use is written to the change history. If the only Owner loses both the password and the recovery key, there is no other way to reset it. Keep your recovery key, your sign-in password and your backup password somewhere safe, consider a second Owner account, and make regular backups.

There is no default account or password: the first Owner account is created by whoever starts Nexabode for the first time. The sign-in screen has no open "create account" option on purpose, because anyone at the computer could use it to get full access.

Very weak passwords (very common ones, repeated characters, or ones containing the username) are refused whenever a new password or backup password is chosen.

Three failed sign-in attempts in a row lock sign-in for a while. Wrong recovery keys count toward the same lock. Failed attempts are never written to the change history with the username or password that was typed.
