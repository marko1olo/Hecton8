# Content Assets

Put files referenced by `Content/assets.h8manifest.json` here.

Supported starter declarations are bounded to:

- `data_blob`: `.json`, `.bytes`, `.bin`
- `raw_texture`: `.png`, `.jpg`, `.jpeg`, `.webp`
- `audio_clip`: `.wav`, `.ogg`

Declaring a file here does not grant runtime loading. The manifest is an authoring/review contract; runtime use still requires engine-owned approval and bake.
