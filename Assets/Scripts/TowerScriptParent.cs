using UnityEngine;

/// <summary>
/// Legacy tower parent class. Superseded by TowerFactory + Turrent.
/// Kept to avoid compile errors on any scene objects that still reference it.
/// Safe to delete once all old tower prefabs are removed from the scene.
/// </summary>
public class TowerScriptParent : MonoBehaviour
{
    // No-op stub. TowerFactory handles all tower creation now.
}
