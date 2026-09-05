// Shared by local staging and package validation. This is an API compatibility
// floor, not evidence that a packaged build has passed live-game validation.
export const releaseManifestIdentity = {
  Name: "Wildfire",
  Version: "0.1.0.0",
  Id: "JasonKleinberg.Wildfire",
  MinimumGameVersion: "1.1.2.4",
} as const;
