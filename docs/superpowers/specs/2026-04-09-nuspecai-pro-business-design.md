# NuSpec.AI.Pro — Business Site Design

**Date:** 2026-04-09
**Status:** Approved
**Domain:** nuspec.ai

---

## Overview

Build a self-serve marketing and purchase site for NuSpec.AI.Pro at `nuspec.ai`. A visitor lands, understands the product, buys with a credit card, and receives a working license key by email — with no manual step in between. Stripe handles payment, tax, invoicing, and renewal. A Vercel serverless webhook handler signs and delivers the JWT license key automatically.

---

## Business Model

| Plan | Price | License term |
|------|-------|-------------|
| Annual | $99/year | JWT `exp = now + 1 year`; renewed annually via Stripe |
| Lifetime | $279 one-time | JWT `exp = now + 100 years` (effectively perpetual) |

**Licensing scope:** Per organization. One key covers all packages (`packages: "*"`), all developers, all CI environments. No seat counting.

**Renewal (annual):** Stripe fires `invoice.paid` each year → webhook issues a fresh JWT, updates the existing `licenses` row (`license_key`, `expires_at`), and emails the new key to the customer. The outgoing key remains valid until its natural expiry, giving customers a grace window.

---

## System Architecture

### Components

```
nuspec.ai (Next.js / Vercel)
├── / ........................ Marketing homepage
├── /docs .................... Getting started + format reference
├── /license ................. Email-based license key lookup
├── /success ................. Post-purchase confirmation page
├── /api/webhook ............. Stripe webhook handler (signs + stores + emails JWT)
└── /api/license ............. Rate-limited license key lookup API

External services
├── Stripe Checkout .......... Hosted payment page (tax, invoices, receipts)
├── Stripe Customer Portal ... Subscription management, billing history
├── Vercel Postgres .......... License storage
└── Resend ................... Transactional email (license delivery, renewals)
```

### Purchase Flow (end-to-end)

1. Visitor clicks "Get Pro" → redirected to **Stripe Checkout** (hosted)
2. Customer completes payment → Stripe fires `checkout.session.completed` webhook
3. `/api/webhook` handler:
   - Verifies Stripe webhook signature
   - Signs an RS256 JWT (`sub=email`, `scope=pro`, `packages=*`, `exp` per plan)
   - Stores record in Vercel Postgres
   - Emails license key via Resend (React email template)
4. Customer redirected to `/success` with next steps
5. License key arrives by email within 60 seconds

### License Storage

**Table: `licenses`**

| Column | Type | Notes |
|--------|------|-------|
| `id` | uuid | Primary key |
| `email` | text | Customer email (indexed) |
| `license_key` | text | Signed JWT |
| `plan` | text | `annual` or `lifetime` |
| `stripe_customer_id` | text | For portal links |
| `stripe_subscription_id` | text | Null for lifetime |
| `created_at` | timestamptz | |
| `expires_at` | timestamptz | 1 year out for annual; 100 years for lifetime |

### Key Security

- Private signing key stored as Vercel environment secret (`NUSPEC_AI_PRIVATE_KEY`)
- The same public key is already embedded in `NuSpec.AI.Tool` — no code changes needed
- Stripe webhook secret stored as `STRIPE_WEBHOOK_SECRET`
- `/api/license` lookup: rate-limited by IP (max 5 requests/minute)

---

## Tech Stack

| Concern | Choice | Rationale |
|---------|--------|-----------|
| Framework | Next.js 14 (App Router) | Same as consulting site; familiar Vercel workflow |
| Hosting | Vercel | Instant deploys, env secrets, serverless functions built-in |
| Payments | Stripe | Industry standard; handles tax, invoices, renewals |
| Database | Vercel Postgres | Zero-config with Vercel; simple schema |
| Email | Resend | Best-in-class Next.js integration; React email templates |
| Styles | Tailwind CSS | Utility-first, fast to build |

---

## Site Pages

### `/` — Homepage

Visual style: dark hero section, light body below (like Stripe/Railway/Resend).

Section order (B — Hook → Understand → Trust → Buy):

1. **Hero** — dark background
   - Headline: "Token-optimized AI context for your NuGet packages"
   - Subhead: "Up to 77% fewer tokens. Offline. CI-ready. Per-org licensing."
   - CTAs: `Get Pro — $99/yr` · `View docs`

2. **How it works** — 3 steps
   - Install `NuSpec.AI.Pro` (add PackageReference)
   - Set your license key (env var or MSBuild property)
   - Run `dotnet pack` — Pro formats generated automatically

3. **Format comparison table**
   - Columns: Project profile, Types, Source size, JSON tokens, YAML tokens, Compact tokens, Ultra tokens
   - Data from 42 real production projects
   - Source column included (mirrors the free package readme table)

4. **Token savings stats** — three numbers: 77% fewer tokens (ultra), 42 projects measured, 100% offline validation

5. **Pricing cards**
   - Annual: $99/year — "Per organization · Renews automatically · Cancel anytime"
   - Lifetime: $279 once — "Per organization · Never expires · One payment"
   - Both cards have a "Buy now" button → Stripe Checkout

6. **Final CTA** — "Start shipping smarter packages today" + button

---

### `/docs` — Getting Started

Sections:
- **Install** — free vs Pro PackageReference, PrivateAssets
- **Set your license key** — three methods: MSBuild property, env var (`NUSPEC_AI_LICENSE_KEY`), file (`~/.nuspec-ai/license.key`)
- **Format reference** — what each format looks like, token savings, when to use each
- **Attribute reference** — `[AiRole]`, `[AiIgnore]`, `[AiDescription]` with examples
- **CI/CD setup** — GitHub Actions (add secret), Azure DevOps
- **Coexistence** — what happens when both free and Pro are installed
- **FAQ** — what happens on expiry, offline validation, key scope

---

### `/license` — Key Lookup

- Email input form
- On submit: rate-limited call to `/api/license`
- Response: masked key with copy button, plan, expiry date
- "Manage billing →" → Stripe Customer Portal
- "Didn't get your email?" → link to resend

---

### `/success` — Post-Purchase

- Confirmation message: "You're all set — your license key is on its way"
- Expected arrival: within 60 seconds
- Quick-start snippet showing exactly where to paste the key
- Link to `/docs`

---

## Pending Readme Fix (independent of site build)

The `NUGET_README_PRO.md` token comparison table is missing the `Source` column that the free readme includes. Measure source sizes for the 42 production projects, add the column, and publish as a new NuGet version whenever convenient.

---

## Out of Scope (v1)

- Custom login / authenticated customer portal
- Team seat management
- Volume / enterprise discounts
- API for programmatic license issuance
- License revocation UI (handled via Stripe subscription cancellation → key expires naturally)
