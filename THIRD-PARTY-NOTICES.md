# Dependencies and notices

CopperScreen source is MIT licensed. Its package dependencies retain their own
licenses and notices. See the locked versions in the projects' `packages.lock.json`
and the corresponding package metadata before redistributing a binary bundle.

CopperDisk and CopperMod.Amiga.Lightweight are source projects in this repository.
External package dependencies include Copper68k, Avalonia, Avalonia.Desktop,
Avalonia.Themes.Fluent, CopperPad, CopperPad.HidSharp, HotPathGuard.Abstractions,
HotPathGuard.Analyzers and Miniaudio-CS. Test-only packages include
Microsoft.NET.Test.Sdk, xUnit and its Visual Studio runner.

See [CopperDisk's notices](CopperDisk/THIRD-PARTY-NOTICES.md) for its media-decoding
components. Native runtime dependencies are supplied by the corresponding NuGet
packages. Packaging an application does not change their license obligations.

The application includes the original CopperStart A1200 boot ROM and CopperOS
HDF pinned by the firmware manifests. CopperStart is MIT licensed; its license
is included as `CopperStart-LICENSE.txt`. Native Kickstart ROMs, Commodore
Workbench distributions and game images are not supplied or licensed here.
