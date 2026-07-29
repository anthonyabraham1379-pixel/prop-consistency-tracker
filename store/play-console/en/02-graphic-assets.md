# Graphic Assets

Section: **Store listing → Graphics**. These are the official specs plus a
concrete plan for what to capture. Assets themselves (icon, feature
graphic, screenshots) are shared across every listing language — you
don't need separate images for en-US unless you want localized captions
burned into the screenshots.

## Required

| Asset | Spec | Status |
|---|---|---|
| App icon | 32-bit PNG, **512 × 512 px**, max 1 MB, Play applies the mask | Export from `assets/icon.png` at 512×512 |
| Feature graphic | JPG or 24-bit PNG, **1024 × 500 px**, no transparency | **Still needs to be created** |
| Phone screenshots | Minimum **2**, 4–8 recommended. JPG/PNG, side between 320–3840 px, 16:9 or 9:16 ratio (use 1080 × 1920 or higher) | **Still needed** |

## Optional (recommended)

| Asset | Spec |
|---|---|
| 7" tablet screenshots | Min. 1080 px on the short side, up to 8 |
| 10" tablet screenshots | Same |
| Promo video | Public YouTube URL |

## Feature graphic — design brief

- Dark background `#08090D` (same as the adaptive icon) with the
  green/red/blue accent palette from the app (`config/theme.js`).
- Large headline: **"Your challenge, under control"** (or "Prop
  Consistency Tracker"). Avoid claims like "pass your evaluation".
- Include a visual element from the dashboard (consistency donut or
  sparkline) — a mockup is fine, doesn't need to be a literal screenshot.
- Don't include the Google Play badge or pricing inside the image.

## Phone screenshot plan (1080 × 1920)

Capture on a device/emulator with realistic sample data (never the empty
app). Suggested order — the first two get seen the most. If you want
English captions burned into the images for the en-US listing, use these:

1. **Dashboard** with an advanced challenge: consistency %, goal progress
   and drawdown all visible. Suggested caption: *"All your rules, one
   screen"*.
2. **Monthly calendar** with green/red days. Caption: *"Your month at a
   glance"*.
3. **Log day** (add-day modal). Caption: *"Log your P&L in seconds"*.
4. **Analytics** with stats. Caption: *"Know your numbers"*.
5. **Onboarding/challenge setup** showing configurable parameters.
   Caption: *"Any prop firm, your rules"*.
6. **Multi-account switcher.** Caption: *"All your challenges at once"*.

Tips:
- Capture with a clean status bar (full battery, no notifications) or use
  device frames.
- Captions can be overlaid with any tool (Figma/Canva); keep typography
  and palette consistent with the app.
- Fictional data is fine, but keep it plausible (a few hundred dollars of
  daily P&L on a $100k account, not millions).

## How to generate sample data

Create a "Tradeify Select 100k" challenge (profit target $6,000, trailing
drawdown $3,000, 40% consistency) and log ~15 days mixing green and red,
with the best day around 30% of the total so the donut looks healthy but
still interesting. Switch the app to English in Settings before capturing
if you want the en-US screenshots to show English UI text.
