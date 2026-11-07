namespace OpenPhantasie.Core;

/// <summary>
/// Which way a combatant on IV's 7 × 7 field faces (record +3; <c>FORMAT.md</c> §6.9 "The combat field"): 0 toward the
/// viewer, 1 away (up the field), 2 left, 3 right. Members start at 1 (<c>_enc_char</c>), monsters at 0. It changes only in
/// <c>_b_face</c>, after each step (the step's square delta) and for both sides of every <c>_f2f</c> call (the delta
/// toward the other, <c>_b_chkdir</c>): melee and missile attacks and the spell casts listed in <c>combat.md</c> §9.4.
/// The figure drawn is <see cref="Frame"/>.
/// </summary>
public static class P4BattleFacing
{
    public const int Front = 0, Back = 1, Left = 2, Right = 3;

    /// <summary><c>_b_face</c>: the facing after a move or turn by square delta <paramref name="delta"/> (±1, ±6, ±7, ±8).</summary>
    public static int Turn(int facing, int delta) => facing switch
    {
        Front => delta switch { -7 => Back, -8 or -1 => Left, 1 or -6 => Right, _ => facing },
        Back => delta switch { 7 => Front, -1 or 6 => Left, 1 or 8 => Right, _ => facing },
        Left => delta switch { -6 or -7 => Back, 7 or 8 => Front, 1 => Right, _ => facing },
        Right => delta switch { -1 => Left, -7 or -8 => Back, 6 or 7 => Front, _ => facing },
        _ => facing,
    };

    /// <summary><c>_b_chkdir(from, to)</c>: the square delta pointing from <paramref name="from"/> toward <paramref name="to"/> (0 if the same).</summary>
    public static int Toward(int from, int to)
    {
        if (from == to)
            return 0;
        int dc = P4Battle.Col(from) - P4Battle.Col(to), dr = P4Battle.Row(from) - P4Battle.Row(to);
        if (dc == 0)
            return dr >= 0 ? -7 : 7;
        if (dr == 0)
            return dc < 0 ? 1 : -1;
        if (dc < 0)
            return dr < 0 ? 8 : -6;
        return dr < 0 ? 6 : -8;
    }

    /// <summary>
    /// <c>_dtb</c> (<c>0x44278</c>): the sprite frame (0–4 of the record) and whether it is mirrored, for a facing; step 0
    /// is the standing pose, step 1 the walking pose shown only during the move animation (timing <c>[?]</c>).
    /// </summary>
    public static (int Frame, bool Mirrored) Frame(int facing, int step = 0) => (facing, step) switch
    {
        (Front, 0) => (0, false),
        (Front, _) => (1, false),
        (Back, 0) => (4, false),
        (Back, _) => (4, true),
        (Left, 0) => (2, false),
        (Left, _) => (3, false),
        (Right, 0) => (2, true),
        _ => (3, true),
    };
}
