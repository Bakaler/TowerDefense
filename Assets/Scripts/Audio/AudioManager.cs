using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-house audio playback. Self-bootstraps (no scene wiring), owns a pooled set of
/// AudioSources, and enforces the playback discipline defined in sounds.json:
/// buses, per-sound rate limits, voice caps, priority stealing, pitch jitter.
///
///   AudioManager.Play("shotgun_fire");        — play a sound definition by id
///   AudioManager.PlayEvent("wave_start");     — play whatever sound the event maps to
///   AudioManager.PlayMusicEvent("music_game");— crossfade looping music
///
/// Buses: master, music, combat, ui, ambient — volumes persisted via PlayerPrefs.
/// Music sources ignore AudioListener.pause so pausing gameplay keeps music running.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const int   SfxVoiceCount  = 24;
    const float MusicFadeTime  = 1.5f;

    public static readonly string[] Buses = { "master", "music", "combat", "ui", "ambient" };

    // ── Bootstrap — created automatically after the first scene loads ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[AudioManager]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>();
    }

    // ── Voices ────────────────────────────────────────────────────────
    class Voice
    {
        public AudioSource src;
        public string      soundId;
        public int         priority;
        public float       startTime;
    }

    readonly List<Voice>                _voices     = new();
    Voice _uiVoiceThisFrame;
    int   _uiFrame     = -1;
    int   _uiPriority;
    readonly Dictionary<string, float>  _lastPlay   = new();
    readonly Dictionary<string, int>    _clipCursor = new();
    readonly Dictionary<string, AudioClip> _clipCache = new();
    readonly HashSet<string>            _warned     = new();
    readonly Dictionary<string, float>  _bus        = new();

    AudioSource     _musicA, _musicB;
    bool            _musicOnA = true;
    string          _musicSoundId;
    SoundDefinition _musicDef;
    float           _musicDefVolume = 1f;
    Coroutine       _musicFade;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        foreach (var b in Buses)
            _bus[b] = PlayerPrefs.GetFloat("audio.bus." + b, 1f);

        for (int i = 0; i < SfxVoiceCount; i++)
        {
            var src = NewSource($"Voice_{i}", false);
            _voices.Add(new Voice { src = src });
        }

        _musicA = NewSource("MusicA", true);
        _musicB = NewSource("MusicB", true);
    }

    AudioSource NewSource(string name, bool isMusic)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src           = go.AddComponent<AudioSource>();
        src.playOnAwake   = false;
        src.spatialBlend  = 0f;   // pure 2D
        if (isMusic)
        {
            src.loop = true;
            src.ignoreListenerPause = true;   // music keeps playing while gameplay is paused
        }
        return src;
    }

    // ── Public API ────────────────────────────────────────────────────

    public static void Play(string soundId)           => Instance?.PlayInternal(soundId);

    public static void PlayEvent(string eventId)
    {
        var soundId = SoundLibrary.GetEventSound(eventId);
        if (!string.IsNullOrEmpty(soundId)) Play(soundId);
    }

    public static void PlayMusic(string soundId)      => Instance?.PlayMusicInternal(soundId);

    public static void PlayMusicEvent(string eventId)
    {
        var soundId = SoundLibrary.GetEventSound(eventId);
        if (!string.IsNullOrEmpty(soundId)) PlayMusic(soundId);
    }

    public static void StopMusic()                    => Instance?.StopMusicInternal();

    /// <summary>Pauses/resumes all SFX (music is unaffected). Wire to the game pause button.</summary>
    public static void SetSfxPaused(bool paused)      => AudioListener.pause = paused;

    public static float GetBusVolume(string bus) =>
        Instance != null && Instance._bus.TryGetValue(bus, out var v) ? v : 1f;

    public static void SetBusVolume(string bus, float volume)
    {
        if (Instance == null || !Instance._bus.ContainsKey(bus)) return;
        Instance._bus[bus] = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("audio.bus." + bus, Instance._bus[bus]);
        Instance.ApplyMusicVolume();
    }

    // ── SFX playback ──────────────────────────────────────────────────

    void PlayInternal(string soundId)
    {
        var def = SoundLibrary.Get(soundId);
        if (def == null) { WarnOnce($"Unknown sound id '{soundId}'."); return; }
        if (def.loop)    { PlayMusicInternal(soundId); return; }

        float now = Time.unscaledTime;

        // Rate limit
        if (def.minInterval > 0f && _lastPlay.TryGetValue(soundId, out var last) && now - last < def.minInterval)
            return;

        // Per-sound voice cap — steal the oldest instance of the same sound
        if (def.maxVoices > 0)
        {
            Voice oldest = null; int active = 0;
            foreach (var v in _voices)
                if (v.soundId == soundId && v.src.isPlaying)
                {
                    active++;
                    if (oldest == null || v.startTime < oldest.startTime) oldest = v;
                }
            if (active >= def.maxVoices) oldest?.src.Stop();
        }

        // One UI sound per frame: a button's semantic sound (sell, upgrade, wave horn)
        // replaces the generic ui_click that MakeBtn fires on the same click.
        if (def.bus == "ui" && _uiFrame == Time.frameCount && _uiVoiceThisFrame != null)
        {
            if (_uiPriority > def.priority) return;   // existing UI sound outranks this one
            _uiVoiceThisFrame.src.Stop();
        }

        var voice = GetFreeVoice(def.priority);
        if (voice == null) return;   // pool full of higher-priority sounds

        var clip = ResolveClip(def);
        if (clip == null) { WarnOnce($"Sound '{soundId}' has no playable clip (and no synth fallback)."); return; }

        voice.soundId   = soundId;
        voice.priority  = def.priority;
        voice.startTime = now;
        voice.src.clip   = clip;
        voice.src.pitch  = def.pitch + (def.pitchJitter > 0f ? Random.Range(-def.pitchJitter, def.pitchJitter) : 0f);
        voice.src.volume = def.volume * BusMult(def.bus);
        voice.src.Play();

        _lastPlay[soundId] = now;

        if (def.bus == "ui")
        {
            _uiVoiceThisFrame = voice;
            _uiFrame          = Time.frameCount;
            _uiPriority       = def.priority;
        }
    }

    Voice GetFreeVoice(int incomingPriority)
    {
        Voice steal = null;
        foreach (var v in _voices)
        {
            if (!v.src.isPlaying) return v;
            // Candidate to steal: strictly lower priority, prefer the oldest
            if (v.priority < incomingPriority && (steal == null || v.startTime < steal.startTime))
                steal = v;
        }
        if (steal != null) steal.src.Stop();
        return steal;
    }

    // ── Music ─────────────────────────────────────────────────────────

    void PlayMusicInternal(string soundId)
    {
        if (soundId == _musicSoundId) return;

        var def = SoundLibrary.Get(soundId);
        if (def == null) { WarnOnce($"Unknown music sound id '{soundId}'."); return; }

        _musicSoundId   = soundId;
        _musicDef       = def;
        _musicDefVolume = def.volume;
        AdvanceMusic();
    }

    /// <summary>Starts the next track of the current music definition (crossfaded).
    /// Single clip (or synth) loops; multiple clips rotate as a shuffled playlist.</summary>
    void AdvanceMusic()
    {
        if (_musicDef == null) return;
        var clip = ResolveClip(_musicDef);
        if (clip == null) { WarnOnce($"Music '{_musicDef.id}' has no playable clip."); return; }

        var from = _musicOnA ? _musicA : _musicB;
        var to   = _musicOnA ? _musicB : _musicA;
        _musicOnA = !_musicOnA;

        int loadedClips = 0;
        if (_musicDef.clips != null)
            foreach (var c in _musicDef.clips)
                if (LoadClipPath(c) != null) loadedClips++;

        to.clip   = clip;
        to.pitch  = _musicDef.pitch;
        to.loop   = loadedClips <= 1;   // playlist mode when there's something to rotate
        to.volume = 0f;
        to.Play();
        Debug.Log($"[AudioManager] Music → {clip.name}");

        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(CrossfadeMusic(from, to));
    }

    void Update()
    {
        // Playlist rotation: when a non-looping music track nears its end,
        // crossfade into the next shuffled clip of the same definition.
        if (_musicDef == null || _musicFade != null) return;
        var cur = _musicOnA ? _musicA : _musicB;
        if (cur.clip == null || cur.loop) return;

        if (!cur.isPlaying || cur.clip.length - cur.time <= MusicFadeTime)
            AdvanceMusic();
    }

    void StopMusicInternal()
    {
        _musicSoundId = null;
        _musicDef     = null;
        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(CrossfadeMusic(_musicOnA ? _musicA : _musicB, null));
    }

    /// <summary>
    /// Call after SoundLibrary.Reload() — rebinds the live music definition so edits
    /// made during play (new playlist clips, volume, synth params) take effect.
    /// </summary>
    public void OnDefinitionsReloaded()
    {
        AudioSynth.ClearCache();
        _warned.Clear();

        if (string.IsNullOrEmpty(_musicSoundId)) return;
        var def = SoundLibrary.Get(_musicSoundId);
        if (def == null) return;

        _musicDef       = def;
        _musicDefVolume = def.volume;

        int loadedClips = 0;
        if (def.clips != null)
            foreach (var c in def.clips)
                if (LoadClipPath(c) != null) loadedClips++;

        // Playlist mode kicks in at the end of the current track
        var cur = _musicOnA ? _musicA : _musicB;
        cur.loop = loadedClips <= 1;
        if (_musicFade == null) cur.volume = _musicDefVolume * BusMult("music");
    }

    IEnumerator CrossfadeMusic(AudioSource from, AudioSource to)
    {
        float fromStart = from != null ? from.volume : 0f;
        float target    = _musicDefVolume * BusMult("music");
        float t = 0f;
        while (t < MusicFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / MusicFadeTime);
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to   != null) to.volume   = Mathf.Lerp(0f, target, k);
            yield return null;
        }
        if (from != null) from.Stop();
        _musicFade = null;
    }

    void ApplyMusicVolume()
    {
        // Live-update the currently playing music track when bus volumes change
        if (_musicFade != null || string.IsNullOrEmpty(_musicSoundId)) return;
        var current = _musicOnA ? _musicA : _musicB;
        current.volume = _musicDefVolume * BusMult("music");
    }

    // ── Clip resolution ───────────────────────────────────────────────

    AudioClip ResolveClip(SoundDefinition def)
    {
        if (def.clips != null && def.clips.Length > 0)
        {
            int idx = PickClipIndex(def);
            for (int attempt = 0; attempt < def.clips.Length; attempt++)
            {
                var clip = LoadClipPath(def.clips[(idx + attempt) % def.clips.Length]);
                if (clip != null) return clip;
            }
        }
        return AudioSynth.GetClip(def);
    }

    AudioClip LoadClipPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_clipCache.TryGetValue(path, out var clip))
        {
            clip = Resources.Load<AudioClip>(path);
            _clipCache[path] = clip;
        }
        return clip;
    }

    int PickClipIndex(SoundDefinition def)
    {
        int count = def.clips.Length;
        if (count == 1) return 0;

        _clipCursor.TryGetValue(def.id, out int last);
        int idx = def.pickMode == "random"
            ? Random.Range(0, count)
            : (last + 1 + Random.Range(0, count - 1)) % count;   // shuffle: any index except last
        _clipCursor[def.id] = idx;
        return idx;
    }

    float BusMult(string bus)
    {
        _bus.TryGetValue(bus ?? "combat", out var b);
        _bus.TryGetValue("master", out var m);
        return b * m;
    }

    void WarnOnce(string message)
    {
        if (_warned.Add(message)) Debug.LogWarning($"[AudioManager] {message}");
    }
}
