# System Health & Validation Overview

The [Validation Status](/validation-status) dashboard provides automated health checks across your database, Action chains, Triggers, and audio asset files.

---

### Running a Validation Scan

1. Click **Run Validation Now** in the left panel.
2. The validator scans every Action, SubAction, Trigger, Audio Command, and Timer Group in your database.
3. Once complete, the scan records:
   - **Validation Date**: Timestamp of the latest scan.
   - **Summary Cards**: Quick count of Errors, Warnings, and Total Issues discovered.
   - **Categorized Issue List**: Itemized breakdown grouped by rule severity and module.

---

### Understanding Severities

- **Error (Red)**: Critical configuration issue that will cause an action or command to fail during execution (e.g. referencing an OBS connection that does not exist, missing sound file, or an invalid nested sub-action reference).
- **Warning (Yellow)**: Non-breaking configuration anomaly (e.g. an action with no triggers or an audio command with a low volume setting).
- **Info (Blue)**: Informational suggestions for optimization or clean-up.

