using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenPhantasie.Client.Updates;

/// <summary>
/// The running build's version and the release it updates from (s174). A release build gets its version from CI
/// (<c>-p:Version=1.2.3</c>, from the <c>v1.2.3</c> tag); a local build is <c>0.0.0-dev</c> and never updates itself.
/// </summary>
public static class AppVersion
{
    private static readonly Assembly Self = typeof(AppVersion).Assembly;

    /// <summary>The informational version without its <c>+commit</c> suffix (<c>1.0.0</c>, <c>0.0.0-dev</c>).</summary>
    public static string Text { get; } = Strip(
        Self.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0-dev");

    /// <summary>Parsed <see cref="Text"/> (0.0.0 when it does not parse).</summary>
    public static Version Current { get; } = TryParse(Text) ?? new Version(0, 0, 0);

    /// <summary>A build CI made from a release tag: only those check for and apply updates.</summary>
    public static bool IsRelease { get; } = !Text.Contains('-') && Current > new Version(0, 0, 0);

    /// <summary>The GitHub repository releases come from (<c>owner/name</c>; the project's <c>UpdateRepository</c> metadata).</summary>
    public static string Repository { get; } =
        Self.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "UpdateRepository")?.Value
        ?? "jjanow/OpenPhantasie";

    /// <summary>The releases page, for a manual download.</summary>
    public static string ReleasesPage => $"https://github.com/{Repository}/releases";

    /// <summary>
    /// The release archive this machine takes: <c>win-x64</c>, <c>linux-x64</c>, <c>osx-x64</c> or <c>osx-arm64</c>
    /// (also <c>-arm64</c> on Windows / Linux, built only if a release offers it). Null on any other OS.
    /// </summary>
    public static string? PlatformId { get; } = ComputePlatformId(OSPlatformName(), RuntimeInformation.OSArchitecture);

    internal static string? OSPlatformName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
        : null;

    public static string? ComputePlatformId(string? os, Architecture arch)
    {
        var a = arch switch { Architecture.X64 => "x64", Architecture.Arm64 => "arm64", _ => null };
        return os is null || a is null ? null : $"{os}-{a}";
    }

    /// <summary><c>1.2.3+abc</c> → <c>1.2.3</c>.</summary>
    public static string Strip(string informational)
    {
        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }

    /// <summary>A release tag or version (<c>v1.2</c>, <c>1.2.3</c>) as a <see cref="Version"/> with at least three parts; null if it has a suffix or doesn't parse.</summary>
    public static Version? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.Trim();
        if (t.StartsWith('v') || t.StartsWith('V'))
            t = t[1..];
        if (t.Contains('-') || !Version.TryParse(t, out var v))
            return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }
}
