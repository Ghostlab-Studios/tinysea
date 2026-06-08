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
