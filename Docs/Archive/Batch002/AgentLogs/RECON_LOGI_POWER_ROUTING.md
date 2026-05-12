# LOGI_POWER_ROUTING Recon

Status: PENDING VERIFICATION
Scan command: `rg -n "class\s+PowerNode|RecursivePower" Hecton8\Assets Hecton8\Packages -g "*.cs"`

## Offenders

- `Hecton8\Assets\_Project\Scripts\PowerNode.cs:52` - runtime OOP `PowerNode` component. Kept as scene bridge and grid membership owner; hot flow moved into `LogisticsNetworkGraph`.
- `Hecton8\Packages\com.unity.shadergraph\Editor\Data\Nodes\Math\Basic\PowerNode.cs:6` - Unity package ShaderGraph math node. Not project runtime power logic.

## Not Found

- `RecursivePower` - no symbol match.
