# Change Log

## 2026-06-07 — Remove WebGL simulation + S3, simplify to desktop downloads

Simulation page is now a static download page for the Windows/macOS builds.

**Page (`index.php`)**
- Removed the Unity WebGL canvas, loader script, loading/drop overlays, fullscreen
  control, and CSV drag-and-drop handler.
- Rebuilt as a download-focused landing page: hero intro, prominent Windows/macOS
  download card (version-aware via `?v=1|2`), macOS setup-guide link, getting-started
  steps, and an "About this simulation" section. Reuses existing About-page CSS.

**S3 removal (WebGL-only, now orphaned)**
- Deleted the entire `api/` stack (`s3.php`, `session.php`, `upload.php`,
  `download.php`, `download_page.php`, `files.php`, `file.php`, `limits.php`,
  `config.php`, `.htaccess`, `.user.ini`). Only the (now-removed) WebGL build used it;
  standalone builds save to disk — see `ServerUpload.cs` `IsAvailable`.
- Scrubbed AWS keys from `.env`. **TODO (manual): deactivate those IAM keys in AWS.**

**Cleanup**
- Deleted WebGL build folders `builds/v{1,2}/TinySeaWebGL/` (kept the zips + PDF).
- Deleted `diagnostic.html` (Unity WebGL debug tool) and stale `overview.txt`.
- Tightened `.htaccess` CSP (no scripts/Brotli/WebGL allowances); removed Brotli MIME
  block. `.htaccess` is gitignored — re-upload it via FileZilla to apply on the server.
- Removed dead WebGL CSS from `style.css`; updated `README.md`, `builds/README.txt`,
  `.gitignore`.

Not touched: Unity project (`tinysea/`). `ServerUpload.cs` and its WebGL bulk-upload
callers still exist there but are dead without the server API — clean up separately if desired.

## 2026-06-08 — Visual redesign (ocean-depth system)

Ran a 5-lens "LLM design council" → synthesized spec → implemented. Goal: kill the
generic look the owner disliked.

- **Killed the purple.** Body now a light-shallows → abyssal-navy "water column"
  gradient built from the teal brand family + a subtle caustics overlay. New `:root`
  token system (color/elevation/spacing/type).
- **Hierarchy via 3-tier elevation.** Hero + dark About band float (`--elev-2`),
  normal cards sit (`--elev-1`), inset panels recess (`--foam`). Removed the blanket
  left-border accents.
- **Hero rebuilt** (`index.php`): two-column, download buttons fused in above the fold
  (removed the scroll-to CTA), gradient top-accent bar, decorative inline-SVG ocean art.
- **Download buttons unified** to one teal gradient (dropped vendor blue/grey).
- **About section → dark contrast band** (ghost button, translucent data panels).
- **Type scale** (Montserrat/Open Sans, added Open Sans 700), fluid hero `clamp()`.
- **a11y:** visible `:focus-visible` everywhere, skip link + `<main id="main">`,
  AA-checked tokens, `prefers-reduced-motion` kill-switch, `prefers-contrast` block.
- Emoji icons → inline monoline SVGs (home + about). About-page diagram CSS re-paletted
  to a marine ramp. Header/footer wave strips recolored (footer purple → abyss navy).

Files: `style.css` (full rewrite), `index.php`, `about/index.php`, `includes/header.php`.
Verified via Playwright at 1280 + 388 px (fresh CSS): ocean gradient live, hero 2-col→1-col,
buttons stack on mobile, no purple/rainbow literals remain. Before/after PNGs in `.tools/screenshots/`.
(Note: the built-in preview renderer cached the old CSS, hard-refresh to see the new design.)

## 2026-06-08 — Dash removal, version fallback, Unity art, About page

- **Removed all em/en dashes** (`&mdash;`/`&ndash;`/—/–) from the live pages (`index.php`,
  `about/index.php`) — rephrased to commas/parentheses/hyphens. Verified none remain.
  (The macOS Setup Guide PDF/its HTML source still contain dashes — separate artifact.)
- **Version fallback generalized.** `index.php` + `builds/download.php` now treat v1 as the
  default and honor any `?v=N` only if `builds/vN/` exists; otherwise they serve v1. A
  non-default missing version shows an on-page notice ("Version N isn't available, so you're
  seeing the default (version 1)."). Verified: `?v=2`/`?v=3` → notice + v1 build (HTTP 200).
- **Unity environment art** (from `tinysea/Assets/art/environment/`, 1920×1080) optimized to
  web JPGs in `assets/images/`: `ocean-hero.jpg` (53 KB, hero right column),
  `ocean-band.jpg` (94 KB, home About band bg), `ocean-seagrass.jpg` (89 KB, About hero banner).
  Creature sprites were multi-part animation sheets, not usable standalone.
- **About page enriched:** underwater banner hero, new "Two Ways to Explore" section
  (game vs. simulation), and a "Run It Yourself" CTA band linking back to the download.

New deploy assets (tracked, must FileZilla): `assets/images/ocean-{hero,band,seagrass}.jpg`.

## 2026-06-08 — Hero restructure + floating creatures

- **Fixed hero dead space.** Hero grid now `align-items:stretch`; the ocean scene
  (`.hero-art .scene`) fills the full column with `object-fit:cover` (art height now equals
  copy height, 483px — no more empty space above/below). Copy is vertically centered.
- **Download buttons made horizontal** — relabeled to "Windows" / "macOS" (OS glyph + short
  label) so they sit on one row instead of wrapping.
- **5 floating creatures** over the hero scene from the *new* shop icons
  (`tinysea/Assets/art/Shop Icons/*_new.png`): hexapod C/A/T + Sheplik + Yelloo, copied to
  `assets/images/creatures/`. Absolutely positioned, ~50-72px, gentle CSS `bob` float
  (varied delay/duration), `aria-hidden`, contained by `overflow:hidden` + reduced-motion safe.

New deploy assets (tracked): `assets/images/creatures/{hexapod-c,hexapod-a,hexapod-t,sheplik,yelloo}.png`.

### Creature tweaks (per feedback)
- Removed Yelloo; added **Grabbler** (near the ground), **Sploof** (near the sky), **Gelgi A** (middle).
- **Tier 1 (Hexapod + Gelgi) shrunk** (38-46px) vs predators (Sheplik/Grabbler/Sploof 58-66px).
- Now 7 creatures total. Assets: `creatures/{grabbler,sploof,gelgi-a}.png` added; `yelloo.png` removed.
