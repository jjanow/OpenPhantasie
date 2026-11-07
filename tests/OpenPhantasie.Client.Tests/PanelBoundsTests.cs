using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>Every modal panel fits the 150-pixel content area (13 lines of 10 px) in the worst case.</summary>
public class PanelBoundsTests
{
    private const int Max = InspectDialog.MaxLines;

    private static GameSession WorstCase()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(9));
        s.OpenGuild();
        for (var i = 0; i < RulesProfile.PhantasieI.GuildMaxCharacters; i++)
        {
            var c = s.CreateAndEnroll($"M{i:00}", i % 2 == 0 ? Race.Human : Race.Elf,
                i % 2 == 0 ? CharacterClass.Wizard : CharacterClass.Priest);
            Array.Fill(c.SpellsKnown, 1);
            c.SpellsKnown[0] = 2;
            c.MagicCur = c.MagicMax = 40;
            c.InventoryItemIds.Clear();
            c.InventoryItemIds.AddRange([101, 102, 103, 111, 112, 141, 150, 160, 121]);
            Array.Fill(c.Phantasie1.Runes, 1);
            if (i < 6)
                Assert.True(s.TryAddToParty(c, out _));
        }
        return s;
    }

    private static void AssertFits(IModalDialog d, GameSession s) =>
        Assert.True(d.Lines(s, ClientSessions.Data).Count <= Max, string.Join(" | ", d.Lines(s, ClientSessions.Data)));

    [OriginalDataFact]
    public void AllDialogs_FitTheContentArea()
    {
        var s = WorstCase();
        var menu = new PromptMenu();

        var add = new GuildAddDialog(menu, ClientSessions.Data);
        add.Open(s);
        AssertFits(add, s);

        var inspect = new InspectDialog(menu, ClientSessions.Data);
        inspect.Open(s);
        AssertFits(inspect, s);
        inspect.HandleKeys([Keys.D1], s);
        AssertFits(inspect, s);
        for (var page = 0; page < 4; page++)
        {
            inspect.HandleKeys([Keys.D1 + page], s);
            AssertFits(inspect, s);
            inspect.HandleKeys([Keys.Space], s);
        }

        s.DepartGuildToWilderness();
        var health = new HealthDialog();
        health.Open();
        AssertFits(health, s);

        var potion = new UsePotionDialog();
        potion.Open(s);
        AssertFits(potion, s);
        potion.HandleKeys([Keys.D1], s);
        AssertFits(potion, s);

        var cast = new CastDialog();
        cast.Open(s);
        AssertFits(cast, s);
        cast.HandleKeys([Keys.D1], s);
        AssertFits(cast, s);

        s.EnterTown(1);
        var learn = new LearnSpellDialog();
        learn.Open(s, s.Party.Members[0]);
        AssertFits(learn, s);

        var caster = s.Party.Members[0];
        caster.SpellsKnown[SpellId.Transportation] = 1;
        Assert.True(s.CastSpell(caster, SpellId.Transportation).Success);
        var transport = new TransportDialog();
        transport.OpenIfPending(s);
        AssertFits(transport, s);
    }
}
