namespace OpenPhantasie.Client;

/// <summary>
/// The report shown above "YOUR PARTY HAS BEEN DEFEATED": what the step or fight that caused it logged. Every line of it
/// (s99, user: never lose content to fit): a long report (a lost fight logs 10–30 lines) makes the prompt box scroll
/// (<see cref="UiOverlays.PromptScroll"/>) rather than dropping its middle lines.
/// </summary>
public static class DefeatReport
{
    /// <summary>The report's lines, trimmed, blank ones dropped; <paramref name="bannerLines"/> is kept for the callers.</summary>
    public static List<string> Fit(IEnumerable<string> notes, int bannerLines = 1) =>
        notes.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
}
