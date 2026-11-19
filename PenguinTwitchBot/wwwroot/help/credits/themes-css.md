# Themes, Media & Custom CSS

## Built-In & Custom Themes

Stream Credits ships with 7 built-in themes and full support for creating, cloning, importing, and exporting custom themes as JSON:

1. **Classic Cinema** *(Default)* — Regular smooth vertical end credits with clean typography and Twitch avatars.
2. **Galactic Crawl** — Star Wars-style 3D opening perspective crawl over an animated starfield with golden text, circular avatars, and `Name xCount` entries.
3. **Lumia Spotlight** — Clean rounded pills over transparent dark obsidian framing.
4. **Neon Cyber** — Futuristic synthwave glow headings and 2-column grid layout.
5. **Cozy Frosted Glass** — Warm cream/gold palette with rounded avatar cards.
6. **Retro Arcade** — Pixel-style arcade typography on deep CRT midnight.
7. **Minimal Transparent** — Overlay-ready no-backdrop credits for floating over gameplay or facecam.

Built-in themes can be customized or reset to defaults at any time, and cloned into custom themes you can share via JSON.

## Media, Avatars & Audio

- **Images**: Upload or link header logos, footer graphics, per-section header icons, background images, custom fallback avatars, and per-row static cast images.
- **Twitch Avatars**: Profile pictures are batch-fetched and cached cleanly from Twitch. Configure circle/rounded/square shape, border color, or fallbacks (**Colored Silhouette**, **Initials**, **Custom Image**, or **Hide**).
- **Optional Audio**: Upload MP3/WAV/OGG/AAC music or link an external URL with independent volume, loop, fade-in, and fade-out controls.

## Custom CSS Variables & Selectors

Theme Custom CSS loads first, followed by Profile Override CSS so you can tweak individual profiles on top of shared themes.

Useful CSS variables:
- `--credits-bg`, `--credits-text`, `--credits-secondary-text`, `--credits-accent`
- `--credits-header-color`, `--credits-section-color`, `--credits-text-shadow`
- `--credits-font-heading`, `--credits-font-body`
- `--credits-header-size`, `--credits-section-title-size`, `--credits-entry-size`
- `--credits-avatar-size`, `--credits-max-width`, `--credits-section-gap`

Useful CSS selectors:
- `.credits-root`, `.credits-header`, `.credits-header-title`, `.credits-footer`, `.credits-footer-title`
- `.credits-section`, `.credits-section-title`, `.credits-entry`, `.credits-avatar`
