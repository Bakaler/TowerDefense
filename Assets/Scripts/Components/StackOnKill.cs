using UnityEngine;

/// <summary>
/// Generic component: gains a stack on each kill by this tower, up to maxStacks.
/// Each stack multiplies damage via TowerInfo.ExtraMultiplier.
/// JSON keys: perStackBonus, maxStacks
/// </summary>
public class StackOnKill : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("stack_on_kill", typeof(StackOnKill));

    public float perStackBonus = 0.04f;
    public int   maxStacks     = 50;

    private int       _stacks;
    private TowerInfo _info;

    [System.Serializable]
    class Data { public float perStackBonus = 0.04f; public int maxStacks = 50; }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        perStackBonus = d.perStackBonus;
        maxStacks     = d.maxStacks;
    }

    void Awake()  => _info = GetComponent<TowerInfo>();
    void OnEnable()  => TowerInfo.OnTowerKill += HandleKill;
    void OnDisable() => TowerInfo.OnTowerKill -= HandleKill;

    void HandleKill(TowerInfo killer)
    {
        if (killer != _info || _stacks >= maxStacks) return;
        _stacks++;
        _info.ExtraMultiplier = 1f + perStackBonus * _stacks;
    }

    public int Stacks => _stacks;
}
