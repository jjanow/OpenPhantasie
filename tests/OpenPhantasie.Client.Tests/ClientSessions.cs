using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>Real Core sessions for the Client helper tests (no MonoGame graphics involved).</summary>
internal static class ClientSessions
{
    public static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    /// <summary>A two-member party on the overworld: a fighter and a priest.</summary>
    public static GameSession Overworld(int seed = 11)
    {
        var session = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        session.OpenGuild();
        var a = session.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter);
        var b = session.CreateAndEnroll("Priesto", Race.Elf, CharacterClass.Priest);
        Assert.True(session.TryAddToParty(a, out _));
        Assert.True(session.TryAddToParty(b, out _));
        session.DepartGuildToWilderness();
        return session;
    }

    /// <summary>A normal (no surprise) wilderness fight against a weak monster.</summary>
    public static GameSession Fight(int seed = 11)
    {
        var session = Overworld(seed);
        session.BeginWildernessCombat(Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);
        return session;
    }
}
