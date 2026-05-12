# Steam Publisher Metadata Template

Status: `PENDING VERIFICATION`

## Legal Boundary

The project must not publish through a placeholder person or a nominal "friend in UK" unless that entity is the real contracting publisher with tax, banking, and support responsibility. Steamworks onboarding data must match the legal entity that owns distribution rights.

## Required Fields

- Legal entity name
- Entity type: individual / company / publisher partner
- Registered country and address
- Tax interview country and treaty basis
- Bank beneficiary name matching entity
- Support email and support URL
- Privacy policy URL
- Controller support statement: gamepad, Steam Deck gyro, trackpad quick slots
- Build depots: Windows x64, Linux x64, macOS Universal when signed

## Operational Notes

- Western-based publisher path is acceptable only as a real publisher agreement.
- Steam Deck compatibility claims remain blocked until Linux/Vulkan build, controller glyphs, text input, suspend/resume, and performance are device-tested.
- Steamworks native binaries must come from the official Steamworks SDK `redistributable_bin` folder, not public mirrors.
