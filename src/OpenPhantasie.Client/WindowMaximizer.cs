using System.Runtime.InteropServices;

namespace OpenPhantasie.Client;

/// <summary>
/// Maximizes the game window at start. MonoGame DesktopGL has no maximize call, so this asks SDL2 directly
/// (<c>SDL_MaximizeWindow</c> on <c>Window.Handle</c>, the SDL window). The SDL library MonoGame already loaded is
/// found by name; if it can't be found the window just keeps its scaled size.
/// </summary>
internal static class WindowMaximizer
{
    private static readonly string[] LibraryNames =
        ["libSDL2-2.0.so.0", "SDL2.dll", "libSDL2-2.0.0.dylib", "libSDL2.so", "SDL2"];

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SdlMaximizeWindow(IntPtr window);

    public static bool TryMaximize(IntPtr sdlWindow)
    {
        if (sdlWindow == IntPtr.Zero)
            return false;
        foreach (var name in LibraryNames)
        {
            if (!TryLoad(name, out var lib) || !NativeLibrary.TryGetExport(lib, "SDL_MaximizeWindow", out var fn))
                continue;
            Marshal.GetDelegateForFunctionPointer<SdlMaximizeWindow>(fn)(sdlWindow);
            return true;
        }
        return false;
    }

    private static bool TryLoad(string name, out IntPtr lib)
    {
        if (NativeLibrary.TryLoad(name, out lib))
            return true;
        var rid = RuntimeInformation.RuntimeIdentifier.StartsWith("osx", StringComparison.Ordinal) ? "osx"
            : $"{(OperatingSystem.IsWindows() ? "win" : "linux")}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", name);
        return NativeLibrary.TryLoad(path, out lib);
    }
}
