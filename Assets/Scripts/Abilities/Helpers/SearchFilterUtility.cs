public static class SearchFilterUtility
{
    public static bool PassesFilters(UnitParentClass caster, UnitParentClass candidate, SearchFilterFlags flags)
    {
        // IsDead
        bool isDead = !candidate.isAlive;
        switch (flags.IsDead)
        {
            case FilterRule.Required:
                if (!isDead) return false;
                break;
            case FilterRule.Excluded:
                if (isDead) return false;
                break;
        }

        // IsSelf
        bool isSelf = candidate == caster;
        switch (flags.IsSelf)
        {
            case FilterRule.Required:
                if (!isSelf) return false;
                break;
            case FilterRule.Excluded:
                if (isSelf) return false;
                break;
        }

        return true;
    }
}
