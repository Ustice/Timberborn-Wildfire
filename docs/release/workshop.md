# Workshop Release Assets

Keep `release/` as local Steam Workshop staging output. The durable Workshop thumbnail source and final upload preview live at:

- Source: `release/workshop/wildfire-workshop-thumbnail.png`
- Final upload preview: `release/workshop/wildfire-workshop-thumbnail.jpg`
- Generated background source: `release/workshop/wildfire-workshop-thumbnail-source.png`
- Provenance notes: `release/workshop/thumbnail-provenance.md`

Before publishing, regenerate the compressed Workshop preview image from the tracked source:

```bash
magick release/workshop/wildfire-workshop-thumbnail.png -resize 1920x1080^ -gravity center -extent 1920x1080 -strip -quality 82 release/workshop/wildfire-workshop-thumbnail.jpg
```

The reusable publish command is:

```bash
bun run workshop:publish -- --user <steam-account>
```

Build the Workshop content root before publishing:

```bash
bun run workshop:package
```

The Workshop package shape is deliberate:

- `release/workshop/content/` is the SteamCMD VDF `contentfolder`.
- `release/workshop/content/version-1.0/` is the Timberborn mod payload folder.
- `release/workshop/content/version-1.0/manifest.json` is the Timberborn mod manifest.
- The payload must include the same required shippable files as the release package, including `Scripts/`, `ComputeShaders/`, Timberborn data folders, and `Sprites/`.

Do not point the VDF `contentfolder` at `release/workshop/content/version-1.0/`, and do not upload `release/package/Wildfire-*.zip` as the Workshop item content. Steam stores the folder supplied by `contentfolder`; Timberborn then sees the versioned mod payload inside that Workshop item root.

The generated `release/workshop/wildfire-workshop-thumbnail.jpg` is referenced by the generated local VDF and is the file Steam receives as the preview. Steam rejects preview files at or above 1 MB, so the publish script generates a compressed JPG from the tracked PNG source and must not use the larger PNG as the upload preview.

Do not publish from `docs/assets/workshop/wildfire-workshop-cover-source.png`; that older cover source includes a fleeing beaver and is not the approved honest Workshop thumbnail.
