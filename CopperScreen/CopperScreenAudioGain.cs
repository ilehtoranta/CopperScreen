namespace CopperScreen;

// Host playback gain is applied after dequeue, so mute also affects queued audio.
internal static class CopperScreenAudioGain
{
    public static float Validate(float gain)
        => float.IsFinite(gain) && gain is >= 0 and <= 1
            ? gain : throw new ArgumentOutOfRangeException(nameof(gain));

    public static void Apply(Span<float> samples, float gain)
    {
        if (gain == 1) return;
        if (gain == 0) { samples.Clear(); return; }
        for (var i = 0; i < samples.Length; i++) samples[i] *= gain;
    }
}
