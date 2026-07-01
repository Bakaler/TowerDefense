/// <summary>
/// Carries the player's level + difficulty choice from LevelSelectScene into GameScene.
/// Simple statics survive scene loads without DontDestroyOnLoad.
/// </summary>
public static class LevelSelection
{
    public static int   SelectedLevel      = 1;
    public static int   SelectedDifficulty = 0;
    // Set from DifficultyDef before level load
    public static float EnemyHpMult    = 1f;
    public static float EnemySpeedMult = 1f;
    public static float GoldMult       = 1f;
    public static float BountyMult     = 1f;
}
