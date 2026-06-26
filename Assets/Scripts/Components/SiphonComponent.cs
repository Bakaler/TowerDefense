using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short-range drain beam. Targets the nearest enemy, fires Effect_DrainLife on a timer,
/// and draws a thin beam visual toward the target. Registered as "siphon_tower".
/// </summary>
public class SiphonComponent : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("siphon_tower", typeof(SiphonComponent));

    public float  range        = 3f;
    public float  drainInterval = 9f;   // seconds between drains
    public string drainEffectId = "drain_life";

    private float   _timer;
    private Effect  _drainEffect;
    private LineRenderer _beam;

    [Serializable]
    class Data
    {
        public float  range         = 3f;
        public float  drainInterval = 9f;
        public string drainEffectId = "drain_life";
    }

    public void Initialize(string dataJson)
    {
        if (!string.IsNullOrEmpty(dataJson))
        {
            var d = JsonUtility.FromJson<Data>(dataJson);
            range         = d.range;
            drainInterval = d.drainInterval;
            drainEffectId = d.drainEffectId;
        }

        if (EffectLibrary.Instance != null)
            _drainEffect = EffectLibrary.Instance.GetEffect(drainEffectId);

        // Beam visual
        var beamGO           = new GameObject("SiphonBeam");
        beamGO.transform.SetParent(transform, false);
        _beam                = beamGO.AddComponent<LineRenderer>();
        _beam.positionCount  = 2;
        _beam.startWidth     = 0.05f;
        _beam.endWidth       = 0.02f;
        _beam.useWorldSpace  = true;
        _beam.sortingLayerName = "Units";
        _beam.sortingOrder   = 8;
        _beam.material       = new Material(Shader.Find("Sprites/Default"));
        _beam.startColor     = new Color(0.4f, 1f, 0.55f, 0.9f);
        _beam.endColor       = new Color(0.4f, 1f, 0.55f, 0f);
        _beam.enabled        = false;

        _timer = drainInterval * 0.5f;   // offset so first drain doesn't fire instantly
    }

    void Update()
    {
        var target = FindNearest();
        if (target == null) { _beam.enabled = false; return; }

        // Draw beam
        _beam.enabled = true;
        _beam.SetPosition(0, transform.position);
        _beam.SetPosition(1, target.transform.position);

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = drainInterval;

        if (_drainEffect == null) return;
        var ctx = new EffectContext
        {
            CasterTransform = transform,
            Target          = target,
            TargetPoint     = target.transform.position,
            AimOrigin2D     = transform.position,
            CustomData      = new Dictionary<string, object>(),
        };
        EffectExecutor.ExecuteEffect(_drainEffect, ctx);
    }

    UnitParentClass FindNearest()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, range, LayerMask.GetMask("Enemy"));
        UnitParentClass best  = null;
        float           bestD = float.MaxValue;
        foreach (var col in hits)
        {
            var unit = col.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;
            float d = Vector2.SqrMagnitude((Vector2)col.transform.position - (Vector2)transform.position);
            if (d < bestD) { bestD = d; best = unit; }
        }
        return best;
    }
}
