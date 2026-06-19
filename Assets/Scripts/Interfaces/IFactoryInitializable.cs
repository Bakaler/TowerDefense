/// <summary>
/// Implemented by any MonoBehaviour component that receives its configuration
/// from a factory rather than from Inspector-assigned fields.
///
/// The factory adds all required components via AddComponent, then calls
/// Initialize() on each in a second pass — guaranteeing every sibling
/// component already exists before any of them read each other.
///
/// Rules:
///   - Do NOT resolve sibling components in Awake(). Cache them in Initialize().
///   - dataJson is the raw JSON block from the definition file (may be null/empty).
///     Use JsonUtility.FromJson to deserialize into a local config class.
/// </summary>
public interface IFactoryInitializable
{
    void Initialize(string dataJson);
}
