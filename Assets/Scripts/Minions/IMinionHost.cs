using UnityEngine;

/// <summary>
/// Implemented by components that own minions (e.g. DroneSwarm).
/// Gives minions their home position and kill-credit object. Damage comes
/// from the impact effect (effects.json), scaled by the host tower's tier.
/// </summary>
public interface IMinionHost
{
    /// <summary>The hive the minion wanders around and returns to.</summary>
    Transform HomeTransform { get; }

    /// <summary>Tower GameObject used for kill tracking, damage scaling, and tiered art.</summary>
    GameObject HostObject { get; }
}
