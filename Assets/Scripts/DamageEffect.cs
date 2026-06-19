using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageEffect : MonoBehaviour
{
    public float moveSpeed = 1;
    public int damage = 1;

    public Vector3 destination;

    void Update()
    {
        // Move the projectile forward based on its speed
        transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Impact");

        if (collision.gameObject.layer == 10)
        {
            // Check if the projectile hits the target unit
            UnitParentClass unitHealth = collision.gameObject.GetComponent<UnitParentClass>();
            if (unitHealth != null)
            {
                unitHealth.TakeDamage(damage, 0f, 0f, float.MaxValue, DamageType.Physical);

                Destroy(transform.parent.gameObject);
                // Destroy the projectile after hitting the unit
                Destroy(gameObject);
            }
        }

    }

}
