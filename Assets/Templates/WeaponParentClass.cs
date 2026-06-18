using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponParentClass : MonoBehaviour
{

    // Range
    public CircleCollider2D rangeCollider;
    public List<UnitParentClass> unitsInRange = new List<UnitParentClass>();
    public float range = 0;
    public float rangeMultiplier = 1;
    public float rangeAddition = 0;
    public float rangeSubtraction = 0;

    public float minimumRange = 0;

    // Attack
    public float period = 0;
    public float periodMultiplier = 1;

    public bool neverMiss = false;
    public EffectParentClass postEffect;
    public EffectParentClass preEffect;

    // Charges
    public int chargeMax = 0;
    public int chargeStart = 0;
    public int chargeCurrent = 0;
    public int chargeCost = 0;
    public float chargeRecharge = 0;
    public float chargeRechargeMultiplier = 1;

    public GameObject target;
    public List<GameObject> enemiesInRange = new List<GameObject>();

    public GameObject projectile;

    private float timer = 0;

    // Start is called before the first frame update
    void Start()
    {
        rangeCollider = gameObject.GetComponent<CircleCollider2D>();
        rangeCollider.radius = range;
    }

    // Update is called once per frame
    void Update()
    {

        CleanUpDead();

        if (timer < (period*periodMultiplier))
        {
            timer += Time.deltaTime;
        }
        else
        {
            if (target != null && target.gameObject.GetComponent<UnitParentClass>().isAlive)
            {
                Fire();
                timer = 0;
            }
            else
            {
                GetClosestEnemy();
            }
        }

    }

    void Fire()
    {
        GameObject newProjectile = Instantiate(projectile, new Vector3(transform.position.x, transform.position.y, 0), transform.rotation);
        newProjectile.GetComponent<ProjectileFollow>().target = target;
    }

    void CleanUpDead()
    {
        List<GameObject> aliveEnemiesInRange = new List<GameObject>();
        for (int i = 0; i < enemiesInRange.Count; i++)
        {
            if (enemiesInRange[i].GetComponent<UnitParentClass>().isAlive)
            {
                aliveEnemiesInRange.Add(enemiesInRange[i]);
            }
        }
        enemiesInRange = aliveEnemiesInRange;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == 10)
        {
            enemiesInRange.Add(collision.gameObject);
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {

        if (collision.gameObject == target)
        {
            target = null;
        }

        if (collision.gameObject.layer == 10 && enemiesInRange.Contains(collision.gameObject))
        {
            enemiesInRange.Remove(collision.gameObject);
        }
    }

    void GetClosestEnemy()
    {
        if (enemiesInRange.Count > 0)
        {
            GameObject bestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;
            Vector3 currentPosition = transform.position;
            for (int i = 0; i < enemiesInRange.Count; i++)
            {
                Vector3 directionToTarget = enemiesInRange[i].transform.position - currentPosition;
                float dSqrToTarget = directionToTarget.sqrMagnitude;
                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    bestTarget = enemiesInRange[i];
                }
            }
            target = bestTarget;
        }
    }

}
