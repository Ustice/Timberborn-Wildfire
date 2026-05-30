import { describe, expect, test } from "bun:test";
import { evaluateBlockers, parseStatusTokens } from "../scripts/qa-blocker-preflight.ts";

describe("qa-blocker-preflight", () => {
  test("parses Timberborn status tokens", () => {
    const tokens = parseStatusTokens(
      "wildfire_command_result command=status success=true runtime_loaded=true tick_count=123 gpu_field_renderer_updated_tick=placeholder",
    );

    expect(tokens).toMatchObject({
      command: "status",
      gpu_field_renderer_updated_tick: "placeholder",
      runtime_loaded: "true",
      tick_count: "123",
    });
  });

  test("flags renderer counter blocker after active visual deltas", () => {
    const reports = evaluateBlockers(
      parseStatusTokens(
        [
          "runtime_loaded=true loaded_game_ready=true simulator_integrated=true",
          "gpu_field_renderer_enabled=true gpu_field_renderer_material_ready=true gpu_field_renderer_surface_bound=true",
          "last_delta_consumer_visual_effect_events=12",
          "gpu_field_renderer_visible_regions=0 gpu_field_renderer_updated_regions=0",
          "gpu_field_renderer_last_nonzero_updated_regions=0 gpu_field_renderer_max_updated_regions=0",
        ].join(" "),
      ),
    );

    expect(reports.find((report) => report.issue === 45)?.status).toBe("blocked");
  });

  test("flags burn-duration blocker when sustained heat never starts burning", () => {
    const reports = evaluateBlockers(
      parseStatusTokens(
        [
          "runtime_loaded=true loaded_game_ready=true simulator_integrated=true",
          "burn_duration_proof_status=queued burn_duration_proof_sustained_heat_complete=true",
          "burn_duration_proof_burn_start_tick=placeholder burn_duration_proof_depletion_tick=placeholder",
        ].join(" "),
      ),
    );

    expect(reports.find((report) => report.issue === 43)?.status).toBe("blocked");
    expect(reports.find((report) => report.issue === 44)?.status).toBe("blocked");
  });

  test("keeps stored-material and persistence blockers until fixture-specific proofs exist", () => {
    const reports = evaluateBlockers(
      parseStatusTokens(
        [
          "runtime_loaded=true loaded_game_ready=true simulator_integrated=true",
          "last_delta_consumer_stored_good_burn_destroyed_items=1",
          "last_delta_consumer_stored_good_burn_hazardous_goods=0",
          "contamination_fire_contaminated_burn_sources=0",
          "fertile_ash_collected_goods=0 fertile_ash_collection_depleted_cells=0",
          "last_delta_consumer_crop_burn_killed_crops=0 ash_water_washout_tainted_ash_washed=0",
        ].join(" "),
      ),
    );

    expect(reports.find((report) => report.issue === 60)?.blockers).toContain(
      "No explosive stored-good or native blast proof in this status.",
    );
    expect(reports.find((report) => report.issue === 17)?.blockers).toContain(
      "Fertile ash collection/depletion is not proven.",
    );
  });
});
