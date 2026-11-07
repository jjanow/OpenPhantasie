using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// IV's location-map view cycling (<c>_ianim</c>, s120; <c>FORMAT.md</c> §6.10 "Dungeon <c>_ianim</c>"): one
/// <see cref="P4DungeonAnimation"/> for the game (its counters never reset), run in real time at the measured pass costs
/// while the view is up, with <c>_iaflag</c> following the dungeon's prompt (<see cref="P4DungeonAnimation.NextRows"/>).
/// The view draws the steps' pictures over the plain view (<c>P4DungeonScreen.View3D.cs</c>). Original behaviour, so no option.
/// </summary>
public sealed partial class P4DungeonScreen
{
    private P4DungeonAnimation? _viewAnimation;
    private P4ViewAnimationRules? _viewRules;
    private P4DungeonPrompt _viewPrompt = P4DungeonPrompt.Action;
    private bool _viewCommand;

    /// <summary>The view's cycling (null without a pack that has <c>dungeon_view_animation</c>).</summary>
    public P4DungeonAnimation? ViewAnimation => _viewAnimation;

    /// <summary>One frame of real time for the location-map view.</summary>
    private void TickView(double milliseconds)
    {
        var rules = Assets?.Pack?.DungeonViewAnimation;
        if (!ReferenceEquals(rules, _viewRules))
        {
            _viewRules = rules;
            _viewAnimation = rules is null ? null : new P4DungeonAnimation(rules, new SystemRandomSource());
        }
        if (_viewAnimation is not { } a)
            return;
        if (Game.Battle is not null || !Game.LoopBuilt || View is not { } view)
        {
            a.Hide();
            return;
        }
        var command = MenuOpen; // a party menu or command screen: _dmain carries out a command
        var change = P4DungeonAnimation.NextRows(a.Rows, _viewPrompt, D.Prompt, D.InEvent, command);
        a.Show(view, Game.Party.Levitating);
        if (change.Redraw || (_viewCommand && !command))
            a.Redraw();
        a.Rows = change.Rows;
        _viewPrompt = D.Prompt;
        _viewCommand = command;
        a.Advance(milliseconds);
    }
}
