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

## Gallery refresh — 2026-09-21

Added Super Cars II, Lotus Turbo Challenge 2, Apidya, and North & South, bringing
the gallery to nine games with one image each. The homepage features the three
new gameplay captures; North & South is explicitly labeled as a main-menu view.
Existing images remain unchanged. The preparation script uses the same full PAL
LCD viewport and proportional thumbnail transform, with source hashes checked
before conversion. These are authentic September 20 replay captures selected on
September 21, not new desktop-window captures or a claim of full-game coverage.

| Game | Capture relative to `artifacts/` | Original BMP SHA-256 |
| --- | --- | --- |
| Super Cars II | `supercars2-investigation/driving-fixed/frame-019000.bmp` | `091D8461C89A76B378284BCDD3F67A5FED1B92C276185A2BCF766A66EBE54439` |
| Lotus Turbo Challenge 2 | `game-corpus-2026-09-20/lotus2-gameplay/frame-013020.bmp` | `67E80A46F06F344D40735E1E390165FE176A8D692D7A95B5EC3BA293057444EE` |
| Apidya | `game-corpus-2026-09-20/apidya-gameplay/frame-008820.bmp` | `27DB1B829CE77D1AF43EF47B4B4438EDF917DD260EC3EE3035190A1EB2A5DCE7` |
| North & South | `north-south-investigation/cp-candidate/frame-008500.bmp` | `8BD3B202C1BDDE9928E08D9DF279B3E0E93FAE366BE45456E367129FA6896862` |

Lotus and Apidya use the `1db2d0d` engine, SHA-256
`501A3DBD82A29DEA310E308D077BB93965CB31D9F4E8E166C7050CA5166DFA02`;
see the [game corpus](engine/GAME_CORPUS_2026-09-20.md). North & South uses the
corrected Copper candidate committed as `19c525b`, engine SHA-256
`4DEF0F612624A6556F4BCF49304BDF6D9C7F1B74F9CB9A1CC53C9CDC6AEC6968`;
see its [investigation](engine/NORTH_SOUTH_INVESTIGATION.md). Super Cars II uses
the combined Copper/chip-mirror candidate accepted on September 21, engine SHA-256
`AE033B1E1F787E37ACFDFA355DFCE97EFF6F7C0433CB7CB7ED915F136DA03FC6`;
see its [driving replay](engine/SUPER_CARS_II_INVESTIGATION.md). All four use the
512 KiB chip + 512 KiB slow-RAM PAL OCS profile with Kickstart 1.3.

Keep visible copy factual. Do not claim comparative performance against other
emulators, full hardware correctness or broad compatibility without evidence.

## Games and demos refresh � 2026-09-21

Added Major Motion, Alien Breed, Miami Chase, Arte, Desert Dream and Inside the
Machine, bringing the gallery to **15 titles with one image each**. Miami Chase
is explicitly a menu capture; the three demos are identified in their captions.
The homepage features Major Motion, Alien Breed and Inside the Machine. Lemmings'
existing entry and hero image are refreshed from the corrected blitter build.
Lotus III's unresolved disk-2 prompt is not presented as gameplay.

These are authentic retained native captures with the same full PAL viewport
transform described above. No game overscan is removed and no image effects are
added. Use `scripts/prepare-website-screenshots.ps1 -CaptureRoot artifacts -Names`
with the selected image IDs to regenerate a subset without needing older captures.
Omitting `-Names` regenerates all locally reproducible entries.

| Title | Capture relative to `artifacts/` | Original BMP SHA-256 |
| --- | --- | --- |
| Major Motion | `native-corpus-2026-09-21/major-motion-play/frame-006960.bmp` | `83815F816A4A1B653DF5C7F0462C27045A59B9B6478089A220B3F75E3F4160F3` |
| Alien Breed | `native-corpus-2026-09-21/alien-breed-gameplay/frame-016380.bmp` | `9577B2BF341D4EE094D1E7DBF5766EE0ECF19157A3D1A6AAFDCA33761E3E00CA` |
| Inside the Machine | `inside-machine-investigation/full-demo/frame-003840.bmp` | `14414E2A9E9859B4C350FE0B8816B1B1BBE943B0056622EE0510C6D8335AD0EF` |
| Miami Chase | `inside-machine-investigation/miami-chase/frame-018000.bmp` | `AD116E700066DBE60D3FDBF664C9ED2583E8D4E4F06BF21C3281099A06AF3A21` |
| Arte | `native-corpus-2026-09-21/arte-boot/frame-008160.bmp` | `35D88C1F40E70307EA27E1D0177DBC15F2917B366B25EF8EF2D90A46E7010645` |
| Desert Dream | `native-corpus-2026-09-21/desert-dream-boot/frame-016380.bmp` | `E15FE7F8401AB34067C8107FE2344924EFD5436915F4B880B5C265349F34AD9A` |
| Lemmings | `inside-machine-investigation/lemmings-candidate/frame-014520.bmp` | `0919509F56690100BF0E74604EE95E55C3504DCF9007BD98F8E8565A9A146D58` |

Major Motion, Alien Breed, Arte and Desert Dream use the corpus engine
`5934338EFBB493787F29644030F0B6F9971CB4BF3972061E40D6BB2D5AF44C8F`;
see the [native corpus](engine/NATIVE_CORPUS_2026-09-21.md). Inside the Machine,
Miami Chase and the refreshed Lemmings use the final-row blitter correction,
engine `DDD6C3634ECA4DA1D53D9E8B09A49ED36D7383D44122C15B2274578FC796A365`,
accepted and committed as `c92b16b`; see the [investigation](engine/BLITTER_FINAL_MODULO.md).
Earlier capture provenance remains historical evidence. Images demonstrate these
specific scenes, not complete game/demo compatibility or a common release build.
