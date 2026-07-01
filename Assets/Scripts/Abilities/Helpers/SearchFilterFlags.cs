[System.Serializable]
public class SearchFilterFlags
{
    public FilterRule IsDead = FilterRule.Excluded;
    public FilterRule IsSelf = FilterRule.Allowed;
}
