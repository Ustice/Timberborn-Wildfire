# Wildfire Fertilize Toolbar Icons

`WildfireFertilizeCropsToolIcon.png` and `WildfireFertilizeTreesToolIcon.png` are derived Wildfire-owned toolbar assets for `TWF-163`.

Sources:

- `docs/reference/assets/menu-icons/FieldsPlantingToolGroupIcon.png`
- `docs/reference/assets/menu-icons/ForestryPlantingToolGroupIcon.png`
- `docs/reference/assets/goods-icons/FertileAshIcon.png`

Runtime copies:

- `src/Wildfire.Timberborn/Assets/WildfireFertilizeCropsToolIcon.png`
- `src/Wildfire.Timberborn/Assets/WildfireFertilizeTreesToolIcon.png`

Binding note:

- `TimberbornFertilizeToolButtons.cs` still creates native Timberborn tool buttons using the relevant Fields or Forestry group icon names as safe fallbacks, then replaces the `ToolImage` background with the embedded Wildfire-owned PNG. This mirrors the existing Wildfire burn-tool icon path and avoids depending on unresolved mod sprite lookup behavior for `ToolButtonFactory.Create`.
