using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PurchaseBuilding1 : MonoBehaviour
{

    public GameObject towerType;
    public GameObject tower;

    public int n;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }
    public void OnButtonPress()
    {
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        tower = Instantiate(towerType, new Vector3(mousePosition.x, mousePosition.y, 0), transform.rotation);
        tower.transform.SetParent(transform);
    }


}
