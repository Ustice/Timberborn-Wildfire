# QA command access in release

The native file bridge defaults to diagnostics in every build configuration. Debug builds do not implicitly grant mutation access. The command policy is selected from the game process arguments when the bridge is constructed; it is not a player setting or saved-world field.

## Available commands

| Access | Commands | Behavior |
|---|---|---|
| Diagnostics | `help`, `status`, `qa-readiness` | Report registered commands, runtime state, and loaded-world readiness. `qa-readiness` does not prepare a fixture. |
| Diagnostics | `qa-soil-moisture-range`, `qa-ash-cell <index>` | Inspect native soil moisture or one simulator ash cell without changing the world. |
| Diagnostics | `qa-borrowed-duty-status` | Report an explicit development offer and current borrowed executor ownership; no recruitment. |
| Development | `qa-borrowed-duty-arm <donor-guid> <x> <y> <z>`, `qa-borrowed-duty-cancel` | Arm one explicit workplace offer or request its cancellation. Arm also requires `--wildfire-enable-borrowed-duty`; [controller semantics](../qa/borrowed-duty-native-prototype.md). |
| Development | `qa-delta-stimulus`, `qa-building-burnout-stimulus`, `qa-burn-duration-stimulus`, `qa-stored-material-stimulus` | Force heat, burn, or stored-material consequence scenarios. |
| Development | `qa-water-suppression-stimulus`, `qa-ash-water-stimulus` | Force water/ash suppression or washout inputs. |
| Development | `qa-fire-preset`, `qa-adjust-inventory` | Change simulator tuning or native inventories. |

Only the six diagnostic commands are allowlisted. Newly registered commands require development access unless their read-only behavior is explicitly reviewed and added to the allowlist. The historical `qa-fixture-readiness` command is not implemented or registered in the current bridge; it must not be treated as safe merely because of its name.

Disabled mutation requests return `status=failure` and `message=qa_mutations_disabled` before querying runtime state or invoking a handler. They are omitted from `help` and `known_commands`. Unknown commands still return an unknown-command failure. `help` and the `wildfire_command_bridge_ready` startup log disclose `command_access=diagnostics` or `command_access=development`.

## Deliberate development access

On a copied or disposable save, start Timberborn with this exact standalone process argument:

```text
--wildfire-enable-qa-mutations
```

There is no inbox command, environment variable, settings toggle, or saved-world value that enables it. Similar spellings and `--wildfire-enable-qa-mutations=false` do not enable access. Relaunch without the argument to return to diagnostics.

For Steam, add the argument to Timberborn's Launch Options for the QA session and remove it afterward. Valve also documents the `steam://run/<appid>//<command line>/` launch format; a controller can request a single launch without changing saved Launch Options:

```bash
open 'steam://run/1062090//--wildfire-enable-qa-mutations/'
```

Respect the shared controller and launch guard before using that route. Confirm `command_access=development` in the new process log before issuing a stimulus; the URI alone does not prove the game received the argument. The one-shot route was verified in the [native Warden run](../qa/warden-native-charge-return-live.md): source `85961c2`, PID31274, fresh `command_access=development`, and a successful copied-save `qa-delta-stimulus`. Saved Steam launch options were unchanged. See [Valve's launch-parameter documentation](https://partner.steamgames.com/doc/api/ISteamApps#GetLaunchCommandLine).

## Files and packaging

The bridge uses `WildfireQA/command-inbox.txt` and `command-outbox.txt` beneath Unity's persistent data directory. On this Mac that is `~/Library/Application Support/Mechanistry/Timberborn/`. A loaded bridge reads and removes an inbox request, executes the gated command, then writes a result and UTC timestamp through a temporary outbox file. The outbox is replaced for each result. The existing single-request transport is for the sole QA controller, not concurrent clients; a stale outbox or the startup log alone does not prove that a newly loaded world consumed a command.

A pending mutation file cannot bypass diagnostic access. Development access intentionally permits forced changes, so inspect the inbox before starting a development session and use a disposable save. See [the QA runbook](../TEST_PLAN.md) for fresh-response and advancing-tick checks.

Deployment copies the mod manifest, compiled assemblies, four required macOS bundles, and selected game data directories. Package validation rejects source/test/docs/QA directories and source-code extensions. Repository launch, scenario-generation, inventory, publishing, and cleanup scripts are developer tools and are not copied into the player payload. The diagnostic bundle is intentional runtime support; bundle presence does not enable debug overlays. Visual debug visibility defaults to hidden and is controlled by the existing accepted release setting.

Offline command-policy tests establish rejection and deliberate opt-in dispatch. The opt-in copied-save stimulus is now backed by the native run above. Release acceptance still requires a fresh mutation rejection in a normal diagnostic process and a subsequent restart without the flag to verify development access does not persist. Do not close those gates from unit tests or an opted-in process.
