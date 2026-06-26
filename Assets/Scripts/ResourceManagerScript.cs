using UnityEngine;

/// <summary>
/// Holds the player's resource values.
/// Display is handled by GameHUD — no Text refs needed here.
/// </summary>
public class ResourceManagerScript : MonoBehaviour
{
    [Header("Starting Values")]
    public int   resourceOne = 25;   // Gold
    public int   research    = 0;
    public float funding     = 1f;
    public float development = 0f;

    public void ResetToStart(int gold)
    {
        resourceOne = gold;
        research    = 0;
        funding     = 1f;
        development = 0f;
    }

    public void ChangeResourceOne(int delta)
    {
        resourceOne += delta;
    }

    public void ChangeResearch(int delta)
    {
        research += delta;
    }

    public void SetResearch(int value)
    {
        research = value;
    }

    public void ChangeFunding(float delta)
    {
        funding += delta;
    }

    public void ChangeDevelopment(float delta)
    {
        development += delta;
    }

    public void UpdateDevelopmentScore()
    {
        development += research * funding;
    }
}
