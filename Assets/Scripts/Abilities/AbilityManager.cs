using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityManager : MonoBehaviour
{
    public bool isCasting { get; protected set; }
    public bool isPreparing { get; protected set; }
    public bool isRecovering { get; protected set; }

    protected Dictionary<Ability_Effect, AbilityInstance> activeInstances
        = new Dictionary<Ability_Effect, AbilityInstance>();

    private TowerInfo _towerInfo;

    void Awake() => _towerInfo = GetComponent<TowerInfo>();

    private void Update()
    {
        float speedMult = _towerInfo != null ? _towerInfo.AuraSpeedMultiplier : 1f;
        Tick(Time.deltaTime * speedMult);
    }

    public void RegisterAbility(Ability_Effect ability)
    {
        if (!activeInstances.ContainsKey(ability))
            activeInstances[ability] = new AbilityInstance(ability);
    }

    public AbilityInstance GetInstance(Ability_Effect ability)
    {
        activeInstances.TryGetValue(ability, out var instance);
        return instance;
    }

    public IEnumerable<AbilityInstance> GetInstances() => activeInstances.Values;

    public void Tick(float deltaTime)
    {
        foreach (var inst in activeInstances.Values)
            inst.Tick(deltaTime);
    }

    public bool TryExecuteAbility(Ability_Effect ability, EffectContext context)
    {
        if (!activeInstances.TryGetValue(ability, out var instance))
        {
            RegisterAbility(ability);
            instance = activeInstances[ability];
        }

        if (!instance.IsReady) return false;

        instance.Trigger();
        StartCoroutine(ExecuteAbilityCoroutine(ability, context));
        return true;
    }

    private IEnumerator ExecuteAbilityCoroutine(Ability_Effect ability, EffectContext context)
    {
        isPreparing = true;
        isCasting = false;

        if (ability.prepare_time > 0)
            yield return new WaitForSeconds(ability.prepare_time);

        isPreparing = false;
        isCasting = true;

        float castDelay = ability.cast_start_time - ability.prepare_time;
        if (castDelay > 0)
            yield return new WaitForSeconds(castDelay);

        if (ability.effect != null)
        {
            Debug.Log($"[AbilityManager] Firing effect '{ability.effect.name}' on '{context.Target?.name}'");
            EffectExecutor.ExecuteEffect(ability.effect, context);
        }
        else
        {
            Debug.LogWarning($"[AbilityManager] ability.effect is NULL on '{ability.abilityID}' — effect won't fire!");
        }

        float castFinishDelay = ability.cast_finish_time - ability.cast_start_time;
        if (castFinishDelay > 0)
            yield return new WaitForSeconds(castFinishDelay);

        isRecovering = true;

        float finishDelay = ability.finish_time - ability.cast_finish_time;
        if (finishDelay > 0)
            yield return new WaitForSeconds(finishDelay);

        isCasting = false;
        isRecovering = false;
    }
}
