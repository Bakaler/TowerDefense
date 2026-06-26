using UnityEngine;

/// <summary>
/// Debug cheat codes. Active in Editor and Development builds only.
///
///   Ctrl + Alt + M  — +9999 gold
///   Ctrl + Alt + T  — unlock all research tiers (T2→T5) + dump 999 tech
/// </summary>
public class CheatManager : MonoBehaviour
{
    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.O)) CheatMoney();
        if (Input.GetKeyDown(KeyCode.P)) CheatTech();
#endif
    }

    static void CheatMoney()
    {
        var rm = FindFirstObjectByType<ResourceManagerScript>();
        if (rm == null) return;
        rm.ChangeResourceOne(9999);
        Debug.Log("[Cheat] +9999 gold");
    }

    static void CheatTech()
    {
        var tm = TechManager.Instance;
        if (tm == null) return;
        tm.AddTech(999);
        tm.TryUnlockT2();
        tm.TryUnlockT3();
        tm.TryUnlockT4();
        tm.TryUnlockT5();
        Debug.Log("[Cheat] All research tiers unlocked + 999 tech");
    }
}
