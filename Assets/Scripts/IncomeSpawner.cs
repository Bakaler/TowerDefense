using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IncomeSpawner : MonoBehaviour
{

    public GameObject fallingIncome;
    public float spawnRate = 10;
    private float timer = 4;
    public float heightOffset = 1;
    public float widthOffset = 2;

    // Start is called before the first frame update
    void Start()
    {
        SpawnIncome();
    }

    // Update is called once per frame
    void Update()
    {
        if (timer < spawnRate)
        {
            timer += Time.deltaTime;
        }
        else
        {
            SpawnIncome();
            timer = 0;
        }
    }

    void SpawnIncome()
    {
        Instantiate(fallingIncome, new Vector3(transform.position.x + Random.Range(-1 * widthOffset, widthOffset), transform.position.y + Random.Range(-1 * heightOffset, heightOffset), 0), transform.rotation);
    }
}

