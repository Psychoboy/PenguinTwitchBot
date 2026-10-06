# Overlay & Dashboard Integration

Display your counters live on stream and monitor them from the dashboard.

---

## Overlay Editor Integration (Recommended)

The recommended way to display counters on stream is as a widget inside your overlay layouts via the **Overlay Editor** (`/overlay`):

### 1. Add Counter Widget to Layout
1. Navigate to **Stream Tools > Overlay Editor** in the navigation menu.
2. Select or create an overlay layout (e.g., *Gameplay* or *Just Chatting*).
3. Click **Add Widget** and select **Counter**.
4. Drag and resize the counter widget anywhere on your stream canvas.

### 2. Configure Counter Display
With the counter widget selected, use the configuration panel on the right:
- **Counters**: Add one or more counters to the widget.
  - **Counter Type**: Choose *Generic Counter* or *Death Counter*.
  - **Select Counter**: Pick the specific counter name from your created counters.
  - **Custom Label, Prefix & Suffix**: Optionally override display labels or add prefixes (e.g., `Deaths: ` or `x`).
  - **Custom Colors**: Override colors per counter item.
- **Typography & Styling**: Customize font family, font size, font weight, letter spacing, text shadow, and alignment.
- **Box & Container**: Set container background, item background, border radius, padding, gap, and layout direction (*Row* or *Column*).
- Click **Save** in the toolbar to save your layout.

### 3. Add to OBS Studio
1. In the Overlay Editor toolbar, click **Copy URL** (or use `http://localhost:5000/overlay.html?layout=YourLayoutName`).
2. In OBS Studio, add a **Browser Source**.
3. Set the URL to your copied overlay URL.
4. Set the Width and Height to match your overlay canvas (e.g., `1920` x `1080`).
5. All counters update in real time via WebSockets and SignalR without needing to refresh the browser source.

---

## Standalone Browser Source (`/counter.html`)

If you prefer to embed a counter directly as its own dedicated OBS source without using the Overlay Editor, you can use the standalone `/counter.html` endpoint.

### Setup in OBS
1. In OBS Studio, add a new **Browser Source**.
2. Set the URL to your bot's counter overlay endpoint, for example:
   `http://localhost:5000/counter.html?counters=deaths`
3. Set the width and height to fit your widget (e.g., `400` x `100`).
4. Check **Shutdown source when not visible** (optional) and click **OK**.

### Standalone URL Parameters

Customize standalone appearance using URL query parameters:

| Parameter | Description | Example |
| :--- | :--- | :--- |
| `counters` | Comma-separated list of counter names to display | `counters=deaths,wins` |
| `fontFamily`| Font family used for label and value | `fontFamily=Roboto` |
| `fontSize` | CSS font size for counter numbers | `fontSize=32px` |
| `color` | Text color for the counter value | `color=%23ffffff` |
| `labelColor`| Text color for the counter label | `labelColor=%2390caf9` |
| `bgColor` | Background color for the overlay container | `bgColor=transparent` |
| `itemBgColor` | Background color for individual counter boxes | `itemBgColor=rgba(0,0,0,0.6)` |
| `layout` | Direction of items: `row` (horizontal) or `column` (vertical) | `layout=row` |
| `align` | Alignment of items: `start`, `center`, or `end` | `align=center` |
| `gap` | CSS gap between counter items | `gap=16px` |
| `padding` | Padding around each counter item or container | `padding=12px` |

---

## Dashboard Widget

The **Homepage Dashboard** includes a dedicated Counters widget:
- Displays all active counters with their current counts and display names.
- Quick action buttons to increment (`+`), decrement (`-`), or reset values with one click while broadcasting.
- Synchronized in real time with OBS overlays and chat commands.
