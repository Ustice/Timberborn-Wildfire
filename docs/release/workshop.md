# Workshop Release Assets

Keep `release/` as local Steam Workshop staging output. The durable cover artwork source lives at:

- `docs/assets/workshop/wildfire-workshop-cover-source.png`

Before publishing, regenerate the ignored Workshop preview image:

```bash
magick docs/assets/workshop/wildfire-workshop-cover-source.png -resize 1920x1080^ -gravity center -extent 1920x1080 -strip -quality 82 release/workshop/wildfire-workshop-cover.jpg
```

The reusable publish command is:

```bash
bun run workshop:publish -- --user <steam-account>
```

The generated `release/workshop/wildfire-workshop-cover.jpg` is referenced by the generated local VDF and should not be committed unless the release directory policy changes. Steam rejects preview files at or above 1 MB, so the publish script generates a compressed JPG from the tracked PNG source.
