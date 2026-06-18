using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogicManager : MonoBehaviour
{
    public ResourceManagerScript resourceManager;
    public float lives;
    public float developmentWinCondition;

    public bool gameOver = false;

    // Start is called before the first frame update
    void Start()
    {
        resourceManager = GameObject.FindGameObjectWithTag("ResourceManager").GetComponent<ResourceManagerScript>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!gameOver)
        {
            CheckDevelopmentWinCondition();
            CheckLivesLossCondition();
        }
    }

    public void CheckDevelopmentWinCondition()
    {
        if (resourceManager.development >= developmentWinCondition)
        {
            Win();
        }
    }

    public void CheckLivesLossCondition()
    {
        if (lives <= 0)
        {
            Lose();
        }
    }

    public void UpdateLives(float livesUpdate)
    {
        lives += livesUpdate; 
    }


    public void Win()
    {
        Debug.Log("WINNER");
        gameOver = true;
    }

    public void Lose()
    {
        Debug.Log("LOSER");
        gameOver = true;
    }
}
