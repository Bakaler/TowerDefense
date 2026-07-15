using UnityEngine;

/// <summary>
/// Shared runtime materials. All code-built LineRenderers and sprite effects tint
/// through vertex/renderer color, so they can share one Sprites/Default material.
/// Per-object `new Material(...)` instances are NOT destroyed with their GameObject
/// and leak over a long session of building/selling — always assign this instead
/// (via sharedMaterial so no instance is created).
/// </summary>
public static class RuntimeMaterials
{
    static Material _spriteDefault;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _spriteDefault = null;

    public static Material SpriteDefault
    {
        get
        {
            if (_spriteDefault == null)
                _spriteDefault = new Material(Shader.Find("Sprites/Default"));
            return _spriteDefault;
        }
    }
}
