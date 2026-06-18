using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FormationSpawn : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0, limi = transform.childCount; i < limi; i++)
        {

        }
    }

    public void SetChildWayPoint(Vector3 waypoint)
    {
        while (transform.childCount > 0)
        {

            UnitManager c = transform.GetChild(0).GetComponent<UnitManager>();
            c.xOffset = c.gameObject.transform.position.x - transform.position.x;
            c.yOffset = c.gameObject.transform.position.y - transform.position.y;
            c.wayPoint = waypoint;

            transform.GetChild(0).SetParent(null);
        }

        Destroy(gameObject);
    }
}
