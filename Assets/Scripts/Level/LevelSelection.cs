/// <summary>
/// Carries the player's level choice from LevelSelectScene into GameScene.
/// A simple static int survives scene loads without needing DontDestroyOnLoad.
/// </summary>
public static class LevelSelection
{
    public static int SelectedLevel = 1;
}
