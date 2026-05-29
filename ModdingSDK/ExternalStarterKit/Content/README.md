# Content

Declare assets in `assets.h8manifest.json`. Put referenced files under `Content/Assets/` and use `h8mod.ps1 -Action asset-snippet` plus `h8mod.ps1 -Action apply-asset-snippet` instead of hand-editing JSON when possible.

Runtime loading is not granted by placing files here. The SDK/packer must CRC-approve assets and generate envelope references before gameplay can use them.
