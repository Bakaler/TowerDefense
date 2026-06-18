using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerScriptParent : MonoBehaviour
{
    public ResourceManagerScript resourceManager;
    public List<GameObject> turrentTypes;
    public List<GameObject> turrents = new List<GameObject>();
    public bool purchased = false;

    public int towerCostResourceOne = 0;

    public int towerResearchScore = 1;




    // Start is called before the first frame update
    void Start()
    {
        resourceManager = GameObject.FindGameObjectWithTag("ResourceManager").GetComponent<ResourceManagerScript>();
        resourceManager.structures.Add(gameObject);
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (purchased)
        {

        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Mouse1))
            {
                Place();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                Destroy(gameObject);
            }
        }
    }

    public int GetTowerResearchScore()
    {
        return towerResearchScore;
    }

    void Place()
    {

        if (resourceManager.resourceOne >= towerCostResourceOne)
        {
            transform.SetParent(null);
            for (int i = 0, limi = turrentTypes.Count; i < limi; i++)
            {
                turrents.Add(Instantiate(turrentTypes[0], transform.position, transform.rotation));
            }
            purchased = true;
            resourceManager.ChangeResourceOne(towerCostResourceOne * -1);
        }
    }
}
