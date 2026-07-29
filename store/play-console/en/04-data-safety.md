# Data Safety Form

Section: **App content → Data safety**. This is the English reference for
the same declaration documented in `../04-seguridad-de-datos.md` — the
Data Safety form itself is a single global declaration (not per listing
language), so you only fill it out once. This file exists so you can read
it in English while filling out the Play Console form.

These answers reflect what the app actually does today: local data in
AsyncStorage; if the user signs in with Google, sync to Supabase; AdMob
on the free tier; RevenueCat for subscriptions.

> If you later remove AdMob, add analytics, or change the login flow,
> this form MUST be updated — a mismatch between the form and the app's
> real behavior is grounds for suspension.

## General questions

| Question | Answer |
|---|---|
| Does your app collect or share any of the required data types? | **Yes** |
| Is all user data encrypted in transit? | **Yes** (Supabase, RevenueCat and AdMob all use HTTPS/TLS) |
| Do you provide a way for users to request data deletion? | **Yes** |
| Account/data deletion URL | `https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/delete-account.html` |

## Data types to declare

### 1. Personal info → Email address
*(Google Sign-In via Supabase Auth)*

| Field | Answer |
|---|---|
| Collected? | Yes |
| Shared? | No (Supabase acts as a service provider) |
| Processed ephemerally? | No |
| Optional or required? | **Optional** (the app works without signing in) |
| Purposes | App functionality, Account management |

### 2. Financial info → Other financial info
*(Daily P&L and challenge parameters the user enters manually, synced to
Supabase only if signed in)*

| Field | Answer |
|---|---|
| Collected? | Yes |
| Shared? | No |
| Processed ephemerally? | No |
| Optional or required? | **Optional** (without login, data never leaves the device) |
| Purposes | App functionality |

*Note: this is data the user types in themselves about practice/
evaluation accounts; the app never accesses real financial accounts.
Still, declaring it as financial info is the conservative, safe reading.*

### 3. Purchases → Purchase history
*(RevenueCat / Google Play Billing)*

| Field | Answer |
|---|---|
| Collected? | Yes |
| Shared? | No (RevenueCat is a service provider) |
| Processed ephemerally? | No |
| Optional or required? | Optional (only if the user buys Premium) |
| Purposes | App functionality |

### 4. Device or other IDs → Device IDs
*(Advertising ID, collected by the Google Mobile Ads / AdMob SDK)*

| Field | Answer |
|---|---|
| Collected? | Yes |
| Shared? | **Yes** (with Google/advertising partners, per the AdMob SDK disclosure) |
| Processed ephemerally? | No |
| Optional or required? | Required for free-tier users |
| Purposes | Advertising or marketing |

### 5. App activity → App interactions
*(Ad interactions, collected by AdMob)*

| Field | Answer |
|---|---|
| Collected? | Yes |
| Shared? | Yes (Google, for advertising) |
| Processed ephemerally? | No |
| Purposes | Advertising or marketing |

> **Verify before submitting:** Google publishes the official data
> disclosure for the Google Mobile Ads SDK at
> https://developers.google.com/admob/android/play-data-disclosure —
> cross-check sections 4 and 5 against the current SDK version
> (`react-native-google-mobile-ads` wraps that SDK). Same for
> https://www.revenuecat.com/docs/platform-resources/google-plays-data-safety
> for RevenueCat.

## What is NOT declared (because it isn't collected)

Location, contacts, photos/videos, audio, files, calendar, web browsing
history, health info, messages, installed-apps data, name, address,
phone number, or user IDs belonging to other users. The app also doesn't
use any analytics or crash-reporting SDK today — if one is added later
(e.g. Sentry/Firebase), this form needs to be revisited.
