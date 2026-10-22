# Third-party notices

Controlability itself is MIT licensed (see `LICENSE`). It ships with, or installs next to, the components below.
Versions are the ones in the 1.0.0-rc1 build.

## Shipped inside the app folder

| Component | Version | License | Copyright | Link |
|-----------|---------|---------|-----------|------|
| SDL3 (native `SDL3.dll`) | as packaged by ppy.SDL3-CS | zlib | Sam Lantinga and SDL contributors | https://www.libsdl.org/ |
| ppy.SDL3-CS (C# bindings) | 2026.1002.1 | MIT | ppy Pty Ltd | https://github.com/ppy/SDL3-CS |
| Nefarius.ViGEm.Client | 1.21.256 | MIT | Nefarius Software Solutions e.U. | https://github.com/ViGEm/ViGEm.NET |
| WPF-UI (and WPF-UI.Abstractions) | 4.3.0 | MIT | Leszek Pomianowski and WPF UI Contributors | https://github.com/lepoco/wpfui |
| H.NotifyIcon, H.NotifyIcon.Wpf, H.GeneratedIcons.System.Drawing | 2.4.1 | MIT | havendv | https://github.com/HavenDV/H.NotifyIcon |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | .NET Foundation and Contributors | https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.* (Hosting, DependencyInjection, Logging, Configuration, Options, FileProviders, Diagnostics, Primitives) | 10.0.13 | MIT | Microsoft Corporation | https://github.com/dotnet/runtime |
| .NET 10 runtime and Windows Desktop runtime (self-contained) | 10.0.x | MIT | Microsoft Corporation and .NET Foundation | https://github.com/dotnet/runtime, https://github.com/dotnet/wpf |

## Drivers installed separately (not part of the app)

The installer can install these two drivers if they are missing. They are separate programs with their own licenses.
They are either bundled unchanged (the official installer) or downloaded from the official release page during setup.

| Component | Version | License | Copyright | Link |
|-----------|---------|---------|-----------|------|
| ViGEmBus | 1.22.0 | BSD-3-Clause | Nefarius Software Solutions e.U. | https://github.com/nefarius/ViGEmBus |
| HidHide | 1.5.230 | MIT | Nefarius Software Solutions e.U. | https://github.com/nefarius/HidHide |

Both licenses allow redistribution of the binaries. ViGEmBus is no longer maintained by its author.

## License texts

### MIT License

Applies to every component marked MIT above. The copyright line is the one in the table for that component.

```
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

### zlib License (SDL3)

```
Copyright (C) 1997-2026 Sam Lantinga <slouken@libsdl.org>

This software is provided 'as-is', without any express or implied warranty. In no event will the authors be held
liable for any damages arising from the use of this software.

Permission is granted to anyone to use this software for any purpose, including commercial applications, and to alter
it and redistribute it freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software.
   If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is
   not required.
2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original
   software.
3. This notice may not be removed or altered from any source distribution.
```

### BSD 3-Clause License (ViGEmBus)

```
Copyright (c) Nefarius Software Solutions e.U. and contributors. All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following
   disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following
   disclaimer in the documentation and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote
   products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

The authoritative text for each driver is in its repository (`LICENSE`); check it when you update the pinned version.
