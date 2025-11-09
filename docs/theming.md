# Theming

slskd supports custom CSS theming to personalize the appearance of the web UI. Themes can customize colors, backgrounds, fonts, and other visual elements.

# Quick Start

1. Choose or create a custom CSS file
2. Configure slskd to use the CSS file
3. Refresh your browser to see the changes

# Configuration

Custom themes are applied by specifying the path to a CSS file in the application configuration.

| Command-Line | Environment Variable | Description |
| ------------ | -------------------- | ----------- |
| `--custom-css-path` | `SLSKD_CUSTOM_CSS_PATH` | The path to a custom CSS file for theme customization |

# Included Themes

slskd includes several example themes to get you started. These can be found in the `/docs/themes` directory:

- **`bubblegum.css`** - A playful theme with hot pink and bubblegum colors in light mode, and dark purples and blacks in dark mode
- **`gruvbox.css`** - Based on the popular Gruvbox color scheme with warm, earthy tones
- **`solarized.css`** - An implementation of the Solarized color palette with precision colors

# Creating Custom Themes

Custom themes are standard CSS files that override the default CSS variables used throughout the application. (Hint: `src/web/src/components/App.css`)