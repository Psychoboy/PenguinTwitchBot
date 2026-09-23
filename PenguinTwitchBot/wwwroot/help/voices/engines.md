# Text-to-Speech (TTS) Engines

Penguin Twitch Bot supports multiple high-performance Text-to-Speech synthesis engines for chat read-aloud, donations, channel point redemptions, and Action subactions.

---

### Supported Engines

| Engine | Type | Requirements | Strengths |
| :--- | :--- | :--- | :--- |
| **Kokoro** | Local Neural (ONNX) | Bundled (.npy voice models) | Outstanding natural intonation, multiple accents (US, UK, ES, FR, JA, ZH), runs locally with no cloud API keys needed. |
| **Piper** | Local Lightweight (ONNX) | Downloadable `.onnx` models | Extremely fast CPU synthesis, wide international language support, low memory footprint. |
| **Google Cloud TTS** | Cloud API | Google API credentials | Huge library of multilingual Wavenet and Neural2 cloud voices. |

---

### Kokoro Engine Settings

- **Multi-Threading**: Configure thread count (default: `2`) to balance synthesis speed with CPU usage during gaming streams.
- **System Fallback Voice**: The bot automatically resolves your system locale and assigns an optimal fallback voice (e.g. `af_heart` for US English, `bf_emma` for British English).

---

### Audio Previews

On the [Voices](/voices) page:
- Click the play icon next to any Kokoro, Piper, or Google voice to immediately generate and audition an audio preview in your browser before assigning it to viewers.

