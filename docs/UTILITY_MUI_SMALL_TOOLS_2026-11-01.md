# Four small MUI utility additions — 2026-11-01

The collection now includes four more original classic m68k MUI utilities.
All four executables are below 23 KB. Acquisition, documentation and integrity
checks were completed before execution. Subsequent [native A/B testing](UTILITY_MUI_SMALL_TOOLS_AB_2026-11-01.md)
now records compatibility blockers for all four; acquisition is not a runtime pass.

| Utility | Archive / executable bytes | Documented requirements | Proposed bounded test |
| --- | ---: | --- | --- |
| [MUImem 1.1](https://aminet.net/package/util/wb/MUImem) | 8,747 / 21,756 | MUI; exact minimum version unspecified. Readme mentions MUI 3 features. Binary references graphics, icon and mathffp libraries. Optional Retina support stays disabled. | Check memory display/refresh against guest AvailMem observations, then repeated close/reopen. |
| [MUIJoin 1.0](https://aminet.net/package/util/wb/MUIJoin10) | 21,652 / 22,220 | AmigaOS 2.0+, MUI 2.3+; binary references ASL. | Split and rejoin a deterministic scratch file; compare output bytes/hash, list ordering and requester cancellation. |
| [MUIMenu 1.00](https://aminet.net/package/util/wb/MUIMenu) | 29,280 / 22,620 | Workbench 2.0+, MUI 2.0+/classes. Original C source included. | Configure two buttons for harmless guest commands and verify distinct launch/output witnesses; close and reopen. |
| [MUI Icon Update 1.2](https://aminet.net/package/util/wb/MUI_IconUpd12) | 26,479 / 19,260 | MUI 2.0, icon.library and classic icon support. ARexx is a separate optional scenario. Original source included. | Replace scratch icon imagery while preserving tooltypes/default tool; reload and inspect image and metadata. |

These offer different test surfaces: live numeric updates, lists/string fields
and filesystem I/O, dynamic launcher layout/child processes, and image/checkbox/
icon metadata handling. The distributions do not list external MCC packages;
this is documentation review, not proof that every required class/API is
implemented by CopperOS. The m68k classification also does not establish a
68000 CPU minimum.

Suggested order is MUIMenu, MUImem, MUIJoin, then MUI Icon Update. Reuse the
explicit 5 MiB MUI diagnostic lane and native Kickstart 3.1 reference only after
recording each program's actual prerequisites. Keep the product preset and
qualified ROM pins unchanged. Missing APIs/classes are compatibility findings,
not permission to count an unavailable scenario as a pass.

Use only generated/copied scratch files for join/split and icon tests. Memory
totals need comparison with each ROM's own guest observations; absolute free
memory need not match across OS implementations. No personal icons, real disk
copies or system preferences are modified by this acquisition.

## Original distributions and terms

- **MUImem:** Roland Fulde calls it freeware. Retain all five archive members;
  no open-source licence is inferred.
- **MUIJoin:** Roberto Fabbri's emailware. Its guide permits intact
  noncommercial redistribution; commercial distribution needs permission.
  Retain all seven members. No email is sent by this workflow.
- **MUIMenu:** Giuseppe Chiesa's giftware. Redistribution must retain the entire
  unchanged archive. Preserve all seven members, including source and docs.
- **MUI Icon Update:** Robert Poole's giftware. Preserve all 14 members. Its
  documentation permits intact distribution but prohibits redistribution of
  modified source/binaries; source recompilation is for personal use.

Primary package descriptions are the linked Aminet pages and their original
readmes. Requirements and terms were also checked in `MUIJoin.guide`,
`MUIMenu.doc` and `MUI_IconUpdate.doc` inside the original distributions. MUIMenu's
1.00 version is present in both its supplied source and executable string.

The [inspection receipt](UTILITY_MUI_SMALL_TOOLS_2026-11-01.json) records archive,
readme, executable and member hashes. Every LHA member was decompressed and its
stored CRC16 verified. Complete original archives and readmes are kept locally
in `media/utilities/originals/`, outside source control. No patched executable,
recompiled third-party program or original MUI installation is included.

Repeat download/hash verification with:

```powershell
./scripts/get-utility-test-media.ps1 -Id mui-mem-11,mui-join-10,mui-menu-100,mui-icon-update-12 -VerifyOnly
```

Omit `-VerifyOnly` to acquire missing originals. The manifest now points to the
subsequent native A/B report and its explicit failed/partial scopes. The inspection
receipt remains the original acquisition evidence.
