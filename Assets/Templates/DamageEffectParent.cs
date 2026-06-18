using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageEffectParent : EffectParentClass
{

    public float minimum = 0;
    public float maximum = 10000;
    public DamageType type = DamageType.Physical;
    public float amount = 0;
    public float shieldBonus = 0;
    public float searchArea = 0;
    public float arc = 0;
    public int maxCount = 0;
    public float radius = 0;

    public float criticalChance = 0;
    public float criticalDamageModifier = 0;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnCollisionEnter2D(Collision2D collision)
    {

        if (collision.gameObject.layer == 10)
        {
            // Check if the projectile hits the target unit
            UnitParentClass unit = collision.gameObject.GetComponent<UnitParentClass>();
            if (unit != null)
            {
                // Deal damage to the unit
                float damageAmount = amount;
                if (Random.Range(0f,1f) <= criticalChance)
                {
                    damageAmount *= criticalDamageModifier;
                }

                unit.TakeDamage(damageAmount, shieldBonus, minimum, maximum, type);

                Destroy(transform.parent.gameObject);
                // Destroy the projectile after hitting the unit
                Destroy(gameObject);
            }
        }

    }


}
