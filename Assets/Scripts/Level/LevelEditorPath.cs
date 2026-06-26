using UnityEngine;

/// <summary>
/// Marks a path group created by LevelEditorWindow.
/// Stores the spawnerIndex so it survives round-trips through the editor.
/// </summary>
public class LevelEditorPath : MonoBehaviour
{
    public int spawnerIndex;
}
