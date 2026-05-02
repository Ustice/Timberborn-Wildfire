---
ticket: TWF-030
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-013
   - TWF-017
   - TWF-019
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-030-load-latest-save-and-unpause-utility.md
---

# TWF-030: Add Latest-Save Startup Utility

## Goal

Create a guarded Bun/TypeScript QA utility that gets Timberborn from a closed or freshly opened state into the latest loaded save, then unpauses it so live Wildfire runtime evidence can begin.

## Requirements

- Use Bun and TypeScript.
- Launch or attach to Timberborn through bundle id `com.mechanistry.timberborn`.
- Validate the documented `1920 x 1080` display assumption before clicking.
- Click through the startup Mods dialog when it appears.
- Click through the Experimental Mode Information modal when it appears.
- Continue the latest save from the main menu.
- Wait until the loaded-save HUD is visible.
- Unpause the simulation.
- Fail loudly if the expected screen is not visible before a click.
- Save screenshots for each transition: startup Mods, Experimental Mode Information, main menu, loaded save before unpause, and loaded save after unpause.
- Use only documented coordinate targets from `docs/timberborn-menu-coordinate-guide.md`, or update that guide with verified coordinates as part of this ticket if any required target is missing.
- Do not add generic menu navigation, arbitrary UI invocation, save deletion, save selection, or destructive debug commands.
- Document command usage, prerequisites, output paths, and limitations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-013` provides the coordinate-guide contract and core menu targets.
- `TWF-017` provides startup harness conventions, lock behavior, and evidence-bundle expectations.
- `TWF-019` provides the live command bridge for post-load status checks.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).
- Use the local [Timberborn QA Utility skill](../../.codex/skills/timberborn-qa-utility/SKILL.md).

## Verification

- Run `git diff --check`.
- Run the utility help or dry-run mode with `bun`.
- Run the utility against live Timberborn from a fresh launch state.
- Confirm screenshots exist and are `1920 x 1080`.
- Run `bun scripts/invoke-timberborn-command.ts status --wait=6` after unpause and capture `wildfire_command_result command=status success=true status=success simulator_integrated=true`.
- Confirm `Player.log` shows post-unpause dispatch evidence such as `wildfire_timberborn_dispatch_completed`.

## Notes

- The first version should encode the known startup path from current QA: startup Mods dialog, Experimental Mode Information modal, main menu Continue, loaded-save HUD, unpause.
- This ticket is intentionally narrower than general Timberborn menu automation.
- If Timberborn starts directly at a later state, the utility may skip already-satisfied screens only after positively identifying the current screen.

## Worker Notes - 2026-05-01

- Branch base was refreshed before final verification: `codex/TWF-030-startup-utility` fast-forwarded from `78aa0bc` to current `main` at `e8ccb3c`.
- Added `scripts/load-latest-save-and-unpause.ts`.
- The utility is intentionally narrow: it recognizes only startup Mods, Experimental Mode Information, standalone main menu, and loaded-save HUD; it clicks only `startup_mods.ok`, `experimental_mode.start`, `main.continue`, and `hud.speed1`.
- The script writes screenshots and summaries under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/<timestamp>/`.
- Added `docs/TEST_PLAN.md` usage, prerequisites, artifact path, limitations, and post-unpause status expectations.
- Added missing TWF-030 coordinate-guide targets `experimental_mode.start` and `hud.speed1` as `Visible`, not `Yes`, because TWF-030 live click verification is blocked by the macOS alert described below.

## Verification Notes - 2026-05-01

- `bun install --frozen-lockfile`: passed using the existing `bun.lock`.
- `bun run typecheck`: passed.
- `bun scripts/load-latest-save-and-unpause.ts --help`: passed.
- `bun scripts/load-latest-save-and-unpause.ts --dry-run`: passed and validated the documented `1920x1080` display assumption.
- `git diff --check`: passed.
- `dotnet test`: passed, 73 tests.
- Live command attempted: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`.
- Live run artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-01T18-55-24-865Z/`.
- Live run blocker screenshot: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-01T18-55-24-865Z/00-current-screen-18.png`.
- Blocker screenshot dimensions: `1920 x 1080`, confirmed with `sips -g pixelWidth -g pixelHeight`.
- Live run result: blocked before any click because a macOS system alert, `Unable to Connect to Jason's iPad Pro`, covered the Timberborn startup Mods dialog. The utility kept classifying the covered screen as `unknown` and did not click an undocumented system modal.
- The blocked live run was stopped manually after preserving screenshots. Its stale QA lock was removed only after confirming the script process had exited.
- `bun scripts/invoke-timberborn-command.ts status --wait=6` was not run after unpause because the live flow never reached a loaded save.
- Post-unpause `Player.log` dispatch evidence was not inspectable for this run because the live flow never reached unpause.

## Follow-Up Worker Notes - 2026-05-01

- Coordinator retry command: `bun scripts/load-latest-save-and-unpause.ts --attach --wait=240 --lock-timeout=120`.
- Retry artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-01T19-36-53-424Z/`.
- Captured retry screenshots are `1920 x 1080` and show the startup Mods dialog with Wildfire visible and enabled. `00-current-screen-01.png` also contains a real macOS `Unable to Connect to Jason's iPad Pro (2)` system alert over the upper part of the dialog.
- Root cause for the partial classifier failure: the system-alert detector used broad samples that matched ordinary startup Mods list icon/text colors, so it collapsed the visible Timberborn screen into `blocked-system-alert`.
- Added screenshot-only classification mode: `bun scripts/load-latest-save-and-unpause.ts --classify-screenshot <path>`.
- Classifier fix: the visible Timberborn screen and blocking overlay are now reported separately.
- Classification evidence: `bun scripts/load-latest-save-and-unpause.ts --classify-screenshot "~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-01T19-36-53-424Z/00-current-screen-01.png" --skip-resolution-check` returns `screen=startup-mods blocking_overlay=mac-system-alert`.
- Non-live regression checks: `docs/reference/screenshots/timberborn-menu-coordinate-guide/05-mods-menu.png` returns `screen=unknown blocking_overlay=none`; `08-post-startup-loaded-save.png` returns `screen=loaded-save blocking_overlay=none`; `10-main-menu.png` returns `screen=main-menu blocking_overlay=none`.
- Guard behavior remains conservative: live automation fails before clicking any Timberborn coordinate when `blocking_overlay=mac-system-alert` is detected.
- Coordinator cleared the stale QA lock before this classifier fix assignment. This worker did not remove locks, deploy, restart, or run live QA.
- Classifier fix verification passed: `bun run typecheck`, `git diff --check`, `bun scripts/load-latest-save-and-unpause.ts --help`, `bun scripts/load-latest-save-and-unpause.ts --dry-run --skip-resolution-check`, the target `--classify-screenshot ... --skip-resolution-check` command, and `dotnet test` with 73 passing tests.

## Worker Notes - 2026-05-02 Main-Menu Classifier Fix

- QA no-click evidence path: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-44-35-307Z/`.
- Failure screenshot: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-44-35-307Z/02-after-experimental-mode-189.png`.
- Root cause: the main-menu classifier depended on a single feedback-site row sample that is teal in the reference main-menu screenshot but beige/disabled in the QA final screenshot, so the visible main menu fell through to `screen=unknown`.
- Classifier fix: main-menu identification now requires a positive multi-row match across the documented main-menu button stack, allows either teal enabled rows or cream disabled rows, and still requires title/menu-panel/update-panel anchor samples. The utility still clicks `main.continue` only after `screen=main-menu` and still stops on blocking overlays or unknown screens.
- Evidence command: `bun scripts/load-latest-save-and-unpause.ts --classify-screenshot "~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-44-35-307Z/02-after-experimental-mode-189.png" --skip-resolution-check`.
- Evidence result: `screen=main-menu blocking_overlay=none`.
- Non-live guard checks: `docs/reference/screenshots/timberborn-menu-coordinate-guide/10-main-menu.png` reports `screen=main-menu blocking_overlay=none`, `05-mods-menu.png` reports `screen=unknown blocking_overlay=none`, and `08-post-startup-loaded-save.png` reports `screen=loaded-save blocking_overlay=none`.
- Verification passed: `bun run typecheck`, `git diff --check`, `bun scripts/load-latest-save-and-unpause.ts --help`, `bun scripts/load-latest-save-and-unpause.ts --dry-run --skip-resolution-check`, the target `--classify-screenshot ... --skip-resolution-check` command, and `dotnet test` with 73 passing tests.
- This worker did not deploy, restart Timberborn, run live QA, or take the shared QA lock.

## QA Notes - 2026-05-02 After Main-Menu Classifier Fix

- QA scope honored: no implementation changes and no board symlink moves.
- Safer attach-first precheck found Timberborn already running at the main menu with no overlay. Precheck screenshot: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/manual-precheck/current.png`; classifier output was `screen=main-menu blocking_overlay=none`; screenshot dimensions were `1920 x 1080`.
- To validate the assigned cold-launch path, QA quit the already-running main-menu instance cleanly, confirmed `timberborn_closed=true`, then ran `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`.
- Cold-launch artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-10-688Z/`.
- Startup Mods evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-10-688Z/00-current-screen-05.png`; classifier output was `screen=startup-mods blocking_overlay=none`; screenshot dimensions were `1920 x 1080`.
- Cold-launch result: failed after the documented `startup_mods.ok` click. The script released the lock and reported `Expected main-menu or loaded-save after startup gates, got startup-mods.`
- No-click post-failure evidence showed the click did advance after the script exited: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/manual-post-failure/current.png`; classifier output was `screen=experimental-mode blocking_overlay=none`; screenshot dimensions were `1920 x 1080`.
- QA continuation command, with Timberborn now at the Experimental Mode modal: `bun scripts/load-latest-save-and-unpause.ts --attach --wait=240 --lock-timeout=120`.
- Continuation artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/`.
- Continuation summary: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/latest-save-startup-summary.txt` reported `wildfire_latest_save_startup_result=pass`, `mode=attach`, and `observed_screens=experimental-mode,main-menu,loaded-save`.
- Experimental Mode evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/00-current-screen-01.png`; classifier output was `screen=experimental-mode blocking_overlay=none`; screenshot dimensions were `1920 x 1080`.
- Main-menu Continue evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/02-after-experimental-mode-01.png`; classifier output was `screen=main-menu blocking_overlay=none`; screenshot dimensions were `1920 x 1080`.
- Loaded-save evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/03-after-main-continue-13.png`; classifier output was `screen=loaded-save blocking_overlay=none`; screenshot dimensions were `1920 x 1080`.
- Before-unpause evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/04-loaded-save-before-unpause.png`; screenshot dimensions were `1920 x 1080`.
- After-unpause evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/05-loaded-save-after-unpause.png`; screenshot dimensions were `1920 x 1080`.
- Post-unpause status path: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/command-status-after-unpause.txt`.
- Post-unpause status token: `wildfire_command_result command=status success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=3 queued_changes=0 last_delta_count=0 message=ok`.
- Copied Player.log path: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-57-50-109Z/Player.log`.
- Copied Player.log dispatch evidence included `wildfire_timberborn_dispatch_completed tick=1 delta_count=0`, `wildfire_timberborn_dispatch_completed tick=2 delta_count=0`, and `wildfire_timberborn_dispatch_completed tick=3 delta_count=0`.
- Lock evidence: `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock` did not exist after the failed cold-launch run or after the passing attach continuation; final check reported `lock_released=true`.
- QA result: fail the single-command cold-launch acceptance because `--launch --wait=240 --lock-timeout=120` did not reach a pass summary. Runtime continuation evidence passes from Experimental Mode through unpause without manual clicking.
- Recommended board move: move back to `03-in-progress` for a narrow transition-wait fix after `startup_mods.ok`, then return to `04-verify` for another QA-owned cold-launch run.

## Blockers

- The main-menu classifier blocker is resolved by live continuation evidence.
- The previous cold-launch transition blocker is addressed in code by making the default `--launch` route use the fast recorded startup sequence instead of depending on transient startup Mods classifier timing.
- `experimental_mode.start`, `main.continue`, and `hud.speed1` were live-click verified by the passing continuation run.
- QA still needs to run the default cold-launch command and inspect the generated frame samples, status result, copied `Player.log`, and summary.

## Recommended Board Move

- Recommend `04-verify` after the worker checks pass and the commit is ready, so QA can rerun the default cold-launch command.

## Jason Suggestion - Fast Recorded Startup Path

- Jason observed that the fastest reliable manual flow is not perfect modal recognition: after opening Timberborn, press Enter after about one second, wait briefly, press Enter again, then move the cursor to the Continue target and click once. If the click is queued while Timberborn is still loading, Timberborn registers it at the first opportunity.
- Implement a guarded fast path that records the screen while it runs, down-samples or samples frames for evidence, and still verifies the final loaded-save HUD/status/dispatch outcome.
- The fast path should remain narrow and safe: only Enter for known startup confirmation gates and one documented `main.continue` click target, with post-run proof from screenshots/video-derived frames, command status, and `Player.log`.
- Keep the existing classifier path as a safety fallback or evidence aid, but do not make the whole cold-launch success depend on recognizing every transient loading frame.

## Worker Notes - 2026-05-02 Fast Recorded Startup Default

- Updated `scripts/load-latest-save-and-unpause.ts` so cold `--launch` is the fast recorded startup path by default, per Jason's direction. There is no extra flag for the fast path.
- The fast path is guarded to cold starts: if Timberborn is already running when `--launch` is invoked, the utility falls back to the existing classifier path instead of sending `Enter` inputs into an unknown live state.
- The fast path starts screenshot sampling before queued inputs, presses `Enter` after 1000 ms, presses `Enter` again after 1200 ms, clicks only documented `main.continue` after another 500 ms, then waits for loaded-save HUD classification.
- Evidence now includes sampled PNG frames plus `fast-frame-samples.csv`. True video capture was not added because Bun/macOS video recording is more brittle than repeatable `screencapture` samples for this utility.
- The existing classifier path, `--classify-screenshot`, loaded-save HUD check, post-unpause `status`, copied `Player.log`, dispatch evidence extraction, and summary artifact remain in place.
- Updated `docs/TEST_PLAN.md` with default command usage, cold-launch behavior, attach fallback behavior, screenshot-sampling limitation, and evidence expectations.

## Verification Notes - 2026-05-02 Fast Recorded Startup Default

- `bun run typecheck`: passed.
- `git diff --check`: passed.
- `bun scripts/load-latest-save-and-unpause.ts --help`: passed and documents default `--launch` as the fast recorded cold-start path.
- `bun scripts/load-latest-save-and-unpause.ts --dry-run --skip-resolution-check`: passed and reports `startup_path=fast-recorded` plus fast startup timings.
- `bun scripts/load-latest-save-and-unpause.ts --classify-screenshot docs/reference/screenshots/timberborn-menu-coordinate-guide/10-main-menu.png --skip-resolution-check`: passed with `screen=main-menu blocking_overlay=none`.
- `bun scripts/load-latest-save-and-unpause.ts --classify-screenshot docs/reference/screenshots/timberborn-menu-coordinate-guide/08-post-startup-loaded-save.png --skip-resolution-check`: passed with `screen=loaded-save blocking_overlay=none`.
- `bun scripts/load-latest-save-and-unpause.ts --classify-screenshot docs/reference/screenshots/timberborn-menu-coordinate-guide/05-mods-menu.png --skip-resolution-check`: passed with `screen=unknown blocking_overlay=none`.
- `dotnet test`: passed, 73 tests.
- This worker did not deploy, restart Timberborn, run live QA, or take the shared QA lock.

## QA Notes - 2026-05-02 Fast Recorded Startup Default

- QA scope honored: no implementation changes and no board symlink moves.
- Confirmed worktree commit before validation: `8e248a20db74a3cd2fff8de0373dfd35781a541a`.
- Initial preflight found no Timberborn process for bundle id `com.mechanistry.timberborn` and no shared lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`.
- Required command attempt 1: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`.
- Attempt 1 artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T01-25-05-605Z/`.
- Attempt 1 result: failed before a summary/status file with `Could not activate Timberborn bundle com.mechanistry.timberborn: 52:60: execution error: Timberborn got an error: Connection is invalid. (-609)`.
- Attempt 1 fast-path artifacts existed: `fast-frame-01.png`, `fast-frame-02.png`, and `fast-frame-samples.csv`.
- Attempt 1 released the shared lock. Post-attempt no-click precheck screenshot `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/manual-qa-fast-retry-precheck/current.png` classified as `screen=startup-mods blocking_overlay=none` and measured `1920 x 1080`.
- QA quit the half-started Timberborn instance cleanly before retrying the required cold-launch command.
- Required command attempt 2: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`.
- Attempt 2 artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T01-25-49-333Z/`.
- Attempt 2 fast-path artifacts existed: `fast-frame-01.png`, `fast-frame-02.png`, `fast-frame-03.png`, `fast-frame-04.png`, and `fast-frame-samples.csv`.
- Attempt 2 `fast-frame-samples.csv` classified every captured frame as `screen=unknown blocking_overlay=none`.
- Attempt 2 screenshot dimensions: `fast-frame-01.png`, `fast-frame-02.png`, `fast-frame-03.png`, and `fast-frame-04.png` each measured `1920 x 1080` with `sips -g pixelWidth -g pixelHeight`.
- Attempt 2 result: failed before final loaded-save screenshots, before `latest-save-startup-summary.txt`, before post-unpause status, and before copied-log dispatch proof, with `Could not activate Timberborn bundle com.mechanistry.timberborn: 52:60: execution error: Timberborn got an error: Connection is invalid. (-609)`.
- Post-failure no-click screenshot `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/manual-qa-fast-retry-postcheck/current.png` classified as `screen=startup-mods blocking_overlay=none` and measured `1920 x 1080`.
- Copied Player.log for the failed attempt: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T01-25-49-333Z/Player.log`.
- Copied Player.log had no `wildfire_timberborn_dispatch_completed`, `wildfire_command_result`, or `wildfire_timberborn_runtime_simulator_initialized` token for this run because the utility never reached a loaded save.
- Final lock check: `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock` was absent after the retry; final state `lock_released=true`.
- QA quit the half-started Timberborn instance cleanly after preserving evidence.
- QA result: fail the fast recorded cold-launch acceptance. Required pass evidence is incomplete: no summary reporting `wildfire_latest_save_startup_result=pass`, no final loaded save before/after unpause screenshots, no post-unpause `simulator_integrated=true` status, and no copied Player.log dispatch token.
- Recommended board move: move back to `03-in-progress` for a narrow fix around post-launch app activation / activation retry timing in the fast recorded path, then return to `04-verify` for another QA-owned cold-launch run.

## Worker Notes - 2026-05-02 Activation Retry Fix

- Root cause from QA cold-launch attempts: Timberborn could appear in the process list before macOS accepted `tell application id "com.mechanistry.timberborn" to activate`, returning AppleEvents `Connection is invalid. (-609)`.
- Updated the activation helper to retry for up to 20 seconds before failing. The same guarded activation path is used for launch, attach, Enter keypresses, and documented coordinate clicks.
- Kept the fast recorded path narrow: no new Timberborn UI states, no new click targets, and no destructive commands.
- Updated `docs/TEST_PLAN.md` to call out the activation readiness retry.

## Verification Notes - 2026-05-02 Activation Retry Fix

- `bun run typecheck`: passed.
- `git diff --check`: passed.
- `bun scripts/load-latest-save-and-unpause.ts --help`: passed.
- `bun scripts/load-latest-save-and-unpause.ts --dry-run --skip-resolution-check`: passed.
- `dotnet test`: passed, 73 tests.
- This worker did not deploy, restart Timberborn, run live QA, or take the shared QA lock.

## Direct Validation Notes - 2026-05-02 Fast Recovery Fix

- Jason suspected the fast path did not have enough delay between Enter presses. A direct cold-launch run with the second Enter delay raised from 1200 ms to 3000 ms showed the deeper problem: both fast inputs can still happen before Timberborn is ready to accept the startup Mods dialog.
- Updated the cold `--launch` route to keep the fast recorded inputs, then recover through the existing positively identified classifier path for any startup screen still visible afterward.
- Live command: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`.
- Live artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T01-38-48-321Z/`.
- Live result: pass. Summary reported `wildfire_latest_save_startup_result=pass`, `mode=launch`, `startup_path=fast-recorded`, and `observed_screens=unknown,startup-mods,experimental-mode,main-menu,loaded-save`.
- Post-unpause status reported `wildfire_command_result command=status success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=3`.
- Copied Player.log dispatch evidence included `wildfire_timberborn_dispatch_completed` for ticks 1, 2, and 3.
- Evidence screenshots `fast-frame-02.png`, `fast-frame-09.png`, `02-after-experimental-mode-01.png`, `03-after-main-continue-12.png`, `04-loaded-save-before-unpause.png`, and `05-loaded-save-after-unpause.png` were confirmed at `1920 x 1080`.
- Final lock check reported `lock_released=true`.

## Direct Validation Notes - 2026-05-02 Signal-Driven Startup Fix

- Replaced blind Enter timing with a signal-driven cold-start path: wait for `startup-mods`, press Enter, retry while the same modal remains, wait for `experimental-mode`, press Enter, then click Continue from `main-menu`.
- Added a documented `startup_mods.ok` fallback if repeated Enter does not clear the startup Mods gate.
- Added `num-1` speed probes only after Continue leaves the main menu, so the loaded game can begin advancing as soon as Timberborn accepts simulation-speed input.
- Fixed loaded-save classification to rely on stable top-HUD bars while excluding the in-game Mods panel overlay.
- Live command: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`.
- Live artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T01-57-57-260Z/`.
- Live result: pass. Summary path: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T01-57-57-260Z/latest-save-startup-summary.txt`.
- Post-unpause status reported `wildfire_command_result command=status success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=3`.
- Copied Player.log dispatch evidence included `wildfire_timberborn_dispatch_completed`.
- Final lock check reported `lock_released=true`.

## Integration Notes - 2026-05-02

- Integrated into the main checkout from `~/repos/wildfire-TWF-030` commit `0c6cb5f`.
- Main-checkout verification passed: `bun run typecheck`, `git diff --check`, `bun scripts/load-latest-save-and-unpause.ts --help`, screenshot classification checks for Mods panel, loaded save, and main menu, and `dotnet test` with 78 passing tests.
- Board moved to `06-done`.
