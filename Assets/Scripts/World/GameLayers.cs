using UnityEngine;

/// <summary>
/// Cached physics layer masks. UnitFactory builds every enemy on the "Enemy" layer
/// (falls back to index 10), so enemy searches can skip all other colliders instead
/// of scanning the whole scene. If a unit definition ever overrides def.layer to a
/// different layer, masked queries will not see it — extend the mask here.
/// </summary>
public static class GameLayers
{
    static int _enemyMask;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _enemyMask = 0;

    public static int EnemyMask
    {
        get
        {
            if (_enemyMask == 0)
            {
                int layer = LayerMask.NameToLayer("Enemy");
                _enemyMask = 1 << (layer >= 0 ? layer : 10);
            }
            return _enemyMask;
        }
    }
}
