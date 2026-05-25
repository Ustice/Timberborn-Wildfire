# Wildfire Fertilize Toolbar Icons

`WildfireFertilizeToolIcon.png` is the shared Wildfire-owned toolbar asset for the combined fertile ash designation tool. The same tool is surfaced in the Fields and Forestry planting tool groups.

Sources:

- `docs/reference/assets/menu-icons/FieldsPlantingToolGroupIcon.png`
- `docs/reference/assets/menu-icons/ForestryPlantingToolGroupIcon.png`
- User-provided `fertilize.png` attachment, `106 x 91` RGBA.

Runtime copies:

- `src/Wildfire.Timberborn/Assets/WildfireFertilizeToolIcon.png`
- `docs/reference/assets/menu-icons/WildfireFertilizeToolIcon.png`

Binding note:

- `TimberbornFertilizeToolButtons.cs` creates native Timberborn tool buttons in both Fields and Forestry using the relevant group icon names as safe fallbacks, then replaces the `ToolImage` background with the embedded Wildfire-owned PNG. Both buttons point at the same combined `TimberbornFertilizeTool`.
