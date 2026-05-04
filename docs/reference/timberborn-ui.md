# Timberborn Native UI Guide

Use this guide when building Timberborn-facing Wildfire UI. The goal is not to copy Timberborn files; the goal is to structure new UXML and USS so it looks like it belongs inside Timberborn.

Timberborn UI is compact, image-backed, and class-composed. Native-looking UI comes from using the same shell slots, nine-slice containers, text classes, row patterns, state modifiers, and fixed dimensions that Timberborn uses.

## Native Authoring Rules

- Build UXML from small fragments, not from one large custom panel.
- Put new UI into existing shell slots or entity/tool-panel insertion points.
- Use `cui:*` controls for Timberborn-native behavior and rendering.
- Use `cui:Localizable*` controls for production-facing text.
- Use `cui:NineSliceVisualElement` for framed panels and subpanels.
- Stack existing USS classes first; add a Wildfire class only for missing layout or domain-specific state.
- Keep panels compact, fixed-width, and row-oriented.
- Prefer icon buttons and small state indicators over large text buttons.
- Use Timberborn background classes instead of custom colors.
- Use modifiers for state; do not duplicate base classes with new names.

## UXML File Shape

Every new Timberborn-facing UXML should follow this order:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:cui="Timberborn.CoreUI">
    <Style src="/Assets/Resources/UI/Views/Core/CoreStyle.uss" />
    <Style src="/Assets/Resources/UI/Views/Common/CommonStyle.uss" />
    <Style src="/Assets/Resources/UI/Views/Common/EntityPanel/EntityPanelCommonStyle.uss" />
    <Style src="/Assets/Resources/UI/Views/Game/EntityPanel/EntityPanelGameStyle.uss" />

    <!-- One root element. -->
</ui:UXML>
```

Use only the style sheets needed for the target surface:

- Entity fragments: `CoreStyle.uss`, `CommonStyle.uss`, `EntityPanelCommonStyle.uss`, and usually `EntityPanelGameStyle.uss`.
- Tool panels: `CoreStyle.uss`, `CommonStyle.uss`, `ToolPanelStyle.uss`, and relevant `Game/ToolPanel` styles.
- Bottom-bar buttons: `CoreStyle.uss`, `CommonStyle.uss`, and `BottomBarStyle.uss`.
- General game widgets: `CoreStyle.uss`, `CommonStyle.uss`, `GameStyle.uss`, and sometimes `GameMiscStyle.uss`.
- Modal boxes: `CoreStyle.uss`, `CommonStyle.uss`, and whichever feature stylesheet owns the body.

## Native Container Pattern

The usual native arrangement is:

1. A single root element.
2. A Timberborn background/frame class on the root.
3. Named child slots if runtime code will populate content.
4. Row containers for dense controls.
5. Small reusable child controls.
6. State expressed with modifier classes.

Example:

```xml
<cui:NineSliceVisualElement name="WildfireStatusFragment" class="entity-sub-panel bg-sub-box--green wildfire-status-fragment">
    <ui:VisualElement class="entity-panel-setting">
        <cui:LocalizableLabel name="IntensityLabel" text-loc-key="Wildfire.Intensity" class="game-text-normal entity-panel-setting__text" />
        <cui:NineSliceIntegerField name="IntensityValue" class="entity-panel-setting__input entity-panel-input" />
    </ui:VisualElement>

    <ui:VisualElement class="entity-panel__button-wrapper">
        <cui:LocalizableButton name="InspectButton" text-loc-key="Wildfire.Inspect" class="entity-fragment__button entity-fragment__button--green game-text-normal" />
    </ui:VisualElement>
</cui:NineSliceVisualElement>
```

## Entity Panel UI

Entity panel UI is the most important pattern for Wildfire.

### Where It Goes

Mount ordinary selected-object content into the entity panel's `Fragments` slot. Use `DiagnosticFragments` only for debug/dev content. Use `SideFragments` only for companion panels that should sit to the left of the main entity panel.

```text
EntityPanel
  Header
    LeftButtons
    MiddleButtons
    RightButtons
  EntityDescription
  Fragments
  DiagnosticFragments
  SideFragments
```

### Fragment Root

Use this root for most Wildfire fragments:

```xml
<cui:NineSliceVisualElement name="WildfireFragment" class="entity-sub-panel bg-sub-box--green wildfire-fragment">
    ...
</cui:NineSliceVisualElement>
```

Use `bg-sub-box` variants intentionally:

| Base         | Modifier           | Use                                             |
| ------------ | ------------------ | ----------------------------------------------- |
| `bg-sub-box` | `--green`          | Ordinary settings/status fragment               |
| `bg-sub-box` | `--blue`           | Descriptive/info fragment                       |
| `bg-sub-box` | `--red-striped`    | Danger, blocked, error, destructive warning     |
| `bg-sub-box` | `--purple-striped` | Header-like or selected-object context          |
| `bg-sub-box` | `--frame`          | Outer frame only, not a nested fragment default |

Do not put a second framed card inside an `entity-sub-panel`. Use rows directly inside the fragment.

### Label And Value Row

Use `entity-panel-setting` for label/value rows:

```xml
<ui:VisualElement class="entity-panel-setting">
    <cui:LocalizableLabel name="HeatLabel" text-loc-key="Wildfire.Heat" class="game-text-normal entity-panel-setting__text" />
    <ui:Label name="HeatValue" text="0" class="entity-panel__text" />
</ui:VisualElement>
```

For editable numeric values:

```xml
<ui:VisualElement class="entity-panel-setting">
    <cui:LocalizableLabel name="RadiusLabel" text-loc-key="Wildfire.Radius" class="game-text-normal entity-panel-setting__text" />
    <cui:NineSliceIntegerField name="RadiusInput" class="entity-panel-setting__input entity-panel-input" />
</ui:VisualElement>
```

### Toggle Row

Use Timberborn's toggle class, then add entity text sizing:

```xml
<cui:LocalizableToggle name="EnabledToggle" text-loc-key="Wildfire.Enabled" class="game-toggle entity-panel__text entity-panel__toggle" />
```

For a centered toggle:

```xml
<cui:LocalizableToggle name="EnabledToggle" text-loc-key="Wildfire.Enabled" class="game-toggle entity-panel__text entity-panel__toggle entity-panel__toggle--centered" />
```

### Action Row

Use `entity-panel__button-wrapper` and `entity-fragment__button`:

```xml
<ui:VisualElement class="entity-panel__button-wrapper">
    <cui:LocalizableButton name="StartButton" text-loc-key="Wildfire.Start" class="entity-fragment__button entity-fragment__button--green game-text-normal" />
    <cui:LocalizableButton name="StopButton" text-loc-key="Wildfire.Stop" class="entity-fragment__button entity-fragment__button--red game-text-normal" />
</ui:VisualElement>
```

Use these `entity-fragment__button` modifiers for action-button variants:

| Base                      | Modifier   | Use                                 |
| ------------------------- | ---------- | ----------------------------------- |
| `entity-fragment__button` | `--green`  | Positive or ordinary action         |
| `entity-fragment__button` | `--red`    | Destructive, stop, or danger action |
| `entity-fragment__button` | `--narrow` | Small secondary action in a row     |
| `entity-fragment__button` | `--silent` | No click sound                      |

### Header Icon Button

Use circular entity-panel buttons for header actions:

```xml
<ui:Button name="WildfireViewButton" class="entity-panel__button entity-panel__button--green">
    <ui:VisualElement class="entity-panel__button wildfire-view-icon" />
</ui:Button>
```

Add a Wildfire icon class that only sets `background-image`, width, and height. Let the native button classes own the circle, hover, and size.

## Tool Panel UI

Tool panels are narrow and centered. Use them for active-tool options, placement controls, warnings, and cost/description sections.

### Tool Panel Item

```xml
<cui:NineSliceVisualElement name="WildfireToolPanel" class="tool-panel-item bg-box--green wildfire-tool-panel">
    <cui:LocalizableLabel name="Title" text-loc-key="Wildfire.Tool.Title" class="game-text-heading text--centered" />
    <ui:VisualElement class="entity-panel-setting">
        <cui:LocalizableLabel name="RadiusLabel" text-loc-key="Wildfire.Radius" class="game-text-normal entity-panel-setting__text" />
        <cui:NineSliceIntegerField name="RadiusInput" class="entity-panel-setting__input entity-panel-input" />
    </ui:VisualElement>
</cui:NineSliceVisualElement>
```

Use these `tool-panel-item` modifiers for tool-panel variants:

| Base              | Modifier       | Use                        |
| ----------------- | -------------- | -------------------------- |
| `tool-panel-item` | `--warning`    | Warning panel              |
| `tool-panel-item` | `--map-editor` | Map-editor tool panel only |

### Placement Controls

For rotate/flip controls, follow the native icon-row pattern:

```xml
<ui:VisualElement name="PlacementControls" class="block-object-placement-panel">
    <ui:Button name="RotateClockwise" class="block-object-placement-panel__button bg-box--green">
        <ui:VisualElement class="block-object-placement-panel__button-image block-object-placement-panel__button-image--clockwise" />
        <cui:NineSliceLabel name="Binding" class="tool-panel-item__binding key-binding" />
    </ui:Button>
</ui:VisualElement>
```

## Bottom-Bar UI

Bottom-bar buttons are layered images. Keep the nested structure or the active/locked/hover states will not look native.

### Tool Button

```xml
<ui:Button name="WildfireToolButton" class="bottom-bar-button--tool bottom-bar-button--clickable">
    <ui:VisualElement name="Background" class="bottom-bar-button__background">
        <ui:VisualElement name="ToolImage" class="bottom-bar-button__icon wildfire-tool-icon">
            <ui:VisualElement name="LockIcon" class="bottom-bar-button__inner-item bottom-bar-button__lock-icon" />
        </ui:VisualElement>
        <ui:VisualElement name="Frame" picking-mode="Ignore" class="bottom-bar-button__inner-item bottom-bar-button__frame" />
    </ui:VisualElement>
</ui:Button>
```

Add these state classes at runtime:

| Base            | Modifier        | Use               |
| --------------- | --------------- | ----------------- |
| `button`        | `--active`      | Active tool       |
| `button`        | `--locked`      | Locked tool       |
| `tutorial-tool` | `--highlighted` | Tutorial emphasis |

For a hex tool, apply the hex modifier to the relevant bottom-bar block and elements:

| Base                            | Modifier     |
| ------------------------------- | ------------ |
| `bottom-bar-button`             | `--hex-tool` |
| `bottom-bar-button__background` | `--hex`      |
| `bottom-bar-button__icon`       | `--hex`      |
| `bottom-bar-button__inner-item` | `--hex`      |
| `bottom-bar-button__frame`      | `--hex`      |

## Modal And Box UI

Use `NamedBoxTemplate.uxml` for modal-like boxes instead of inventing a frame.

```xml
<ui:Template name="Box" src="/Assets/Resources/UI/Views/Common/NamedBoxTemplate.uxml" />

<ui:Instance template="Box" class="options-panel">
    <ui:AttributeOverrides element-name="Header" text-loc-key="Wildfire.Settings" />
    <cui:LocalizableLabel name="Body" text-loc-key="Wildfire.Settings.Body" class="box__text" />
    <ui:VisualElement name="Buttons" class="box-buttons">
        <cui:LocalizableButton name="Confirm" text-loc-key="Menu.Confirm" class="menu-button menu-button--medium" />
    </ui:VisualElement>
</ui:Instance>
```

Use modal boxes for settings, confirmation, and workflow screens. Do not use them for selected-entity details.

## Transient Hazard Notifications

Use `Timberborn.QuickNotificationSystem.QuickNotificationService.SendWarningNotification(...)` for one-shot player-facing hazard warnings that should appear in Timberborn's native quick-notification area without opening a custom panel. Keep these messages bounded and aggregate per update or dispatch; do not send one notification per changed cell.

Quick notifications are appropriate for status such as "new fire cells appeared" or "burnout consequences happened." Use alert-panel fragments or entity/tool-panel fragments only when the state must remain inspectable after the transient warning fades.

## Lists, Scrolls, And Rows

Rows should be separate components when they repeat. Lists should use Timberborn scroll decoration.

```xml
<ui:ScrollView name="Rows" class="game-scroll-view scroll--green-decorated wildfire-row-list" />
```

For a row:

```xml
<ui:VisualElement name="WildfireRow" class="entity-panel__row wildfire-row">
    <ui:Image name="Icon" class="wildfire-row__icon" />
    <cui:LocalizableLabel name="Label" text-loc-key="Wildfire.Row" class="entity-panel__text wildfire-row__label" />
    <ui:Label name="Value" text="0" class="entity-panel__text wildfire-row__value" />
</ui:VisualElement>
```

Use `list-view`, `panel-list-view`, and `list-view__label--padding` for menu/options style lists. Use `entity-panel__row` for selected-object detail rows.

## Dropdowns

Use the native dropdown shape:

```xml
<Timberborn.DropdownSystem.Dropdown name="ModeDropdown" class="game-dropdown wildfire-mode-dropdown" />
```

For custom dropdown UXML, preserve the native slots:

```text
Dropdown
  Label
  Selection
    SelectedItem
      SelectedItemContent
      SelectedItemOverlay
    ArrowLeft
    ArrowRight
    ArrowDown
```

For dropdown item variants:

| Base            | Modifier     | Use                      |
| --------------- | ------------ | ------------------------ |
| `dropdown-item` | `--medium`   | Medium icon/item density |
| `dropdown-item` | `--large`    | Large icon/item density  |
| `dropdown-item` | `--none`     | Empty or none option     |
| `dropdown-item` | `--selected` | Current selection        |

## Progress Bars

Use the native layered progress shape:

```text
ProgressBar
  background layer
  SimpleProgressBar fill layer
  frame layer
  ContentContainer overlay
```

Use these progress-bar color modifiers:

| Base           | Modifier  |
| -------------- | --------- |
| `progress-bar` | `--teal`  |
| `progress-bar` | `--blue`  |
| `progress-bar` | `--green` |
| `progress-bar` | `--red`   |

Put text and icons in `ContentContainer`, not beside the progress bar.

## USS For New Wildfire UI

Keep Wildfire USS narrow. Let Timberborn classes own the native look.

Good:

```css
.wildfire-row {
  flex-direction: row;
  align-items: center;
}

.wildfire-row__icon {
  width: 20px;
  height: 20px;
  margin-right: 4px;
  background-image: resource("UI/Images/Game/your-icon");
}

.wildfire-row__value {
  min-width: 40px;
  -unity-text-align: middle-right;
}
```

Avoid:

```css
.wildfire-panel {
  background-color: #123456;
  border-radius: 8px;
  padding: 24px;
}
```

That will look like a web app pasted into Timberborn.

## Class Stack Recipes

### Ordinary Entity Status Fragment

```text
cui:NineSliceVisualElement:
entity-sub-panel bg-sub-box--green wildfire-*

labels:
game-text-normal entity-panel__text

setting rows:
entity-panel-setting
entity-panel-setting__text
entity-panel-setting__input entity-panel-input
```

### Entity Warning Fragment

```text
cui:NineSliceVisualElement:
entity-sub-panel bg-sub-box--red-striped wildfire-*

title:
game-text-heading text--centered

body:
entity-panel__text entity-panel__text--centered
```

### Compact Tool Option

```text
cui:NineSliceVisualElement:
tool-panel-item bg-box--green wildfire-*

label:
game-text-normal

slider:
slider / precise-slider / integer-slider
```

### Native Button Row

```text
ui:VisualElement:
entity-panel__button-wrapper

cui:LocalizableButton:
entity-fragment__button entity-fragment__button--green game-text-normal
entity-fragment__button entity-fragment__button--red game-text-normal
```

### Native Modal Action

```text
cui:LocalizableButton:
menu-button
menu-button menu-button--medium
menu-button menu-button--stretched
```

## Modifier Catalog

Use modifiers as state and variant classes. When discussing one block or element, refer to only the modifier suffix. For example, for `bottom-bar-button`, say `--hex-tool`, not `bottom-bar-button--hex-tool`.

### Layout And Text

| Base                        | Modifiers                                                                      |
| --------------------------- | ------------------------------------------------------------------------------ |
| `content-row-centered`      | `--no-grow`                                                                    |
| `bottom-padding`            | `--medium`                                                                     |
| `box__text`                 | `--centered`                                                                   |
| `capsule-header`            | `--lower`                                                                      |
| `description-panel-section` | `--prioritized`                                                                |
| `description-text`          | `--single-section`                                                             |
| `game-text`                 | `--black`, `--red`                                                             |
| `game-text-separator`       | `--small`                                                                      |
| `list-view__label`          | `--padding`                                                                    |
| `text`                      | `--big`, `--bold`, `--centered`, `--default`, `--grey`, `--header`, `--yellow` |
| `tooltip`                   | `--not-centered`                                                               |
| `unity-text`                | `--shaded`                                                                     |

### Backgrounds And Frames

| Base            | Modifiers                                                                            |
| --------------- | ------------------------------------------------------------------------------------ |
| `bg-box`        | `--brown`, `--green`, `--red`                                                        |
| `bg-striped`    | `--green`, `--red`                                                                   |
| `bg-sub-box`    | `--blue`, `--frame`, `--green`, `--pale-purple`, `--purple-striped`, `--red-striped` |
| `square-large`  | `--brown`, `--green`, `--light-red`, `--red`, `--transparent-purple`                 |
| `sliced-border` | `--nontransparent`                                                                   |

### Buttons

| Base                 | Modifiers                                               |
| -------------------- | ------------------------------------------------------- |
| `button`             | `--active`, `--locked`                                  |
| `button-arrow-down`  | `--inverted`                                            |
| `button-arrow-left`  | `--inverted`                                            |
| `button-arrow-right` | `--inverted`                                            |
| `button-arrow-up`    | `--inverted`                                            |
| `button-checkmark`   | `--bare`                                                |
| `button-cross`       | `--bare`                                                |
| `button-minus`       | `--bare`, `--inverted`                                  |
| `button-plus`        | `--bare`, `--inverted`, `--margin`                      |
| `button-reset`       | `--bare`                                                |
| `button-square`      | `--large`, `--small`                                    |
| `menu-button`        | `--centered`, `--large-text`, `--medium`, `--stretched` |
| `reset-button`       | `--highlighted`                                         |

### Entity Panel

| Base                              | Modifiers                                  |
| --------------------------------- | ------------------------------------------ |
| `clickable`                       | `--alternate`                              |
| `debug-fragment`                  | `--margin`                                 |
| `entity-fragment__button`         | `--green`, `--narrow`, `--red`, `--silent` |
| `entity-panel__button`            | `--green`, `--red`                         |
| `entity-panel__buttons`           | `--left`, `--middle`, `--right`            |
| `entity-panel__description`       | `--hidden`, `--none`                       |
| `entity-panel__description-hider` | `--none`, `--show-icon`                    |
| `entity-panel__text`              | `--centered`, `--highlight-white`          |
| `entity-panel__toggle`            | `--centered`                               |

### Bottom Bar

| Base                            | Modifiers                                                                     |
| ------------------------------- | ----------------------------------------------------------------------------- |
| `bottom-bar-button`             | `--blue`, `--clickable`, `--green`, `--hex-tool`, `--main`, `--red`, `--tool` |
| `bottom-bar-button__background` | `--hex`                                                                       |
| `bottom-bar-button__frame`      | `--hex`                                                                       |
| `bottom-bar-button__icon`       | `--hex`                                                                       |
| `bottom-bar-button__inner-item` | `--hex`                                                                       |
| `bottom-bar-button__lock-icon`  | `--hex`                                                                       |
| `tutorial-tool`                 | `--highlighted`                                                               |

### Inputs, Toggles, Sliders, Dropdowns, And Scrolls

| Base                                 | Modifiers                                           |
| ------------------------------------ | --------------------------------------------------- |
| `dropdown-item`                      | `--large`, `--medium`, `--none`, `--selected`       |
| `export-threshold-slider`            | `--highlighted`                                     |
| `game-toggle`                        | `--small`                                           |
| `new-game-mode-panel__setting-input` | `--invalid`                                         |
| `progress-bar`                       | `--blue`, `--green`, `--no-grow`, `--red`, `--teal` |
| `scroll`                             | `--green-decorated`                                 |
| `slider-toggle`                      | `--locked`                                          |
| `slider-toggle__button`              | `--disabled`                                        |
| `slider-toggle__element`             | `--active`                                          |
| `text-field`                         | `--large`                                           |

### Tool Panel And Placement

| Base                                         | Modifiers                                                       |
| -------------------------------------------- | --------------------------------------------------------------- |
| `block-object-placement-panel__button-image` | `--clockwise`, `--counterclockwise`, `--flipped`, `--unflipped` |
| `materials-section__tool-icon`               | `--cost`                                                        |
| `tool-panel-item`                            | `--map-editor`, `--warning`                                     |
| `tool-panel-item__text-wrapper`              | `--numberless`                                                  |

### Status And Alert

| Base                                         | Modifiers                                                                        |
| -------------------------------------------- | -------------------------------------------------------------------------------- |
| `alert-panel-row`                            | `--blink`                                                                        |
| `automation-state-icon`                      | `--indicator`, `--large`, `--on`, `--small`, `--small-indicator`, `--unfinished` |
| `date-panel`                                 | `--badtide`, `--drought`                                                         |
| `hazardous-weather-notification__background` | `--badtide`, `--dry`, `--wet`                                                    |
| `hazardous-weather-notification__fade`       | `--enabled`                                                                      |
| `hazardous-weather-toggle__icon`             | `--badtide`, `--drought`, `--temperate`                                          |
| `icon`                                       | `--hidden`, `--plus`                                                             |
| `level-visibility-panel`                     | `--game`, `--map-editor`                                                         |
| `level-visibility-panel__level-button`       | `--held`                                                                         |
| `manual-migration-row__highlight`            | `--on`                                                                           |
| `panel`                                      | `--district`                                                                     |
| `task-view`                                  | `--transparent`                                                                  |
| `top-bar-counter__wrapper`                   | `--district`                                                                     |
| `top-right-item`                             | `--first-column`                                                                 |
| `weather-panel`                              | `--badtide`, `--blink`, `--dry`                                                  |
| `wellbeing`                                  | `--negative`, `--positive`                                                       |
| `wellbeing-score`                            | `--small`                                                                        |

### Production, Resource, Inventory, And Population

| Base                                            | Modifiers                                                                                                                                                                                                                     |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `beaver-buildings-batch-control-row-item__icon` | `--empty`, `--homeless`, `--jobless`                                                                                                                                                                                          |
| `beaver-buildings-fragment__icon`               | `--empty`, `--homeless`, `--jobless`                                                                                                                                                                                          |
| `beaver-buildings-fragment__item`               | `--bottom`                                                                                                                                                                                                                    |
| `described-amount`                              | `--power`, `--science`, `--workers`                                                                                                                                                                                           |
| `good-selection-box-row`                        | `--no-margin`                                                                                                                                                                                                                 |
| `good-stockpile-tooltip_text`                   | `--margin-between`                                                                                                                                                                                                            |
| `import-background`                             | `--auto`, `--disabled`, `--forced`                                                                                                                                                                                            |
| `import-icon`                                   | `--auto`, `--disabled`, `--export-all`, `--export-none`, `--forced`, `--reset`                                                                                                                                                |
| `inventory-row`                                 | `--informational`                                                                                                                                                                                                             |
| `inventory-row-informational__type`             | `--input`, `--output`                                                                                                                                                                                                         |
| `need-view`                                     | `--green`, `--red`                                                                                                                                                                                                            |
| `need-view__image`                              | `--critical`, `--marker`                                                                                                                                                                                                      |
| `population-counter__icon`                      | `--adult`, `--beds`, `--bot`, `--child`, `--contamination`, `--employment-beaver`, `--employment-bot`, `--homeless`, `--housing`, `--science`, `--unemployed-beaver`, `--unemployed-bot`, `--vacancy-beaver`, `--vacancy-bot` |
| `priority-toggle`                               | `--checked`                                                                                                                                                                                                                   |
| `production-item`                               | `--shaded`                                                                                                                                                                                                                    |
| `production-item__arrow-wrapper`                | `--left`, `--right`                                                                                                                                                                                                           |
| `production-item__icon`                         | `--or-icon`                                                                                                                                                                                                                   |
| `production-item__items`                        | `--input`                                                                                                                                                                                                                     |
| `productivity-batch-control-row-item__icon`     | `--high`, `--low`, `--medium`, `--very-high`, `--very-low`                                                                                                                                                                    |
| `recoverable-good-content`                      | `--in-box`                                                                                                                                                                                                                    |
| `resource-yield`                                | `--inactive`                                                                                                                                                                                                                  |
| `resource-yield-tooltip__text`                  | `--margin`                                                                                                                                                                                                                    |
| `resource-yield__icon`                          | `--calendar`, `--calendar-cycle`                                                                                                                                                                                              |
| `stockpile-priority-toggle__icon`               | `--accept`, `--empty`, `--obtain`, `--supply`                                                                                                                                                                                 |
| `wellbeing-bonus-tooltip__next`                 | `--background`                                                                                                                                                                                                                |
| `wellbeing-summary__bonus`                      | `--icon`, `--text`                                                                                                                                                                                                            |
| `worker-type-toggle__icon`                      | `--beaver`, `--bot`                                                                                                                                                                                                           |

### Mechanical, Automation, And Water

| Base                                  | Modifiers                                  |
| ------------------------------------- | ------------------------------------------ |
| `clutch-toggle__icon`                 | `--automated`, `--disengaged`, `--engaged` |
| `farmhouse-toggle__icon`              | `--harvesting`, `--planting`               |
| `gate-toggle__icon`                   | `--automated`, `--closed`, `--open`        |
| `sluice-toggle__icon`                 | `--auto`, `--close`, `--open`              |
| `timer-fragment__timer-progress`      | `--flipped`                                |
| `transmitter-fragment__state-label`   | `--unfinished`                             |
| `transmitter-selector`                | `--automatable`, `--automatable-none`      |
| `water-mover-toggle__icon`            | `--unfiltered`                             |
| `water-source-regulator-toggle__icon` | `--automated`, `--closed`, `--open`        |

### Menu, Options, Map, Modding, And Debug

| Base                                               | Modifiers                                                                                    |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| `batch-control-box__border`                        | `--middle`, `--right`                                                                        |
| `batch-control-box__row`                           | `--highlighted`                                                                              |
| `batch-control-box__row-item-group`                | `--green`                                                                                    |
| `batch-control-panel__tab-button`                  | `--active`                                                                                   |
| `batch-control-row-item__change-button`            | `--decrease`, `--increase`                                                                   |
| `console-panel`                                    | `--collapsed`, `--expanded`                                                                  |
| `crash-screen__input`                              | `--comment`, `--exception`                                                                   |
| `credits__label`                                   | `--supporter`                                                                                |
| `credits__name`                                    | `--centered`, `--supporter`                                                                  |
| `customizable-illuminator-fragment__color-hashtag` | `--invisible`                                                                                |
| `customizable-illuminator-preset`                  | `--selected`                                                                                 |
| `decal-button__frame`                              | `--fade`                                                                                     |
| `dev-panel-button`                                 | `--title`                                                                                    |
| `fixed-key-binding__item`                          | `--direction`, `--mouse-button`                                                              |
| `map-item__icon`                                   | `--recommended`, `--unconventional`                                                          |
| `map-selection__map-type-button`                   | `--flipped`                                                                                  |
| `mod-item__icon`                                   | `--cloud`, `--local`                                                                         |
| `mod-item__priority`                               | `--decrease`, `--increase`                                                                   |
| `new-game-mode-panel__mode`                        | `--selected`                                                                                 |
| `object-viewer-foldout`                            | `--odd`                                                                                      |
| `speed-button`                                     | `--0`, `--1`, `--2`, `--3`, `--big`, `--custom`, `--highlighted`, `--small`                  |
| `square-toggle`                                    | `--construction-guidelines`, `--natural-resources`, `--stockpile-overlay`, `--water-opacity` |
| `steam-workshop-tag`                               | `--margin`                                                                                   |
| `tutorial-panel`                                   | `--highlighted`                                                                              |
| `tutorial-step-view`                               | `--finished`                                                                                 |
| `tutorial-step-view__key-binding`                  | `--text`                                                                                     |

## Wildfire-Specific USS Rules

When a Wildfire-specific class is needed, use it only for:

- Custom icon image assignment.
- Widths for new columns.
- Row alignment unique to Wildfire data.
- Visibility/state hooks that do not already exist in Timberborn.

Use this naming:

```text
wildfire-fragment
wildfire-fragment__row
wildfire-fragment__icon
wildfire-fragment__value
wildfire-fragment--warning
wildfire-fragment--inactive
```

Do not redefine native classes such as `entity-sub-panel`, `bg-sub-box--green`, `game-text-normal`, or `entity-fragment__button--green`.

## Native UI Checklist

Before shipping a new Timberborn-facing UI:

- The root is a `cui:NineSliceVisualElement`, `ui:VisualElement`, or native Timberborn control appropriate for the target surface.
- The root uses a native background/frame class.
- The UXML has one clear root and named slots only where runtime code needs them.
- Text uses `cui:LocalizableLabel`, `cui:LocalizableButton`, or `cui:LocalizableToggle`.
- Rows use existing row classes such as `entity-panel-setting`, `entity-panel__row`, or feature-local row classes.
- Buttons use existing button families and state modifiers.
- Scrollable lists use `scroll--green-decorated`.
- Progress bars use native progress structure and color modifiers.
- USS additions are narrow and do not restyle native primitives.
- State is represented by modifiers, not by swapping whole custom class stacks.
