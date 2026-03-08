# Tiny Sea Simulation Website

## Setup Instructions

1. **Extract this archive** to your local computer

2. **Add your files:**
   - Place your logo at: `assets/images/logo.png` (315×250px)
   - Place your Unity build files in: `build/TinySeaWebGL/Build/`
     - TinySeaWebGL.loader.js
     - TinySeaWebGL.data.br
     - TinySeaWebGL.framework.js.br
     - TinySeaWebGL.wasm.br

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
└── build/
    └── TinySeaWebGL/
        └── Build/           # [Add your Unity build files here]
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
