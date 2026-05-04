---
ticket: TWF-114
agent_level: Medium
role: researcher
requires_qa: false
doc_only: false
dependencies: []
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/world-consequence-first-pass.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-114-define-resource-fuel-flammability-catalog.md
---

# TWF-114: Define Resource Fuel Flammability Catalog

## Goal

Define the resource-level fuel, flammability, smoke, and residue catalog used by stored goods, construction materials, plants, and future explosives.

## Why

Fire consequences need consistent accounting. Logs, planks, gears, paper, and food should not all behave the same, and metal should not fuel fire. Buildings and storage should draw from the same catalog so resource burn behavior is tunable in one place.

## Requirements

- Inspect current Timberborn good/resource identifiers that can appear in construction, storage, crops, trees, and explosive-like content.
- Define catalog fields for `fuelValue`, `flammability`, `smokeProfile`, `residueQuality`, and `hazardClass`.
- Classify metal and stone-like resources as non-burnable or effectively inert fuel.
- Classify logs, planks, gears, paper, books, and similar dry goods as burnable.
- Identify food and medicine-like goods separately so they can smoke, spoil, or be destroyed without necessarily becoming high fuel.
- Identify explosive or volatile goods as hazardous and defer their pulse behavior to `TWF-116`.
- Keep catalog lookup deterministic and host-adapter-owned; do not move Timberborn good names into `Wildfire.Core`.
- Add deterministic tests for default lookup, unknown resources, non-burnable resources, and representative burnable resources.
- Document any intentionally unresolved resource names in the ticket notes before moving to verification.

## Dependencies

- None. `TWF-075` and downstream consequence tickets consume this catalog.

## Role

- Researcher or worker.
- Follow [researcher.md](../roles/researcher.md) for the resource survey and [worker.md](../roles/worker.md) if implementing catalog code.

## Implementation Notes

- Start with `docs/world-consequence-first-pass.md`, especially the stored-items section, then inspect current Timberborn resource/good identifiers from the installed game assets or repo-backed reference docs before hard-coding names.
- Keep Timberborn resource identifiers in the adapter-facing catalog layer. `Wildfire.Core` may consume normalized fuel/flammability values, but it should not know Timberborn good ids.
- Prefer a deterministic lookup shape with explicit unknown-resource behavior. Unknown goods should default to safe low fuel plus a searchable warning or unresolved list, not high hazard.
- Catalog fields should be tuned for downstream tickets: stored goods destruction in `TWF-115`, explosive pulse design in `TWF-116`, infrastructure classification in `TWF-117`, and generated scenario validation in `TWF-118`/`TWF-119`.
- Research output is valid if it records unresolved resource ids and why they are deferred. Implementation output should add tests for inert, burnable dry, food/medicine-like, hazardous, and unknown examples.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- Design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Stored Items And Explosives".
- This ticket should not implement inventory destruction; that belongs to `TWF-115`.
- This ticket should not implement explosion pulses; that belongs to `TWF-116`.
- Worker commit `f679a6b` added adapter-owned `TimberbornResourceFuelCatalog`, stockpile-source mapping overloads, catalog tests, and test-plan notes. The worker inspected installed Timberborn blueprints for 60 shipped `Good.*` ids plus construction `GoodId` references, crop/bush/tree templates, fireworks variants, dynamite/detonator ids, and recipe-only pseudo ids.
- Fresh review passed at `f679a6b`: no Timberborn good ids leaked into `Wildfire.Core`, unknown resources default conservatively to fuel `1`, flammability `0`, unresolved residue, and `Unknown` hazard, and checks passed with `git diff --check main...HEAD`, `dotnet test` with 149 tests, and `dotnet build Wildfire.slnx`.
- Downstream notes: `TWF-115` should consume the resource-id catalog overload for stored-good destruction, `TWF-116` owns explosive pulse behavior, and `TWF-117` owns explosive-like building/infrastructure classification.
