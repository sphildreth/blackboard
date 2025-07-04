# Blackboard Assets

This directory contains logo and branding assets for the Blackboard BBS project.

## Logo Files

### SVG Files (Vector Format)
- `logo.svg` - Main project logo (400x200)
- `social-preview.svg` - Social media preview image (1200x630)
- `mini-logo.svg` - Small avatar/profile logo (80x80)
- `chalkboard-icon.svg` - Pure chalkboard icon (80x80)

### PNG Files (Generated)
- `logo.png` - Main project logo 
- `social-preview.png` - Social media preview image
- `mini-logo.png` - Small avatar/profile logo
- `chalkboard-icon.png` - Pure chalkboard icon
- `favicon.png` - Small icon for web use (32x32)

## Converting SVG to PNG

To convert the SVG files to PNG format, run:

```bash
./convert-logos.sh
```

This script requires either ImageMagick or Inkscape to be installed.

### Installation Instructions

**Ubuntu/Debian:**
```bash
sudo apt install imagemagick
```

**macOS:**
```bash
brew install imagemagick
```

**Windows:**
Download from https://imagemagick.org/

## Usage Guidelines

### Social Media Preview
- Use `social-preview.png` for GitHub repository social preview
- Optimal size: 1200x630 pixels
- Perfect for Twitter, LinkedIn, and Facebook sharing

### General Logo
- Use `logo.png` for documentation, presentations, and general branding
- Size: 400x200 pixels
- Works well in README files and documentation

### Mini Logo
- Use `mini-logo.png` for avatars, profile pictures, and small displays
- Size: 80x80 pixels
- Perfect for GitHub profile pictures, Discord avatars, and app icons

### Chalkboard Icon
- Use `chalkboard-icon.png` for pure chalkboard representation
- Size: 80x80 pixels
- Realistic chalkboard with wood frame, chalk, and eraser
- Perfect for educational contexts and clean icon usage

### Favicon
- Use `favicon.png` for web applications and browser tabs
- Size: 32x32 pixels
- Can be used as application icon

## Design Elements

The logo design incorporates:
- **Retro Terminal Aesthetic** - Green text on black background
- **Chalkboard Emoji** - Represents the educational/bulletin board nature
- **Monospace Typography** - Classic terminal font styling
- **ASCII Art Borders** - Traditional BBS decorative elements
- **Scan Lines Effect** - Nostalgic CRT monitor appearance
- **Animated Cursor** - Blinking terminal cursor (SVG only)

## Color Scheme

- **Primary Green:** `#00ff00` - Main text and highlights
- **Secondary Green:** `#00aa00` - Secondary text and borders
- **Background:** `#000000` - Terminal black background
- **Accent:** `#333333` - Frame and border elements

## Brand Guidelines

- Always use the logo on dark backgrounds for best visibility
- Maintain the retro terminal aesthetic
- Use monospace fonts when possible for consistency
- Keep the chalkboard emoji as it represents the educational bulletin board nature
- Preserve the ASCII art elements that define the BBS aesthetic

## File Formats

- **SVG** - Use for scalable applications and web
- **PNG** - Use for social media and documentation
- **Transparent background** - All files have transparent backgrounds for versatility

## License

These assets are part of the Blackboard project and are licensed under the MIT License. See the main project LICENSE file for details.
