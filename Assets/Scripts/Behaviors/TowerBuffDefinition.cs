/// <summary>
/// Data describing a permanent or level-scoped stat buff applied to a tower.
/// Created at runtime by ModifierBuffApplicator — not a ScriptableObject.
/// </summary>
[System.Serializable]
public class TowerBuffDefinition
{
    public string id            = "";
    public string displayName   = "";
    public float  damageMult    = 0f;   // additive bonus, e.g. 0.25 = +25%
    public float  fireRateMult  = 0f;   // additive bonus
    public float  rangeMult     = 0f;   // additive bonus
}
