using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window for painting tower placement zones onto a PlacementZones asset.
/// Open via: TowerDefense → Placement Zone Painter
///
/// Controls (while paint mode is active in Scene view):
///   Left-click  — add a zone circle at cursor
///   Right-click — erase any overlapping zones
///   Scroll      — resize brush
/// </summary>
public class PlacementZonePainter : EditorWindow
{
    PlacementZones _asset;
    float          _brushRadius  = 0.6f;
    bool           _painting;
    bool           _erasing;     // right-mouse held
    Color          _zoneColor    = new Color(0.2f, 1f, 0.3f, 0.25f);
    Color          _zoneBorder   = new Color(0.2f, 1f, 0.3f, 0.85f);
    Color          _eraseColor   = new Color(1f, 0.2f, 0.2f, 0.6f);

    [MenuItem("TowerDefense/Placement Zone Painter")]
    static void Open() => GetWindow<PlacementZonePainter>("Zone Painter");

    void OnEnable()  => SceneView.duringSceneGui += OnSceneGUI;
    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        _painting = false;
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        _asset = (PlacementZones)EditorGUILayout.ObjectField("Placement Zones", _asset, typeof(PlacementZones), false);

        if (_asset == null)
        {
            EditorGUILayout.HelpBox("Assign or create a Placement Zones asset.\nAssets → Create → TowerDefense → Placement Zones", MessageType.Info);
            if (GUILayout.Button("Create New Asset"))
                CreateAsset();
            return;
        }

        EditorGUILayout.Space(4);
        _brushRadius = EditorGUILayout.Slider("Brush Radius", _brushRadius, 0.1f, 5f);
        _zoneColor   = EditorGUILayout.ColorField("Zone Fill",   _zoneColor);
        _zoneBorder  = EditorGUILayout.ColorField("Zone Border", _zoneBorder);

        EditorGUILayout.Space(6);
        GUI.backgroundColor = _painting ? Color.red : Color.green;
        if (GUILayout.Button(_painting ? "■  Stop Painting" : "▶  Start Painting", GUILayout.Height(36)))
        {
            _painting = !_painting;
            if (_painting) SceneView.lastActiveSceneView?.Focus();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Left-click: paint   Right-click: erase   Scroll: resize", EditorStyles.miniLabel);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"Zones: {_asset.zones.Count}", EditorStyles.boldLabel);

        if (GUILayout.Button("Clear All Zones"))
        {
            Undo.RecordObject(_asset, "Clear Placement Zones");
            _asset.zones.Clear();
            EditorUtility.SetDirty(_asset);
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Merge Overlapping (Optimize)"))
            MergeZones();
    }

    void OnSceneGUI(SceneView sv)
    {
        if (_asset == null) return;

        // Draw existing zones
        foreach (var z in _asset.zones)
        {
            Handles.color = _zoneColor;
            Handles.DrawSolidDisc(z.center, Vector3.forward, z.radius);
            Handles.color = _zoneBorder;
            Handles.DrawWireDisc(z.center, Vector3.forward, z.radius);
        }

        if (!_painting) return;

        // Consume all input so the scene view doesn't pan/select
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Event e         = Event.current;
        Vector2 worldPos = GetWorldPos(e.mousePosition, sv);

        // Scroll to resize brush
        if (e.type == EventType.ScrollWheel)
        {
            _brushRadius = Mathf.Clamp(_brushRadius - e.delta.y * 0.05f, 0.1f, 5f);
            Repaint();
            e.Use();
        }

        bool lmb = e.button == 0;
        bool rmb = e.button == 1;

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
        {
            if (lmb) { Paint(worldPos); e.Use(); }
            if (rmb) { Erase(worldPos); e.Use(); }
        }
        if (e.type == EventType.MouseUp) _erasing = false;

        // Brush preview
        Handles.color = rmb || _erasing ? _eraseColor : new Color(0.2f, 1f, 0.3f, 0.5f);
        Handles.DrawWireDisc(new Vector3(worldPos.x, worldPos.y, 0f), Vector3.forward, _brushRadius);
        Handles.color = new Color(1f, 1f, 1f, 0.2f);
        Handles.DrawSolidDisc(new Vector3(worldPos.x, worldPos.y, 0f), Vector3.forward, _brushRadius);

        sv.Repaint();
    }

    void Paint(Vector2 center)
    {
        // Don't add if fully covered by an existing zone
        foreach (var z in _asset.zones)
            if (Vector2.Distance(center, z.center) + _brushRadius <= z.radius)
                return;

        Undo.RecordObject(_asset, "Paint Placement Zone");
        _asset.zones.Add(new PlacementZones.Zone { center = center, radius = _brushRadius });
        EditorUtility.SetDirty(_asset);
        Repaint();
    }

    void Erase(Vector2 center)
    {
        Undo.RecordObject(_asset, "Erase Placement Zone");
        int removed = _asset.zones.RemoveAll(z =>
            Vector2.Distance(center, z.center) < _brushRadius + z.radius);
        if (removed > 0) { EditorUtility.SetDirty(_asset); Repaint(); }
    }

    void MergeZones()
    {
        // Simple greedy merge: absorb zones whose center is inside another zone
        Undo.RecordObject(_asset, "Merge Placement Zones");
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < _asset.zones.Count; i++)
            {
                for (int j = i + 1; j < _asset.zones.Count; j++)
                {
                    var a = _asset.zones[i];
                    var b = _asset.zones[j];
                    float dist = Vector2.Distance(a.center, b.center);
                    // b is fully inside a
                    if (dist + b.radius <= a.radius)
                    {
                        _asset.zones.RemoveAt(j);
                        changed = true;
                        break;
                    }
                    // a is fully inside b
                    if (dist + a.radius <= b.radius)
                    {
                        _asset.zones.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
                if (changed) break;
            }
        }
        EditorUtility.SetDirty(_asset);
        Repaint();
    }

    static Vector2 GetWorldPos(Vector2 mousePos, SceneView sv)
    {
        mousePos.y = sv.camera.pixelHeight - mousePos.y;
        return sv.camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
    }

    void CreateAsset()
    {
        var asset = CreateInstance<PlacementZones>();
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Placement Zones", "PlacementZones", "asset", "Choose save location");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _asset = asset;
    }
}
