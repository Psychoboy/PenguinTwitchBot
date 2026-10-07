# Prize Wheel Management

The [Wheel Spin](/wheelspin) dashboard allows streamers and editors to build customized multi-slice wheels for channel giveaways, punishments, challenge runs, and sub goals.

## Creating & Editing Wheels

- **Wheel Name**: An internal identifier for the wheel.
- **Winning Message**: The chat announcement sent when a slice is selected (supports the `{label}` placeholder).
- **Adding Slices**:
  - **Single Slice**: Enter the slice label and optional numerical weight (e.g. 1 for standard, higher weights increase relative slice thickness and landing chance).
  - **Bulk Add**: Expand **Bulk Add Slices** and paste multiple options (one per line) to populate a wheel instantly.
- **Slice Weights**: Adjust weight to make rare prizes smaller and common outcomes larger on the visual overlay.
- **Shuffle**: Click **Shuffle** to randomize the physical order of slices around the wheel perimeter.
- **Saving Wheels**: Click **Save Wheel** to persist changes to the database.

## Overlay Actions

- **Show**: Broadcasts the wheel to the OBS browser overlay widget.
- **Spin**: Triggers a physics-based spin animation in the browser source that slows to a stop on the randomly selected slice.
- **Hide**: Hides the active wheel from the overlay.

