# Website maintenance

The public site is static HTML/CSS with progressive enhancement for the gallery.
GitHub Pages deploys `docs/` through `.github/workflows/pages.yml`. No framework,
package installation, analytics, external fonts or runtime API is required.

## Add screenshots

1. Add a full-size PNG and a smaller proportional thumbnail under
   `docs/assets/screenshots/`. Keep filenames lowercase with hyphens.
2. Add an entry to `docs/gallery.json` with a unique `id`, `title`, `caption`,
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

`scripts/prepare-website-screenshots.ps1 -CaptureRoot <local capture directory>`
reproduces the PNGs from the original BMPs. It selects beam coordinates
`x=196, y=26, width=712, height=285`, then duplicates each row to 712x570.
This is the application's full PAL LCD viewport (`FullViewport` in
`CopperScreenLightweightSession` uses y/height doubled); only hardware blanking
is removed. No game overscan is trimmed. Thumbnails are 356x285, nearest-neighbor.

| Image | Original local capture (relative to capture directory) | Original SHA-256 |
| --- | --- | --- |
| Lemmings gameplay | `hiredguns-sprite-repaired-probe/lemmings/frame-014520.bmp` | `7F25E37ADADBDE931C93E55EABB4CD05BC1910F2B18887B56BF9009C2D588BBB` |
| Hired Guns gameplay | `hiredguns-sprite-scheduled-probe/final-training/frame-020648.bmp` | `A23962DD19BB44D24A0B5B42D243D0371095F32445A14A5F40916A808C4A2801` |
| Hired Guns menu | `hiredguns-sprite-repaired-probe/menu/frame-009000.bmp` | `EF2B4C42053E2A0AD965AC3C4159FCA43596F26BE38208E5FD0407AF6FDB735B` |

Game imagery belongs to its respective owners. ROMs and disk images are not
website assets. The old CopperStart Full Contact screenshot is not used to
represent current application support.

Keep visible copy factual. Do not claim comparative performance against other
emulators, full hardware correctness or broad compatibility without evidence.
