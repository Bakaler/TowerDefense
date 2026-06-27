using System.Collections.Generic;
using UnityEngine;

public class EffectContext
{
    public UnitParentClass Caster;
    public UnitParentClass Target;
    public Vector3 TargetPoint;
    public Ability_Effect OriginAbility;
    public Dictionary<string, object> CustomData;

    // Spawn/aim reference when Caster UnitParentClass is not available (e.g. turrets)
    public Transform CasterTransform;

    // Tower that originated this effect chain — used for kill tracking
    public GameObject OriginTower;

    // 2D aim data — used by SearchArea effects
    public Vector2? AimOrigin2D;
    public Vector2? AimDirection2D;

    // Optional overrides for Effect_Damage — used by components that compute damage themselves
    // (continuous beam, drones). >0 replaces damageBase; non-null replaces damageType.
    public float      DamageOverride;
    public DamageType? DamageTypeOverride;

    public EffectContext CloneForNewTarget(UnitParentClass newTarget)
    {
        return new EffectContext
        {
            Caster = this.Caster,
            Target = newTarget,
            TargetPoint = this.TargetPoint,
            OriginAbility = this.OriginAbility,
            CustomData = new Dictionary<string, object>(this.CustomData ?? new Dictionary<string, object>()),
            AimOrigin2D = this.AimOrigin2D,
            AimDirection2D = this.AimDirection2D,
            CasterTransform    = this.CasterTransform,
            OriginTower        = this.OriginTower,
            DamageOverride     = this.DamageOverride,
            DamageTypeOverride = this.DamageTypeOverride,
        };
    }
}
