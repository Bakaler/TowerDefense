using UnityEngine;

/// <summary>
/// When this unit dies it spawns 4 smaller "splitter_mini" units that continue along the path.
/// Attach via units.json components entry "splitter_on_death".
/// </summary>
public class SplitterOnDeath : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("splitter_on_death", typeof(SplitterOnDeath));

    public string miniUnitId = "splitter_mini";
    public int    miniCount  = 4;

    private UnitManager _unit;
    private bool        _split;

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<SplitterData>(dataJson);
        if (d == null) return;
        if (!string.IsNullOrEmpty(d.miniUnitId)) miniUnitId = d.miniUnitId;
        if (d.miniCount > 0) miniCount = d.miniCount;
    }

    void Start() => _unit = GetComponent<UnitManager>();

    void Update()
    {
        if (_split || _unit == null || _unit.isAlive) return;
        _split = true;
        SpawnMinis();
    }

    void SpawnMinis()
    {
        if (UnitFactory.Instance == null || PathGraph.Instance == null) return;

        // Grab the route progress from this unit's RouteFollower
        var myFollower = GetComponent<RouteFollower>();

        for (int i = 0; i < miniCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(-0.3f, 0.3f), 0f);

            var go = UnitFactory.Instance.Build(miniUnitId, transform.position + offset);
            if (go == null) continue;

            var unit = go.GetComponent<UnitManager>();
            if (unit == null) continue;

            // Resume from the same point on the path
            if (myFollower != null && myFollower.HasRoute)
            {
                var follower = go.GetComponent<RouteFollower>();
                if (follower == null) follower = go.AddComponent<RouteFollower>();
                follower.StartRoute(myFollower.CurrentRoute, unit.speedMax, myFollower.Progress);
            }
            else
            {
                // Fallback: let WaveManager/spawner logic handle it
            }

            WaveManager.Instance?.RegisterUnit(unit);
        }
    }

    [System.Serializable]
    class SplitterData
    {
        public string miniUnitId;
        public int    miniCount;
    }
}
