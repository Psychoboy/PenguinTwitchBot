# Overlay Editor Guide

The **Overlay Editor** provides a visual drag-and-drop canvas for designing rich, dynamic stream overlays for OBS Studio, Streamlabs Desktop, and browser sources.

---

### Layouts & Canvas Controls

- **Layout Management**:
  - Create multiple layouts for different stream scenes (e.g. *Just Chatting*, *Gameplay*, *BRB Screen*, *Tournament*).
  - Mark one layout as **Default**; default layouts load automatically when opening browser sources without specifying an ID parameter.
  - Duplicate, rename, or delete existing layouts from the left-hand panel.
- **Canvas Navigation**:
  - **Zoom**: Zoom in, zoom out, or reset zoom to 100% using the toolbar controls.
  - **Pan**: Pan up, down, left, and right to navigate complex 1080p / 1440p / 4K overlay dimensions.
- **Positioning & Sizing**:
  - Drag widgets around the canvas to position them.
  - Resize widgets using corner and edge handles.
  - Adjust Z-Index to arrange widgets in layers.
- **Locking Widgets**:
  - Mark widgets as **Locked** to enable click-through mode and prevent accidental movement while editing.

---

### OBS Embedding

1. Click **Save** in the toolbar to save your changes to the database.
2. Click **Copy URL** (copy icon) in the toolbar to copy the OBS browser source link to your clipboard.
3. In OBS Studio:
   - Add a new **Browser Source**.
   - Paste the copied URL into the URL field.
   - Set the resolution to your canvas resolution (e.g. Width: `1920`, Height: `1080`).
   - Check **Shutdown source when not visible** and **Refresh browser when scene becomes active** as desired.

