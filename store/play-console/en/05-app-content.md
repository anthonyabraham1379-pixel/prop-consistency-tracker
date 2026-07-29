# "App Content" Declarations

Section: **Policy → App content**. English reference for the same
declarations documented in `../05-contenido-de-la-app.md` (besides
Content Rating and Data Safety, which have their own files). These are
global declarations, not per listing language — filled out once.

## 1. Privacy policy

```
https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/
```
Requires GitHub Pages to be enabled (Settings → Pages → branch `master`,
folder `/docs`). Verify the URL loads BEFORE pasting it — Google validates
it. An English version of the policy is available at
`https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/index-en.html`
if you want to link it from the en-US listing's website field.

## 2. App access

**Answer: "All functionality is available without special access".**

Rationale: the app works fully without signing in (Google login only
enables cloud sync) and Premium content sits behind a standard Google
Play purchase, which reviewers can inspect via the subscription listing.
No test credentials are needed.

*If Google objects (rare), the fallback is to create a test Google
account and provide it under "Access instructions".*

## 3. Ads

**Answer: Yes, my app contains ads.** (AdMob, banners on the free tier.)

## 4. Target audience and content

| Question | Answer |
|---|---|
| Target age groups | **18+ only** |
| Could the app unintentionally appeal to children? | No (financial subject matter, sober UI, no child-oriented elements) |

Declaring 18+ only means Families policies don't apply and you don't need
to join the "Designed for Families" program.

## 5. Financial features declaration

**Answer: "My app doesn't offer any financial features".**

Rationale (keep on file in case of manual review): Prop Consistency
Tracker is a manual journal/tracking tool. It doesn't offer loans, doesn't
facilitate trading or investing, doesn't connect to brokers, doesn't
execute orders, doesn't custody funds, and doesn't give advice or
recommendations. The "financial data" it shows is numbers the user types
in by hand about their prop firm evaluations — equivalent to a spreadsheet
or journal.

> Important consistency point: the store listing copy (see `01-store-
> listing.md`) already includes the "not financial advice, doesn't
> connect to brokers, doesn't guarantee results" disclaimer. Keeping that
> disclaimer visible inside the app too (onboarding or Settings)
> strengthens the case if a human reviewer examines the app under the
> financial services policy.

## 6. News apps

**No, it is not a news app.**

## 7. Government apps

**No.**

## 8. Health apps

**No, it has no health features.**

## 9. Data safety / account deletion

Because the app supports account creation (Google Sign-In), Play requires
a public URL where the user can request account and data deletion
**without needing to install the app**:

```
https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/delete-account.html
```

(The page already exists at `docs/delete-account.html`; the process is by
email until the app has self-service deletion built in. An English
version is at `docs/delete-account-en.html`.)

## 10. Advertising ID

Under the "Advertising ID" declaration (shown for apps targeting API
33+): **Yes, the app uses the advertising ID**, for **Advertising or
marketing** purposes (used by the AdMob SDK). The
`com.google.android.gms.permission.AD_ID` permission is added
automatically by `react-native-google-mobile-ads`.

## 11. Additional app-level settings (main tab)

| Field | Value |
|---|---|
| Free or paid app? | Free (with in-app purchases) |
| Distribution countries | Start with: Mexico, Colombia, Argentina, Chile, Peru, Spain, US — or "all countries" if preferred |
| Category | Finance |
| Contains in-app purchases? | Yes ($3.99/month, mark the IAP price range) |
