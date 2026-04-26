# Tiny Sea Simulation Website

## Setup Instructions

1. **Extract this archive** to your local computer

2. **Add your files:**
   - Place your logo at: `assets/images/logo.png` (315×250px)
   - Place your Unity build files in: `builds/v1/TinySeaWebGL/Build/` (and `builds/v2/TinySeaWebGL/Build/` for the v2 variant)
     - TinySeaWebGL.loader.js
     - TinySeaWebGL.data.br
     - TinySeaWebGL.framework.js.br
     - TinySeaWebGL.wasm.br
   - Place standalone downloads alongside the WebGL folder in each version directory:
     - `builds/v1/TinySea-Windows.zip`, `builds/v1/TinySea-macOS.zip`, `builds/v1/TinySea macOS Setup Guide.pdf`
     - `builds/v2/...` (same filenames)

### Version switching

The site serves two parallel builds via a URL query parameter:
- Default / `?v=1` → loads everything from `builds/v1/`
- `?v=2`           → loads everything from `builds/v2/` (WebGL + Windows/macOS downloads + PDF)

Any other value falls back to v1.

3. **Upload to your server:**
   - Use FileZilla or your hosting control panel
   - Upload the entire contents to your web root (public_html or simulation.playtinysea.com)
   - Make sure .htaccess is uploaded (it's hidden by default)

4. **Set proper permissions:**
   - PHP files: 644
   - Directories: 755
   - .htaccess: 644

5. **Test the site:**
   - Visit your domain
   - Check that the Unity simulation loads
   - Test navigation between pages
   - Verify responsive design on mobile

## File Structure

```
.
├── index.php                 # Main simulation page
├── style.css                 # All styling
├── .htaccess                 # Server configuration
├── includes/
│   ├── header.php           # Shared header component
│   └── footer.php           # Shared footer component
├── about/
│   └── index.php            # About page
├── assets/
│   └── images/
│       └── logo.png         # [Add your logo here]
└── builds/
    ├── download.php           # Version-aware download handler (?v=1|2)
    ├── README.txt
    ├── v1/
    │   ├── TinySeaWebGL/
    │   │   └── Build/         # Unity WebGL build (v1)
    │   ├── TinySea-Windows.zip
    │   ├── TinySea-macOS.zip
    │   └── TinySea macOS Setup Guide.pdf
    └── v2/
        ├── TinySeaWebGL/
        │   └── Build/         # Unity WebGL build (v2)
        ├── TinySea-Windows.zip
        ├── TinySea-macOS.zip
        └── TinySea macOS Setup Guide.pdf
```

## Server Requirements

- PHP 7.4 or higher
- Apache with mod_headers
- Brotli compression support
- .htaccess enabled

## Browser Support

- Chrome, Firefox, Safari, Edge (modern versions)
- WebGL support required for Unity simulation

## Notes

- The site is fully modular with PHP includes
- All pages share the same header and footer
- Responsive design works on mobile and tablet
- Ocean theme with animated waves
- Unity WebGL loading with progress bar

## Support

For issues or questions, refer to SPECIFICATION.md for complete details.
