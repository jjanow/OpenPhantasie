# Third-party software

OpenPhantasie's own code is under the MIT licence (`LICENSE`). The source tree contains no third-party code; the
packages below are fetched by NuGet at build time, and builds of the game redistribute the runtime ones.

| Package | Used by | Licence |
|---|---|---|
| MonoGame.Framework.DesktopGL 3.8 | the game client (graphics, input, audio) | Microsoft Public License (MS-PL) |
| MonoGame.Content.Builder.Task 3.8, dotnet-mgcb tools | the client's content build | MS-PL |
| SDL2 (native library shipped inside MonoGame.Framework.DesktopGL) | window, input | zlib licence |
| OpenAL Soft (native library shipped inside MonoGame.Framework.DesktopGL) | audio output | GNU LGPL 2.1 (dynamically linked) |
| xunit, xunit.runner.visualstudio | tests only | Apache License 2.0 |
| Microsoft.NET.Test.Sdk | tests only | MIT |
| coverlet.collector | tests only | MIT |

MonoGame's MS-PL is not compatible with the GPL, which is why OpenPhantasie is MIT-licensed. When you redistribute a
build, keep the licence files of the packages above with it (MonoGame's NuGet packages include them).

The Phantasie games themselves (their programs, text, maps, pictures and music) are not part of this project. The
importer reads them from the player's own copy at run time and keeps what it builds on the player's machine.
