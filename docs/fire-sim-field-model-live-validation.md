# Fire Sim Field Model Live Validation

## 2026-05-10 Attempt

Deterministic, build, and deploy gates completed for the field-model tuning pass, but live in-game validation is not complete.

Evidence gathered:

- `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60` completed successfully and released `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`.
- Deploy reported `timberborn_running=false` before replacing `~/Documents/Timberborn/Mods/Wildfire`.
- Timberborn launched as process `81156` from `~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app`.
- The current `~/Library/Logs/Mechanistry/Timberborn/Player.log` only reached Steam connection startup lines and did not show current Wildfire mod load, command bridge, save load, or fire dispatch evidence.
- Computer Use could not attach to the Timberborn window. `get_app_state` for `Timberborn` returned `appNotFound`, and `get_app_state` for `com.mechanistry.timberborn` timed out.
- A `status` command written to `~/Library/Application Support/com.mechanistry.timberborn/WildfireQA/command-inbox.txt` was not consumed after three seconds. The command was removed after the failed attempt.
- `~/Library/Application Support/com.mechanistry.timberborn/WildfireQA/command-outbox.txt` remained a stale result from `2026-05-07T20:42:20.5128060Z`.
- The launched Timberborn process was closed after the failed validation attempt.

Live blocker:

- The game process started, but this session could not reach an interactive window or a current QA command bridge. There is no current-save proof for slower fire, reduced hitches, or burgundy contaminated smoke.

Next exact validation commands:

```bash
bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60
open "$HOME/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app"
tail -f "$HOME/Library/Logs/Mechanistry/Timberborn/Player.log"
```

After a save is loaded and `wildfire_command_bridge_ready` appears, run:

```bash
printf 'qa-readiness\n' > "$HOME/Library/Application Support/com.mechanistry.timberborn/WildfireQA/command-inbox.txt"
cat "$HOME/Library/Application Support/com.mechanistry.timberborn/WildfireQA/command-outbox.txt"
```

Then ignite a local source in-game and confirm the log/status evidence includes current `wildfire_timberborn_gpu_dispatch_kernel_completed ... elapsed_ms=...`, `wildfire_timberborn_gpu_readback_completed ... elapsed_ms=...`, non-explosive tick deltas, and visible clean versus burgundy contaminated smoke.
