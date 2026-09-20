# Website maintenance

The public site is static HTML/CSS with progressive enhancement for the gallery.
GitHub Pages deploys `docs/` through `.github/workflows/pages.yml`. No framework,
package installation, analytics, external fonts or runtime API is required.

## Add screenshots

1. Add a full-size PNG and a smaller proportional thumbnail under
   `docs/assets/screenshots/`. Keep filenames lowercase with hyphens.
2. Keep **one screenshot per game** in `docs/gallery.json`, replacing an existing
   game's entry when choosing a newer scene. Each entry has a unique `id`, `title`, `caption`,
   useful `alt` text, `image`, `thumbnail`, full-size `width`/`height`, and
   `featured: true` if it should appear on the homepage. Array order is display
   order; the homepage shows up to three featured entries.
3. Run `node scripts/build-website.mjs` to update the marked gallery regions in
   both HTML files. Do not hand-edit those generated regions. Commit the assets,
   manifest and regenerated pages. CI also regenerates before deploying.
4. Check local references, image loading and dimensions. Preserve the complete
   application's display viewport and pixel proportions. Do not use CSS cover
   cropping, AI retouching, fake scanlines or images of unrelated builds as proof
   of current compatibility. Record new capture provenance here.

Links open full-size PNGs even without JavaScript. With JavaScript, the native
dialog supports previous/next buttons, arrow keys, Escape, focus restoration,
and an original-image link. No auto-advance or animation is used.

## Initial capture provenance

The initial three images are existing native Lightweight diagnostic captures
from the local validation work, not newly captured application-window images.
They do not establish whole-game compatibility. Source build hashes were not
attached to these individual bitmap files; do not invent a release attribution.

The preparation script selects beam coordinates
`x=196, y=26, width=712, height=285`, then duplicates each row to 712x570.
This is the application's full PAL LCD viewport (`FullViewport` in
`CopperScreenLightweightSession` uses y/height doubled); only hardware blanking
is removed. No game overscan is trimmed. Thumbnails are 356x285, nearest-neighbor.

| Image | Original local capture (relative to capture directory) | Original SHA-256 |
| --- | --- | --- |
| Lemmings gameplay | `hiredguns-sprite-repaired-probe/lemmings/frame-014520.bmp` | `7F25E37ADADBDE931C93E55EABB4CD05BC1910F2B18887B56BF9009C2D588BBB` |
| Hired Guns gameplay | `hiredguns-sprite-scheduled-probe/final-training/frame-020648.bmp` | `A23962DD19BB44D24A0B5B42D243D0371095F32445A14A5F40916A808C4A2801` |
| Hired Guns menu | `hiredguns-sprite-repaired-probe/menu/frame-009000.bmp` | `EF2B4C42053E2A0AD965AC3C4159FCA43596F26BE38208E5FD0407AF6FDB735B` |

## Gallery refresh — 2026-09-20

The gallery now contains one gameplay image each for Lemmings, Hired Guns,
Full Contact, Shadow of the Beast and Operation Thunderbolt. The Hired Guns
menu entry and its two assets were removed; its gameplay image is retained.
The homepage features the three newly added games. Its Lemmings hero image
remains the same scene.

These are existing native Lightweight replay captures from September 18–19,
selected and visually checked on September 20, not newly taken screenshots of
the September 20 application. They are bounded gameplay evidence described in
[NATIVE_VALIDATION.md](NATIVE_VALIDATION.md), not a claim of complete compatibility
or of a common release build. The Beast capture includes the Copper border fix;
Thunderbolt is from the replay after the trace/audio corrections.

Run `scripts/prepare-website-screenshots.ps1 -CaptureRoot artifacts` to reproduce
the four locally available sources below. The script checks each original BMP's
SHA-256 before applying the same full-viewport transform documented above.
It preserves the committed Hired Guns gameplay image and thumbnail because its
original BMP from the earlier machine is not available locally.

| Game | Capture relative to `artifacts/` | Original BMP SHA-256 |
| --- | --- | --- |
| Lemmings | `beam-sync-2026-09-19/native-candidate-lemmings/frame-014520.bmp` | `7F25E37ADADBDE931C93E55EABB4CD05BC1910F2B18887B56BF9009C2D588BBB` |
| Full Contact | `storage-2026-09-18/ipf-fullcontact-gameplay/frame-012000.bmp` | `3EC6CDD7135609F8EA6602BF8B1C8556866D3E343C50D02F3EE91144D0FEAEBC` |
| Shadow of the Beast | `beast-border-2026-09-18/native/frame-018120.bmp` | `1661C0201823DBA6BE9E4D233DB18BC8ED6A9F378F0FC4B33135FD71370806D8` |
| Operation Thunderbolt | `trace-exception-2026-09-18/thunderbolt-input/frame-007980.bmp` | `6C473C0B0F79CEC0874CCB5AA59F3888C1CE8723FACD73C0D1F4F510EDE88A08` |

The recent Lemmings replay's BMP is byte-identical to the initial gallery source,
so regenerating it leaves both website PNGs unchanged. All five full-size images
are 712×570 with proportional 356×285 thumbnails. The generator checks image
dimensions, paths, IDs and one entry per game.

Game imagery belongs to its respective owners. ROMs and disk images are not
website assets. Full Contact uses the Lightweight gameplay capture above, not
the old CopperStart screenshot.

Keep visible copy factual. Do not claim comparative performance against other
emulators, full hardware correctness or broad compatibility without evidence.
