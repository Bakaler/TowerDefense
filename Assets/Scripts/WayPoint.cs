// DEPRECATED — replaced by the PathGraph / PathNode / RouteFollower spline system.
//
// To migrate:
//   1. Delete WayPoint GameObjects from the scene.
//   2. Place PathNode GameObjects and connect them via PathNode.connections.
//   3. Add a PathGraph component to a persistent manager GameObject and
//      register all PathNode objects in its 'nodes' list.
//   4. Assign UnitSpawner.headNode to the PathNode nearest each spawner.
//
// This file intentionally kept as a compile-safe stub so any remaining
// scene references don't break compilation.

using UnityEngine;

[System.Obsolete("Use PathNode / PathGraph instead.")]
public class WayPoint : MonoBehaviour { }
