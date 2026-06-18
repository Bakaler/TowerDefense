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

    // Behaviors
    public List<BehaviorParentClass> behaviors = new List<BehaviorParentClass>();

    // Cost
    public int resourceOne = 0;
    public int resourceTwo = 0;
    public int resourceThree = 0;

    // Effects
    public EffectParentClass birthEffect;
    public EffectParentClass deathEffect;

    // Weapon
    public WeaponParentClass weapon;

    // Information
    public string unitName = "";
    public string unitDescription = "";

    // Footprint
    public List<int> footprint = new List<int>();

    // Win Condition Attributes
    public float research = 0;
    public float funding = 0;
    public float development = 0;

    // Defense
    public int physicalDefense = 0;
    public int elementalDefense = 0;
    public int arcanaDefense = 0;
    public int poisonDefense = 0;
    public int pureDefense = 0;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleDeath();
    }


    public void TakeDamage(float damageAmount, float shieldBonus, float minimum, float maximum, DamageType type)
    {
        float damageTaken = Mathf.Min(Mathf.Max(TotalDamageAfterReduction(damageAmount, type), minimum), maximum);

        if (hasShields)
        {
            shieldCurrent -= damageTaken;
            if (shieldCurrent < 0)
            {
                lifeCurrent += shieldCurrent;
                shieldCurrent = 0;
            }
        }
        else
        {
            lifeCurrent -= damageTaken;
        }
    }


    public float TotalDamageAfterReduction(float damageAmount, DamageType type)
    {
        float returnedDamageAmount = 0;
        switch (type)
        {
            case DamageType.Arcana:
                returnedDamageAmount = damageAmount * Mathf.Pow(damageReductionBaseModifier, arcanaDefense);
                break;
            case DamageType.Elemental:
                returnedDamageAmount = damageAmount * Mathf.Pow(damageReductionBaseModifier, elementalDefense);
                break;
            case DamageType.Physical:
                returnedDamageAmount = damageAmount * Mathf.Pow(damageReductionBaseModifier, physicalDefense);
                break;
            case DamageType.Poison:
                returnedDamageAmount = damageAmount * Mathf.Pow(damageReductionBaseModifier, poisonDefense);
                break;
            case DamageType.Pure:
                returnedDamageAmount = damageAmount * Mathf.Pow(damageReductionBaseModifier, pureDefense);
                break;
            default:
                returnedDamageAmount = damageAmount;
                break;
        }
        return returnedDamageAmount;
    }

    public void HandleDeath()
    {
        if (isAlive == true)
        {
            if (lifeCurrent < 0)
            {
                isAlive = false;
            }
        }
        else
        {
            if (decayTimer > 0)
            {
                decayTimer -= 1;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
