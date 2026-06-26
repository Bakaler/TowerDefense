using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Music-sheet style wave timeline.
/// One row per spawn group. X axis = time. Each unit spawn = one icon/dot.
/// Opens from the Level Editor "Graph" button and auto-refreshes as wave data changes.
/// </summary>
public class WaveGraphWindow : EditorWindow
{
    // ── Static shared state ───────────────────────────────────────────
    private static WaveCollection _data;
    private static string[]       _unitIds = System.Array.Empty<string>();

    // ── Per-unit color palette ────────────────────────────────────────
    static readonly Color[] UnitColors =
    {
        new Color(0.55f, 0.75f, 1.00f),   // 0 blue
        new Color(1.00f, 0.65f, 0.35f),   // 1 orange
        new Color(0.55f, 1.00f, 0.55f),   // 2 green
        new Color(1.00f, 0.45f, 0.45f),   // 3 red
        new Color(0.85f, 0.55f, 1.00f),   // 4 purple
        new Color(1.00f, 1.00f, 0.40f),   // 5 yellow
        new Color(0.45f, 1.00f, 0.90f),   // 6 cyan
        new Color(1.00f, 0.70f, 0.85f),   // 7 pink
    };

    // ── Layout constants ──────────────────────────────────────────────
    const float RowH       = 28f;
    const float SpawnerH   = 18f;   // spawner sub-header height
    const float LabelW     = 130f;
    const float DotR       = 7f;
    const float TickH      = 8f;
    const float HeaderH    = 32f;
    const float WaveGapH   = 10f;
    const float PadRight   = 20f;

    float TimeW => Mathf.Max(200f, position.width - LabelW - PadRight);

    // Returns total pixel height for one wave block
    float WaveHeight(WaveDefinition wave)
    {
        // Count spawner sections (one header per distinct spawner transition)
        int sections = 0;
        int last = int.MinValue;
        foreach (var g in wave.groups)
            if (g.spawnerIndex != last) { sections++; last = g.spawnerIndex; }
        return HeaderH + sections * SpawnerH + wave.groups.Count * RowH + WaveGapH;
    }

    // ── Sprite cache (null = not found) ──────────────────────────────
    readonly Dictionary<string, Sprite> _spriteCache = new();

    // ── Scroll ────────────────────────────────────────────────────────
    Vector2 _scroll;

    // ── API ───────────────────────────────────────────────────────────

    public static void OpenWith(WaveCollection data, string[] unitIds)
    {
        Refresh(data, unitIds);
        var win = GetWindow<WaveGraphWindow>("Wave Graph");
        win.minSize = new Vector2(820f, 300f);
        win.Show();
    }

    public static void Refresh(WaveCollection data, string[] unitIds)
    {
        _data    = data;
        _unitIds = unitIds ?? System.Array.Empty<string>();

        // Repaint the open window if it exists
        var existing = Resources.FindObjectsOfTypeAll<WaveGraphWindow>();
        foreach (var w in existing) w.Repaint();
    }

    // ── GUI ───────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (_data == null || _data.waves == null || _data.waves.Count == 0)
        {
            GUILayout.Label("No wave data. Load a level in the Level Editor.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        // Build per-unit-id color map from all wave groups
        var unitColorMap = BuildColorMap();

        // Find global max end time for scale
        float maxTime = 1f;
        foreach (var wave in _data.waves)
            foreach (var g in wave.groups)
                maxTime = Mathf.Max(maxTime, g.EndTime);

        float pxPerSec = TimeW / maxTime;

        // Total canvas height (accounts for spawner sub-headers)
        float totalH = 10f;
        foreach (var wave in _data.waves) totalH += WaveHeight(wave);

        _scroll = GUI.BeginScrollView(
            new Rect(0, 0, position.width, position.height),
            _scroll,
            new Rect(0, 0, LabelW + TimeW + 20f, totalH + 20f));

        float y = 10f;

        for (int wi = 0; wi < _data.waves.Count; wi++)
        {
            var wave = _data.waves[wi];

            float waveEnd = 0f;
            foreach (var g in wave.groups) waveEnd = Mathf.Max(waveEnd, g.EndTime);

            // ── Wave header ───────────────────────────────────────────
            EditorGUI.DrawRect(new Rect(0f, y, LabelW + TimeW, HeaderH - 4f), new Color(0.18f, 0.18f, 0.22f, 1f));

            var hdrStyle = new GUIStyle(EditorStyles.boldLabel);
            hdrStyle.normal.textColor = new Color(1f, 0.9f, 0.5f);
            GUI.Label(new Rect(6f, y + 4f, LabelW - 8f, HeaderH), $"Wave {wi + 1}", hdrStyle);

            DrawTimeAxis(LabelW, y + 2f, HeaderH - 4f, pxPerSec, maxTime);

            float endX = LabelW + waveEnd * pxPerSec;
            EditorGUI.DrawRect(new Rect(endX - 1f, y, 2f, HeaderH - 4f), new Color(1f, 0.5f, 0.5f, 0.8f));
            var endStyle = new GUIStyle(EditorStyles.miniLabel);
            endStyle.normal.textColor = new Color(1f, 0.55f, 0.55f);
            GUI.Label(new Rect(endX + 3f, y + 4f, 60f, 16f), $"{waveEnd:F1}s", endStyle);

            y += HeaderH;

            // ── Group rows with spawner sub-headers ───────────────────
            int lastSpawner = int.MinValue;
            int rowParity   = 0;

            for (int gi = 0; gi < wave.groups.Count; gi++)
            {
                var g   = wave.groups[gi];
                var col = unitColorMap.TryGetValue(g.unitDefinitionId, out var c) ? c : Color.white;

                // Spawner section sub-header
                if (g.spawnerIndex != lastSpawner)
                {
                    EditorGUI.DrawRect(new Rect(0f, y, LabelW + TimeW, SpawnerH),
                        new Color(0.13f, 0.15f, 0.20f, 1f));

                    // Vertical spawner rule across timeline
                    EditorGUI.DrawRect(new Rect(LabelW, y, TimeW, 1f),
                        new Color(0.3f, 0.4f, 0.55f, 0.6f));

                    var spStyle = new GUIStyle(EditorStyles.miniLabel);
                    spStyle.normal.textColor = new Color(0.45f, 0.7f, 1f);
                    GUI.Label(new Rect(10f, y + 2f, LabelW - 12f, SpawnerH - 2f),
                        $"Spawner {g.spawnerIndex}", spStyle);

                    lastSpawner = g.spawnerIndex;
                    rowParity   = 0;
                    y += SpawnerH;
                }

                // Row background
                EditorGUI.DrawRect(new Rect(0f, y, LabelW + TimeW, RowH),
                    rowParity % 2 == 0
                        ? new Color(0.14f, 0.14f, 0.18f, 1f)
                        : new Color(0.12f, 0.12f, 0.16f, 1f));
                rowParity++;

                // Label (no spawner index prefix since it's now in the section header)
                string shortId = g.unitDefinitionId.Replace("_enemy", "").Replace("_", " ");
                var labelStyle = new GUIStyle(EditorStyles.miniLabel);
                labelStyle.normal.textColor = col;
                GUI.Label(new Rect(18f, y + (RowH - 14f) * 0.5f, LabelW - 20f, 14f),
                    $"{shortId}  x{g.count}", labelStyle);

                // Spawn dots / sprites
                var sprite = GetSprite(g.unitDefinitionId);
                for (int si = 0; si < g.count; si++)
                {
                    float t  = g.startTime + si * g.spawnInterval;
                    float cx = LabelW + t * pxPerSec;
                    float cy = y + RowH * 0.5f;
                    float sz = DotR * 2.2f;
                    var dotRect = new Rect(cx - sz * 0.5f, cy - sz * 0.5f, sz, sz);

                    if (sprite != null)
                    {
                        var t2d = sprite.texture;
                        var tr  = sprite.textureRect;
                        var uv  = new Rect(tr.x / t2d.width, tr.y / t2d.height,
                                           tr.width / t2d.width, tr.height / t2d.height);
                        GUI.DrawTextureWithTexCoords(dotRect, t2d, uv, true);
                    }
                    else
                    {
                        EditorGUI.DrawRect(dotRect, col);
                    }
                }

                // Start line
                float startX = LabelW + g.startTime * pxPerSec;
                EditorGUI.DrawRect(new Rect(startX, y, 1f, RowH),
                    new Color(col.r, col.g, col.b, 0.5f));

                y += RowH;
            }

            y += WaveGapH;
        }

        GUI.EndScrollView();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    void DrawTimeAxis(float xOff, float y, float h, float pxPerSec, float maxTime)
    {
        // Draw a tick every whole second, label every 5s
        int totalSecs = Mathf.CeilToInt(maxTime);
        for (int s = 0; s <= totalSecs; s++)
        {
            float x = xOff + s * pxPerSec;
            bool major = s % 5 == 0;
            float tickH = major ? h : TickH;
            EditorGUI.DrawRect(new Rect(x, y + h - tickH, 1f, tickH),
                major ? new Color(0.8f, 0.8f, 0.8f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.4f));
            if (major)
            {
                var style = new GUIStyle(EditorStyles.miniLabel);
                style.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(x + 2f, y, 28f, 14f), $"{s}s", style);
            }
        }
    }

    Dictionary<string, Color> BuildColorMap()
    {
        var map = new Dictionary<string, Color>();
        int colorIdx = 0;
        if (_data == null) return map;
        foreach (var wave in _data.waves)
            foreach (var g in wave.groups)
                if (!map.ContainsKey(g.unitDefinitionId))
                {
                    map[g.unitDefinitionId] = UnitColors[colorIdx % UnitColors.Length];
                    colorIdx++;
                }
        return map;
    }

    Sprite GetSprite(string unitId)
    {
        if (_spriteCache.TryGetValue(unitId, out var cached)) return cached;

        var ta = Resources.Load<TextAsset>("Definitions/units");
        if (ta != null)
        {
            var col = JsonUtility.FromJson<UnitDefCollection>(ta.text);
            if (col?.units != null)
                foreach (var u in col.units)
                    if (u.id == unitId && !string.IsNullOrEmpty(u.animSheet))
                    {
                        string path = $"Assets/Resources/{u.animSheet}.png";
                        var assets  = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var asset in assets)
                            if (asset is Sprite sp)
                            {
                                _spriteCache[unitId] = sp;
                                return sp;
                            }
                        break;
                    }
        }

        _spriteCache[unitId] = null;
        return null;
    }

    [System.Serializable] class UnitDefCollection { public List<UnitDefStub> units; }
    [System.Serializable] class UnitDefStub       { public string id; public string animSheet; }
}
