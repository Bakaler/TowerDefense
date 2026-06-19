public class AbilityInstance
{
    public Ability_Effect Definition { get; private set; }
    public float CooldownRemaining { get; private set; }

    public AbilityInstance(Ability_Effect definition)
    {
        Definition = definition;
        CooldownRemaining = 0f;
    }

    public bool IsReady => CooldownRemaining <= 0f;

    public void Trigger()
    {
        if (Definition.cost != null)
            CooldownRemaining = Definition.cost.cooldownDuration;
    }

    public void Tick(float deltaTime)
    {
        if (CooldownRemaining > 0f)
            CooldownRemaining -= deltaTime;
    }
}
