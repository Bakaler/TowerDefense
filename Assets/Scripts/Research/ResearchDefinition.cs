using System;

[Serializable]
public class ResearchDefinition
{
    public string id          = "";
    public string towerId     = "";    // matches TowerDefinition.id
    public string displayName = "";
    public string description = "";
    public int    techCost    = 20;
    public string effectType  = "";    // "FireRateBonus" | "BulletCountBonus" | "ProjectileHitsBonus"
    public float  effectValue = 0f;
    public string abilityId   = "";    // used by FireRateBonus
    public string effectId    = "";    // used by BulletCountBonus
    public string projectileId = "";   // used by ProjectileHitsBonus
}

[Serializable]
class ResearchDefinitionList
{
    public ResearchDefinition[] researches;
}
