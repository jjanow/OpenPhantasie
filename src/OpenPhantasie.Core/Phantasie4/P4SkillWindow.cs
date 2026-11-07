namespace OpenPhantasie.Core;

/// <summary>
/// One row of <see cref="P4SkillWindow"/>: the skill's name, its value now and after one more pick (<c>_calsk</c>), and
/// the pick prompt's choice it stands for (the skill's index).
/// </summary>
public sealed record P4SkillRow(string Skill, int Now, int After, int Choice);

/// <summary>
/// <c>_sklup</c>'s level-up skill window (s152, FORMAT.md 6.9 "Level-up skill picks"): window 32 over the training hall,
/// "Training Menu" in its bar, one row per skill in skill order (<c>_sklpr</c>), <see cref="Prompt"/> ("You can train N
/// skill(s)") while a pick is open, <see cref="Trained"/> after a pick ("&lt;skill&gt; skill trained"), and
/// <see cref="Done"/> once the three picks are made (the bar then says the session is over). The prompt's choices and keys
/// are unchanged; the window is how the original shows them.
/// </summary>
public sealed record P4SkillWindow(IReadOnlyList<P4SkillRow> Rows, int Left, string? Prompt, string? Trained, bool Done)
{
    /// <summary>The Client pack's key for the window's layout.</summary>
    public const string Name = "_sklup";
}

/// <summary>What <c>_trang</c> prints under the member's name in its window (s156, FORMAT.md 6.9 "Level-up skill picks").</summary>
public enum P4TrainingNote
{
    /// <summary>"… is in poor health, / so cannot be trained." (<c>0x3FDAE</c>, <c>0x3FDBF</c> at (32, 32)).</summary>
    PoorCondition,

    /// <summary>"…, there is nothing more I can teach you." (<c>0x3FD13</c>).</summary>
    NothingMoreToTeach,

    /// <summary>"… needs" / N / "more experience points …" (<c>0x3FD34</c>, the number, <c>0x3FD47</c>).</summary>
    NeedsExperience,

    /// <summary>"…: the training will cost" / N / " gold." / "Is that all right?" (<c>0x3FD5B</c>, the number,
    /// <c>0x3FD6E</c>, <c>0x3FD8A</c>).</summary>
    Cost,
}

/// <summary>
/// <c>_trang</c>'s own window 32 (s156, FORMAT.md 6.9 "Level-up skill picks"): "Training Hall", the member's name and
/// <see cref="Note"/> (with <see cref="Amount"/>, the experience needed or the cost), and "You do not have enough gold."
/// after a YES the purse cannot pay (<see cref="ShortOfGold"/>). The original opens it again at every member pick; a
/// level-up's <see cref="P4SkillWindow"/> sits in its lower part. Its lines are also the text column's.
/// </summary>
public sealed record P4TrainingWindow(string Member, P4TrainingNote Note, long Amount, bool ShortOfGold)
{
    /// <summary>The Client pack's key for the window's layout.</summary>
    public const string Name = "_trang";
}
