# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | Yes       |

## Credential Handling

GLic requires two sensitive files: a GCP service-account key (`service-account.json`)
and a workspace config (`glic.json`). These files are handled as follows:

- **Never committed to source control.** `.gitignore` and the MSBuild publish gate
  (`tools/Assert-CleanModule.ps1`) both verify these files are absent from the staged payload.
- **Per-user install (`CurrentUser`):** stored in `%APPDATA%\GLic\`. Access is limited
  to the installing user by the profile's default ACLs.
- **Machine-wide install (`AllUsers`):** stored in `%ProgramData%\GLic\` with ACLs
  restricted to `SYSTEM` + `BUILTIN\Administrators` via `icacls /inheritance:r`.
  Standard users cannot read the key by default; pass `-ReadAccessAccount` to grant
  a scheduled-task or service identity read access.

## Reporting a Vulnerability

To report a security vulnerability, email **schelld@croslex.org** with the subject
`[GLic Security] <brief summary>`. Do **not** open a public GitHub issue for security
vulnerabilities. You will receive an acknowledgement within 72 hours and a fix timeline
within 7 days for confirmed vulnerabilities.
