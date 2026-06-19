public static class EffectExecutor
{
    public static void ExecuteEffect(Effect effect, EffectContext context)
    {
        if (effect == null) return;
        effect.Execute(context);
    }
}
