using UnityEngine;

public class UnitParentClass : MonoBehaviour
{
    // Game Logic
    public float damageReductionBaseModifier = 0.995f;
    public bool isAlive = false;
    /// <summary>Seconds a corpse lingers before the GameObject is destroyed.</summary>
    public float decayTimer = 0.8f;

    // Life
    public float lifeMax = 0;
    public float lifeCurrent = 0;

    // Shield
    public bool hasShields = false;
    public float shieldMax = 0;
    public float shieldCurrent = 0;

    // Movement
    public float speedMax = 0;
    public float speedCurrent = 0;

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
                // Shield breaks — only the base damage beyond the shield's remaining
                // HP carries into life. shieldBonus speeds up the break but never
                // adds (or removes) life damage.
                float carry = Mathf.Max(0f, damageTaken - shieldCurrent);
                shieldCurrent = 0;
                lifeCurrent  -= carry;
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
                decayTimer -= Time.deltaTime;
            else
                Destroy(gameObject);
        }
    }
}
