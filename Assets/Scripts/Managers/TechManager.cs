using UnityEngine;

public class TechManager : MonoBehaviour
{
    public static TechManager Instance { get; private set; }

    public int  Tech       { get; private set; }
    public bool T2Unlocked { get; private set; }
    public bool T3Unlocked { get; private set; }

    public const int T2Cost = 15;
    public const int T3Cost = 30;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddTech(int amount)
    {
        if (amount <= 0) return;
        Tech += amount;
    }

    public bool TryUnlockT2()
    {
        if (T2Unlocked || Tech < T2Cost) return false;
        Tech -= T2Cost;
        T2Unlocked = true;
        return true;
    }

    public bool TryUnlockT3()
    {
        if (!T2Unlocked || T3Unlocked || Tech < T3Cost) return false;
        Tech -= T3Cost;
        T3Unlocked = true;
        return true;
    }
}
