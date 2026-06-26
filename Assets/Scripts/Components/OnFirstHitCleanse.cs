using System.Collections;
using UnityEngine;

/// <summary>
/// On the first hit this unit receives: removes all debuffs (resets speedCurrent to speedMax)
/// and grants a 200% move-speed bonus for 1.5 seconds.
/// Attach via units.json components entry "on_first_hit_cleanse".
/// </summary>
public class OnFirstHitCleanse : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("on_first_hit_cleanse", typeof(OnFirstHitCleanse));

    public float speedMultiplier = 3f;   // 200% bonus = 3× base speed
    public float duration        = 1.5f;

    private UnitManager _unit;
    private bool        _triggered;

    public void Initialize(string dataJson) { }

    void Start()
    {
        _unit = GetComponent<UnitManager>();
    }

    void Update()
    {
        if (_triggered || _unit == null || !_unit.isAlive) return;

        // Detect first hit: took damage OR had a debuff applied (e.g. root with no damage)
        if (_unit.lifeCurrent < _unit.lifeMax || _unit.speedCurrent < _unit.speedMax)
        {
            _triggered = true;
            StartCoroutine(CleanseAndBurst());
        }
    }

    IEnumerator CleanseAndBurst()
    {
        // Cleanse all CC types — unit can still cast regardless of behavior state
        var behaviorHandler = _unit.GetComponent<BehaviorHandler>();
        if (behaviorHandler != null)
        {
            behaviorHandler.RemoveByType(BehaviorType.Rooted);
            behaviorHandler.RemoveByType(BehaviorType.Slowed);
            behaviorHandler.RemoveByType(BehaviorType.Stunned);
            behaviorHandler.RemoveByType(BehaviorType.Silenced);
        }
        _unit.speedCurrent = _unit.speedMax * speedMultiplier;

        yield return new WaitForSeconds(duration);

        // Let BehaviorHandler reapply any active slows; fall back to speedMax if none
        if (_unit != null && _unit.isAlive)
        {
            if (behaviorHandler != null) behaviorHandler.Refresh();
            else _unit.speedCurrent = _unit.speedMax;
        }
    }
}
