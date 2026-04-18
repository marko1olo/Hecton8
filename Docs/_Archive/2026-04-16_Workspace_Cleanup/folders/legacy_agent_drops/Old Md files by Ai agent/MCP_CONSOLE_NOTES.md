**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# MCP Console Notes

These recurring console messages are currently coming from **MCP-for-Unity serialization / inspection**, not from the gameplay HUD/PDA feature itself:

- `minVolume is not supported anymore. Use min-, maxDistance and rolloffMode instead.`
- `maxVolume is not supported anymore. Use min-, maxDistance and rolloffMode instead.`
- `rolloffFactor is not supported anymore. Use min-, maxDistance and rolloffMode instead.`
- `TransformHandle object is null...`
- some `UniversalAdditionalCameraData` serialization warnings/errors when MCP inspects camera stacks

## What this means

These are produced when MCP reads component properties through reflection and Unity logs on access.
They are not the same as:
- game compile errors
- runtime PDA/HUD logic errors
- sky rendering logic bugs

## Practical rule

When debugging gameplay/UI:
- prioritize real compile errors
- prioritize real NullReferenceExceptions from project scripts
- deprioritize these MCP serialization warnings unless they flood so badly that they block work

## Possible future cleanup

If needed later, patch MCP package cache serializers to skip:
- obsolete `AudioSource.minVolume`
- obsolete `AudioSource.maxVolume`
- obsolete `AudioSource.rolloffFactor`
- fragile `TransformHandle` properties
- unsafe camera stack properties on overlay cameras

That is a tooling cleanup task, not a gameplay feature task.
