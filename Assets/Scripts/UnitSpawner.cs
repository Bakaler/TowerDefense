using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    public RoundManager roundManager;

    public WayPoint firstWayPoint;
    public List<GameObject> formationSpawn;

    public int spawnCount = -1;
    public int formationArrayIndex = -1;
    public bool isActive = false;

    public float timer;
    public float coolDown;


    // Start is called before the first frame update
    void Start()
    {
        roundManager = GameObject.FindGameObjectWithTag("RoundManager").GetComponent<RoundManager>();
        roundManager.spawners.Add(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        if (formationArrayIndex != -1)
        {
            if (formationArrayIndex == spawnCount)
            {
                isActive = false;
            }
            else
            {
                isActive = true;
            }

            if (timer < coolDown)
            {
                timer += Time.deltaTime;
            }
            else if (formationArrayIndex < spawnCount)
            {
                Spawn();
                timer = 0;
            }
            
        }
    }

    void Spawn()
    {
        GameObject newFormation = Instantiate(formationSpawn[formationArrayIndex], new Vector3(transform.position.x, transform.position.y, 0), transform.rotation);

        FormationSpawn formation = newFormation.GetComponent<FormationSpawn>();

        for (int i = 0, limi = newFormation.transform.childCount; i<limi; i++)
        {
            //Debug.Log(newFormation.transform.GetChild(i).gameObject);
            roundManager.aliveEnemies.Add(newFormation.transform.GetChild(i).gameObject);

        }
        //Debug.Log(firstWayPoint.transform.position);
        //Debug.Log(formation);
        formation.SetChildWayPoint(firstWayPoint.transform.position);
        formationArrayIndex++;
    }
}
