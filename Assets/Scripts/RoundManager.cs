using UnityEngine;

/// <summary>
/// Legacy stub. Wave logic has moved to WaveManager.
/// This class is kept so existing scene references don't break immediately.
/// You can safely remove it once WaveManager is confirmed working.
/// </summary>
public class RoundManager : MonoBehaviour
{
    void Start()
    {
        Debug.LogWarning("[RoundManager] This component is a legacy stub. " +
                         "Add WaveManager to this GameObject instead.");
    }
}
