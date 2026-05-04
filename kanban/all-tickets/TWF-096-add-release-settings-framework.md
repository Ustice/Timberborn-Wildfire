---
ticket: TWF-096
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-046
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/reference/timberborn-ui.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-096-add-release-settings-framework.md
---

# TWF-096: Add Release Settings Framework

## Goal

Add the stable Timberborn-facing settings owner or equivalent settings framework for release settings.

## Why

Specific settings should not be scattered through ad hoc debug toggles. Wildfire needs stable keys, conservative defaults, and one clear interpretation path.

## Requirements

- Add the narrowest Timberborn-native settings surface or settings owner.
- Define stable keys, defaults, and parsing behavior.
- Keep player preferences in settings, not save data.
- Add deterministic tests for setting defaults and interpretation.
- Document the settings surface in `docs/TEST_PLAN.md` or `docs/reference/timberborn-ui.md` where durable.

## Dependencies

- `TWF-046` proves the live loop that settings will control.

## Parent Reference

- Parent gate: `TWF-048`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start by checking `docs/reference/modding-guide.md` and `docs/reference/timberborn-ui.md` for the current Timberborn 1.0 settings pattern, then bind the smallest settings owner from `src/Wildfire.Timberborn/WildfireConfigurator.cs` or a nearby Timberborn adapter module.
- Keep settings interpretation in `Wildfire.Timberborn`; do not persist player preferences into save data and do not make `Wildfire.Core` depend on Timberborn setting APIs.
- Define stable keys and conservative defaults before adding child settings. This framework ticket should provide the owner, parse/default behavior, status/log visibility, and unit-testable interpretation contract.
- If a native settings UI dependency is missing or incompatible, block safely with the missing type/API and a recommended fallback owner instead of inventing a parallel settings store.
- Evidence should include deterministic tests for defaults and invalid values, a build, and either docs or QA status/log tokens that show where later settings will report their active values.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- Specific enable/disable, behavior, and visual/debug settings belong to child tickets.
- Worker commit `70966da` added adapter-owned `WildfireReleaseSettings`, bound it in `WildfireConfigurator`, added deterministic settings tests, and documented the release settings surface.
- Settings owner stays in `Wildfire.Timberborn`; `Wildfire.Core` remains host-agnostic. Key prefix: `JasonKleinberg.Wildfire.release.`; framework key: `JasonKleinberg.Wildfire.release.settings_schema_version`; default schema version: `1`.
- Worker found `Timberborn.SettingsSystem.ISettings` in installed Timberborn assemblies but did not find `ModSettingsOwner` / `ModSetting<T>`, so this ticket uses a narrow interpretation owner and leaves UI/dependency choices to child tickets.
- Moved to `04-verify`; requires fresh review before integration.
- Fresh review passed at `70966da`: `git diff --check main...HEAD`, `dotnet test`, and `dotnet build Wildfire.slnx` passed. Reviewer confirmed the settings owner stays Timberborn-adapter-local, uses `ISettings`, keeps preferences out of save data, and does not introduce a `Wildfire.Core` dependency on Timberborn settings APIs.
- Residual risk accepted for child tickets: no live Timberborn QA was required here; the next child setting should prove the runtime binding/log token in-game.
