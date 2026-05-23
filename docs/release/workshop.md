# Workshop Release Assets

Keep `release/` as local Steam Workshop staging output. The durable cover artwork source lives at:

- `docs/assets/workshop/wildfire-workshop-cover-source.png`

Before publishing, regenerate the ignored Workshop preview image:

```bash
magick docs/assets/workshop/wildfire-workshop-cover-source.png -resize 1920x1080^ -gravity center -extent 1920x1080 release/workshop/wildfire-workshop-cover.png
```

The generated `release/workshop/wildfire-workshop-cover.png` is referenced by the local VDF and should not be committed unless the release directory policy changes.
