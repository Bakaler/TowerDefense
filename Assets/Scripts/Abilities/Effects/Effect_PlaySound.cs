using UnityEngine;

/// <summary>
/// Plays a sound definition (sounds.json). Composable via "set" like any effect,
/// so any impact/chain/death chain can carry audio without code changes.
/// </summary>
[CreateAssetMenu(fileName = "NewEffect_PlaySound", menuName = "Effect/Play Sound")]
public class Effect_PlaySound : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("play_sound", typeof(Effect_PlaySound));

    /// <summary>ID of the sound definition in sounds.json.</summary>
    public string soundId = "";

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (!string.IsNullOrEmpty(soundId)) AudioManager.Play(soundId);
    }
}
