namespace OpenPhantasie.Core;

/// <summary>
/// One row of a <see cref="P4ListWindow"/> in the original's order: its text (null = the key character alone, as
/// <c>_splist</c> letters a spell it does not offer now), an optional second field (the Hall list's race) and the open
/// prompt's choice it stands for (null = none: no button).
/// </summary>
public sealed record P4ListRow(string? Text, int? Choice, string? Extra = null);

/// <summary>
/// A list the original draws as text buttons in its Command window (s151, FORMAT.md 6.9 "Lists in window 34"): the
/// spell list (<c>_getmagic</c>; "_getmagic.8" in the Magic Training Hall), Transportation's towns (<c>_cast_47</c>),
/// creation's race (<c>_racemenu</c>), Random Creature (<c>_rracemenu</c>), class (<c>_classmenu</c>) and Hall
/// (<c>_set_fame_lst</c>) lists. <paramref name="Name"/> is the Client pack's list key; <paramref name="Rows"/> are the
/// rows in the original's order, which may hold rows the prompt's choices leave out; <paramref name="HeadChoices"/> the
/// choice each text button above the rows stands for (−1 = back, SPACE). The prompt's choices and keys are unchanged: the
/// window is how the original shows them.
/// </summary>
public sealed record P4ListWindow(string Name, IReadOnlyList<P4ListRow> Rows, IReadOnlyList<int> HeadChoices)
{
    /// <summary>A list whose rows are the prompt's choices one for one.</summary>
    public static P4ListWindow Of(string name, IEnumerable<string> texts) =>
        new(name, [.. texts.Select((t, i) => new P4ListRow(t, i))], []);
}
