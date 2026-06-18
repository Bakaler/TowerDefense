using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WayPoint : MonoBehaviour
{
    public LogicManager logic;

    public GameObject nextWayPoint;
    public bool endPoint;


    // Start is called before the first frame update
    void Start()
    {
        logic = GameObject.FindGameObjectWithTag("Logic").GetComponent<LogicManager>();

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == 10)
        {
            UnitManager unit = collision.gameObject.GetComponent<UnitManager>();

            if (!endPoint)
            {
                unit.nextWayPoint = nextWayPoint.gameObject.transform.position;
            }
            else
            {
                logic.UpdateLives(unit.deathBlow * -1);
                unit.Die();
            }
        }
    }
}
