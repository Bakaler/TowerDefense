using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ResourceManagerScript : MonoBehaviour
{
    // UI
    public Text researchText;
    public Text fundingText;
    public Text developmentText;

    public Text resourceOneText;


    // Winning Resources
    public int research;
    public float funding;
    public float development;

    // Trading Resources
    public int resourceOne;


    public List<GameObject> structures = new List<GameObject>();


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateResearchScore()
    {
        int researchValue = 0;
        for (int i = 0, limi = structures.Count; i<limi; i++)
        {
            if (structures[i].GetComponent<TowerScriptParent>().purchased)
            {
                researchValue += structures[i].GetComponent<TowerScriptParent>().towerResearchScore;
            }
        }
        SetResearch(researchValue);
    }

    public void UpdateDevelopmentScore()
    {
        ChangeDevelopment(research * funding);
    }

    [ContextMenu("Change ResourceOne")]
    public void ChangeResourceOne(int resourceOneChange)
    {
        resourceOne += resourceOneChange;
        resourceOneText.text = resourceOne.ToString();
    }

    [ContextMenu("Change Research")]
    public void ChangeResearch(int researchChange)
    {
        research += researchChange;
        researchText.text = research.ToString();
    }

    public void SetResearch(int researchValue)
    {
        research = researchValue;
        researchText.text = research.ToString();
    }

    [ContextMenu("Change Funding")]
    public void ChangeFunding(int fundingChange)
    {
        funding += fundingChange;
        fundingText.text = funding.ToString();
    }

    [ContextMenu("Change Development")]
    public void ChangeDevelopment(float developmentChange)
    {
        development += developmentChange;
        developmentText.text = development.ToString();
    }

}
