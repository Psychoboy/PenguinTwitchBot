# Theme Defaults & Presets

Manage active theme selection, system presets, and user permissions across the web dashboard.

---

### Setting the Default Theme

Streamers can set which theme loads by default for all visitors and viewers:

1. Locate the desired theme in the theme list.
2. Click **Set as Default**.
3. The selected theme will automatically load for new sessions and unauthenticated visitors.

> [!NOTE]
> Authenticated users can choose their own theme preference from the theme switcher dropdown in the top application bar (`MainLayout`). If their chosen theme is deleted or disabled, the bot automatically falls back to your configured **Default Theme**.

---

### Built-in Presets

The bot ships with pre-configured themes designed for optimal contrast and readability:

- **Default Theme (MudBlazor Classic)**: The clean standard blue and purple theme.
- **Penguin Dark**: High-contrast modern stream dark aesthetic.
- **Cyberpunk / Neon**: Vibrant magenta and cyan accent styling.
- **Midnight Purple**: Deep purple and violet tones.

---

### Resetting to Presets

If custom themes ever encounter styling conflicts or missing color fields:
- Click **Reset to Presets** in the top-right toolbar.
- Restores the standard shipped themes to their factory default palette definitions without affecting your other bot configurations.

---

### Importing & Exporting Themes

You can easily backup your themes or share them with others:

- **Exporting a Single Theme**: Click the **Export theme to JSON** (download) icon next to any theme in the themes table or inside the theme editor. This downloads a `.json` file containing the theme's complete palette definition.
- **Exporting All Themes**: Click the **Export All** button in the top toolbar to download a backup package of all custom and preset themes (both enabled and disabled).
- **Importing Themes**: Click **Import** in the top toolbar. You can either upload a theme `.json` file from your device or paste raw theme JSON directly. Preview the colors and theme details, then click **Import Theme** to add it to your bot.

