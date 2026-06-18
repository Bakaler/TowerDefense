using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingIncomeScript : MonoBehaviour
{

    public ResourceManagerScript resourceManager;
    //public float moveSpeed = 3;

    public float lifeSpan = 5;
    private float timer = 0;
    // Start is called before the first frame update
    void Start()
    {
        resourceManager = GameObject.FindGameObjectWithTag("ResourceManager").GetComponent<ResourceManagerScript>();
    }

    // Update is called once per frame
    void Update()
    {

        if (timer < lifeSpan)
        {
            timer += Time.deltaTime;
        }
        else
        {

            Destroy(gameObject);
        }


        if (Input.GetMouseButtonDown(0))
        {

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                resourceManager.ChangeResourceOne(5);
                Destroy(gameObject);
            }
        }

        
    }
}





//Debug.Log(">", hit.collider.gameObject);
/**
if (hit.collider != null && hit.collider.gameObject == gameObject)
{

    if (isAlive)
    {
        logic.addScore(1);
        isAlive = false;
    }

    Rigidbody2D hitObject = hit.collider.GetComponent<Rigidbody2D>();
    hitObject.gravityScale = 10;

    if (Random.Range(0, 4) > 2)
    {
        deathSFX.PlayOneShot(deathClip);
    }
*/