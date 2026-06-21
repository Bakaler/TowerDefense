using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PathNode))]
public class PathNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var node = (PathNode)target;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Node Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Before"))
            InsertBefore(node);

        if (GUILayout.Button("+ After"))
            InsertAfter(node);

        GUI.color = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Delete"))
            DeleteNode(node);
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // ── Insert a new node halfway between this node's predecessor and this node ──

    static void InsertBefore(PathNode node)
    {
        PathNode predecessor = FindPredecessor(node);

        Vector3 newPos;
        if (predecessor != null)
            newPos = Vector3.Lerp(predecessor.transform.position, node.transform.position, 0.5f);
        else
            newPos = node.transform.position + Vector3.left * 2f;

        PathNode newNode = CreateNode(node, $"{node.name}_pre");
        newNode.transform.position = newPos;

        // Re-wire: predecessor → newNode → node
        if (predecessor != null)
        {
            int idx = predecessor.connections.IndexOf(node);
            Undo.RecordObject(predecessor, "Insert PathNode Before");
            predecessor.connections[idx] = newNode;
            EditorUtility.SetDirty(predecessor);
        }

        newNode.connections.Add(node);
        RegisterWithGraph(newNode);

        Selection.activeGameObject = newNode.gameObject;
        EditorUtility.SetDirty(newNode);
    }

    // ── Insert a new node halfway between this node and its first connection ────

    static void InsertAfter(PathNode node)
    {
        PathNode successor = node.connections.Count > 0 ? node.connections[0] : null;

        Vector3 newPos;
        if (successor != null)
            newPos = Vector3.Lerp(node.transform.position, successor.transform.position, 0.5f);
        else
            newPos = node.transform.position + Vector3.right * 2f;

        PathNode newNode = CreateNode(node, $"{node.name}_post");
        newNode.transform.position = newPos;

        // Re-wire: node → newNode → successor
        Undo.RecordObject(node, "Insert PathNode After");
        if (successor != null)
        {
            node.connections[0] = newNode;
            newNode.connections.Add(successor);
        }
        else
        {
            node.connections.Add(newNode);
        }

        RegisterWithGraph(newNode);
        EditorUtility.SetDirty(node);

        Selection.activeGameObject = newNode.gameObject;
        EditorUtility.SetDirty(newNode);
    }

    // ── Delete this node and re-wire its predecessor directly to its successor ──

    static void DeleteNode(PathNode node)
    {
        if (!EditorUtility.DisplayDialog("Delete PathNode",
            $"Delete '{node.name}' and re-wire connections?", "Delete", "Cancel"))
            return;

        PathNode predecessor = FindPredecessor(node);
        PathNode successor   = node.connections.Count > 0 ? node.connections[0] : null;

        // Re-wire predecessor to point to successor
        if (predecessor != null)
        {
            Undo.RecordObject(predecessor, "Delete PathNode");
            int idx = predecessor.connections.IndexOf(node);
            if (successor != null)
                predecessor.connections[idx] = successor;
            else
                predecessor.connections.RemoveAt(idx);
            EditorUtility.SetDirty(predecessor);
        }

        // Remove from PathGraph nodes list
        var graph = Object.FindFirstObjectByType<PathGraph>();
        if (graph != null)
        {
            Undo.RecordObject(graph, "Delete PathNode");
            graph.nodes.Remove(node);
            EditorUtility.SetDirty(graph);
        }

        Undo.DestroyObjectImmediate(node.gameObject);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    static PathNode CreateNode(PathNode sibling, string preferredName)
    {
        var go = new GameObject(preferredName);
        Undo.RegisterCreatedObjectUndo(go, "Create PathNode");
        go.transform.SetParent(sibling.transform.parent);
        go.transform.position = sibling.transform.position;
        return go.AddComponent<PathNode>();
    }

    static void RegisterWithGraph(PathNode node)
    {
        var graph = Object.FindFirstObjectByType<PathGraph>();
        if (graph == null) return;
        Undo.RecordObject(graph, "Register PathNode");
        if (!graph.nodes.Contains(node))
            graph.nodes.Add(node);
        EditorUtility.SetDirty(graph);
    }

    static PathNode FindPredecessor(PathNode node)
    {
        foreach (var candidate in Object.FindObjectsByType<PathNode>(FindObjectsSortMode.None))
            if (candidate != node && candidate.connections.Contains(node))
                return candidate;
        return null;
    }
}
