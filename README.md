# Tiny Sea Simulation Website

A lightweight PHP site that hosts the **standalone Tiny Sea Simulation downloads**
(Windows and macOS) plus an About page describing the research platform.

> The site previously embedded a Unity WebGL build of the simulation and an
> S3-backed data-upload API. Both have been removed — the site now simply serves
> the desktop builds as static downloads.

## Setup Instructions

1. **Extract this archive** to your local computer

2. **Add your files:**
   - Place your logo at: `assets/images/logo.png`
   - Place standalone downloads in each version directory:
     - `builds/v1/TinySea-Windows.zip`, `builds/v1/TinySea-macOS.zip`, `builds/v1/TinySea macOS Setup Guide.pdf`
     - `builds/v2/...` (same filenames)

### Version switching

The download links serve two parallel build sets via a URL query parameter:
- Default / `?v=1` → serves files from `builds/v1/`
- `?v=2`           → serves files from `builds/v2/`

Any other value falls back to v1. Missing files return "Build not available yet",
so v2 assets can be added incrementally.

3. **Upload to your server:**
   - Use FileZilla or your hosting control panel
   - Upload the entire contents to your web root (public_html or simulation.playtinysea.com)
   - Make sure `.htaccess` is uploaded (it's hidden by default)

4. **Set proper permissions:**
   - PHP files: 644
   - Directories: 755
   - `.htaccess`: 644

5. **Test the site:**
   - Visit your domain
   - Confirm the Windows and macOS downloads work
   - Test navigation between pages
   - Verify responsive design on mobile

## File Structure

```
.
├── index.php                 # Simulation download page
├── style.css                 # All styling
├── .htaccess                 # Server configuration (security headers, etc.)
├── includes/
│   ├── header.php            # Shared header component
│   └── footer.php            # Shared footer component
├── about/
│   └── index.php             # About page
├── assets/
│   └── images/
│       └── logo.png          # [Add your logo here]
└── builds/
    ├── download.php          # Version-aware download handler (?file=windows|macos|macos-guide&v=1|2)
    ├── macos-setup-guide.html# HTML source for the macOS setup guide PDF
    ├── README.txt
    ├── v1/
    │   ├── TinySea-Windows.zip
    │   ├── TinySea-macOS.zip
    │   └── TinySea macOS Setup Guide.pdf
    └── v2/
        ├── TinySea-Windows.zip
        ├── TinySea-macOS.zip
        └── TinySea macOS Setup Guide.pdf
```

## Server Requirements

- PHP 7.4 or higher
- Apache with mod_headers (for the security headers in `.htaccess`)
- `.htaccess` enabled

## Browser Support

- Chrome, Firefox, Safari, Edge (modern versions)

## Notes

- The site is fully modular with PHP includes; all pages share the same header and footer
- Responsive design works on mobile and tablet
- Ocean theme with animated waves
