# Interactive Filter Tester

To ensure your blacklist rules match offending messages without inadvertently catching innocent viewer chat, PenguinTwitchBot features an interactive **Filter Tester**.

## Using the Tester

The Filter Tester card sits at the top of the Blacklist dashboard:
1. **Tester Mode**:
   - **Test Editor Filter**: Tests your input strictly against the phrase and regex settings currently in the edit form below.
   - **Test All Blacklist Rules**: Evaluates the input string across all saved, active blacklist rules in your bot database.
2. **Interactive Testing**:
   - Type or paste candidate chat messages into the **Test chat message** input field.
   - The test result displays live visual indicators (Match / No Match), showing whether the text would trigger a timeout, the matching rule, and the reason.
   - Use this to verify regex patterns before committing them live to your channel!

