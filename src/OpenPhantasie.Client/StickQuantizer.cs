using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// s183 "Pad leftovers": one direction per push of an analogue stick. The pad's stick "buttons" (LeftThumbstickUp…) cross
/// their thresholds on different frames, so a diagonal push came as Up first, then Up + Right (IV's fights and maps: N,
/// then NE). Here a push counts once the stick passes <see cref="PressThreshold"/>; the direction is read after
/// <see cref="SettleMs"/> (the stick has reached its angle by then), quantised to 8 (or 4) directions, and sent once. The
/// stick must come back under <see cref="ReleaseThreshold"/> before the next push counts. A push let go before the window
/// ends still counts, at the strongest sample seen.
/// </summary>
public sealed class StickQuantizer
{
    /// <summary>A push starts once the stick is this far out (0…1).</summary>
    public const float PressThreshold = 0.5f;

    /// <summary>The stick is back at rest (the next push may count) under this.</summary>
    public const float ReleaseThreshold = 0.3f;

    /// <summary>How long a push settles before its direction is read.</summary>
    public const double SettleMs = 60;

    private enum Phase { Rest, Settling, Sent }

    private Phase _phase = Phase.Rest;
    private double _settled;
    private Vector2 _strongest;

    /// <summary>Forget any push in progress (the stick counts again from rest).</summary>
    public void Reset()
    {
        _phase = Phase.Rest;
        _settled = 0;
        _strongest = Vector2.Zero;
    }

    /// <summary>
    /// This frame's stick position (<paramref name="stick"/>, X right, Y up, as <see cref="GamePadThumbSticks"/>) after
    /// <paramref name="elapsedMs"/>: the push's direction on the frame it is decided (Dx −1 / 0 / 1 left to right, Dy −1 / 0
    /// / 1 up to down), else null. <paramref name="eightWay"/> false: only up, down, left or right.
    /// </summary>
    public (int Dx, int Dy)? Update(Vector2 stick, double elapsedMs, bool eightWay)
    {
        var length = stick.Length();
        switch (_phase)
        {
            case Phase.Rest:
                if (length < PressThreshold)
                    return null;
                _phase = Phase.Settling;
                _settled = 0;
                _strongest = stick;
                return null; // the first sample past the threshold starts the window
            case Phase.Settling:
                if (length < ReleaseThreshold)
                {
                    _phase = Phase.Rest; // a quick flick: its strongest sample
                    return Quantise(_strongest, eightWay);
                }
                if (length >= _strongest.Length())
                    _strongest = stick;
                _settled += elapsedMs;
                if (_settled < SettleMs)
                    return null;
                _phase = Phase.Sent;
                return Quantise(length >= PressThreshold ? stick : _strongest, eightWay);
            default:
                if (length < ReleaseThreshold)
                    _phase = Phase.Rest;
                return null;
        }
    }

    /// <summary><paramref name="stick"/>'s direction: the nearest of 8 (or 4) compass points (Dy −1 is up).</summary>
    public static (int Dx, int Dy) Quantise(Vector2 stick, bool eightWay)
    {
        var sectors = eightWay ? 8 : 4;
        var angle = Math.Atan2(stick.Y, stick.X); // 0 = right, counter-clockwise (Y up)
        var n = ((int)Math.Round(angle / (2 * Math.PI / sectors)) % sectors + sectors) % sectors;
        var turns = n * (8 / sectors); // in eighths of a turn
        return turns switch
        {
            0 => (1, 0),
            1 => (1, -1),
            2 => (0, -1),
            3 => (-1, -1),
            4 => (-1, 0),
            5 => (-1, 1),
            6 => (0, 1),
            _ => (1, 1),
        };
    }

    /// <summary>A direction as the stick's buttons (both for a diagonal), as <see cref="GamePadState"/> names them.</summary>
    public static IReadOnlyList<Buttons> ButtonsOf((int Dx, int Dy) direction)
    {
        var buttons = new List<Buttons>(2);
        if (direction.Dy < 0)
            buttons.Add(Buttons.LeftThumbstickUp);
        else if (direction.Dy > 0)
            buttons.Add(Buttons.LeftThumbstickDown);
        if (direction.Dx < 0)
            buttons.Add(Buttons.LeftThumbstickLeft);
        else if (direction.Dx > 0)
            buttons.Add(Buttons.LeftThumbstickRight);
        return buttons;
    }

    /// <summary>The stick's own buttons (<see cref="ButtonsOf"/>): replaced by this quantiser's output where it is used.</summary>
    public static bool IsStickButton(Buttons b) =>
        b is Buttons.LeftThumbstickUp or Buttons.LeftThumbstickDown or Buttons.LeftThumbstickLeft or Buttons.LeftThumbstickRight;
}
