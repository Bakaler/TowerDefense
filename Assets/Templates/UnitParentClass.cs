using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitParentClass : MonoBehaviour
{
    // Game Logic
    public float damageReductionBaseModifier = 0.995f;
    public bool isAlive = false;
    public int decayTimer = 50;

    // Life
    public float lifeMax = 0;
    public float lifeCurrent = 0;
    public float lifeRegen = 0;
    public float lifeRegenMulitplier = 1;
    public float lifeRegenDelay = 0;

    // Shield
    public bool hasShields = false;
    public float shieldMax = 0;
    public float shieldCurrent = 0;
    public float shieldRegen = 0;
    public float shieldRegenMulitplier = 1;
    public float shieldRegenDelay = 0;

    // Energy
    public float energyMax = 0;
    public float energyCurrent = 0;
    public float energyRegen = 0;
    public float energyRegenMulitplier = 1;
    public float energyRegenDelay = 0;
    public float energyArmor = 0;

    // Movement
    public float speedMax = 0;
    public float speedMin = 0;
    public float speedCurrent = 0;
    public float speedCurrentMulitplier = 1;

    // Abilities
    public List<AbilityParentClass> abilities = new List<AbilityParentClass>();
    public List<int> commandCard = new List<int>();

    // Cost
    public int resourceOne = 0;
    public int resourceTwo = 0;
    public int resourceThree = 0;

    // Effects
    public EffectParentClass birthEffect;
    public EffectParentClass deathEffect;

    // Information
    public string unitName = "";
    public string unitDescription = "";

    // Footprint
    public List<int> footprint = new List<int>();

    // Win Condition Attributes
    public float research = 0;
    public float funding = 0;
    public float development = 0;

    // Defense — Piercing/Poison/Pure damage bypasses these entirely
    public int physicalDefense = 0;
    public int elementalDefense = 0;
    public int arcanaDefense = 0;

    protected virtual void Update()
    {
        HandleDeath();
    }

    public virtual void TakeDamage(float damageAmount, float shieldBonus, float minimum, float maximum, DamageType type)
    {
        float damageTaken = Mathf.Min(Mathf.Max(TotalDamageAfterReduction(damageAmount, type), minimum), maximum);

        if (hasShields && shieldCurrent > 0)
        {
            // shieldBonus modifies damage dealt to the shield only (may be negative)
            float shieldDamage = Mathf.Max(0f, damageTaken + shieldBonus);
            if (shieldDamage < shieldCurrent)
            {
                shieldCurrent -= shieldDamage;
            }
            else
            {
                // Shield breaks — the unspent fraction of the hit carries into life at normal damage
                float usedFraction = shieldDamage > 0f ? shieldCurrent / shieldDamage : 1f;
                shieldCurrent = 0;
                lifeCurrent  -= damageTaken * (1f - usedFraction);
            }
        }
        else
        {
            lifeCurrent -= damageTaken;
        }
    }

    public float TotalDamageAfterReduction(float damageAmount, DamageType type)
    {
        switch (type)
        {
            case DamageType.Arcane:
                return damageAmount * Mathf.Pow(damageReductionBaseModifier, arcanaDefense);
            case DamageType.Elemental:
                return damageAmount * Mathf.Pow(damageReductionBaseModifier, elementalDefense);
            case DamageType.Physical:
                return damageAmount * Mathf.Pow(damageReductionBaseModifier, physicalDefense);
            // Piercing, Poison, and Pure ignore defenses entirely — only
            // behaviors that modify damage taken directly can reduce them.
            case DamageType.Piercing:
            case DamageType.Poison:
            case DamageType.Pure:
                return damageAmount;
            default:
                return damageAmount;
        }
    }

    public void HandleDeath()
    {
        if (isAlive)
        {
            if (lifeCurrent <= 0)
                isAlive = false;
        }
        else
        {
            if (decayTimer > 0)
                decayTimer -= 1;
            else
                Destroy(gameObject);
        }
    }
}
