using System.Collections;
using UnityEngine;

/// <summary>
/// Periodically launches a ResearchOrb from off-screen toward this tower.
/// Click it in-flight: full tech value. Let it arrive: half value.
/// Arcane balance score reduces the spawn interval.
/// </summary>
public class ResearchTower : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("research_tower", typeof(ResearchTower));

    // ── Data ──────────────────────────────────────────────────────────
    public float  orbInterval    = 12f;
    public float  minInterval    = 4f;
    public float  arcaneScale    = 0.12f;
    public float  travelTime     = 9f;
    public int    fullValue      = 2;
    public int    arrivalValue   = 1;
    public string orbSpriteSheet = "";
    public int    orbSpriteIndex = 1;

    // ── IFactoryInitializable ─────────────────────────────────────────

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<ResearchTowerData>(dataJson);
        if (d == null) return;
        if (d.orbInterval    > 0f)                   orbInterval    = d.orbInterval;
        if (d.minInterval    > 0f)                   minInterval    = d.minInterval;
        if (d.arcaneScale    > 0f)                   arcaneScale    = d.arcaneScale;
        if (d.travelTime     > 0f)                   travelTime     = d.travelTime;
        if (d.fullValue      > 0)                    fullValue      = d.fullValue;
        if (d.arrivalValue   > 0)                    arrivalValue   = d.arrivalValue;
        if (!string.IsNullOrEmpty(d.orbSpriteSheet)) orbSpriteSheet = d.orbSpriteSheet;
        if (d.orbSpriteIndex >= 0)                   orbSpriteIndex = d.orbSpriteIndex;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start() => StartCoroutine(SpawnLoop());

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(Random.Range(1f, orbInterval * 0.4f));

        while (true)
        {
            float arcane   = BalanceManager.Instance != null ? BalanceManager.Instance.Arcane : 0f;
            float interval = Mathf.Max(minInterval, orbInterval / (1f + arcane * arcaneScale));
            yield return new WaitForSeconds(interval);

            ResearchOrb.Spawn(transform.position, orbSpriteSheet, orbSpriteIndex,
                              fullValue, arrivalValue, travelTime);
        }
    }
}

[System.Serializable]
public class ResearchTowerData
{
    public float  orbInterval    = 12f;
    public float  minInterval    = 4f;
    public float  arcaneScale    = 0.12f;
    public float  travelTime     = 9f;
    public int    fullValue      = 2;
    public int    arrivalValue   = 1;
    public string orbSpriteSheet = "";
    public int    orbSpriteIndex = 1;
}
