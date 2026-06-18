using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Turrent : MonoBehaviour
{

    public GameObject target;
    public List<GameObject> enemiesInRange = new List<GameObject>();


    public GameObject projectile;
    public float coolDown = 5;


    private float timer = 5;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        CleanUpDead();

        if (timer < coolDown)
        {
            timer += Time.deltaTime;
        }
        else
        {
            if (target != null && !target.gameObject.GetComponent<UnitManager>().isDead)
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
        GameObject newProjectile = Instantiate(projectile, new Vector3(transform.position.x, transform.position.y , 0), transform.rotation);
        newProjectile.GetComponent<ProjectileFollow>().target = target;
    }

    void CleanUpDead()
    {
        List<GameObject> aliveEnemiesInRange = new List<GameObject>();
        for (int i = 0; i < enemiesInRange.Count; i++)
        {
            if (!enemiesInRange[i].GetComponent<UnitManager>().isDead)
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

        if(collision.gameObject == target)
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
            for (int i = 0; i< enemiesInRange.Count; i++)
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
