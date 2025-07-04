#!/bin/bash

# Convert SVG logos to PNG format for social media
# Requires ImageMagick or Inkscape to be installed

echo "Converting Blackboard logos to PNG format..."

# Check if ImageMagick is available (try magick first, then convert)
if command -v magick &> /dev/null; then
    echo "Using ImageMagick v7 for conversion..."
    
    # Convert main logo
    magick logo.svg -background transparent logo.png
    echo "✓ Created logo.png"
    
    # Convert social preview (high quality)
    magick social-preview.svg -background transparent -density 300 social-preview.png
    echo "✓ Created social-preview.png"
    
    # Create favicon
    magick logo.svg -background transparent -resize 32x32 favicon.png
    echo "✓ Created favicon.png"
    
    # Create mini logo
    magick mini-logo.svg -background transparent mini-logo.png
    echo "✓ Created mini-logo.png"
    
    # Create chalkboard icon
    magick chalkboard-icon.svg -background transparent chalkboard-icon.png
    echo "✓ Created chalkboard-icon.png"

elif command -v convert &> /dev/null; then
    echo "Using ImageMagick v6 for conversion..."
    
    # Convert main logo
    convert logo.svg -background transparent logo.png
    echo "✓ Created logo.png"
    
    # Convert social preview (high quality)
    convert social-preview.svg -background transparent -density 300 social-preview.png
    echo "✓ Created social-preview.png"
    
    # Create favicon
    convert logo.svg -background transparent -resize 32x32 favicon.png
    echo "✓ Created favicon.png"
    
    # Create mini logo
    convert mini-logo.svg -background transparent mini-logo.png
    echo "✓ Created mini-logo.png"
    
    # Create chalkboard icon
    convert chalkboard-icon.svg -background transparent chalkboard-icon.png
    echo "✓ Created chalkboard-icon.png"
    
elif command -v inkscape &> /dev/null; then
    echo "Using Inkscape for conversion..."
    
    # Convert main logo
    inkscape --export-type=png --export-filename=logo.png logo.svg
    echo "✓ Created logo.png"
    
    # Convert social preview
    inkscape --export-type=png --export-filename=social-preview.png --export-dpi=300 social-preview.svg
    echo "✓ Created social-preview.png"
    
    # Create favicon
    inkscape --export-type=png --export-filename=favicon.png --export-width=32 --export-height=32 logo.svg
    echo "✓ Created favicon.png"
    
    # Create mini logo
    inkscape --export-type=png --export-filename=mini-logo.png mini-logo.svg
    echo "✓ Created mini-logo.png"
    
    # Create chalkboard icon
    inkscape --export-type=png --export-filename=chalkboard-icon.png chalkboard-icon.svg
    echo "✓ Created chalkboard-icon.png"
    
else
    echo "❌ Neither ImageMagick nor Inkscape found!"
    echo "Please install one of them to convert SVG to PNG:"
    echo "  Ubuntu/Debian: sudo apt install imagemagick"
    echo "  macOS: brew install imagemagick"
    echo "  Windows: Download from https://imagemagick.org/"
    exit 1
fi

echo ""
echo "🎉 Logo conversion complete!"
echo "Files created:"
echo "  - logo.png (400x200) - General use logo"
echo "  - social-preview.png (1200x630) - Social media preview"
echo "  - mini-logo.png (80x80) - Small avatar/profile logo"
echo "  - chalkboard-icon.png (80x80) - Pure chalkboard icon"
echo "  - favicon.png (32x32) - Favicon for web use"
echo ""
echo "You can now use these PNG files for:"
echo "  - GitHub repository social preview"
echo "  - README.md header image"
echo "  - Documentation and presentations"
echo "  - Social media posts"
echo "  - Profile pictures and avatars (mini-logo)"
echo "  - App icons and small displays"
