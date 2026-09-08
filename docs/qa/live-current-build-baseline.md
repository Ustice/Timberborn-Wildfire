# Current-build live baseline — 2026-09-07

Actual macOS player proof of pinned source `8cbeb19`, following the [installed-build baseline](live-signedin-baseline.md). One controller gracefully exited the old game, backed up its whole mod directory and copied-save directory, built Release plus all four AssetBundles with the normal deployment script, then issued one guarded raw Steam launch. No skip-build/skip-bundle/open-game override was used. Source was a detached worktree; nothing was published.

All 40 installed files matched that pinned build, including all nested Data. Native DLL SHA-256: `00f4de117d08ea7dfb1f032885243a0594c407ea90470a0639e011ea5f02d071`; Core: `d052ad57c39d6fcfc03fe7d73f59a5d321b2a57ea08a0ea6d5c7c436ec5f15e0`. Bundle builder: licensed Unity 6000.3.6f1. Actual game: Timberborn 1.1.2.4-52e959e-xsm / Unity 6000.5.5f1 / Apple M2 Pro Metal. The solution build had zero warnings/errors; all bundle processes exited successfully. Their logs include the existing license access-token refresh notice, but no shader/UAV warning was found.

Camera Bookmarks, Hats, Wildfire, and Wildfire Asset Preview remained selected. The test used a new settlement directory, `Wildfire Current Build QA 2026-09-07`, containing a byte-identical copy of the prior lifecycle save. Original source and earlier QA saves remained unchanged.

| Actual operation | Result |
| --- | --- |
| Load the new source copy | 7,961 ms; all runtime assets loaded; 50×50×23 GPU simulator ready; WF1 tick 0 restored |
| Save `QA current 8cbeb19 lifecycle`, reload | 0.14 s save; 7,198 ms reload; Ready and tick 0 restored |
| Ordinary unpaused simulation, then pause | Tick 33 reached; no dispatch exception; no forced fire inputs |
| Save `QA current 8cbeb19 tick33`, reload | 0.11 s save; 7,034 ms reload; actual restore and diagnostic readiness both report tick 33 |
| Default release QA access | Live bridge reports `command_access=diagnostics`; read-only readiness succeeds; `qa-fire-preset default` returns `success=false message=qa_mutations_disabled` |

Both current-build saves reported `initialization_state=Ready preserved_original=false`. Final readiness reports `loaded_game_ready=true queued_changes=0 last_delta_count=0`. No runtime exception or dispatch failure appeared in the captured Player.log. The final scene is paused at tick 33; this remains a zero-beaver asset-review world, so it proves ordinary dispatch and persistence, not worker gameplay, burning consequences, or healthy-colony support. The active runtime still uses WF1; this does not activate or validate OWNED4 gameplay. Development opt-in acceptance has not yet been exercised in the player.

Evidence root: `/tmp/wildfire-current-build-live`. It includes full deploy/build logs, installed and save SHA-256 manifests, exact launch action, backups, `Player-current-lifecycle.log`, three readiness results, mutation rejection, and actual `loaded-world.png` / `reloaded-tick33-world.png` screenshots. Tick-33 save SHA-256: `7e8c82511cea4e379caacce3f0d08ba8efb4695f8c529e72ec7513174295b7b5` (165,944 bytes). The original and first copy still hash to `5bc969e4c38a6221eba9a9496a8d9be6ebd909c91b4cdda40e043236bc5c89d6`; prior lifecycle and new source copy still hash to `654f7042a8af7c5aa53e23d030bf39454be22ea0780599470b91f564995d0411`.

Next useful live coverage is disposable healthy Folktails and Iron Teeth colonies, followed by controlled worker/fire/suppression workflows. The shared controller remains the sole game/Unity owner; a bridge-ready menu alone is insufficient evidence.
