# Wildfire Workshop Thumbnail Provenance

## Asset

- Final Workshop thumbnail: `release/workshop/wildfire-workshop-thumbnail.jpg`
- Publish source image with title overlay: `release/workshop/wildfire-workshop-thumbnail.png`
- Generated background source image: `release/workshop/wildfire-workshop-thumbnail-source.png`
- Publish integration: `scripts/publish-steam-workshop.ts` uses the publish source image as its default `--source` and the JPEG as its default `--preview`.

## Dimensions And Format

- `wildfire-workshop-thumbnail.jpg`: 1920 x 1080 JPEG, 16:9, approximately 296 KB and under Steam's 1 MB preview limit.
- `wildfire-workshop-thumbnail.png`: 1280 x 720 PNG publish source with title overlay, 16:9, approximately 1.4 MB.
- `wildfire-workshop-thumbnail-source.png`: 1672 x 941 PNG generated background source, approximately 2.3 MB.

## Source Basis

This thumbnail uses an AI-generated key-art background created with Codex built-in image generation on 2026-05-24. It is not a live Timberborn screenshot and should not be used as runtime QA evidence.

No external stock image, Steam Workshop image, Timberborn screenshot, or third-party illustration was used as an input. The existing tracked source image at `docs/assets/workshop/wildfire-workshop-cover-source.png` was reviewed but not selected because the fleeing beaver could imply unproven beaver danger behavior.

The rendered title overlay was added locally with ImageMagick using the installed system Verdana Bold font. The font file is not redistributed in this repository.

## Prompt

```text
Use case: ads-marketing
Asset type: Steam Workshop key graphic / thumbnail background for a game mod named Wildfire
Primary request: Create a dramatic but honest 16:9 promotional key art background for a wildfire simulation mod for a beaver-colony survival city builder. The image should show a stylized low-poly forest edge with active orange flames moving through trees on the right, dark smoke rising into the sky, scattered embers, and gray ash/charred ground after the fire. The left side should remain darker green forest and terrain for contrast and later title placement.
Scene/backdrop: Isometric-ish game-key-art composition, close enough to game-like colony terrain to feel like a mod thumbnail, but not a fake in-game screenshot or UI.
Subject: Wildfire behavior only: fire, smoke, ash, charred vegetation, embers, and forest terrain.
Composition: 16:9 landscape, strong focal fire on the right half, readable darker negative space on the upper-left/center-left for a title overlay, high contrast, thumbnail legibility at small size.
Style: polished stylized 3D game promotional art, warm firelight, smoky atmosphere, crisp silhouettes, no photorealism.
Avoid: no animals, no beavers, no characters, no firefighting, no buckets, no water sprays, no buildings, no UI panels, no screenshots, no logos, no words, no letters, no watermarks, no feature claims, no fantasy magic.
```

## Accuracy Notes

- The image shows only wildfire visuals currently represented by accepted visual baselines: active fire, smoke, embers, ash, charred vegetation, and unaffected nearby forest.
- The image intentionally avoids beaver reactions, firefighting, faction-specific systems, buildings, resource collection, UI, and live screenshot framing.
- The asset is promotional key art, not a claim that current runtime rendering exactly matches the composition or polish.
- `TWF-101` should still provide live screenshot capture separately.
- Release metadata integration uses this asset as the default Workshop publish source and preview.

## License And Attribution Notes

- Generated asset source: Codex built-in image generation, created for this repository on 2026-05-24.
- External inputs: none.
- External attribution currently required: none identified for this asset.
- Follow-up: `TWF-061` should include this provenance file in the release-wide license and attribution pass.
