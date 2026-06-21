using UnityEngine;

/// <summary>
/// Entropy Tower component — gains a damage stack (max 50) on each kill by this tower.
/// Each stack multiplies damage by (1 + perStackBonus * stacks).
/// </summary>
public class EntropyTower : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("entropy_tower", typeof(EntropyTower));

    public float perStackBonus = 0.04f;
    public int   maxStacks     = 50;

    private int       _stacks;
    private TowerInfo _info;

    [System.Serializable]
    private class Data { public float perStackBonus = 0.04f; }

    public void Initialize(string dataJson)
    {
        if (!string.IsNullOrEmpty(dataJson))
        {
            var d = JsonUtility.FromJson<Data>(dataJson);
            if (d != null) perStackBonus = d.perStackBonus;
        }
    }

    void Awake()
    {
        _info = GetComponent<TowerInfo>();
    }

    void OnEnable()  => TowerInfo.OnTowerKill += HandleKill;
    void OnDisable() => TowerInfo.OnTowerKill -= HandleKill;

    void HandleKill(TowerInfo killer)
    {
        if (killer != _info) return;
        if (_stacks >= maxStacks) return;

        _stacks++;
        ApplyMultiplier();
    }

    void ApplyMultiplier()
    {
        if (_info == null) return;
        _info.ExtraMultiplier = 1f + perStackBonus * _stacks;
    }

    public int Stacks => _stacks;
}
