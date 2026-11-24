# Dependencies and notices

CopperScreen source is MIT licensed. Its package dependencies retain their own
licenses and notices. See the locked versions in the projects' `packages.lock.json`
and the corresponding package metadata before redistributing a binary bundle.

CopperDisk and CopperMod.Amiga.Lightweight are source projects in this repository.
External package dependencies include Copper68k, Avalonia, Avalonia.Desktop,
Avalonia.Themes.Fluent, CopperPad, CopperPad.HidSharp, HotPathGuard.Abstractions,
HotPathGuard.Analyzers, Hst.Compression, Hst.Core, Miniaudio-CS and NLayer. Test-only packages include
Microsoft.NET.Test.Sdk, xUnit and its Visual Studio runner.

See [CopperDisk's notices](CopperDisk/THIRD-PARTY-NOTICES.md) for its media-decoding
components. Native runtime dependencies are supplied by the corresponding NuGet
packages. Packaging an application does not change their license obligations.

The application includes the original CopperStart A1200 boot ROM and CopperOS
HDF pinned by the firmware manifests. CopperStart is proprietary, with all rights reserved; its copyright notice
is included as `CopperStart-LICENSE.txt`. Native Kickstart ROMs, Commodore
Workbench distributions and game images are not supplied or licensed here.

## NLayer

CopperMod.Amiga.Lightweight uses NLayer to decode CD audio MP3 tracks.
[Upstream license](https://github.com/naudio/NLayer/blob/cdd16ea2a389b30f99d5797ad7b09224ab29e01a/LICENSE)

MIT License

Copyright (c) 2018 Mark Heath, Andrew Ward & Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Hst.Compression and Hst.Core

CopperScreen uses Hst.Compression's managed LhA reader for the WHDLoad launcher.
The [upstream notices](https://github.com/henrikstengaard/hst-core/blob/f3f9cefcf6903f4a7028c8656ba907dcf83913fa/src/Hst.Compression/THIRD-PARTY-NOTICES.txt)
also credit `unlzx.c` 1.1 in the separate LZX component. CopperScreen does not use
the LZX reader in this launcher.

Copyright (c) 2022-2026 Henrik Nørfjand Stengaard

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
