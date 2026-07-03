using System;
using UnityEngine;

/// <summary>
/// Defines a sound as a playback recipe — clips, mix bus, and playback discipline.
/// All definitions live in Resources/Definitions/sounds.json.
/// When no clip resolves, the synth fields generate a procedural placeholder
/// (same philosophy as the fallback circle sprites) so audio works with zero files.
/// </summary>
[Serializable]
public class SoundDefinition
{
    // ── Identity ──────────────────────────────────────────────────────
    public string id;
    public string displayName;

    // ── Clips ─────────────────────────────────────────────────────────
    /// <summary>Resources paths (no extension). Multiple = variation set.</summary>
    public string[] clips = Array.Empty<string>();
    /// <summary>shuffle (never repeat the same clip twice in a row) | random</summary>
    public string pickMode = "shuffle";

    // ── Playback ──────────────────────────────────────────────────────
    /// <summary>music | combat | ui | ambient</summary>
    public string bus = "combat";
    public float  volume      = 1f;
    public float  pitch       = 1f;
    /// <summary>Random pitch offset per play: pitch ± jitter.</summary>
    public float  pitchJitter = 0f;
    public bool   loop        = false;

    // ── Discipline (anti-clusterfuck) ────────────────────────────────
    /// <summary>Minimum seconds between two plays of this sound. 0 = none.</summary>
    public float minInterval = 0f;
    /// <summary>Max overlapping instances; oldest is stolen. 0 = unlimited.</summary>
    public int   maxVoices   = 0;
    /// <summary>When the global voice pool is full, higher priority steals lower.</summary>
    public int   priority    = 1;

    // ── Synth fallback (when no clip resolves) ───────────────────────
    /// <summary>none | sine | square | triangle | saw | noise | drone</summary>
    public string synthWave     = "none";
    public float  synthFreq     = 440f;
    /// <summary>End frequency for a pitch slide. 0 = no slide.</summary>
    public float  synthFreqEnd  = 0f;
    public float  synthDuration = 0.15f;
    public float  synthAttack   = 0.005f;
}

[Serializable]
public class SoundEventEntry
{
    /// <summary>Well-known event name fired from code (e.g. "wave_start").</summary>
    public string eventId;
    /// <summary>Sound definition id to play for it.</summary>
    public string soundId;
}

[Serializable]
public class SoundDefinitionCollection
{
    public SoundDefinition[] sounds;
    public SoundEventEntry[] events;
}
