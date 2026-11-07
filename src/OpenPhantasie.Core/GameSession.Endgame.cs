namespace OpenPhantasie.Core;

/// <summary>
/// P0.8 (s36) / T1.4: the session side of dungeon event scripts, the game's win and party defeat. The state lives here (saves
/// and resets handle it); the rules are the campaign's (<see cref="ICampaign"/>, Phantasie I: <see cref="Phantasie1Campaign"/>).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>An event menu waiting for <see cref="AnswerDungeonEvent"/> (OV2 <c>0x2a95</c> on the event message).</summary>
    public PendingDungeonEvent? DungeonEventPrompt { get; internal set; }

    /// <summary>
    /// III's event-text page (<see cref="DungeonTextPage"/>): the texts this step's event program has shown, and whether it
    /// waits for a key. Saved with the dungeon; a step clears it.
    /// </summary>
    public DungeonTextPage? DungeonPage { get; internal set; }

    /// <summary>
    /// The key on III's event-text page ("Press a key", Amiga <c>0x1e8c4(1)</c>: any key or a click): the stopped event
    /// program runs on (<see cref="ICampaign.ResumeDungeonScript"/>). The page goes once nothing more waits on it.
    /// </summary>
    public void AcknowledgeDungeonPage()
    {
        if (DungeonPage is not { WaitsForKey: true } page)
            throw new InvalidOperationException("No event text is waiting for a key.");
        DungeonPage = page with { WaitsForKey = false };
        if (PendingDungeonScript is { TextWait: true } && Dungeon is not null)
            Campaign.ResumeDungeonScript(this);
        if (DungeonPage is { WaitsForKey: false } && DungeonEventPrompt is null)
            DungeonPage = null;
    }

    /// <summary>The party has no non-DEAD member (state 6); call <see cref="ResolvePartyDefeat"/> for the campaign's judgement.</summary>
    public bool PartyDefeatPending { get; internal set; }

    /// <summary>The last endgame moment (victory or defeat), for the front end.</summary>
    public EndgameOutcome? LastEndgame { get; internal set; }

    /// <summary>
    /// A reward scene waiting to be shown (III's gods' reward, <see cref="RewardScene"/>), or null. The reward itself is
    /// already applied; this is only the scene, so it is not saved and a load drops it.
    /// </summary>
    public RewardScene? PendingRewardScene { get; internal set; }

    /// <summary>The front end has shown <see cref="PendingRewardScene"/> to its end.</summary>
    public void AcknowledgeRewardScene() => PendingRewardScene = null;

    /// <summary>
    /// Phantasie I's victory screen waiting to be shown (<see cref="VictoryScene"/>), or null. Like
    /// <see cref="PendingRewardScene"/> only the scene: not saved, a load drops it.
    /// </summary>
    public VictoryScene? PendingVictoryScene { get; internal set; }

    /// <summary>The front end has shown <see cref="PendingVictoryScene"/> (DOS: the key after Zeus' speech).</summary>
    public void AcknowledgeVictoryScene() => PendingVictoryScene = null;

    /// <summary>The GUILD.DAT image written on the victory screen (before DIVINE I — DOS quirk), or null.</summary>
    public IReadOnlyList<Character?>? VictoryGuildWrite { get; internal set; }

    /// <summary>The last party-defeat judgement, or null.</summary>
    public PartyDefeatResult? LastPartyDefeat { get; private set; }

    /// <summary>
    /// Party defeats in this session (not saved). II's ST counts them per program run (<c>0x231fa</c>) for the 5th-defeat
    /// sign (<see cref="PartyDefeatResult.DemonsRanOut"/>); the remaster counts per session `[~]` (on the ST a load in the
    /// same run keeps counting).
    /// </summary>
    public int PartyDefeatsThisRun { get; private set; }

    /// <summary>
    /// An event script stopped at op 6's "KEEP IT?" (<see cref="DungeonScriptEnd.OfferItem"/>): the item is the pending
    /// treasure, and <see cref="AnswerTreasure"/> runs the rest of the script from here (s58: DOS asks mid-script).
    /// </summary>
    public DungeonScriptResume? PendingDungeonScript { get; internal set; }

    /// <summary>Answer the event menu with a 1-based <paramref name="choice"/> and run the chosen script (campaign hook).</summary>
    public void AnswerDungeonEvent(int choice)
    {
        Campaign.AnswerDungeonEvent(this, choice);
        if (DungeonPage is { WaitsForKey: false } && DungeonEventPrompt is null)
            DungeonPage = null; // III: nothing more waits on the page
    }

    /// <summary>
    /// After any end of combat (rewards paid, mode restored): the dungeon cell, then a wipe → state 6 (party defeat
    /// pending); otherwise the campaign's win check (I: the Black Lord killed → state 7, endgame.md §1, §3).
    /// </summary>
    private void AfterCombatEnded(CombatEncounter encounter, CombatResult result)
    {
        AfterDungeonCombat(encounter, result); // 0x2a26 cell rewrite, 0x29a9 step back (GameSession.DungeonCells.cs)
        if (result.PartyWiped)
        {
            PartyDefeatPending = true;
            return;
        }
        Campaign.AfterCombatSurvived(this, encounter, result);
    }

    /// <summary>
    /// DOS menus are modal; Core lets a front end step away instead. A step drops an unanswered exit prompt (= NO) or event
    /// menu (the script does not run), so a later answer can never run an old cell's script from a new position.
    /// </summary>
    private void EnsureNoDungeonPrompt()
    {
        DungeonExitPending = false;
        DungeonEventPrompt = null;
        DungeonTrapPrompt = null;
        DungeonLockPrompt = null;
        PendingDungeonScript = null; // the rest of a script stopped at KEEP IT? is dropped with it
        DungeonPage = null; // III: the step routine starts a new page (Amiga 0x1bf62)
    }

    /// <summary>A dungeon trap cell (0x31e9) can also leave nobody standing → state 6.</summary>
    private void CheckDungeonWipe()
    {
        if (Party.Members.Count > 0 && Party.Members.All(m => m.Status == CharacterStatus.Dead))
            PartyDefeatPending = true;
    }

    /// <summary>Leave the dungeon with no exit prompts: the level, its pending prompts and any stopped script are gone.</summary>
    internal void ClearDungeonState()
    {
        Dungeon = null;
        DungeonExitPending = false;
        DungeonEventPrompt = null;
        DungeonTrapPrompt = null;
        DungeonLockPrompt = null;
        PendingDungeonScript = null;
        DungeonPage = null;
    }

    /// <summary>Put the party on the overworld at its current <see cref="Overworld"/> position, out of any town.</summary>
    internal void ReturnToWilderness()
    {
        CurrentTown = null;
        CurrentTownId = null;
        Mode = GameMode.Overworld;
    }

    /// <summary>
    /// Party defeat (state 6). The engine clears the fight, the dungeon, the pending prompts and the travel time
    /// ([C] OV1 <c>0x467</c> zeroes <c>[0x63f2]</c>; state 6's exits clear <c>[0x6496]</c>, s39); the campaign then judges
    /// the party and places it (I: the Astral Plane and the Great Demon, D-025 — survivors wake at OUT1 (20,5), with nobody
    /// left the game returns to town 1 with an empty party; there is no game over).
    /// </summary>
    public PartyDefeatResult ResolvePartyDefeat()
    {
        if (!PartyDefeatPending)
            throw new InvalidOperationException("The party has not been defeated.");
        PartyDefeatPending = false;
        TravelTime = 0;
        if (Options.IsDos(RuleOptionRegistry.OrdersReset))
            OrdersResets++; // D-078 dos: 0x84 resets the repeat orders to PARRY
        ClearPendingTreasure();
        Combat = null;
        ClearDungeonState();
        Overworld.TileClassFightDone = false;
        WildernessPrompt = null;
        PartyDefeatsThisRun++;
        var result = Campaign.ResolvePartyDefeat(this);
        LastPartyDefeat = result;
        LastEndgame = EndgameOutcome.PartyDefeat;
        return result;
    }
}
