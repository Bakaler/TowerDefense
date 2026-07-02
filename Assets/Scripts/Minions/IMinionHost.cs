using UnityEngine;

/// <summary>
/// Implemented by components that own minions (e.g. DroneSwarm).
/// Gives minions their home position, kill-credit object, and live damage value.
/// </summary>
public interface IMinionHost
{
    /// <summary>The hive the minion wanders around and returns to.</summary>
    Transform HomeTransform { get; }

    /// <summary>Tower GameObject used for kill tracking, damage scaling, and tiered art.</summary>
    GameObject HostObject { get; }

    /// <summary>Current damage per hit. Read per shot so runtime buffs apply. 0 = use effect base damage.</summary>
    float GetMinionDamage();
}
