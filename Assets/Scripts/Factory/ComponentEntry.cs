using System;

/// <summary>
/// One component entry in a unit or tower definition.
/// 'key' must match a key registered in ComponentRegistry.
/// 'data' is a raw JSON string passed to that component's Initialize() method.
/// </summary>
[Serializable]
public class ComponentEntry
{
    public string key;
    public string data;
}
