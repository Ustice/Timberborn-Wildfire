> Historical snapshot archived on 2026-09-04 from repository base `551e8ef`. The dates inside this record identify its original observations; the archive date is not a new validation run. Implementation descriptions, commands, statuses, and instructions below may be superseded. Use the [current documentation index](../../INDEX.md) for current guidance.

# FireSim Field Model Design Notes

This record preserves the proposed field model and the dated observations from its first implementation attempt. It describes the historical design, not the current shader contract.

## Field Semantics

Heat is a short-range reaction driver that affects ignition and burn pressure. Smoke comes from hot fuel and burning material; steam comes from heat meeting water or moisture and decays faster than smoke. Ash is persistent aftermath from fuel loss and smoke settling. Contamination travels with smoke and ash rather than as an independent fluid, and clean smoke does not create contamination.

The original memory design used paired packed reaction buffers, paired packed atmospheric buffers, read-only companion material metadata, and presentation-only visual fields. Atmospheric amounts shared one uint per cell.

## Proposed Tuning Parameters

The original proposal named five tuning concepts: HeatReach for heat radius, WindStretch for downwind elongation, AtmosphericDrift for transported fields, AtmosphericDecay for their decay, and ContaminationCarry for contaminated sources. These were design concepts; they are not a claim that the current runtime exposes five matching parameters.

## Heat Kernel

A source without wind contributes radially symmetric heat within its reach:

```text
t = distance / HeatReach
weight = t < 1 ? (1 - t)^2 : 0
```

The proposed wind model stretches the source's outgoing reach downwind and shortens it upwind:

```text
headReach = HeatReach * (1 + WindStretch * windStrength)
backReach = HeatReach / (1 + WindStretch * windStrength)
e = (headReach - backReach) / (headReach + backReach)
a = (headReach + backReach) / 2
p = a * (1 - e^2)
reach(theta) = p / (1 - e * cos(theta))
cos(theta) = dot(normalized source-to-target horizontal direction, normalized wind direction)
t = distance / reach(theta)
weight = t < 1 ? (1 - t)^2 : 0
```

The source determines the outgoing shape. Equal-distance directions match in calm conditions; wind increases downwind pressure relative to upwind pressure.

## Atmospheric Transport And Contamination

The proposal replaced nearest-neighbor maximum propagation with a small directional transport neighborhood, initially radius 1 or 2. Steam generation uses wet-hot cells; smoke moves with wind; ash persists longer than smoke. Fuel duration is a distinct property from the directional heat kernel.

The original source classification read CompanionContaminationBehavior and CompanionAshQuality:

```text
sourceContamination =
  contaminated material or badwater behavior ? high :
  toxic ash quality ? high :
  normal material ? none
```

The historical proposal combined contamination by maximum:

```text
smokeContamination = max(transportedSmokeContamination, smokeSourceContamination)
ashContamination = max(existingAshContamination, ashSourceContamination, depositedSmokeContamination)
```

These formulas record the proposal, including its smoke mixing assumption; the current shader is authoritative for implemented mixing and deposition behavior. Contaminated smoke becoming contaminated ash is the durable provenance requirement.

## Live Validation History

### 2026-05-10 Field-Model Attempt

Deterministic, build, and deploy gates completed for the field-model tuning pass, but live in-game validation did not complete.

Evidence gathered:

- `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60` completed successfully and released `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`.
- Deploy reported `timberborn_running=false` before replacing `~/Documents/Timberborn/Mods/Wildfire`.
- Timberborn launched from `~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app`.
- The current `~/Library/Logs/Mechanistry/Timberborn/Player.log` only reached Steam connection startup lines and did not show current Wildfire mod load, command bridge, save load, or fire dispatch evidence.
- Computer Use could not attach to the Timberborn window.
- A `status` command written to `~/Library/Application Support/com.mechanistry.timberborn/WildfireQA/command-inbox.txt` was not consumed after three seconds, and `command-outbox.txt` remained stale.
- The launched Timberborn process was closed after the failed validation attempt.

Live blocker:

- The game process started, but the session could not reach an interactive window or a current QA command bridge. There was no current-save proof for slower fire, reduced hitches, or burgundy contaminated smoke.
