using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Loads a pack's <c>dungeon_view_animation</c> (IV's <c>_ianim</c>, s120; <c>FORMAT.md</c> §6.10 "Dungeon <c>_ianim</c>")
/// into Core's <see cref="P4ViewAnimationRules"/>: the picture cycles and the step pass's measured costs. A section of its
/// own, beside <c>picture_animations</c> (which <c>tools/scripts/x68k_p4_anim.py --write</c> rewrites).
/// </summary>
internal static class DungeonViewAnimationLoader
{
    internal static P4ViewAnimationRules FromDto(Dto dto, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: bad dungeon_view_animation ({what}).");

        P4ViewCycle Cycle(int[]? pair, string what) =>
            pair is [var a, var b] && a >= 0 && b > a ? new P4ViewCycle(a, b) : throw Bad(what);
        List<P4ViewCycle> Cycles(List<int[]>? list, string what) =>
            [.. (list ?? throw Bad(what)).Select((p, i) => Cycle(p, $"{what} {i}"))];

        if (dto.StepAfter is not > 0)
            throw Bad("step_after");
        var random = dto.RandomFloor ?? throw Bad("random_floor");
        if (random.Range is not > 0 || random.After is not >= 0 || random.Base is not >= 0)
            throw Bad("random_floor");
        var t = dto.TimingMs ?? throw Bad("timing_ms");
        double Ms(double? v, string what) => v is >= 0 ? v.Value : throw Bad($"timing_ms.{what}");
        if (t.WestDraw is < 0)
            throw Bad("timing_ms.west_draw");
        var timing = new P4ViewAnimationTiming(
            t.Pass is > 0 ? t.Pass.Value : throw Bad("timing_ms.pass"),
            Ms(t.CellOutside, "cell_outside"), Ms(t.CellUnseen, "cell_unseen"), Ms(t.CellSeen, "cell_seen"),
            Ms(t.CellSeenStepped, "cell_seen_stepped"), Ms(t.Row0Less, "row0_less"), Ms(t.FloorDraw, "floor_draw"),
            Ms(t.PartyDraw, "party_draw"), Ms(t.NorthDraw, "north_draw"), t.WestDraw);
        return new P4ViewAnimationRules(dto.StepAfter.Value,
            Cycles(dto.FloorCycles, "floor_cycles"), Cycles(dto.NorthCycles, "north_cycles"), Cycles(dto.WestCycles, "west_cycles"),
            Cycle(random.Floors, "random_floor.floors"), random.Base!.Value, random.Range!.Value, random.After!.Value,
            Cycle(dto.WaterFloors, "water_floors"), timing);
    }

    internal sealed class Dto
    {
        [JsonPropertyName("step_after")] public int? StepAfter { get; set; }
        [JsonPropertyName("floor_cycles")] public List<int[]>? FloorCycles { get; set; }
        [JsonPropertyName("north_cycles")] public List<int[]>? NorthCycles { get; set; }
        [JsonPropertyName("west_cycles")] public List<int[]>? WestCycles { get; set; }
        [JsonPropertyName("random_floor")] public RandomDto? RandomFloor { get; set; }
        [JsonPropertyName("water_floors")] public int[]? WaterFloors { get; set; }
        [JsonPropertyName("timing_ms")] public TimingDto? TimingMs { get; set; }
    }

    internal sealed class RandomDto
    {
        public int[]? Floors { get; set; }
        public int? Base { get; set; }
        public int? Range { get; set; }
        public int? After { get; set; }
    }

    internal sealed class TimingDto
    {
        public double? Pass { get; set; }
        [JsonPropertyName("cell_outside")] public double? CellOutside { get; set; }
        [JsonPropertyName("cell_unseen")] public double? CellUnseen { get; set; }
        [JsonPropertyName("cell_seen")] public double? CellSeen { get; set; }
        [JsonPropertyName("cell_seen_stepped")] public double? CellSeenStepped { get; set; }
        [JsonPropertyName("row0_less")] public double? Row0Less { get; set; }
        [JsonPropertyName("floor_draw")] public double? FloorDraw { get; set; }
        [JsonPropertyName("party_draw")] public double? PartyDraw { get; set; }
        [JsonPropertyName("north_draw")] public double? NorthDraw { get; set; }
        [JsonPropertyName("west_draw")] public double? WestDraw { get; set; }
    }
}
