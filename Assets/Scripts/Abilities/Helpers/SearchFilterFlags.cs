[System.Serializable]
public class SearchFilterFlags
{
    public FilterRule IsDead     = FilterRule.Excluded;
    public FilterRule IsSelf     = FilterRule.Allowed;
    public FilterRule HasShield  = FilterRule.Allowed;
    public FilterRule IsStunned  = FilterRule.Allowed;
    public FilterRule IsVisible  = FilterRule.Allowed;
}
