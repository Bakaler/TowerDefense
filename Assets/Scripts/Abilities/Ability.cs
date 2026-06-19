using UnityEngine;

public abstract class Ability : ScriptableObject
{
    public string abilityName = "Unknown Name";
    public string abilityID = "UnknownName";

    public string editorPrefix = "";
    public string editorSuffix = "";
}
