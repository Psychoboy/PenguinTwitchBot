# Custom Theme Palettes

Penguin Twitch Bot uses **MudBlazor** for its responsive user interface. The Theme Management system allows streamers to customize every aspect of the bot's visual presentation for both Light and Dark modes.

---

### Palette Modes

Each theme contains two independent color palettes:

- **Dark Mode Palette**: Active when the user selects Dark Mode or the system default is dark.
- **Light Mode Palette**: Active when Light Mode is selected.

> [!TIP]
> Both palettes should be configured so that text maintains high contrast against background and surface colors in both modes.

---

### Core Palette Colors

| Property | Default Role | Recommendation |
| :--- | :--- | :--- |
| **Primary** | Main buttons, active tabs, header highlights | Use your brand or channel color. |
| **Secondary** | Accent buttons, badges, secondary icons | Complementary contrasting shade. |
| **Background** | Main page canvas background | Deep gray/black for dark mode, soft white/gray for light mode. |
| **Surface** | Cards, tables, modals, side drawers | Slightly lighter than background in dark mode for depth. |
| **Appbar Background** | Top application navigation bar | Brand color or neutral dark surface. |
| **Appbar Text** | Title and icons inside top navigation bar | High-contrast text color against the Appbar background. |
| **Text Primary** | Primary body and heading text | Crisp white/off-white (dark) or dark charcoal (light). |
| **Text Secondary** | Subtitles, captions, disabled text | Muted gray with adequate readability. |
| **Lines Default** | Dividers, card borders, table grid lines | Subtle semi-transparent border color. |

---

### Live Color Preview

When editing a theme in the Theme Editor:
- Adjusting color pickers updates the live preview card in real time.
- Switch between the **Dark Palette** and **Light Palette** tabs to configure and verify each color scheme.
- Click **Save Theme** to commit changes to the database.

