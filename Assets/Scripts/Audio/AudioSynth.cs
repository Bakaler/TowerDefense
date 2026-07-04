using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural placeholder audio — generates simple synth clips from SoundDefinition
/// synth fields so the whole audio system works before any real files exist
/// (the audio equivalent of the fallback circle sprites).
///
/// Waveforms: sine | square | triangle | saw | noise, all with optional pitch slide
/// and an attack + exponential-decay envelope. "drone" builds a seamless looping
/// three-partial chord pad for placeholder music.
/// </summary>
public static class AudioSynth
{
    const int SampleRate = 44100;

    static readonly Dictionary<string, AudioClip> _cache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _cache.Clear();

    /// <summary>Drop generated clips so edited synth params take effect (live reload).</summary>
    public static void ClearCache() => _cache.Clear();

    /// <summary>Returns a generated clip for the definition, or null if synthWave is "none".</summary>
    public static AudioClip GetClip(SoundDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.synthWave) || def.synthWave == "none")
            return null;

        if (_cache.TryGetValue(def.id, out var cached) && cached != null)
            return cached;

        var clip = def.synthWave == "drone" ? GenerateDrone(def) : GenerateBlip(def);
        _cache[def.id] = clip;
        return clip;
    }

    // ── One-shot blips ────────────────────────────────────────────────

    static AudioClip GenerateBlip(SoundDefinition def)
    {
        float duration = Mathf.Clamp(def.synthDuration, 0.01f, 10f);
        int   samples  = Mathf.CeilToInt(duration * SampleRate);
        var   data     = new float[samples];

        float f0     = Mathf.Max(1f, def.synthFreq);
        float f1     = def.synthFreqEnd > 0f ? def.synthFreqEnd : f0;
        float attack = Mathf.Clamp(def.synthAttack, 0f, duration * 0.5f);
        // Exponential decay tuned so the tail is ~silent by the end
        float decayRate = 5f / duration;

        var rng = new System.Random(def.id?.GetHashCode() ?? 0);

        double phase = 0.0;
        for (int i = 0; i < samples; i++)
        {
            float t     = i / (float)SampleRate;
            float tNorm = t / duration;
            float freq  = Mathf.Lerp(f0, f1, tNorm);

            phase += freq / SampleRate;
            float p = (float)(phase - System.Math.Floor(phase));   // 0..1 sawtooth phase

            float v = def.synthWave switch
            {
                "square"   => p < 0.5f ? 1f : -1f,
                "triangle" => 4f * Mathf.Abs(p - 0.5f) - 1f,
                "saw"      => 2f * p - 1f,
                "noise"    => (float)(rng.NextDouble() * 2.0 - 1.0),
                _          => Mathf.Sin(p * Mathf.PI * 2f),        // sine
            };

            float env = attack > 0f && t < attack
                ? t / attack
                : Mathf.Exp(-decayRate * (t - attack));

            // Force the last few ms to zero so one-shots never click
            float tail = duration - t;
            if (tail < 0.005f) env *= tail / 0.005f;

            data[i] = v * env * 0.5f;   // headroom
        }

        return ToClip(def.id, data, false);
    }

    // ── Looping drone (placeholder music) ─────────────────────────────

    static AudioClip GenerateDrone(SoundDefinition def)
    {
        float duration = Mathf.Clamp(def.synthDuration, 1f, 30f);
        int   samples  = Mathf.CeilToInt(duration * SampleRate);
        var   data     = new float[samples];

        // Quantize each partial so it completes whole cycles — seamless loop
        float root = Mathf.Max(20f, def.synthFreq);
        float[] partials =
        {
            Quantize(root,        duration),
            Quantize(root * 1.5f, duration),   // fifth
            Quantize(root * 2f,   duration),   // octave
        };
        float[] amps = { 0.5f, 0.3f, 0.2f };

        // Slow amplitude LFO completing exactly one cycle per loop
        float lfoFreq = 1f / duration;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float v = 0f;
            for (int k = 0; k < partials.Length; k++)
                v += Mathf.Sin(t * partials[k] * Mathf.PI * 2f) * amps[k];

            float lfo = 0.85f + 0.15f * Mathf.Sin(t * lfoFreq * Mathf.PI * 2f);
            data[i] = v * lfo * 0.35f;
        }

        return ToClip(def.id, data, true);
    }

    static float Quantize(float freq, float duration) =>
        Mathf.Max(1f, Mathf.Round(freq * duration)) / duration;

    static AudioClip ToClip(string name, float[] data, bool looping)
    {
        var clip = AudioClip.Create($"[Synth]{name}", data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
