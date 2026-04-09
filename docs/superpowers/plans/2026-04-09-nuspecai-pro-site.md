# NuSpec.AI.Pro — Marketing & License Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and deploy `nuspec.ai` — a self-serve marketing and purchase site that takes payment via Stripe and automatically issues RS256 JWT license keys by email, with no manual steps.

**Architecture:** Next.js 14 (App Router) on Vercel. Stripe Checkout handles payment; a Vercel serverless webhook handler signs and stores the JWT license, then sends it via Resend. Customers retrieve their key anytime at `/license` by email.

**Tech Stack:** Next.js 14, Tailwind CSS, Stripe, Vercel Postgres, Resend, `jose` (JWT), React Email

---

## New Repo

Create at `E:/repos/nuspec-ai-web`. This is a standalone project — not inside the NuSpec.AI tool repo.

---

## File Map

```
nuspec-ai-web/
├── app/
│   ├── layout.tsx                        # Root layout (font, metadata)
│   ├── page.tsx                          # Homepage (assembles home sections)
│   ├── docs/
│   │   └── page.tsx                      # /docs — getting started guide
│   ├── license/
│   │   └── page.tsx                      # /license — email-based key lookup
│   ├── success/
│   │   └── page.tsx                      # /success — post-purchase confirmation
│   └── api/
│       ├── webhook/
│       │   └── route.ts                  # POST — Stripe webhook handler
│       └── license/
│           └── route.ts                  # GET — rate-limited license key lookup
├── components/
│   ├── layout/
│   │   ├── Header.tsx                    # Nav: logo, Docs, Pricing, Buy Pro
│   │   └── Footer.tsx                    # Links + copyright
│   ├── home/
│   │   ├── Hero.tsx                      # Dark hero + CTAs
│   │   ├── HowItWorks.tsx                # 3-step install guide
│   │   ├── FormatComparison.tsx          # Token savings table (9 projects)
│   │   ├── Stats.tsx                     # 77% / 42 projects / 100% offline
│   │   ├── Pricing.tsx                   # Annual $99 vs Lifetime $279 cards
│   │   └── FinalCta.tsx                  # Bottom CTA section
│   └── ui/
│       ├── CopyButton.tsx                # Copy-to-clipboard button
│       └── MaskedKey.tsx                 # Shows masked JWT + copy button
├── emails/
│   ├── LicenseDelivery.tsx               # React Email: initial license delivery
│   └── LicenseRenewal.tsx                # React Email: annual renewal key
├── lib/
│   ├── db.ts                             # Vercel Postgres client + queries
│   ├── licensing.ts                      # issueJwt(), renewJwt()
│   ├── stripe.ts                         # Stripe client + getPortalUrl()
│   └── email.ts                          # sendLicenseDelivery(), sendLicenseRenewal()
├── schema.sql                            # licenses table DDL
├── .env.local.example                    # Documented required env vars
├── next.config.ts
├── tailwind.config.ts
└── package.json
```

---

## Environment Variables

These must be set in Vercel (and in `.env.local` for local dev):

```
NUSPEC_AI_PRIVATE_KEY=        # RS256 private key PEM (the real one, not the test key)
STRIPE_SECRET_KEY=            # sk_live_... or sk_test_...
STRIPE_WEBHOOK_SECRET=        # whsec_... (from Stripe dashboard)
STRIPE_ANNUAL_PRICE_ID=       # price_... for $99/year recurring product
STRIPE_LIFETIME_PRICE_ID=     # price_... for $279 one-time product
POSTGRES_URL=                 # Vercel Postgres connection string
RESEND_API_KEY=               # re_...
RESEND_FROM_EMAIL=            # license@nuspec.ai
NEXT_PUBLIC_SITE_URL=         # https://nuspec.ai (or http://localhost:3000 locally)
NEXT_PUBLIC_STRIPE_ANNUAL_LINK=    # Stripe Checkout payment link (annual)
NEXT_PUBLIC_STRIPE_LIFETIME_LINK=  # Stripe Checkout payment link (lifetime)
```

---

## Task 1: Project Scaffold

**Files:**
- Create: `package.json`, `next.config.ts`, `tailwind.config.ts`, `.env.local.example`

- [ ] **Step 1: Scaffold the Next.js app**

```bash
cd E:/repos
npx create-next-app@latest nuspec-ai-web \
  --typescript \
  --tailwind \
  --eslint \
  --app \
  --no-src-dir \
  --import-alias "@/*"
cd nuspec-ai-web
```

- [ ] **Step 2: Install dependencies**

```bash
npm install \
  stripe \
  @vercel/postgres \
  resend \
  react-email \
  @react-email/components \
  jose \
  @upstash/ratelimit \
  @upstash/redis
```

> Note: `@upstash/ratelimit` + `@upstash/redis` handle IP rate limiting on the `/api/license` route. Add `UPSTASH_REDIS_REST_URL` and `UPSTASH_REDIS_REST_TOKEN` to the env var list above.

- [ ] **Step 3: Create `.env.local.example`**

```bash
cat > .env.local.example << 'EOF'
# JWT signing key (RS256 private key PEM — the production key, not the test key)
NUSPEC_AI_PRIVATE_KEY="-----BEGIN PRIVATE KEY-----
...
-----END PRIVATE KEY-----"

# Stripe
STRIPE_SECRET_KEY=sk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_ANNUAL_PRICE_ID=price_...
STRIPE_LIFETIME_PRICE_ID=price_...

# Stripe Checkout payment links (created in Stripe dashboard)
NEXT_PUBLIC_STRIPE_ANNUAL_LINK=https://buy.stripe.com/...
NEXT_PUBLIC_STRIPE_LIFETIME_LINK=https://buy.stripe.com/...

# Vercel Postgres
POSTGRES_URL=postgres://...

# Upstash Redis (for rate limiting)
UPSTASH_REDIS_REST_URL=https://...
UPSTASH_REDIS_REST_TOKEN=...

# Resend
RESEND_API_KEY=re_...
RESEND_FROM_EMAIL=license@nuspec.ai

# Site
NEXT_PUBLIC_SITE_URL=http://localhost:3000
EOF
```

- [ ] **Step 4: Copy to `.env.local` and fill in test values**

```bash
cp .env.local.example .env.local
# Fill in STRIPE_SECRET_KEY (test key), POSTGRES_URL, RESEND_API_KEY, UPSTASH_*
# Leave NUSPEC_AI_PRIVATE_KEY as the real production key (from your secure store)
```

- [ ] **Step 5: Verify dev server starts**

```bash
npm run dev
```

Expected: server at `http://localhost:3000` with default Next.js page.

- [ ] **Step 6: Init git and commit**

```bash
git init
echo ".env.local" >> .gitignore
echo ".next" >> .gitignore
git add -A
git commit -m "chore: scaffold Next.js app with dependencies"
```

---

## Task 2: Database Schema + Client

**Files:**
- Create: `schema.sql`
- Create: `lib/db.ts`

- [ ] **Step 1: Create `schema.sql`**

```sql
CREATE TABLE IF NOT EXISTS licenses (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email TEXT NOT NULL,
  license_key TEXT NOT NULL,
  plan TEXT NOT NULL CHECK (plan IN ('annual', 'lifetime')),
  stripe_customer_id TEXT NOT NULL,
  stripe_subscription_id TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS licenses_email_idx ON licenses (email);
CREATE INDEX IF NOT EXISTS licenses_stripe_customer_id_idx ON licenses (stripe_customer_id);
```

- [ ] **Step 2: Run migration against Vercel Postgres**

In the Vercel dashboard: create a Postgres database, link it to your project, copy the `POSTGRES_URL` into `.env.local`. Then:

```bash
# Install the Vercel CLI if needed
npm i -g vercel
vercel env pull .env.local   # pulls Vercel env vars including POSTGRES_URL

# Run the schema
node -e "
const { sql } = require('@vercel/postgres');
const fs = require('fs');
const schema = fs.readFileSync('schema.sql', 'utf8');
sql\`\${schema}\`.then(() => { console.log('Schema applied'); process.exit(0); }).catch(e => { console.error(e); process.exit(1); });
"
```

Expected: `Schema applied`

- [ ] **Step 3: Create `lib/db.ts`**

```typescript
import { sql } from '@vercel/postgres'

export interface LicenseRecord {
  id: string
  email: string
  license_key: string
  plan: 'annual' | 'lifetime'
  stripe_customer_id: string
  stripe_subscription_id: string | null
  created_at: Date
  expires_at: Date
}

export async function insertLicense(record: Omit<LicenseRecord, 'id' | 'created_at'>): Promise<LicenseRecord> {
  const { rows } = await sql<LicenseRecord>`
    INSERT INTO licenses (email, license_key, plan, stripe_customer_id, stripe_subscription_id, expires_at)
    VALUES (${record.email}, ${record.license_key}, ${record.plan}, ${record.stripe_customer_id}, ${record.stripe_subscription_id}, ${record.expires_at.toISOString()})
    RETURNING *
  `
  return rows[0]
}

export async function getLicenseByEmail(email: string): Promise<LicenseRecord | null> {
  const { rows } = await sql<LicenseRecord>`
    SELECT * FROM licenses WHERE email = ${email.toLowerCase()} ORDER BY created_at DESC LIMIT 1
  `
  return rows[0] ?? null
}

export async function updateLicenseKey(
  stripeCustomerId: string,
  licenseKey: string,
  expiresAt: Date
): Promise<void> {
  await sql`
    UPDATE licenses
    SET license_key = ${licenseKey}, expires_at = ${expiresAt.toISOString()}
    WHERE stripe_customer_id = ${stripeCustomerId}
  `
}
```

- [ ] **Step 4: Commit**

```bash
git add schema.sql lib/db.ts
git commit -m "feat: add database schema and client"
```

---

## Task 3: JWT License Issuance

**Files:**
- Create: `lib/licensing.ts`
- Create: `lib/licensing.test.ts`

- [ ] **Step 1: Write the failing test**

```typescript
// lib/licensing.test.ts
import { issueJwt } from './licensing'
import { decodeJwt } from 'jose'

// Use a test RSA private key (PKCS#8 format). Generate with:
//   openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 | openssl pkcs8 -topk8 -nocrypt
const TEST_PRIVATE_KEY = `-----BEGIN PRIVATE KEY-----
MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQCuqozsPzwHPSMo
6EcMCcDh36xKn4L3jGNZVcqrg99Q5wqGU77nJlYVjvDqjKhUPzDvtc+Rx7187V6T
ge1yH3el7cR3fmCQNNSftbqafuLvADVQvvW1oOxl1uAF+UvNJGqGkoUYJaggy7yr
hmJs+1tonrN2ZEoIH9+/YO1/9NQ1MXzu/bRPZN0yFRLQ3R0/HbIrccFb/edH3oP/
LrtkQSD1+ktwyqzL7az5MCSRzLuoWeQVHSzGebyE/1EaHoZaflj4odhiPPOnOsB8
/rHe9V96rx8NNBz3b9iMDwHq4WV4dm5N8TGdPF2UTTl2F0GXy8O2NyDaSpELLuhX
cQ6IPqH1AgMBAAECggEAAfYoXv7Wzb4CBxOUuK3jXKYGaVAhSGZrNzWfcQ2qFF6D
375RBoeHr/ZK/ldWDJwpEIgaLKjxl9WSmlV7NSzlSxfAfRcOPpBZUvHXhqSmJ8j4
0E9UsxV7kik3mtmR4FvoVlqO5BaILNYc6FA6Cr9H54TgvxOhQTYabSvJfwZg27gN
yYVfC3HOzptie9sEP+MIIcbIFVtpLG4Z/iSvfsaeH9iefaSgpEmm/05EL00OGE0+
GNk8Id7mO4NQQoNu7orSV13LlNQAnyZ2zxg67Az1lGkAUHQKw7+uUyLUw31PXxZz
WCSBuGfUTt56Jy0iyZJueIf14+gBePcBrfs4IkDygQKBgQDo/2Y8TxtX27oteWq+
9gZlVdScwBnU/VQylkYIMUVkPoQ9Ma5idTxCHEg6oqpYfw4tTSMBXhD7j6sVi/9/
Ki7LcFSXCHxJzplbiFwyc5IfU4almR9dQI/K5Fky3Hazkr23V86Kp+2Y8w5QlJvH
52Du9Z+db1jkipqQmueL8x448wKBgQC/6O2IabrTGyht9CRrMEwpxUvM6JarY9X/
PNjTYvGvVoHDuHXuMbifZjSc3gHm3QEj94ORSL76jdWz9pNlHDj8E2J9p892qaTj
Baok4xTFPemdHxZphrhgJ07jHmQidGRj+PhD27mmeVIITkBbh6VAPlrYVWo83D0p
etVLzy9zdwKBgCtl/vX2yiIIRFpaBj8BdlmDrjFwOp+IfBlcEjlObB1q45i+Wzvt
mEa8G9wIFnCbYdmgR4fmrIUe0oAV7oYSJlswViE3rGbW+4uoD3w6OJprJWZM6iGl
d+MTu2WU2OtDxuCSk18SPlhB1YW+2HFYsJ5x08QwTD9tbbLHl59irltvAoGARVCl
UssVfqBlhulSqiCEseWgDj/IA9mIdqsMibVIJBNzxTR/6+urim9I+4u4ViFnAw2o
SLZkvGy0Tk72R+PctTdvMIGHDo4RjyoBnVcjrmZBVc3fs3fEan5oIOJeOo+dnvpS
+XeIY5eYSIWy+xxQVJbxCwg22gqWUMAcAEiyE9sCgYA6e99ZQyav6AANa5fiQ4tl
oglZKrUb8U+75lHnOxD8t0rSRBGUwaVln4UJhwlJvFMis5lT6JcWGEQiiF0HmLsK
Bi8vymd7Y9wgK2w9PsyvxHHNCmtV5AjNMjfVVujyz8W+qi0wipD4AVDMmdzHKvS9
YNJsz8wiPoIx5fucnD4k8w==
-----END PRIVATE KEY-----`

// This is the matching public key for the test private key above.
// It matches the public key embedded in NuSpec.AI.Tool.
// In production, NUSPEC_AI_PRIVATE_KEY must be the real private key
// whose public half is embedded in the shipped tool binary.

beforeEach(() => {
  process.env.NUSPEC_AI_PRIVATE_KEY = TEST_PRIVATE_KEY
})

test('issueJwt annual: has correct claims and 1-year expiry', async () => {
  const token = await issueJwt({ email: 'org@example.com', plan: 'annual' })
  const claims = decodeJwt(token)

  expect(claims.sub).toBe('org@example.com')
  expect(claims.scope).toBe('pro')
  expect(claims.packages).toBe('*')
  expect(typeof claims.exp).toBe('number')
  expect(typeof claims.iat).toBe('number')

  // exp should be approximately 1 year from now (within 5 seconds)
  const expectedExp = Math.floor(Date.now() / 1000) + 365 * 24 * 60 * 60
  expect(claims.exp).toBeGreaterThan(expectedExp - 5)
  expect(claims.exp).toBeLessThan(expectedExp + 5)
})

test('issueJwt lifetime: exp is ~100 years out', async () => {
  const token = await issueJwt({ email: 'org@example.com', plan: 'lifetime' })
  const claims = decodeJwt(token)

  const hundredYearsFromNow = Math.floor(Date.now() / 1000) + 100 * 365 * 24 * 60 * 60
  expect(claims.exp).toBeGreaterThan(hundredYearsFromNow - 60)
})

test('issueJwt: token is a valid JWT string', async () => {
  const token = await issueJwt({ email: 'x@y.com', plan: 'annual' })
  expect(token.split('.').length).toBe(3)
})
```

- [ ] **Step 2: Install Jest and run to see it fail**

```bash
npm install -D jest @types/jest ts-jest
npx ts-jest config:init
npx jest lib/licensing.test.ts
```

Expected: FAIL — `Cannot find module './licensing'`

- [ ] **Step 3: Create `lib/licensing.ts`**

```typescript
import { SignJWT, importPKCS8 } from 'jose'

export interface IssueJwtOptions {
  email: string
  plan: 'annual' | 'lifetime'
}

export async function issueJwt({ email, plan }: IssueJwtOptions): Promise<string> {
  const pem = process.env.NUSPEC_AI_PRIVATE_KEY
  if (!pem) throw new Error('NUSPEC_AI_PRIVATE_KEY is not set')

  const privateKey = await importPKCS8(pem, 'RS256')
  const expiresIn = plan === 'lifetime' ? '36500d' : '365d'

  return new SignJWT({ scope: 'pro', packages: '*' })
    .setProtectedHeader({ alg: 'RS256' })
    .setSubject(email.toLowerCase())
    .setIssuedAt()
    .setExpirationTime(expiresIn)
    .sign(privateKey)
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
npx jest lib/licensing.test.ts
```

Expected: PASS — 3 tests

- [ ] **Step 5: Commit**

```bash
git add lib/licensing.ts lib/licensing.test.ts
git commit -m "feat: JWT license issuance with RS256"
```

---

## Task 4: Stripe Client

**Files:**
- Create: `lib/stripe.ts`

- [ ] **Step 1: Create `lib/stripe.ts`**

```typescript
import Stripe from 'stripe'

export const stripe = new Stripe(process.env.STRIPE_SECRET_KEY!, {
  apiVersion: '2024-06-20',
})

/** Resolves the Stripe Customer Portal URL for a given customer. */
export async function getPortalUrl(stripeCustomerId: string): Promise<string> {
  const session = await stripe.billingPortal.sessions.create({
    customer: stripeCustomerId,
    return_url: `${process.env.NEXT_PUBLIC_SITE_URL}/license`,
  })
  return session.url
}

/** Determines plan from a completed Stripe checkout session. */
export function getPlanFromSession(
  session: Stripe.Checkout.Session
): 'annual' | 'lifetime' {
  const priceId = session.line_items?.data[0]?.price?.id
  if (priceId === process.env.STRIPE_LIFETIME_PRICE_ID) return 'lifetime'
  return 'annual'
}

/** Calculates expires_at date for a given plan. */
export function getExpiresAt(plan: 'annual' | 'lifetime'): Date {
  const d = new Date()
  if (plan === 'lifetime') {
    d.setFullYear(d.getFullYear() + 100)
  } else {
    d.setFullYear(d.getFullYear() + 1)
  }
  return d
}
```

- [ ] **Step 2: Commit**

```bash
git add lib/stripe.ts
git commit -m "feat: Stripe client and helpers"
```

---

## Task 5: Email Templates + Sender

**Files:**
- Create: `emails/LicenseDelivery.tsx`
- Create: `emails/LicenseRenewal.tsx`
- Create: `lib/email.ts`

- [ ] **Step 1: Create `emails/LicenseDelivery.tsx`**

```tsx
import {
  Body, Container, Head, Heading, Hr, Html, Link,
  Preview, Section, Text, Code
} from '@react-email/components'

interface LicenseDeliveryProps {
  email: string
  licenseKey: string
  plan: 'annual' | 'lifetime'
  expiresAt: Date
  siteUrl: string
}

export function LicenseDelivery({ email, licenseKey, plan, expiresAt, siteUrl }: LicenseDeliveryProps) {
  const planLabel = plan === 'lifetime' ? 'Lifetime' : 'Annual'
  const expiryStr = plan === 'lifetime'
    ? 'Never expires'
    : `Renews ${expiresAt.toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}`

  return (
    <Html>
      <Head />
      <Preview>Your NuSpec.AI.Pro license key is ready</Preview>
      <Body style={{ fontFamily: 'system-ui, sans-serif', background: '#f9fafb', padding: '40px 0' }}>
        <Container style={{ background: '#fff', borderRadius: 8, padding: '40px', maxWidth: 560 }}>
          <Heading style={{ color: '#111', fontSize: 24 }}>Your NuSpec.AI.Pro license</Heading>
          <Text>Thanks for your purchase. Here&apos;s your <strong>{planLabel}</strong> license key:</Text>

          <Section style={{ background: '#f3f4f6', borderRadius: 6, padding: '16px', margin: '24px 0' }}>
            <Code style={{ fontSize: 11, wordBreak: 'break-all', color: '#374151' }}>
              {licenseKey}
            </Code>
          </Section>

          <Text style={{ fontSize: 13, color: '#6b7280' }}>{expiryStr}</Text>

          <Hr />

          <Heading as="h2" style={{ fontSize: 16, color: '#111' }}>Quick setup</Heading>

          <Text>Add to your <code>.csproj</code>:</Text>
          <Section style={{ background: '#1a1d27', borderRadius: 6, padding: '14px', margin: '8px 0' }}>
            <Code style={{ color: '#a8b4c8', fontSize: 12 }}>
              {`<PropertyGroup>\n  <NuSpecAiLicenseKey>YOUR_KEY_HERE</NuSpecAiLicenseKey>\n  <NuSpecAiFormats>ultra</NuSpecAiFormats>\n</PropertyGroup>`}
            </Code>
          </Section>

          <Text>Or set the <code>NUSPEC_AI_LICENSE_KEY</code> environment variable in CI.</Text>

          <Text>
            <Link href={`${siteUrl}/docs`}>Read the full setup guide →</Link>
          </Text>
          <Text>
            <Link href={`${siteUrl}/license`}>Look up your key any time →</Link>
          </Text>
        </Container>
      </Body>
    </Html>
  )
}
```

- [ ] **Step 2: Create `emails/LicenseRenewal.tsx`**

```tsx
import {
  Body, Container, Head, Heading, Hr, Html, Link,
  Preview, Section, Text, Code
} from '@react-email/components'

interface LicenseRenewalProps {
  email: string
  licenseKey: string
  expiresAt: Date
  siteUrl: string
}

export function LicenseRenewal({ licenseKey, expiresAt, siteUrl }: LicenseRenewalProps) {
  const expiryStr = expiresAt.toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })

  return (
    <Html>
      <Head />
      <Preview>Your NuSpec.AI.Pro license has been renewed</Preview>
      <Body style={{ fontFamily: 'system-ui, sans-serif', background: '#f9fafb', padding: '40px 0' }}>
        <Container style={{ background: '#fff', borderRadius: 8, padding: '40px', maxWidth: 560 }}>
          <Heading style={{ color: '#111', fontSize: 24 }}>License renewed</Heading>
          <Text>Your NuSpec.AI.Pro subscription has been renewed. Here&apos;s your updated license key (valid until {expiryStr}):</Text>

          <Section style={{ background: '#f3f4f6', borderRadius: 6, padding: '16px', margin: '24px 0' }}>
            <Code style={{ fontSize: 11, wordBreak: 'break-all', color: '#374151' }}>
              {licenseKey}
            </Code>
          </Section>

          <Text style={{ fontSize: 13, color: '#6b7280' }}>
            Replace your previous key with this one. Your old key remains valid until {expiryStr}.
          </Text>
          <Hr />
          <Text>
            <Link href={`${siteUrl}/license`}>View your key any time →</Link>
          </Text>
        </Container>
      </Body>
    </Html>
  )
}
```

- [ ] **Step 3: Create `lib/email.ts`**

```typescript
import { Resend } from 'resend'
import { LicenseDelivery } from '@/emails/LicenseDelivery'
import { LicenseRenewal } from '@/emails/LicenseRenewal'

const resend = new Resend(process.env.RESEND_API_KEY)
const FROM = process.env.RESEND_FROM_EMAIL ?? 'license@nuspec.ai'
const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? 'https://nuspec.ai'

export async function sendLicenseDelivery(params: {
  email: string
  licenseKey: string
  plan: 'annual' | 'lifetime'
  expiresAt: Date
}): Promise<void> {
  await resend.emails.send({
    from: FROM,
    to: params.email,
    subject: 'Your NuSpec.AI.Pro license key',
    react: LicenseDelivery({ ...params, siteUrl: SITE_URL }),
  })
}

export async function sendLicenseRenewal(params: {
  email: string
  licenseKey: string
  expiresAt: Date
}): Promise<void> {
  await resend.emails.send({
    from: FROM,
    to: params.email,
    subject: 'Your NuSpec.AI.Pro license has been renewed',
    react: LicenseRenewal({ ...params, siteUrl: SITE_URL }),
  })
}
```

- [ ] **Step 4: Commit**

```bash
git add emails/ lib/email.ts
git commit -m "feat: React Email templates and Resend sender"
```

---

## Task 6: Stripe Webhook Handler

**Files:**
- Create: `app/api/webhook/route.ts`

The webhook handles two events:
- `checkout.session.completed` — initial purchase (annual or lifetime)
- `invoice.paid` — annual renewal

- [ ] **Step 1: Create `app/api/webhook/route.ts`**

```typescript
import { NextRequest, NextResponse } from 'next/server'
import Stripe from 'stripe'
import { stripe, getPlanFromSession, getExpiresAt } from '@/lib/stripe'
import { issueJwt } from '@/lib/licensing'
import { insertLicense, updateLicenseKey, getLicenseByEmail } from '@/lib/db'
import { sendLicenseDelivery, sendLicenseRenewal } from '@/lib/email'

export async function POST(req: NextRequest) {
  const body = await req.text()
  const sig = req.headers.get('stripe-signature')

  if (!sig) return NextResponse.json({ error: 'Missing signature' }, { status: 400 })

  let event: Stripe.Event
  try {
    event = stripe.webhooks.constructEvent(body, sig, process.env.STRIPE_WEBHOOK_SECRET!)
  } catch {
    return NextResponse.json({ error: 'Invalid signature' }, { status: 400 })
  }

  try {
    if (event.type === 'checkout.session.completed') {
      const session = event.data.object as Stripe.Checkout.Session

      // Fetch line items to determine plan
      const sessionWithItems = await stripe.checkout.sessions.retrieve(session.id, {
        expand: ['line_items'],
      })

      const email = session.customer_details?.email
      if (!email) throw new Error('No email in session')

      const plan = getPlanFromSession(sessionWithItems)
      const expiresAt = getExpiresAt(plan)
      const licenseKey = await issueJwt({ email, plan })

      await insertLicense({
        email: email.toLowerCase(),
        license_key: licenseKey,
        plan,
        stripe_customer_id: session.customer as string,
        stripe_subscription_id: plan === 'annual' ? session.subscription as string : null,
        expires_at: expiresAt,
      })

      await sendLicenseDelivery({ email, licenseKey, plan, expiresAt })
    }

    if (event.type === 'invoice.paid') {
      const invoice = event.data.object as Stripe.Invoice
      const customerId = invoice.customer as string

      // Only process subscription renewals (not initial invoice — already handled above)
      if (!invoice.subscription || invoice.billing_reason !== 'subscription_cycle') {
        return NextResponse.json({ received: true })
      }

      // Look up existing license to get email
      const customer = await stripe.customers.retrieve(customerId)
      if (customer.deleted) throw new Error('Customer deleted')
      const email = customer.email
      if (!email) throw new Error('No email on customer')

      const expiresAt = getExpiresAt('annual')
      const licenseKey = await issueJwt({ email, plan: 'annual' })

      await updateLicenseKey(customerId, licenseKey, expiresAt)
      await sendLicenseRenewal({ email, licenseKey, expiresAt })
    }
  } catch (err) {
    console.error('Webhook error:', err)
    return NextResponse.json({ error: 'Internal error' }, { status: 500 })
  }

  return NextResponse.json({ received: true })
}

// Required: disable body parsing so we get the raw body for signature verification
export const config = { api: { bodyParser: false } }
```

- [ ] **Step 2: Test webhook locally with Stripe CLI**

```bash
# Install Stripe CLI if not already installed: https://stripe.com/docs/stripe-cli
stripe login
stripe listen --forward-to localhost:3000/api/webhook

# In another terminal, trigger a test event:
stripe trigger checkout.session.completed
```

Expected: webhook receives event, logs no errors (license won't send without real session data, but handler should not crash).

- [ ] **Step 3: Commit**

```bash
git add app/api/webhook/
git commit -m "feat: Stripe webhook handler for license issuance and renewal"
```

---

## Task 7: License Lookup API

**Files:**
- Create: `app/api/license/route.ts`

- [ ] **Step 1: Create `app/api/license/route.ts`**

```typescript
import { NextRequest, NextResponse } from 'next/server'
import { Ratelimit } from '@upstash/ratelimit'
import { Redis } from '@upstash/redis'
import { getLicenseByEmail } from '@/lib/db'

const ratelimit = new Ratelimit({
  redis: Redis.fromEnv(),
  limiter: Ratelimit.slidingWindow(5, '1 m'),
})

export async function GET(req: NextRequest) {
  const email = req.nextUrl.searchParams.get('email')?.toLowerCase().trim()
  if (!email || !email.includes('@')) {
    return NextResponse.json({ error: 'Valid email required' }, { status: 400 })
  }

  // Rate limit by IP
  const ip = req.headers.get('x-forwarded-for') ?? '127.0.0.1'
  const { success } = await ratelimit.limit(ip)
  if (!success) {
    return NextResponse.json({ error: 'Too many requests. Try again in a minute.' }, { status: 429 })
  }

  const record = await getLicenseByEmail(email)
  if (!record) {
    return NextResponse.json({ error: 'No license found for that email' }, { status: 404 })
  }

  return NextResponse.json({
    email: record.email,
    plan: record.plan,
    license_key: record.license_key,
    expires_at: record.expires_at,
    stripe_customer_id: record.stripe_customer_id,
  })
}
```

- [ ] **Step 2: Verify locally**

```bash
curl "http://localhost:3000/api/license?email=test@example.com"
```

Expected: `{"error":"No license found for that email"}` (404) — no records in DB yet.

- [ ] **Step 3: Commit**

```bash
git add app/api/license/
git commit -m "feat: rate-limited license key lookup API"
```

---

## Task 8: Layout (Header + Footer + Root)

**Files:**
- Create: `components/layout/Header.tsx`
- Create: `components/layout/Footer.tsx`
- Modify: `app/layout.tsx`

- [ ] **Step 1: Create `components/layout/Header.tsx`**

```tsx
import Link from 'next/link'

export function Header() {
  const annualLink = process.env.NEXT_PUBLIC_STRIPE_ANNUAL_LINK ?? '#'

  return (
    <header className="fixed top-0 left-0 right-0 z-50 bg-[#0f1117]/80 backdrop-blur-sm border-b border-white/10">
      <div className="max-w-5xl mx-auto px-6 h-14 flex items-center justify-between">
        <Link href="/" className="font-bold text-white tracking-tight">
          NuSpec.AI <span className="text-violet-400">Pro</span>
        </Link>
        <nav className="flex items-center gap-6 text-sm text-gray-400">
          <Link href="/docs" className="hover:text-white transition-colors">Docs</Link>
          <Link href="/#pricing" className="hover:text-white transition-colors">Pricing</Link>
          <Link href="/license" className="hover:text-white transition-colors">My License</Link>
          <a
            href={annualLink}
            className="bg-violet-600 hover:bg-violet-500 text-white px-4 py-1.5 rounded-md transition-colors font-medium"
          >
            Buy Pro →
          </a>
        </nav>
      </div>
    </header>
  )
}
```

- [ ] **Step 2: Create `components/layout/Footer.tsx`**

```tsx
import Link from 'next/link'

export function Footer() {
  return (
    <footer className="border-t border-gray-200 mt-24 py-12 text-center text-sm text-gray-500">
      <div className="max-w-5xl mx-auto px-6 flex flex-col sm:flex-row justify-between items-center gap-4">
        <span>© {new Date().getFullYear()} NuSpec.AI. All rights reserved.</span>
        <div className="flex gap-6">
          <Link href="/docs" className="hover:text-gray-800 transition-colors">Docs</Link>
          <Link href="/license" className="hover:text-gray-800 transition-colors">My License</Link>
          <a href="https://github.com/sean-m-cooper/NuSpec.AI" className="hover:text-gray-800 transition-colors">GitHub</a>
          <a href="https://www.nuget.org/packages/NuSpec.AI.Pro" className="hover:text-gray-800 transition-colors">NuGet</a>
        </div>
      </div>
    </footer>
  )
}
```

- [ ] **Step 3: Update `app/layout.tsx`**

```tsx
import type { Metadata } from 'next'
import { Geist, Geist_Mono } from 'next/font/google'
import { Header } from '@/components/layout/Header'
import { Footer } from '@/components/layout/Footer'
import './globals.css'

const geist = Geist({ subsets: ['latin'], variable: '--font-geist' })
const geistMono = Geist_Mono({ subsets: ['latin'], variable: '--font-geist-mono' })

export const metadata: Metadata = {
  title: 'NuSpec.AI.Pro — Token-optimized AI context for NuGet packages',
  description:
    'Reduce AI context tokens by up to 77% at dotnet pack time. Offline, CI-ready, per-org licensing.',
  openGraph: {
    title: 'NuSpec.AI.Pro',
    description: 'Token-optimized AI context for your NuGet packages.',
    url: 'https://nuspec.ai',
    siteName: 'NuSpec.AI',
  },
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${geist.variable} ${geistMono.variable}`}>
      <body className="font-sans antialiased bg-white text-gray-900">
        <Header />
        <main className="pt-14">{children}</main>
        <Footer />
      </body>
    </html>
  )
}
```

- [ ] **Step 4: Verify dev server renders header and footer**

```bash
npm run dev
```

Open http://localhost:3000 — should see nav bar and footer around the default placeholder content.

- [ ] **Step 5: Commit**

```bash
git add components/layout/ app/layout.tsx
git commit -m "feat: header, footer, and root layout"
```

---

## Task 9: Homepage — Hero + How It Works

**Files:**
- Create: `components/home/Hero.tsx`
- Create: `components/home/HowItWorks.tsx`

- [ ] **Step 1: Create `components/home/Hero.tsx`**

```tsx
export function Hero() {
  const annualLink = process.env.NEXT_PUBLIC_STRIPE_ANNUAL_LINK ?? '#'

  return (
    <section className="bg-[#0f1117] pt-24 pb-20 px-6">
      <div className="max-w-3xl mx-auto">
        <div className="text-violet-400 text-xs font-semibold tracking-widest uppercase mb-4">
          Developer Tool · .NET · Offline
        </div>
        <h1 className="text-4xl sm:text-5xl font-bold text-white leading-tight tracking-tight mb-5">
          Token-optimized AI context<br />for your NuGet packages
        </h1>
        <p className="text-gray-400 text-lg mb-8 max-w-xl">
          Up to 77% fewer tokens. Offline license validation. CI-ready.
          Per-org pricing — one key for your whole team.
        </p>
        <div className="flex flex-wrap gap-3">
          <a
            href={annualLink}
            className="bg-violet-600 hover:bg-violet-500 text-white font-semibold px-6 py-3 rounded-lg transition-colors"
          >
            Get Pro — $99/yr
          </a>
          <a
            href="/docs"
            className="border border-white/20 text-gray-300 hover:text-white px-6 py-3 rounded-lg transition-colors"
          >
            View docs
          </a>
        </div>
      </div>
    </section>
  )
}
```

- [ ] **Step 2: Create `components/home/HowItWorks.tsx`**

```tsx
const steps = [
  {
    number: '01',
    title: 'Install NuSpec.AI.Pro',
    code: `<PackageReference
  Include="NuSpec.AI.Pro"
  Version="1.0.3"
  PrivateAssets="all" />`,
  },
  {
    number: '02',
    title: 'Set your license key',
    code: `# In CI (recommended):
export NUSPEC_AI_LICENSE_KEY=<your-key>

# Or in .csproj:
<NuSpecAiLicenseKey>$(NUSPEC_AI_LICENSE_KEY)</NuSpecAiLicenseKey>
<NuSpecAiFormats>ultra</NuSpecAiFormats>`,
  },
  {
    number: '03',
    title: 'Run dotnet pack',
    code: `dotnet pack
# → ai/package-map.ultra generated
# → 77% fewer tokens than standard JSON`,
  },
]

export function HowItWorks() {
  return (
    <section className="py-20 px-6 bg-white">
      <div className="max-w-5xl mx-auto">
        <h2 className="text-3xl font-bold text-gray-900 text-center mb-3">How it works</h2>
        <p className="text-gray-500 text-center mb-14">Three steps. No server. No network calls at pack time.</p>
        <div className="grid sm:grid-cols-3 gap-8">
          {steps.map((step) => (
            <div key={step.number}>
              <div className="text-violet-500 font-bold text-sm mb-2">{step.number}</div>
              <h3 className="font-semibold text-gray-900 text-lg mb-3">{step.title}</h3>
              <pre className="bg-gray-950 text-gray-300 rounded-lg p-4 text-xs overflow-x-auto font-mono leading-relaxed">
                {step.code}
              </pre>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
```

- [ ] **Step 3: Commit**

```bash
git add components/home/Hero.tsx components/home/HowItWorks.tsx
git commit -m "feat: homepage hero and how-it-works sections"
```

---

## Task 10: Homepage — Format Comparison + Stats

**Files:**
- Create: `components/home/FormatComparison.tsx`
- Create: `components/home/Stats.tsx`

> **Note on source sizes:** The `Source` column below currently shows placeholder sizes. Before launch, measure actual source sizes for these projects using `find <dir> -name "*.cs" | xargs wc -c` and update the data.

- [ ] **Step 1: Create `components/home/FormatComparison.tsx`**

```tsx
const projects = [
  { profile: 'Models library', types: 723, source: '527 KB', json: '215,221', yaml: '140,003 (−35%)', compact: '110,848 (−48%)', ultra: '32,154 (−85%)' },
  { profile: 'Large shared library', types: 673, source: '1,061 KB', json: '225,503', yaml: '149,950 (−34%)', compact: '122,876 (−46%)', ultra: '44,468 (−80%)' },
  { profile: 'Worker service', types: 131, source: '~180 KB', json: '45,684', yaml: '32,490 (−29%)', compact: '28,652 (−37%)', ultra: '13,866 (−70%)' },
  { profile: 'Providers library', types: 116, source: '510 KB', json: '73,632', yaml: '53,987 (−27%)', compact: '49,229 (−33%)', ultra: '28,364 (−61%)' },
  { profile: 'DAL / repositories', types: 73, source: '~120 KB', json: '30,746', yaml: '19,887 (−35%)', compact: '16,969 (−45%)', ultra: '6,795 (−78%)' },
  { profile: 'Common services', types: 44, source: '~200 KB', json: '30,571', yaml: '23,088 (−24%)', compact: '21,122 (−31%)', ultra: '13,076 (−57%)' },
  { profile: 'Web API surface', types: 50, source: '~150 KB', json: '15,163', yaml: '10,228 (−33%)', compact: '8,447 (−44%)', ultra: '3,283 (−78%)' },
  { profile: 'Azure Functions', types: 16, source: '95 KB', json: '6,677', yaml: '4,926 (−26%)', compact: '4,540 (−32%)', ultra: '2,367 (−65%)' },
  { profile: 'Exception types', types: 8, source: '~20 KB', json: '3,504', yaml: '2,433 (−31%)', compact: '2,009 (−43%)', ultra: '679 (−81%)' },
]

export function FormatComparison() {
  return (
    <section className="py-20 px-6 bg-gray-50">
      <div className="max-w-5xl mx-auto">
        <h2 className="text-3xl font-bold text-gray-900 text-center mb-3">Real token savings</h2>
        <p className="text-gray-500 text-center mb-12">
          Measured across 42 production projects. Token counts approximate (chars ÷ 4).
        </p>
        <div className="overflow-x-auto rounded-xl border border-gray-200 shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-gray-100 text-gray-600 text-xs uppercase">
              <tr>
                <th className="px-4 py-3 text-left">Project profile</th>
                <th className="px-4 py-3 text-right">Types</th>
                <th className="px-4 py-3 text-right">Source</th>
                <th className="px-4 py-3 text-right">JSON</th>
                <th className="px-4 py-3 text-right">YAML</th>
                <th className="px-4 py-3 text-right">Compact</th>
                <th className="px-4 py-3 text-right font-bold text-violet-700">Ultra</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {projects.map((p) => (
                <tr key={p.profile} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-gray-800">{p.profile}</td>
                  <td className="px-4 py-3 text-right text-gray-500">{p.types}</td>
                  <td className="px-4 py-3 text-right text-gray-500">{p.source}</td>
                  <td className="px-4 py-3 text-right text-gray-600">{p.json}</td>
                  <td className="px-4 py-3 text-right text-gray-600">{p.yaml}</td>
                  <td className="px-4 py-3 text-right text-gray-600">{p.compact}</td>
                  <td className="px-4 py-3 text-right font-semibold text-violet-700">{p.ultra}</td>
                </tr>
              ))}
              <tr className="bg-gray-50 font-semibold">
                <td className="px-4 py-3 text-gray-800">All 42 projects</td>
                <td className="px-4 py-3 text-right text-gray-500">—</td>
                <td className="px-4 py-3 text-right text-gray-500">—</td>
                <td className="px-4 py-3 text-right">789,131</td>
                <td className="px-4 py-3 text-right">536,463 (−32%)</td>
                <td className="px-4 py-3 text-right">449,049 (−43%)</td>
                <td className="px-4 py-3 text-right text-violet-700">184,630 (−77%)</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>
  )
}
```

- [ ] **Step 2: Create `components/home/Stats.tsx`**

```tsx
const stats = [
  { value: '77%', label: 'fewer tokens (ultra format)' },
  { value: '42', label: 'production projects measured' },
  { value: '100%', label: 'offline — no network calls at pack time' },
]

export function Stats() {
  return (
    <section className="py-16 px-6 bg-white border-t border-b border-gray-100">
      <div className="max-w-3xl mx-auto grid sm:grid-cols-3 gap-10 text-center">
        {stats.map((s) => (
          <div key={s.value}>
            <div className="text-4xl font-bold text-violet-600 mb-1">{s.value}</div>
            <div className="text-gray-500 text-sm">{s.label}</div>
          </div>
        ))}
      </div>
    </section>
  )
}
```

- [ ] **Step 3: Commit**

```bash
git add components/home/FormatComparison.tsx components/home/Stats.tsx
git commit -m "feat: format comparison table and stats sections"
```

---

## Task 11: Homepage — Pricing + Final CTA + Assemble Page

**Files:**
- Create: `components/home/Pricing.tsx`
- Create: `components/home/FinalCta.tsx`
- Modify: `app/page.tsx`

- [ ] **Step 1: Create `components/home/Pricing.tsx`**

```tsx
export function Pricing() {
  const annualLink = process.env.NEXT_PUBLIC_STRIPE_ANNUAL_LINK ?? '#'
  const lifetimeLink = process.env.NEXT_PUBLIC_STRIPE_LIFETIME_LINK ?? '#'

  return (
    <section id="pricing" className="py-24 px-6 bg-gray-50">
      <div className="max-w-4xl mx-auto">
        <h2 className="text-3xl font-bold text-gray-900 text-center mb-3">Simple, per-org pricing</h2>
        <p className="text-gray-500 text-center mb-14">
          One key covers your whole team, all packages, all CI environments.
        </p>
        <div className="grid sm:grid-cols-2 gap-8">
          {/* Annual */}
          <div className="bg-white rounded-2xl border border-gray-200 p-8 shadow-sm">
            <div className="text-gray-500 text-sm font-medium mb-2">Annual</div>
            <div className="text-4xl font-bold text-gray-900 mb-1">$99</div>
            <div className="text-gray-400 text-sm mb-6">per organization / year</div>
            <ul className="space-y-2 text-sm text-gray-600 mb-8">
              <li>✓ All four output formats</li>
              <li>✓ Per-org license (unlimited devs)</li>
              <li>✓ Renews automatically</li>
              <li>✓ Cancel anytime</li>
              <li>✓ Invoices + receipts via Stripe</li>
            </ul>
            <a
              href={annualLink}
              className="block text-center bg-violet-600 hover:bg-violet-500 text-white font-semibold py-3 rounded-lg transition-colors"
            >
              Get started — $99/yr
            </a>
          </div>

          {/* Lifetime */}
          <div className="bg-[#0f1117] rounded-2xl border border-violet-500/30 p-8 shadow-lg relative overflow-hidden">
            <div className="absolute top-4 right-4 bg-violet-600 text-white text-xs font-bold px-2 py-1 rounded">
              BEST VALUE
            </div>
            <div className="text-violet-400 text-sm font-medium mb-2">Lifetime</div>
            <div className="text-4xl font-bold text-white mb-1">$279</div>
            <div className="text-gray-400 text-sm mb-6">per organization, once</div>
            <ul className="space-y-2 text-sm text-gray-300 mb-8">
              <li>✓ All four output formats</li>
              <li>✓ Per-org license (unlimited devs)</li>
              <li>✓ Never expires</li>
              <li>✓ One payment, forever</li>
              <li>✓ Receipt via Stripe</li>
            </ul>
            <a
              href={lifetimeLink}
              className="block text-center bg-violet-600 hover:bg-violet-500 text-white font-semibold py-3 rounded-lg transition-colors"
            >
              Buy once — $279
            </a>
          </div>
        </div>

        <p className="text-center text-gray-400 text-sm mt-8">
          Not ready to buy?{' '}
          <a href="https://www.nuget.org/packages/NuSpec.AI" className="text-violet-500 hover:underline">
            The free version
          </a>{' '}
          generates standard JSON automatically — no license needed.
        </p>
      </div>
    </section>
  )
}
```

- [ ] **Step 2: Create `components/home/FinalCta.tsx`**

```tsx
export function FinalCta() {
  const annualLink = process.env.NEXT_PUBLIC_STRIPE_ANNUAL_LINK ?? '#'

  return (
    <section className="py-24 px-6 bg-white text-center">
      <div className="max-w-2xl mx-auto">
        <h2 className="text-3xl font-bold text-gray-900 mb-4">
          Start shipping smarter packages today
        </h2>
        <p className="text-gray-500 mb-8">
          Under most enterprise expense policies. No approval needed.
        </p>
        <a
          href={annualLink}
          className="inline-block bg-violet-600 hover:bg-violet-500 text-white font-semibold px-8 py-4 rounded-lg text-lg transition-colors"
        >
          Get NuSpec.AI.Pro — $99/yr
        </a>
      </div>
    </section>
  )
}
```

- [ ] **Step 3: Assemble `app/page.tsx`**

```tsx
import { Hero } from '@/components/home/Hero'
import { HowItWorks } from '@/components/home/HowItWorks'
import { FormatComparison } from '@/components/home/FormatComparison'
import { Stats } from '@/components/home/Stats'
import { Pricing } from '@/components/home/Pricing'
import { FinalCta } from '@/components/home/FinalCta'

export default function HomePage() {
  return (
    <>
      <Hero />
      <HowItWorks />
      <FormatComparison />
      <Stats />
      <Pricing />
      <FinalCta />
    </>
  )
}
```

- [ ] **Step 4: Verify homepage in browser**

```bash
npm run dev
```

Open http://localhost:3000. Verify all six sections render. Check mobile responsiveness.

- [ ] **Step 5: Commit**

```bash
git add components/home/Pricing.tsx components/home/FinalCta.tsx app/page.tsx
git commit -m "feat: pricing, final CTA, and assembled homepage"
```

---

## Task 12: /docs Page

**Files:**
- Create: `app/docs/page.tsx`

- [ ] **Step 1: Create `app/docs/page.tsx`**

```tsx
import { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Docs — NuSpec.AI.Pro',
  description: 'Getting started guide for NuSpec.AI.Pro',
}

export default function DocsPage() {
  return (
    <div className="max-w-3xl mx-auto px-6 py-20">
      <h1 className="text-4xl font-bold text-gray-900 mb-2">Getting Started</h1>
      <p className="text-gray-500 mb-12 text-lg">Everything you need to set up NuSpec.AI.Pro.</p>

      <Section title="Install">
        <p>Add to your packable project (the one you run <code>dotnet pack</code> on):</p>
        <Code>{`<PackageReference Include="NuSpec.AI.Pro" Version="1.0.3" PrivateAssets="all" />

<!-- Optional: attribute support -->
<PackageReference Include="NuSpec.AI.Attributes" Version="1.0.3" />`}</Code>
        <p className="text-sm text-gray-500">NuSpec.AI.Pro is a development dependency — it does not ship as a runtime dependency of your package.</p>
      </Section>

      <Section title="Set your license key">
        <p>Choose one of three ways (highest priority first):</p>
        <h4 className="font-semibold mt-4 mb-2">1. MSBuild property (recommended for CI)</h4>
        <Code>{`<PropertyGroup>
  <NuSpecAiLicenseKey>$(NUSPEC_AI_LICENSE_KEY)</NuSpecAiLicenseKey>
</PropertyGroup>`}</Code>
        <p className="text-sm text-gray-500">Set <code>NUSPEC_AI_LICENSE_KEY</code> as a secret in your CI environment.</p>

        <h4 className="font-semibold mt-4 mb-2">2. Environment variable</h4>
        <Code>{`export NUSPEC_AI_LICENSE_KEY=<your-key>`}</Code>

        <h4 className="font-semibold mt-4 mb-2">3. User file</h4>
        <Code>{`~/.nuspec-ai/license.key`}</Code>
        <p className="text-sm text-gray-500">Paste your key (just the JWT string) into this file.</p>
      </Section>

      <Section title="Choose your formats">
        <Code>{`<PropertyGroup>
  <NuSpecAiFormats>ultra</NuSpecAiFormats>   <!-- 77% fewer tokens -->
  <!-- Options: json | yaml | compact | ultra | all | semicolon-separated -->
</PropertyGroup>`}</Code>
        <table className="w-full text-sm border border-gray-200 rounded-lg overflow-hidden mt-4">
          <thead className="bg-gray-50 text-gray-600 text-xs uppercase">
            <tr>
              <th className="px-4 py-2 text-left">Format</th>
              <th className="px-4 py-2 text-left">File</th>
              <th className="px-4 py-2 text-left">Avg. savings</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            <tr><td className="px-4 py-2">json</td><td className="px-4 py-2 font-mono text-xs">ai/package-map.json</td><td className="px-4 py-2">Baseline</td></tr>
            <tr><td className="px-4 py-2">yaml</td><td className="px-4 py-2 font-mono text-xs">ai/package-map.yaml</td><td className="px-4 py-2">−29%</td></tr>
            <tr><td className="px-4 py-2">compact</td><td className="px-4 py-2 font-mono text-xs">ai/package-map.compact.json</td><td className="px-4 py-2">−40%</td></tr>
            <tr><td className="px-4 py-2 font-semibold">ultra</td><td className="px-4 py-2 font-mono text-xs">ai/package-map.ultra</td><td className="px-4 py-2 font-semibold text-violet-700">−71%</td></tr>
          </tbody>
        </table>
      </Section>

      <Section title="CI/CD setup">
        <h4 className="font-semibold mt-2 mb-2">GitHub Actions</h4>
        <Code>{`# Add to your repo secrets: NUSPEC_AI_LICENSE_KEY
- name: Pack
  env:
    NUSPEC_AI_LICENSE_KEY: \${{ secrets.NUSPEC_AI_LICENSE_KEY }}
  run: dotnet pack`}</Code>

        <h4 className="font-semibold mt-4 mb-2">Azure DevOps</h4>
        <Code>{`# Add to pipeline library: NUSPEC_AI_LICENSE_KEY
- task: DotNetCoreCLI@2
  env:
    NUSPEC_AI_LICENSE_KEY: $(NUSPEC_AI_LICENSE_KEY)
  inputs:
    command: pack`}</Code>
      </Section>

      <Section title="Attribute support">
        <p>Install <code>NuSpec.AI.Attributes</code> to annotate types and members:</p>
        <Code>{`using NuSpec.AI;

[AiRole("aggregate-root", "audited")]
public class Order { }

[AiIgnore]
public string InternalToken { get; set; }

[AiDescription("Do not call for subscription orders.")]
public Task RefundAsync(int orderId) { }`}</Code>
      </Section>

      <Section title="Coexistence with the free package">
        <p>If both <code>NuSpec.AI</code> and <code>NuSpec.AI.Pro</code> are referenced, Pro wins automatically. The free package sits inert. You can safely remove the free package reference.</p>
      </Section>

      <Section title="FAQ">
        <h4 className="font-semibold mt-2 mb-1">What happens when my annual license expires?</h4>
        <p className="text-gray-600 text-sm mb-4">The tool falls back to standard JSON output and emits a <code>NSPECAI001</code> warning. Your build never fails. Renew via the Stripe Customer Portal to get a new key.</p>

        <h4 className="font-semibold mt-2 mb-1">Is the license validated offline?</h4>
        <p className="text-gray-600 text-sm mb-4">Yes — entirely. The license key is a signed JWT verified against a public key embedded in the tool binary. No network calls at pack time, ever.</p>

        <h4 className="font-semibold mt-2 mb-1">Can I use one key for multiple projects and developers?</h4>
        <p className="text-gray-600 text-sm">Yes. Per-org licensing means one key covers all packages, all devs, and all CI environments in your organization.</p>
      </Section>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="mb-14">
      <h2 className="text-2xl font-bold text-gray-900 mb-4 pb-2 border-b border-gray-100">{title}</h2>
      <div className="space-y-4 text-gray-700 leading-relaxed">{children}</div>
    </section>
  )
}

function Code({ children }: { children: React.ReactNode }) {
  return (
    <pre className="bg-gray-950 text-gray-300 rounded-lg p-4 text-xs overflow-x-auto font-mono leading-relaxed my-3">
      {children}
    </pre>
  )
}
```

- [ ] **Step 2: Verify docs page renders**

Open http://localhost:3000/docs — verify all sections render correctly.

- [ ] **Step 3: Commit**

```bash
git add app/docs/
git commit -m "feat: /docs getting started page"
```

---

## Task 13: /license Page

**Files:**
- Create: `components/ui/CopyButton.tsx`
- Create: `components/ui/MaskedKey.tsx`
- Create: `app/license/page.tsx`

- [ ] **Step 1: Create `components/ui/CopyButton.tsx`**

```tsx
'use client'
import { useState } from 'react'

export function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)

  const handleCopy = async () => {
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <button
      onClick={handleCopy}
      className="text-xs bg-gray-100 hover:bg-gray-200 text-gray-600 px-3 py-1.5 rounded-md transition-colors font-medium"
    >
      {copied ? '✓ Copied' : 'Copy'}
    </button>
  )
}
```

- [ ] **Step 2: Create `components/ui/MaskedKey.tsx`**

```tsx
'use client'
import { useState } from 'react'
import { CopyButton } from './CopyButton'

export function MaskedKey({ licenseKey }: { licenseKey: string }) {
  const [revealed, setRevealed] = useState(false)

  const display = revealed
    ? licenseKey
    : licenseKey.slice(0, 20) + '••••••••••••••••••••' + licenseKey.slice(-8)

  return (
    <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
      <div className="flex items-start justify-between gap-3">
        <code className="text-xs text-gray-700 break-all flex-1 font-mono leading-relaxed">
          {display}
        </code>
        <div className="flex gap-2 shrink-0">
          <button
            onClick={() => setRevealed(!revealed)}
            className="text-xs text-gray-400 hover:text-gray-700 transition-colors"
          >
            {revealed ? 'Hide' : 'Show'}
          </button>
          <CopyButton text={licenseKey} />
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 3: Create `app/license/page.tsx`**

```tsx
'use client'
import { useState } from 'react'
import { MaskedKey } from '@/components/ui/MaskedKey'

interface LicenseResult {
  email: string
  plan: string
  license_key: string
  expires_at: string
  stripe_customer_id: string
}

export default function LicensePage() {
  const [email, setEmail] = useState('')
  const [result, setResult] = useState<LicenseResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setResult(null)
    setLoading(true)

    try {
      const res = await fetch(`/api/license?email=${encodeURIComponent(email)}`)
      const data = await res.json()
      if (!res.ok) {
        setError(data.error ?? 'Something went wrong')
      } else {
        setResult(data)
      }
    } catch {
      setError('Network error. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  const portalUrl = result
    ? `/api/portal?customer=${result.stripe_customer_id}`
    : null

  return (
    <div className="max-w-lg mx-auto px-6 py-24">
      <h1 className="text-3xl font-bold text-gray-900 mb-2">Your license key</h1>
      <p className="text-gray-500 mb-10">Enter the email address you used to purchase.</p>

      <form onSubmit={handleLookup} className="space-y-4">
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="you@company.com"
          required
          className="w-full border border-gray-300 rounded-lg px-4 py-3 text-gray-900 focus:outline-none focus:ring-2 focus:ring-violet-500"
        />
        <button
          type="submit"
          disabled={loading}
          className="w-full bg-violet-600 hover:bg-violet-500 disabled:opacity-60 text-white font-semibold py-3 rounded-lg transition-colors"
        >
          {loading ? 'Looking up…' : 'Look up my key'}
        </button>
      </form>

      {error && (
        <div className="mt-6 bg-red-50 border border-red-200 rounded-lg p-4 text-red-700 text-sm">
          {error}
        </div>
      )}

      {result && (
        <div className="mt-8 space-y-4">
          <div className="flex justify-between text-sm text-gray-500">
            <span>Plan: <strong className="text-gray-800 capitalize">{result.plan}</strong></span>
            <span>
              {result.plan === 'lifetime'
                ? 'Never expires'
                : `Expires ${new Date(result.expires_at).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}`}
            </span>
          </div>
          <MaskedKey licenseKey={result.license_key} />
          {result.plan === 'annual' && (
            <a
              href={`/api/portal?customer=${result.stripe_customer_id}`}
              className="block text-center text-sm text-violet-600 hover:underline mt-2"
            >
              Manage billing & subscription →
            </a>
          )}
          <p className="text-xs text-gray-400 text-center">
            Lost your key?{' '}
            <a href="mailto:support@nuspec.ai" className="text-violet-500 hover:underline">
              Contact support
            </a>
          </p>
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 4: Add `/api/portal` redirect route**

Create `app/api/portal/route.ts`:

```typescript
import { NextRequest, NextResponse } from 'next/server'
import { getPortalUrl } from '@/lib/stripe'

export async function GET(req: NextRequest) {
  const customerId = req.nextUrl.searchParams.get('customer')
  if (!customerId) return NextResponse.json({ error: 'Missing customer' }, { status: 400 })

  try {
    const url = await getPortalUrl(customerId)
    return NextResponse.redirect(url)
  } catch {
    return NextResponse.json({ error: 'Could not open portal' }, { status: 500 })
  }
}
```

- [ ] **Step 5: Verify /license page renders**

Open http://localhost:3000/license. Test the form with a fake email — should show "No license found" error.

- [ ] **Step 6: Commit**

```bash
git add components/ui/ app/license/ app/api/portal/
git commit -m "feat: /license page with masked key display and Stripe portal link"
```

---

## Task 14: /success Page

**Files:**
- Create: `app/success/page.tsx`

- [ ] **Step 1: Create `app/success/page.tsx`**

```tsx
import Link from 'next/link'
import { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Purchase complete — NuSpec.AI.Pro',
}

export default function SuccessPage() {
  return (
    <div className="max-w-xl mx-auto px-6 py-24 text-center">
      <div className="text-5xl mb-6">✓</div>
      <h1 className="text-3xl font-bold text-gray-900 mb-3">You&apos;re all set</h1>
      <p className="text-gray-500 mb-2">
        Your license key is on its way to your inbox — usually within 60 seconds.
      </p>
      <p className="text-gray-400 text-sm mb-12">Check your spam folder if it doesn&apos;t arrive.</p>

      <div className="bg-gray-50 border border-gray-200 rounded-xl p-6 text-left mb-10">
        <h2 className="font-semibold text-gray-800 mb-3">Quick setup</h2>
        <pre className="bg-gray-950 text-gray-300 rounded-lg p-4 text-xs font-mono overflow-x-auto leading-relaxed">
{`# 1. Set in CI (recommended):
export NUSPEC_AI_LICENSE_KEY=<your-key>

# 2. Or in your .csproj:
<NuSpecAiLicenseKey>$(NUSPEC_AI_LICENSE_KEY)</NuSpecAiLicenseKey>
<NuSpecAiFormats>ultra</NuSpecAiFormats>

# 3. Run:
dotnet pack`}
        </pre>
      </div>

      <div className="flex flex-col sm:flex-row gap-3 justify-center">
        <Link
          href="/docs"
          className="bg-violet-600 hover:bg-violet-500 text-white font-semibold px-6 py-3 rounded-lg transition-colors"
        >
          Read the docs →
        </Link>
        <Link
          href="/license"
          className="border border-gray-200 text-gray-600 hover:text-gray-900 px-6 py-3 rounded-lg transition-colors"
        >
          Look up my key
        </Link>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Verify /success page**

Open http://localhost:3000/success — verify content and links render.

- [ ] **Step 3: Configure Stripe success URL**

In Stripe dashboard, set the Checkout success URL to:
```
https://nuspec.ai/success
```

- [ ] **Step 4: Commit**

```bash
git add app/success/
git commit -m "feat: /success post-purchase confirmation page"
```

---

## Task 15: Stripe Setup (Manual Steps)

These are dashboard steps — no code to write. Complete before going live.

- [ ] **Step 1: Create Stripe products**

In the [Stripe Dashboard](https://dashboard.stripe.com/products):

1. Create **NuSpec.AI.Pro Annual**
   - Type: Recurring
   - Price: $99.00 USD / year
   - Copy the Price ID → set as `STRIPE_ANNUAL_PRICE_ID` env var

2. Create **NuSpec.AI.Pro Lifetime**
   - Type: One-time
   - Price: $279.00 USD
   - Copy the Price ID → set as `STRIPE_LIFETIME_PRICE_ID` env var

- [ ] **Step 2: Create Stripe Payment Links**

For each product, create a Payment Link in Stripe dashboard:
- Annual: set success URL to `https://nuspec.ai/success`
- Lifetime: set success URL to `https://nuspec.ai/success`

Copy each URL → set as `NEXT_PUBLIC_STRIPE_ANNUAL_LINK` and `NEXT_PUBLIC_STRIPE_LIFETIME_LINK`.

- [ ] **Step 3: Enable Stripe Customer Portal**

In Stripe Dashboard → Settings → Customer Portal:
- Enable "Cancel subscriptions"
- Enable "Update payment methods"
- Set return URL: `https://nuspec.ai/license`

- [ ] **Step 4: Create webhook endpoint**

In Stripe Dashboard → Developers → Webhooks → Add endpoint:
- URL: `https://nuspec.ai/api/webhook`
- Events: `checkout.session.completed`, `invoice.paid`
- Copy signing secret → set as `STRIPE_WEBHOOK_SECRET`

---

## Task 16: Deploy to Vercel

- [ ] **Step 1: Create GitHub repo and push**

```bash
cd E:/repos/nuspec-ai-web
gh repo create nuspec-ai-web --private --push --source .
```

- [ ] **Step 2: Import to Vercel**

Go to [vercel.com/new](https://vercel.com/new), import `nuspec-ai-web`.

- [ ] **Step 3: Add all environment variables**

In Vercel project → Settings → Environment Variables, add every variable from `.env.local.example`. Use the production values (live Stripe keys, real private key, production Postgres URL).

- [ ] **Step 4: Add Vercel Postgres**

In Vercel project → Storage → Create Postgres database. Copy the `POSTGRES_URL` connection string into environment variables.

Run the migration:
```bash
vercel env pull .env.local
node -e "
const { sql } = require('@vercel/postgres');
const fs = require('fs');
sql\`\${fs.readFileSync('schema.sql','utf8')}\`.then(()=>process.exit(0)).catch(e=>{console.error(e);process.exit(1)})
"
```

- [ ] **Step 5: Connect domain**

In Vercel project → Settings → Domains → Add `nuspec.ai` and `www.nuspec.ai`. Follow the DNS configuration instructions for your domain registrar.

- [ ] **Step 6: Verify production deployment**

```
https://nuspec.ai           → homepage renders
https://nuspec.ai/docs      → docs render
https://nuspec.ai/license   → lookup form renders
https://nuspec.ai/success   → success page renders
```

- [ ] **Step 7: Run a test purchase**

Using Stripe test mode (test API keys, card number `4242 4242 4242 4242`):
1. Click "Get Pro" → complete checkout
2. Confirm license key email received within 60 seconds
3. Visit `/license`, enter email → key appears

- [ ] **Step 8: Switch to live Stripe keys**

Update `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET` to live values in Vercel env vars. Redeploy.

- [ ] **Step 9: Final commit**

```bash
git add .
git commit -m "chore: production ready"
git push
```

---

## Pending: Pro Readme Source Column

Before the next NuGet release, measure source sizes for the 9 representative projects in the format comparison table and update `NUGET_README_PRO.md`. Run:

```bash
# Example for one project:
find E:/repos/OneStream/iCOM/iCOM.Common/Models -name "*.cs" | xargs wc -c | tail -1
```

Update the source sizes in `FormatComparison.tsx` at the same time.
