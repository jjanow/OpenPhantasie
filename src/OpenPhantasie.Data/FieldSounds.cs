using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// s139: which sound of the pack's <c>sound_effects</c> a moment outside combat plays (Core's <c>FieldCues</c> names,
/// noted by <c>GameSession.Cue</c>). Phantasie II ST: FORMAT.md 7.4. A moment the map lacks is silent.
/// <see cref="WaitSpeedMs"/>: at a combat Speed of this many ms or more the next sound waits for the running one (the
/// ST's <c>0xb8f2</c>, Speed ≥ 9), below it the new one cuts the old off; null: always in turn.
/// s140: <see cref="Defeat"/>, the defeat scene's sounds; <see cref="Alerts"/>, the alert boxes' beep per message;
/// <see cref="Click"/>, the click after a menu or dialog pick.
/// s141: <see cref="Delays"/>, cues whose sound follows a timed box.
/// </summary>
public sealed record FieldSounds(IReadOnlyDictionary<string, int> Cues, int? WaitSpeedMs = null)
{
    /// <summary>s140: the party-defeat scene's sounds (II ST <c>0x12678</c> / <c>0x12bea</c>), or null.</summary>
    public FieldDefeatSounds? Defeat { get; init; }

    /// <summary>s140: the alert boxes that beep first (II ST <c>0x1628</c> / <c>0x1668</c> / <c>0xb7fa</c>), or null.</summary>
    public FieldAlerts? Alerts { get; init; }

    /// <summary>s140: the click after a GEM pick (II ST <c>0xb7e6</c>, the pack's sound 33), or null.</summary>
    public int? Click { get; init; }

    /// <summary>s141: cues the ST sounds only after a timed box (<see cref="FieldCueDelay"/>); a cue not listed sounds at once.</summary>
    public IReadOnlyDictionary<string, FieldCueDelay> Delays { get; init; } = new Dictionary<string, FieldCueDelay>();

    /// <summary>The sound for <paramref name="moment"/>, or null.</summary>
    public int? SoundFor(string moment) => Cues.TryGetValue(moment, out var n) ? n : null;

    internal static FieldSounds FromDto(Dto dto, string manifestPath)
    {
        var cues = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (moment, sound) in dto.Cues ?? [])
        {
            if (string.IsNullOrWhiteSpace(moment) || sound < 0)
                throw new InvalidDataException($"{manifestPath}: field_sounds: bad cue '{moment}' → {sound}.");
            cues[moment] = sound;
        }
        var alerts = dto.Alerts is { } a ? AlertsFromDto(a, manifestPath, "field_sounds.alerts") : null;
        var delays = new Dictionary<string, FieldCueDelay>(StringComparer.Ordinal);
        foreach (var (moment, delay) in dto.CueDelays ?? [])
        {
            if (string.IsNullOrWhiteSpace(moment) || delay.HoldMs < 0 || delay.AfterMs < 0)
                throw new InvalidDataException($"{manifestPath}: field_sounds.cue_delays: bad delay for '{moment}'.");
            delays[moment] = new FieldCueDelay(delay.HoldMs, delay.PlusSpeed, delay.AfterMs);
        }
        return new FieldSounds(cues, dto.WaitSpeedMs)
        {
            Defeat = dto.DefeatScene is { } d
                ? new FieldDefeatSounds(d.SpeedMs, d.Ghosts, d.Door, d.Demon, d.Fate) { GhostsAfterMs = d.GhostsAfterMs }
                : null,
            Alerts = alerts,
            Click = dto.Click,
            Delays = delays,
        };
    }

    /// <summary>An <c>alerts</c> block (II ST's <c>field_sounds</c>, I Amiga's <c>cue_sounds</c>, s158).</summary>
    internal static FieldAlerts AlertsFromDto(AlertsDto a, string manifestPath, string where)
    {
        var lines = new List<FieldAlertLine>();
        foreach (var line in a.Lines ?? [])
        {
            if (string.IsNullOrWhiteSpace(line.Key) || line.Modes is not { Count: > 0 } modes)
                throw new InvalidDataException($"{manifestPath}: {where}: a line needs a key and modes.");
            lines.Add(new FieldAlertLine(line.Key, modes.Select(m => m.ToLowerInvariant()).ToList(), line.DialogOnly)
                { Box = Alert(line.Box) });
        }
        return new FieldAlerts(a.Sound, lines)
        {
            CueBoxes = (a.CueBoxes ?? []).ToDictionary(c => c.Key, c => Alert(c.Value)!, StringComparer.Ordinal),
            Look = a.Look is { } look ? StAlertLook.FromDto(look, manifestPath) : null,
        };

        StAlert? Alert(string? text)
        {
            if (text is null)
                return null;
            try
            {
                return StAlert.Parse(text);
            }
            catch (FormatException e)
            {
                throw new InvalidDataException($"{manifestPath}: {where}: {e.Message}", e);
            }
        }
    }

    public sealed class Dto
    {
        [JsonPropertyName("_note")] public string? Note { get; set; }
        public Dictionary<string, int>? Cues { get; set; }
        [JsonPropertyName("wait_speed_ms")] public int? WaitSpeedMs { get; set; }
        [JsonPropertyName("defeat_scene")] public DefeatDto? DefeatScene { get; set; }
        public AlertsDto? Alerts { get; set; }
        public int? Click { get; set; }
        [JsonPropertyName("cue_delays")] public Dictionary<string, DelayDto>? CueDelays { get; set; }
    }

    public sealed class DelayDto
    {
        [JsonPropertyName("_note")] public string? Note { get; set; }
        [JsonPropertyName("hold_ms")] public int HoldMs { get; set; }
        [JsonPropertyName("plus_speed")] public bool PlusSpeed { get; set; }
        [JsonPropertyName("after_ms")] public int AfterMs { get; set; }
    }

    public sealed class DefeatDto
    {
        [JsonPropertyName("_note")] public string? Note { get; set; }
        [JsonPropertyName("speed_ms")] public int SpeedMs { get; set; }
        public int Ghosts { get; set; } = -1;
        public int Door { get; set; } = -1;
        public int Demon { get; set; } = -1;
        public int Fate { get; set; } = -1;
        [JsonPropertyName("ghosts_after_ms")] public int GhostsAfterMs { get; set; }
    }

    public sealed class AlertsDto
    {
        [JsonPropertyName("_note")] public string? Note { get; set; }
        public int Sound { get; set; }
        public List<AlertLineDto>? Lines { get; set; }
        [JsonPropertyName("cue_boxes")] public Dictionary<string, string>? CueBoxes { get; set; }
        public StAlertLook.Dto? Look { get; set; }
    }

    public sealed class AlertLineDto
    {
        public string? Key { get; set; }
        public List<string>? Modes { get; set; }
        public string? St { get; set; }
        [JsonPropertyName("dialog_only")] public bool DialogOnly { get; set; }
        public string? Box { get; set; }
    }
}

/// <summary>
/// s140: the party-defeat scene's sounds (II ST, FORMAT.md 7.4, <c>endgame.md</c> 8). <see cref="SpeedMs"/>: the Speed
/// word the scene runs at (<c>0x12694</c> saves it and sets 20, so every sound waits for the one before, whatever the
/// player picked). -1 = none.
/// </summary>
/// <param name="Ghosts">After the ghost row (<c>0x12d5e</c>, 23).</param>
/// <param name="Door">As the door starts to sink (<c>0x12d86</c>, 22).</param>
/// <param name="Demon">Before the Great Demon's face (<c>0x12e36</c>, 20).</param>
/// <param name="Fate">Before each member's fate (<c>0x126d2</c>, 21).</param>
public sealed record FieldDefeatSounds(int SpeedMs, int Ghosts, int Door, int Demon, int Fate)
{
    /// <summary>s141: ms between the defeat page's first line and the ghosts' sound (the line's hold, <c>0x12a7c</c>: 3000
    /// ms or a key; traced 3060 ms). 0 = with the page.</summary>
    public int GhostsAfterMs { get; init; }
}

/// <summary>
/// s141: a cue whose sound the ST plays only after a timed box (II ST field Resurrection: <c>0xa18c</c>'s box held Speed +
/// 1000 ms, a key ending it, then the party-view redraw, then sound 12; FORMAT.md 7.4). From the moment its line shows:
/// <see cref="HoldMs"/> (plus the Speed word when <see cref="PlusSpeed"/>), then <see cref="AfterMs"/>.
/// </summary>
public sealed record FieldCueDelay(int HoldMs, bool PlusSpeed, int AfterMs)
{
    /// <summary>The wait in ms at the Speed word <paramref name="speedMs"/> (null: none added).</summary>
    public int Ms(int? speedMs) => (PlusSpeed ? speedMs ?? 0 : 0) + HoldMs + AfterMs;
}

/// <summary>
/// s140: the alert boxes that beep before they show (II ST <c>0x1628</c> / <c>0x1668</c>: <c>0xb7fa</c>, then
/// <c>form_alert</c>; FORMAT.md 7.4): the remaster's string ids of the lines that stand for them, each with the game
/// modes (<c>town</c>, <c>guild</c>, <c>title</c>, <c>overworld</c>, <c>dungeon</c>) it beeps in.
/// </summary>
public sealed record FieldAlerts(int Sound, IReadOnlyList<FieldAlertLine> Lines)
{
    /// <summary>s158: the box a cue opens (II ST: <c>town.cast_done</c> → PHANT.RSC 9 "D O N E !"), by cue name.</summary>
    public IReadOnlyDictionary<string, StAlert> CueBoxes { get; init; } = new Dictionary<string, StAlert>();

    /// <summary>s158: how the boxes are drawn (<see cref="StAlertLook"/>), or null (no box: the beep only).</summary>
    public StAlertLook? Look { get; init; }
}

/// <summary>One alert line: the string id (matched from the start of a shown line) and the modes it beeps in.
/// <paramref name="DialogOnly"/> (s141): only a line a dialog returned counts, not the session's log (another moment shares
/// the text, e.g. the Armory's "UNUSABLE" after a buy, which the ST does not alert).</summary>
public sealed record FieldAlertLine(string Key, IReadOnlyList<string> Modes, bool DialogOnly = false)
{
    /// <summary>s158: the ST's own alert (its text and button, from the disks) drawn for this line, or null (beep only).</summary>
    public StAlert? Box { get; init; }
}
