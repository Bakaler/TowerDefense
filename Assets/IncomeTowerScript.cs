using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IncomeTowerScript : TowerScriptParent
{


    public GameObject fallingIncome;
    public float spawnRate = 30;
    private float timer = 0;
    public float heightOffset = 0.5f;
    public float widthOffset = 0.5f;

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
        if (purchased)
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

    }

    void SpawnIncome()
    {
        Instantiate(fallingIncome, new Vector3(transform.position.x + Random.Range(-1 * widthOffset, widthOffset), transform.position.y + Random.Range(-1 * heightOffset, heightOffset), 0), transform.rotation);
    }
}
