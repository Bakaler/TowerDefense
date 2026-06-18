using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public ResourceManagerScript resourceManager;
    public List<GameObject> spawners;
    public List<GameObject> aliveEnemies;


    public bool roundIsActive = false;
    public int roundNumber = 0;


    // Start is called before the first frame update
    void Start()
    {
        resourceManager = GameObject.FindGameObjectWithTag("ResourceManager").GetComponent<ResourceManagerScript>();
    }

    // Update is called once per frame
    void Update()
    {
        resourceManager.UpdateResearchScore();


        if (Input.GetKey(KeyCode.Space) && !roundIsActive)
        {
            startRound();
        } 
        else if (roundIsActive)
        {
            checkRoundStatus();
        }
    }

    public void checkRoundStatus()
    {
        bool roundsActive = false;
        for (int i = 0, limi = spawners.Count; i < limi; i++)
        {
            if (spawners[i].GetComponent<UnitSpawner>().isActive)
            {
                roundsActive = true;
                break;
            }
        }


        List<GameObject> aliveEnemiesNew = new List<GameObject>();
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            if (!aliveEnemies[i].GetComponent<UnitManager>().isDead)
            {
                aliveEnemiesNew.Add(aliveEnemies[i]);
            }
        }

        aliveEnemies = aliveEnemiesNew;

        if (aliveEnemies.Count != 0)
        {
            roundsActive = true;
        }

        roundIsActive = roundsActive;

        if (!roundIsActive)
        {
            resourceManager.UpdateDevelopmentScore();
        }
    }


    public void startRound()
    {
        roundIsActive = true;
        for (int i = 0, limi = spawners.Count; i< limi; i++)
        {
            spawners[i].GetComponent<UnitSpawner>().formationArrayIndex = 0;
            if (spawners[i].GetComponent<UnitSpawner>().formationSpawn.Count > spawners[i].GetComponent<UnitSpawner>().spawnCount - 1)
            {
                spawners[i].GetComponent<UnitSpawner>().spawnCount += 1;
            }
        }

        roundNumber += 1;
    }
}
