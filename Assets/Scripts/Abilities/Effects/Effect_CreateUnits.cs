using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns enemy units at the caster's position — the data-driven form of
/// "splits on death" (wire via a behavior's onDeathEffectId).
///
/// The first unit spawns immediately so a death-triggered split can't leave
/// the wave's alive-unit count at zero (which would end the wave mid-split);
/// the rest trickle out on spawnInterval via a WaveManager-hosted coroutine,
/// since the caster may be destroyed the same frame it dies.
///
/// If the caster follows a route, spawned units resume it at the same
/// progress, with jittered speed and desynced walk animation so the pack
/// spreads instead of marching as one clump.
/// </summary>
public class Effect_CreateUnits : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("create_units", typeof(Effect_CreateUnits));

    public string unitId        = "";
    public int    count         = 1;
    /// <summary>Seconds between spawns after the first (0 = all at once).</summary>
    public float  spawnInterval = 0.15f;
    /// <summary>Random positional scatter around the spawn origin.</summary>
    public float  spawnRadius   = 0.3f;
    /// <summary>Spawned units continue the caster's route from its current progress.</summary>
    public bool   inheritRoute  = true;
    /// <summary>Per-unit random speed spread (0.1 = ±10%).</summary>
    public float  speedJitter   = 0.10f;
    /// <summary>Apply the same per-wave HP multiplier the spawner gives normal units.</summary>
    public bool   waveHpScaling = true;

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (string.IsNullOrEmpty(unitId) || count <= 0) return;
        if (UnitFactory.Instance == null)
        {
            Debug.LogWarning($"[Effect_CreateUnits] '{effectID}': UnitFactory missing — no units spawned.");
            return;
        }

        var caster = context.CasterTransform;
        Vector3 origin = caster != null ? caster.position
                       : context.AimOrigin2D.HasValue ? (Vector3)context.AimOrigin2D.Value
                       : context.TargetPoint;

        // Capture route context now — a dying caster is destroyed this frame
        Route route    = null;
        float progress = 0f;
        if (inheritRoute && caster != null)
        {
            var follower = caster.GetComponent<RouteFollower>();
            if (follower != null && follower.HasRoute)
            {
                route    = follower.CurrentRoute;
                progress = follower.Progress;
            }
        }

        SpawnOne(origin, route, progress);
        if (count <= 1) return;

        if (spawnInterval > 0f && WaveManager.Instance != null)
            WaveManager.Instance.StartCoroutine(SpawnRest(origin, route, progress));
        else
            for (int i = 1; i < count; i++)
                SpawnOne(origin, route, progress);
    }

    // Hosted on WaveManager; this effect instance is a cached ScriptableObject
    // and outlives the caster, so reading fields here is safe.
    IEnumerator SpawnRest(Vector3 origin, Route route, float progress)
    {
        for (int i = 1; i < count; i++)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnOne(origin, route, progress);
        }
    }

    void SpawnOne(Vector3 origin, Route route, float progress)
    {
        Vector3 offset = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            Random.Range(-spawnRadius, spawnRadius), 0f);

        var go = UnitFactory.Instance != null ? UnitFactory.Instance.Build(unitId, origin + offset) : null;
        if (go == null)
        {
            Debug.LogWarning($"[Effect_CreateUnits] '{effectID}': factory failed to build '{unitId}'.");
            return;
        }

        var unit = go.GetComponent<UnitManager>();
        if (unit == null) return;

        if (waveHpScaling)
        {
            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 1;
            if (wave > 1)
            {
                float healthMult = Mathf.Pow(1.08f, wave - 1);
                unit.lifeMax    *= healthMult;
                unit.lifeCurrent = unit.lifeMax;
            }
        }

        if (speedJitter > 0f)
        {
            unit.speedMax    *= Random.Range(1f - speedJitter, 1f + speedJitter);
            unit.speedCurrent = unit.speedMax;
        }
        go.GetComponent<SpriteAnimator>()?.RandomizeWalkPhase();

        if (route != null)
        {
            var follower = go.GetComponent<RouteFollower>();
            if (follower == null) follower = go.AddComponent<RouteFollower>();
            follower.StartRoute(route, unit.speedMax, progress);
        }

        WaveManager.Instance?.RegisterUnit(unit);
    }
}
