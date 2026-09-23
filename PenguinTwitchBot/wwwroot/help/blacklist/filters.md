# Blacklist & Word Filters

The [Blacklist](/blacklist) manager provides automated chat moderation to catch and penalize banned phrases, hate speech, scams, and prohibited links.

## Filter Matching Modes

When defining a word filter rule:
- **Substring Match (Default)**:
  - Performs a case-insensitive check to see if the chat message contains the phrase anywhere within the text.
  - Useful for obvious banned words, scam phrases, or specific banned domains.
- **Regular Expressions (Regex)**:
  - Check the **Regex** toggle to evaluate messages against regular expressions.
  - Allows matching advanced patterns such as obfuscated words (`d[i1]sc[o0]rd`), masked links, repeating characters, or specific phone/IP formats.
- **Exemptions**:
  - Broadcasters and Channel Moderators are automatically exempt from blacklist filtering to prevent disruption of moderation duties.

