using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>The typed prompts' cursor bar (s136, events.md 4.1): on while a prompt reads, flipping every 86 ms (XEiJ).</summary>
public class P4CursorBarTests
{
    [OriginalDataFact]
    public void Block_StaysWhileTheBankAmountIsTyped_AndGoesAfter()
    {
        var game = P4Preview.StartInTown(2, GameOptions.ForPreset(GameId.P4, OptionPreset.Dos));
        var s = new P4DungeonScreen(game);
        Assert.False(s.Typing);
        Assert.False(s.CursorOn);
        game.Party.Members[0].Account = 500;
        foreach (var x in game.Party.Members.Skip(1))
            x.Account = 0; // one member to ask (as P4TownScreenTests.Bank_Withdraw_TakesTypedDigits)
        s.HandleKeys([Keys.D4]); // 4 Bank
        s.HandleKeys([Keys.D2]); // W: the amount prompt
        Assert.Equal(P4TownPrompt.Number, game.Town.Prompt);
        Assert.True(s.Typing);
        Assert.True(s.CursorOn); // s151: _numin's 8-pixel block, drawn before each key wait: steady, no blink
        s.Tick(P4DungeonScreen.CursorFlipMilliseconds + 1);
        Assert.True(s.CursorOn);
        s.Tick(P4DungeonScreen.CursorFlipMilliseconds);
        Assert.True(s.CursorOn);
        s.OnTextInput('5');
        s.HandleKeys([Keys.Enter]);
        Assert.False(s.Typing);
        Assert.False(s.CursorOn);
        s.Tick(10); // off a prompt the count stays at 0
        Assert.False(s.CursorOn);
    }
}
