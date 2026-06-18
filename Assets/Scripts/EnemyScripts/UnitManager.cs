using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    public Collider2D myCollider;

    public int bounty;
    public float life;
    public int armor;
    public float moveSpeed;
    public int deathBlow = 1;

    public Vector3 wayPoint = new Vector3(0f, 0f, 0f);
    public Vector3 nextWayPoint = new Vector3(0f, 0f, 0f);


    public int decayTimer = 24;
    public bool isDead = false;

    public float xOffset = 0;
    public float yOffset = 0;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if (!isDead)
        {
            Move();

            if (life <= 0)
            {
                Die();
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


    public void TakeDamage(float damage)
    {
        float damageTaken = (damage - armor);
        if (damageTaken < 1)
        {
            damageTaken = 1;
        }
        life -= damageTaken;
    }

    void Move()
    {
        if (transform.position.x == wayPoint.x && transform.position.y == wayPoint.y)
        {
            wayPoint = nextWayPoint;
        }
        transform.position = Vector3.MoveTowards(transform.position, new Vector3(wayPoint.x, wayPoint.y, wayPoint.z), moveSpeed * Time.deltaTime);
    }

    public void Die()
    {
        //logic.ChangeGold(bounty);
        isDead = true;
        myCollider.enabled = false;
    }
}
