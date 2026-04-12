# Security Policy

## Supported Versions

Only the latest released version of NuSpec.AI receives security updates.

| Version | Supported          |
| ------- | ------------------ |
| Latest  | :white_check_mark: |
| < Latest | :x:               |

## Reporting a Vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

### GitHub Private Vulnerability Reporting (preferred)

1. Go to the [Security Advisories](https://github.com/sean-m-cooper/NuSpec.AI/security/advisories) page
2. Click **"Report a vulnerability"**
3. Fill in the details and submit

### Email (fallback)

Send an email to `77304802+sean-m-cooper@users.noreply.github.com` with:

- **Subject**: `[SECURITY] <brief description>`
- A description of the vulnerability
- Steps to reproduce
- Potential impact

## What to Include

When reporting a vulnerability, please provide:

- NuSpec.AI package version
- .NET SDK version (`dotnet --version`)
- Operating system and version
- Detailed reproduction steps
- Assessment of impact and severity

## Response Timeline

- **Acknowledgement**: within 48 hours
- **Initial assessment**: within 7 days
- **Fix or mitigation**: within 14 days for confirmed vulnerabilities

## Scope

### In scope

- The `NuSpec.AI` NuGet package
- The `NuSpec.AI.Tool` CLI tool
- MSBuild integration (`.props` / `.targets`)
- Generated `ai/package-map.json` output

### Out of scope

- Consumer projects analyzed by the tool
- Third-party dependencies (report those to their maintainers)

## Credit

We are happy to credit security researchers in release notes upon request.
