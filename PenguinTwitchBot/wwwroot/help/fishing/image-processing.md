# Fish Image Processing

Penguin Twitch Bot includes an automated image optimization engine tailored for fish catalog artwork and stream visuals.

---

## Batch Processing

The **Image Processing** utility scans all source fish images in the `/fishes` directory and produces responsive WebP derivatives without cropping:

- **Thumbnail (100x100)**: Compressed at 80% quality for compact chat widgets and quick inventory dropdowns.
- **Small (200x200)**: Formatted at 85% quality for data tables, tournament listings, and leaderboards.
- **Medium (400x400)**: Generated at 90% quality for detailed player inspection dialogs and shop cards.
- **Large (800x800)**: Master display version at 95% quality for full-screen overlay alerts and celebratory catch displays.

---

## Best Practices

- **Aspect Ratio Preservation**: Images are fitted proportionately within bounding dimensions rather than stretched or cropped, preserving the original proportions of custom fish art.
- **WebP Compression**: WebP output ensures fast web dashboard loading times and low memory footprints for overlay browser sources in OBS.
- **Re-running Optimization**: After adding new fish species or custom images in bulk, run **Process All Images** once to regenerate missing resolution tiers.

