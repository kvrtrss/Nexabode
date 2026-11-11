# Security Policy

## Supported versions
Only the latest release receives fixes.

## Reporting a vulnerability
Please **do not** open a public issue for a security problem.

Use the **Report a vulnerability** button on the repository's Security tab to send a private report. Include the version, what you did, and what happened.

**Never include real tenant data** (names, phone numbers, amounts) in a report. Made-up examples are enough.

## About your data
Nexabode stores data only on your own computer, in an encrypted database. There is no server and nothing is sent over the internet.

A forgotten sign-in password can be reset with the account's recovery code (shown once, kept only as a hash) or by an Owner in Settings. If the only Owner loses both the password and the recovery code, there is no other way to reset it. Keep your recovery code, your sign-in password and your backup password somewhere safe, and make regular backups.

Owners also have an emergency access key (shown once, kept only as a hash, written to the change history when used). If no account can be signed in to, it creates a new Owner account from the sign-in screen. It never changes or removes existing accounts, and using it replaces it with a new key. The sign-in screen has no open "create account" option on purpose, because anyone at the computer could use it. Keep the key somewhere secure and away from the computer.

Three failed sign-in attempts in a row lock sign-in for a while. Wrong recovery codes count toward the same lock. Wrong recovery codes and wrong emergency keys count toward the same lock. Failed attempts are never written to the change history with the username or password that was typed.
