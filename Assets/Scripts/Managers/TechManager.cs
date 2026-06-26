using UnityEngine;

public class TechManager : MonoBehaviour
{
    public static TechManager Instance { get; private set; }

    public int  Tech       { get; private set; }
    public bool T2Unlocked { get; private set; }
    public bool T3Unlocked { get; private set; }
    public bool T4Unlocked { get; private set; }
    public bool T5Unlocked { get; private set; }

    public const int T2Cost = 15;
    public const int T3Cost = 30;
    public const int T4Cost = 70;
    public const int T5Cost = 150;

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

    public bool TrySpendTech(int amount)
    {
        if (Tech < amount) return false;
        Tech -= amount;
        return true;
    }

    public int techAmount => Tech;

    public void ResetAll()
    {
        Tech       = 0;
        T2Unlocked = false;
        T3Unlocked = false;
        T4Unlocked = false;
        T5Unlocked = false;
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

    public bool TryUnlockT4()
    {
        if (!T3Unlocked || T4Unlocked || Tech < T4Cost) return false;
        Tech -= T4Cost;
        T4Unlocked = true;
        return true;
    }

    public bool TryUnlockT5()
    {
        if (!T4Unlocked || T5Unlocked || Tech < T5Cost) return false;
        Tech -= T5Cost;
        T5Unlocked = true;
        return true;
    }
}
