# HECTON-8 Gemini Prompt - Inventory Sheet From Current Source

Positive reference image:
`Docs/GeneratedAssets/Gemini/Outputs/Batch30/InventoryIsolatedObjects_20260607/TX_B30_InventoryIsolatedObjects_Source_20260607_Gemini.png`

Use the positive reference only for:
- physical 3D prop readability
- separated object-sheet layout
- realistic hard-surface scale and bevels
- worn metal, ceramic, polymer, glass, gasket, cable, bolt, grime, chipped-paint materials
- three-quarter inventory presentation

Do not use old project UI sprites as references. They are legacy examples and must not influence the result.

## Prompt

Create a new improved HECTON-8 inventory object source sheet.

Use the attached current source sheet as the only positive reference for physicality, spacing, and object readability. Improve it. Do not copy it exactly.

Generate 12 distinct physical objects in a clean 4 x 3 invisible layout. The names in parentheses are project PersistentId targets; do not render these names as text:
1. emergency oxygen micro-tank (Data_EmergencyO2Canister)
2. sealed high-capacity battery cell (Comp_BatteryCell / Comp_HighCapacityCell)
3. bundled copper wire spool (Comp_CopperWire)
4. folded repair multitool (Item_Tool_Repair)
5. rugged sensor package (Comp_SensorPackage)
6. pressure compensator module (Comp_PressureCompensator)
7. cooling cartridge (Comp_CoolingCartridge)
8. reinforced titanium scrap ingot (Data_TitaniumScrap)
9. beacon core puck (Comp_BeaconCore)
10. electrolyte ampoule rack (Data_ElectrolyteAmpoule)
11. pressure seal pack (Comp_PressureSeal)
12. hydraulic actuator (Comp_HydraulicActuator)

Each object must be a believable AA-quality 3D game prop, suitable for a survival-game inventory. Make them better than Subnautica item thumbnails: more believable industrial design, better material breakup, clearer silhouettes, less toy-like, less flat.

Layout constraints:
- one object per invisible cell
- large empty spacing between objects
- every object fully inside its cell with at least 25 percent padding
- no object touches the image border
- no overlap
- no visible grid lines
- neutral dark gray matte background, flat and removable
- no floor horizon
- no cast shadows that connect objects

Hard negative constraints:
- no text
- no labels
- no letters
- no numbers
- no logos
- no UI frames
- no circular badges
- no square icon cards
- no inventory slot backgrounds
- no captions
- no sticker-sheet look
- no mobile-game icon style
- no flat vector art
- no cartoon toy look
- no cropped objects
- no object touching the edge
- no sparkle or watermark-like decorative mark on the objects

Rendering target:
three-quarter view, crisp edges, visible thickness, bevels, bolts, seams, gaskets, scratches, chipped paint, grime, functional cyan instrument accents, realistic material response, strong silhouette at 128 px.

If you need to show identity, use shape and material only, never text.
