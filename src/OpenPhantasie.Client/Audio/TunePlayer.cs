using Microsoft.Xna.Framework.Audio;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// Streams a <see cref="TuneRenderer"/> to the sound card through a MonoGame <see cref="DynamicSoundEffectInstance"/>
/// (s108). <see cref="TryStart"/> returns null, and the game runs silently, when there is no audio device (MonoGame's
/// <see cref="NoAudioHardwareException"/>, an OpenAL library that fails to load, Xvfb without sound, …); any later audio
/// failure also just stops the tune. Call <see cref="Update"/> once a frame to keep the queue fed.
/// </summary>
public sealed class TunePlayer : IDisposable
{
    /// <summary>Frames per submitted buffer (about 1/20 s) and how many buffers to keep queued.</summary>
    private const int ChunkFrames = 2048, QueuedBuffers = 3;

    private readonly TuneRenderer _tune;
    private readonly float[] _floats = new float[2 * ChunkFrames];
    private DynamicSoundEffectInstance? _out;

    private TunePlayer(TuneRenderer tune, DynamicSoundEffectInstance output)
    {
        _tune = tune;
        _out = output;
    }

    /// <summary>True while the tune still has sound queued or to render.</summary>
    public bool Playing => _out is not null && (!_tune.Finished || _out.PendingBufferCount > 0);

    public static TunePlayer? TryStart(TuneRenderer tune)
    {
        DynamicSoundEffectInstance? output = null;
        try
        {
            output = new DynamicSoundEffectInstance(tune.SampleRate, AudioChannels.Stereo);
            var player = new TunePlayer(tune, output);
            player.Update();
            output.Play();
            return player;
        }
        catch (Exception ex) when (IsAudioFailure(ex))
        {
            try { output?.Dispose(); } catch (Exception e) when (IsAudioFailure(e)) { }
            return null;
        }
    }

    /// <summary>Queue the next buffers.</summary>
    public void Update()
    {
        if (_out is null)
            return;
        try
        {
            while (_out.PendingBufferCount < QueuedBuffers && !_tune.Finished)
            {
                var n = _tune.Render(_floats, ChunkFrames);
                if (n == 0)
                    break;
                var bytes = new byte[n * 4];
                for (var i = 0; i < 2 * n; i++)
                {
                    var v = (short)Math.Clamp((int)(_floats[i] * 32767f), short.MinValue, short.MaxValue);
                    bytes[2 * i] = (byte)v;
                    bytes[2 * i + 1] = (byte)(v >> 8);
                }
                _out.SubmitBuffer(bytes);
            }
        }
        catch (Exception ex) when (IsAudioFailure(ex))
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        var o = _out;
        _out = null;
        if (o is null)
            return;
        try
        {
            o.Stop();
            o.Dispose();
        }
        catch (Exception ex) when (IsAudioFailure(ex))
        {
        }
    }

    /// <summary>
    /// What ends the tune instead of the game: sound is optional, so any failure of the audio back end (MonoGame's
    /// <see cref="NoAudioHardwareException"/>, OpenAL's load or device errors, whatever type they come as) means silence.
    /// Only the runtime's fatal ones pass.
    /// </summary>
    private static bool IsAudioFailure(Exception ex) => ex is not (OutOfMemoryException or StackOverflowException);
}
