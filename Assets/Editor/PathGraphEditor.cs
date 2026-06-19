using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for PathGraph.
/// Adds:
///   - "Scan Scene for PathNodes" — auto-populates the nodes list
///   - "Rebuild Visualizer" — regenerates LineRenderers in play or edit mode
///   - Per-node summary with quick-select buttons
/// </summary>
[CustomEditor(typeof(PathGraph))]
public class PathGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var graph = (PathGraph)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("── Tools ──────────────────", EditorStyles.boldLabel);

        // ── Auto-scan ─────────────────────────────────────────────────
        if (GUILayout.Button("Scan Scene for PathNodes"))
        {
            Undo.RecordObject(graph, "Scan PathNodes");
            var found = new List<PathNode>(
                FindObjectsByType<PathNode>(FindObjectsSortMode.None));
            graph.nodes = found;
            EditorUtility.SetDirty(graph);
            Debug.Log($"[PathGraph] Found {found.Count} PathNode(s) in scene.");
        }

        // ── Rebuild lines ─────────────────────────────────────────────
        var vis = graph.GetComponent<PathVisualizer>();
        if (vis != null)
        {
            if (GUILayout.Button("Rebuild Visualizer Lines"))
                vis.RebuildLines();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Add a PathVisualizer component to see the path in the Game view.",
                MessageType.Info);
        }

        // ── Node summary ──────────────────────────────────────────────
        if (graph.nodes == null || graph.nodes.Count == 0) return;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("── Nodes ───────────────────", EditorStyles.boldLabel);

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            var node = graph.nodes[i];
            if (node == null) { EditorGUILayout.LabelField($"  [{i}] (null)"); continue; }

            string kind = node.IsTerminus ? "TERMINUS" :
                          node.IsJunction ? $"JUNCTION ×{node.connections.Count}" :
                                            "node";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  [{i}] {node.name}  — {kind}", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Select", GUILayout.Width(56)))
                Selection.activeGameObject = node.gameObject;
            EditorGUILayout.EndHorizontal();
        }
    }
}
