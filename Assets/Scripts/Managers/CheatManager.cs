using UnityEngine;

/// <summary>
/// Debug cheat codes. Active in Editor and Development builds only.
///
///   O — +9999 gold
///   P — unlock all research tiers (T2→T5) + dump 999 tech
///   I — +9999 tower slots (effectively unlimited this level)
/// </summary>
public class CheatManager : MonoBehaviour
{
    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.O)) CheatMoney();
        if (Input.GetKeyDown(KeyCode.P)) CheatTech();
        if (Input.GetKeyDown(KeyCode.I)) CheatTowerSlots();
#endif
    }

    static void CheatMoney()
    {
        var rm = ResourceManagerScript.Instance;
        if (rm == null) return;
        rm.ChangeResourceOne(9999);
        Debug.Log("[Cheat] +9999 gold");
    }

    static void CheatTowerSlots()
    {
        var bm = BalanceManager.Instance;
        if (bm == null) return;
        bm.AddBonusSlots(9999);
        Debug.Log("[Cheat] +9999 tower slots");
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
