using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window for towers.json, abilities.json, and effects.json.
/// Three-tab layout: list panel on left, detail form on right.
/// </summary>
public class TowerAbilityEditorWindow : EditorWindow
{
    // ── Tabs ──────────────────────────────────────────────────────────────
    enum Tab { Towers, Abilities, Effects }
    Tab _tab;

    // ── Tower state ───────────────────────────────────────────────────────
    readonly List<TowerDefinition> _towers = new();
    int _tIdx = -1;

    // ── Ability state ─────────────────────────────────────────────────────
    readonly List<AbilityDefinition>   _abilities  = new();
    readonly List<List<string>>        _aValidators = new();
    int _aIdx = -1;

    // ── Effect state ──────────────────────────────────────────────────────
    readonly List<EffectDefinition> _effects = new();
    int _eIdx = -1;

    // ── Scroll positions ──────────────────────────────────────────────────
    Vector2 _listScroll, _detailScroll;

    // ── Sprite cache ──────────────────────────────────────────────────────
    readonly Dictionary<string, Sprite> _spriteCache = new();

    // ── Layout ────────────────────────────────────────────────────────────
    const float ListW = 200f;

    static readonly string[] BalanceOptions  = { "Physical", "Elemental", "Arcane" };
    static readonly string[] EffectTypeOptions =
    {
        "damage", "launch_missile", "launch_shotgun", "launch_boomerang",
        "search_area", "set", "apply_behavior", "apply_permanent_speed_buff",
        "railgun", "drain_life"
    };

    // ── Open ──────────────────────────────────────────────────────────────

    [MenuItem("TowerDefense/Tower & Ability Editor")]
    public static void Open()
    {
        var w = GetWindow<TowerAbilityEditorWindow>("Tower & Ability Editor");
        w.minSize = new Vector2(750f, 400f);
        w.Show();
    }

    void OnEnable() => LoadAll();

    // ── Loading ───────────────────────────────────────────────────────────

    void LoadAll()
    {
        _spriteCache.Clear();
        LoadTowers();
        LoadAbilities();
        LoadEffects();
    }

    void LoadTowers()
    {
        _towers.Clear(); _tIdx = -1;
        string text = ReadFile("towers");
        if (text == null) return;
        var col = JsonUtility.FromJson<TowerDefinitionCollection>(Preprocess(text));
        if (col?.towers != null) _towers.AddRange(col.towers);
    }

    void LoadAbilities()
    {
        _abilities.Clear(); _aValidators.Clear(); _aIdx = -1;
        string text = ReadFile("abilities");
        if (text == null) return;
        var col = JsonUtility.FromJson<AbilityDefinitionCollection>(text);
        if (col?.abilities == null) return;
        foreach (var ab in col.abilities)
        {
            _abilities.Add(ab);
            var vlist = new List<string>();
            if (ab.targetValidatorIds != null) vlist.AddRange(ab.targetValidatorIds);
            _aValidators.Add(vlist);
        }
    }

    void LoadEffects()
    {
        _effects.Clear(); _eIdx = -1;
        string text = ReadFile("effects");
        if (text == null) return;
        var col = JsonUtility.FromJson<EffectDefinitionCollection>(Preprocess(text));
        if (col?.effects != null) _effects.AddRange(col.effects);
    }

    // ── Saving ────────────────────────────────────────────────────────────

    void SaveTowers()
    {
        var col  = new TowerDefinitionCollection { towers = _towers.ToArray() };
        WriteFile("towers", Deprocess(JsonUtility.ToJson(col, true)));
    }

    void SaveAbilities()
    {
        for (int i = 0; i < _abilities.Count && i < _aValidators.Count; i++)
            _abilities[i].targetValidatorIds = _aValidators[i].ToArray();
        var col = new AbilityDefinitionCollection { abilities = _abilities.ToArray() };
        WriteFile("abilities", JsonUtility.ToJson(col, true));
    }

    void SaveEffects()
    {
        var col = new EffectDefinitionCollection { effects = _effects.ToArray() };
        WriteFile("effects", Deprocess(JsonUtility.ToJson(col, true)));
    }

    // ── File I/O ──────────────────────────────────────────────────────────

    static string FilePath(string name) =>
        Path.Combine(Application.dataPath, "Resources", "Definitions", $"{name}.json");

    static string ReadFile(string name)
    {
        string p = FilePath(name);
        return File.Exists(p) ? File.ReadAllText(p) : null;
    }

    static void WriteFile(string name, string json)
    {
        File.WriteAllText(FilePath(name), json);
        AssetDatabase.Refresh();
        Debug.Log($"[TowerAbilityEditor] Saved {name}.json");
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────

    void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawList();
        Divider();
        DrawDetail();
        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Toggle(_tab == Tab.Towers,    "Towers",    EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Towers;
        if (GUILayout.Toggle(_tab == Tab.Abilities, "Abilities", EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Abilities;
        if (GUILayout.Toggle(_tab == Tab.Effects,   "Effects",   EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Effects;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadAll();

        EditorGUILayout.EndHorizontal();
    }

    // ── List Panel ────────────────────────────────────────────────────────

    void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(ListW));
        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

        switch (_tab)
        {
            case Tab.Towers:    DrawTowerList();    break;
            case Tab.Abilities: DrawAbilityList();  break;
            case Tab.Effects:   DrawEffectList();   break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawTowerList()
    {
        for (int i = 0; i < _towers.Count; i++)
        {
            string label = string.IsNullOrEmpty(_towers[i].displayName) ? _towers[i].id : _towers[i].displayName;
            ListItem(label, _tIdx == i, () => _tIdx = i);
        }
        GUILayout.Space(4);
        if (GUILayout.Button("+ Tower"))
        {
            _towers.Add(new TowerDefinition { id = "new_tower", displayName = "New Tower" });
            _tIdx = _towers.Count - 1;
        }
    }

    void DrawAbilityList()
    {
        for (int i = 0; i < _abilities.Count; i++)
        {
            string label = string.IsNullOrEmpty(_abilities[i].displayName) ? _abilities[i].id : _abilities[i].displayName;
            ListItem(label, _aIdx == i, () => _aIdx = i);
        }
        GUILayout.Space(4);
        if (GUILayout.Button("+ Ability"))
        {
            _abilities.Add(new AbilityDefinition { id = "new_ability", displayName = "New Ability", cooldownDuration = 1f, range = 5f, fireArc = 360f });
            _aValidators.Add(new List<string>());
            _aIdx = _abilities.Count - 1;
        }
    }

    void DrawEffectList()
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            var e = _effects[i];
            string name = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;
            string label = $"{name}\n<size=10><color=#aaa>{e.type}</color></size>";

            bool sel = _eIdx == i;
            GUI.backgroundColor = sel ? new Color(0.3f, 0.55f, 1f) : Color.white;
            var style = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = true, fixedHeight = 0 };
            if (GUILayout.Button(label, style)) _eIdx = i;
            GUI.backgroundColor = Color.white;
        }
        GUILayout.Space(4);
        if (GUILayout.Button("+ Effect"))
        {
            _effects.Add(new EffectDefinition { id = "new_effect", displayName = "New Effect", type = "damage", chance = 1f, data = "{}" });
            _eIdx = _effects.Count - 1;
        }
    }

    // ── Detail Panel ──────────────────────────────────────────────────────

    void DrawDetail()
    {
        EditorGUILayout.BeginVertical();
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

        switch (_tab)
        {
            case Tab.Towers:    DrawTowerDetail();   break;
            case Tab.Abilities: DrawAbilityDetail(); break;
            case Tab.Effects:   DrawEffectDetail();  break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── Tower Detail ──────────────────────────────────────────────────────

    void DrawTowerDetail()
    {
        if (_tIdx < 0 || _tIdx >= _towers.Count)
        {
            EmptyMsg("Select a tower from the list, or click + Tower.");
            return;
        }
        var t = _towers[_tIdx];

        DrawTowerPreview(t);

        Section("Identity");
        t.id          = TF("ID",           t.id);
        t.displayName = TF("Display Name", t.displayName);
        t.description = TA("Description",  t.description, 3);

        Section("Stats");
        t.resourceCost    = EF.IntField(  "Cost (gold)",       t.resourceCost);
        t.range           = EF.FloatField("Range",             t.range);
        t.placementRadius = EF.FloatField("Placement Radius",  t.placementRadius);
        t.rotationSpeed   = EF.FloatField("Rotation Speed",    t.rotationSpeed);
        t.balanceType     = Popup("Balance Type", t.balanceType, BalanceOptions);

        Section("Ability");
        t.fireAbilityId   = TFDropdown("Fire Ability ID", t.fireAbilityId, AbilityIds());
        t.displayDamage   = EF.FloatField("Display Damage (override)",   t.displayDamage);
        t.displayCooldown = EF.FloatField("Display Cooldown (override)", t.displayCooldown);

        Section("Upgrades");
        t.maxTier               = EF.IntField(  "Max Tier",                t.maxTier);
        t.towerTier             = EF.IntField(  "Tower Tier (research)",   t.towerTier);
        t.upgradeStatMultiplier = EF.FloatField("Stat Multiplier / Tier",  t.upgradeStatMultiplier);
        t.rangePerTier          = EF.FloatField("Extra Range / Tier",      t.rangePerTier);

        Section("Visuals");
        t.spritePath       = TF("Sprite Path",       t.spritePath);
        t.turretSpritePath = TF("Turret Sprite Path", t.turretSpritePath);
        t.spriteSheet      = TF("Sprite Sheet",       t.spriteSheet);
        t.spriteIndex      = EF.IntField(  "Sprite Index", t.spriteIndex);
        t.scale            = EF.FloatField("Scale",        t.scale);
        t.animFps          = EF.FloatField("Anim FPS",     t.animFps);
        t.tintColor        = EF.ColorField("Tint Color",   t.tintColor);
        t.debugColor       = EF.ColorField("Debug Color",  t.debugColor);

        Section("Components");
        if (t.components == null) t.components = Array.Empty<ComponentEntry>();
        var comps = new List<ComponentEntry>(t.components);
        int removeIdx = -1;
        for (int i = 0; i < comps.Count; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Component {i}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeIdx = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            comps[i].key  = TF("Key",          comps[i].key ?? "");
            comps[i].data = TA("Data (JSON)",   string.IsNullOrEmpty(comps[i].data) ? "{}" : comps[i].data, 4);
            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }
        if (removeIdx >= 0) comps.RemoveAt(removeIdx);
        if (GUILayout.Button("+ Component")) comps.Add(new ComponentEntry { key = "", data = "{}" });
        t.components = comps.ToArray();

        SaveDeleteBar(SaveTowers, () => { _towers.RemoveAt(_tIdx); _tIdx = Mathf.Clamp(_tIdx - 1, 0, _towers.Count - 1); SaveTowers(); });
    }

    // ── Ability Detail ────────────────────────────────────────────────────

    void DrawAbilityDetail()
    {
        if (_aIdx < 0 || _aIdx >= _abilities.Count)
        {
            EmptyMsg("Select an ability from the list, or click + Ability.");
            return;
        }
        var a = _abilities[_aIdx];
        while (_aValidators.Count <= _aIdx) _aValidators.Add(new List<string>());
        var validators = _aValidators[_aIdx];

        Section("Identity");
        a.id          = TF("ID",           a.id);
        a.displayName = TF("Display Name", a.displayName);

        Section("Targeting");
        a.effectId = TFDropdown("Effect ID", a.effectId, EffectIds());
        a.range    = EF.FloatField("Range",       a.range);
        a.fireArc  = EF.FloatField("Fire Arc (°)", a.fireArc);

        Section("Cooldown");
        a.cooldownDuration = EF.FloatField("Cooldown (s)", a.cooldownDuration);

        Section("Timing Phases");
        EditorGUILayout.HelpBox("All timings are usually 0 unless you need animation sync.", MessageType.None);
        a.prepare_time     = EF.FloatField("Prepare Time",      a.prepare_time);
        a.cast_start_time  = EF.FloatField("Cast Start Time",   a.cast_start_time);
        a.cast_finish_time = EF.FloatField("Cast Finish Time",  a.cast_finish_time);
        a.finish_time      = EF.FloatField("Finish Time",       a.finish_time);

        Section("Target Validators");
        EditorGUILayout.HelpBox("e.g. \"no_behavior:poisoned\" to skip already-poisoned targets.", MessageType.None);
        int removeIdx = -1;
        for (int i = 0; i < validators.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            validators[i] = EditorGUILayout.TextField(validators[i]);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeIdx = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (removeIdx >= 0) validators.RemoveAt(removeIdx);
        if (GUILayout.Button("+ Validator")) validators.Add("no_behavior:");

        SaveDeleteBar(SaveAbilities, () => { _abilities.RemoveAt(_aIdx); _aValidators.RemoveAt(_aIdx); _aIdx = Mathf.Clamp(_aIdx - 1, 0, _abilities.Count - 1); SaveAbilities(); });
    }

    // ── Effect Detail ─────────────────────────────────────────────────────

    void DrawEffectDetail()
    {
        if (_eIdx < 0 || _eIdx >= _effects.Count)
        {
            EmptyMsg("Select an effect from the list, or click + Effect.");
            return;
        }
        var e = _effects[_eIdx];

        Section("Identity");
        e.id          = TF("ID",           e.id);
        e.displayName = TF("Display Name", e.displayName);

        Section("Type & Chance");
        e.type   = Popup("Type", e.type, EffectTypeOptions);
        e.chance = EF.FloatField("Chance (0–1)", e.chance);

        DrawEffectDataHint(e.type);

        Section("Data (JSON)");
        GUILayout.Label("Type-specific parameters — edit the JSON object below:", EditorStyles.miniLabel);
        if (string.IsNullOrEmpty(e.data)) e.data = "{}";
        e.data = EditorGUILayout.TextArea(e.data, MonoStyle(), GUILayout.MinHeight(140));

        SaveDeleteBar(SaveEffects, () => { _effects.RemoveAt(_eIdx); _eIdx = Mathf.Clamp(_eIdx - 1, 0, _effects.Count - 1); SaveEffects(); });
    }

    // ── Sprite Preview ────────────────────────────────────────────────────

    void DrawTowerPreview(TowerDefinition t)
    {
        const float Size = 96f;
        const float Pad  = 6f;

        Sprite baseSprite = !string.IsNullOrEmpty(t.spritePath)
            ? LoadSprite(t.spritePath)
            : (!string.IsNullOrEmpty(t.spriteSheet) && t.spriteIndex >= 0
                ? LoadSpriteFromSheet(t.spriteSheet, t.spriteIndex)
                : null);

        Sprite turretSprite = string.IsNullOrEmpty(t.turretSpritePath)
            ? null : LoadSprite(t.turretSpritePath);

        if (baseSprite == null && turretSprite == null) return;

        GUILayout.Space(Pad);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(Pad);

        // Single box — compute one shared scale from the largest sprite dimension
        float maxDim = 1f;
        if (baseSprite   != null) maxDim = Mathf.Max(maxDim, baseSprite.textureRect.width,   baseSprite.textureRect.height);
        if (turretSprite != null) maxDim = Mathf.Max(maxDim, turretSprite.textureRect.width, turretSprite.textureRect.height);
        float ppu = Size / maxDim;  // pixels-per-texel so the largest sprite fills the box

        var boxRect = EditorGUILayout.GetControlRect(false, Size, GUILayout.Width(Size));
        EditorGUI.DrawRect(boxRect, new Color(0.08f, 0.08f, 0.12f, 1f));

        if (baseSprite   != null) BlitSprite(baseSprite,   boxRect, t.tintColor, ppu);
        if (turretSprite != null) BlitSprite(turretSprite, boxRect, t.tintColor, ppu);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(Pad);
    }

    // ppu = pixels-per-texel (shared scale so all sprites in a composite are proportional).
    // Sprites are anchored by their pivot so base and turret align correctly.
    static void BlitSprite(Sprite sprite, Rect box, Color tint, float ppu)
    {
        var tex = sprite.texture;
        var tr  = sprite.textureRect;
        var uv  = new Rect(tr.x / tex.width, tr.y / tex.height,
                           tr.width / tex.width, tr.height / tex.height);

        float drawW = tr.width  * ppu;
        float drawH = tr.height * ppu;

        float cx = box.x + box.width  * 0.5f;
        float cy = box.y + box.height * 0.5f;
        float drawX = cx - drawW * 0.5f;
        float drawY = cy - drawH * 0.5f;

        var prev = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(new Rect(drawX, drawY, drawW, drawH), tex, uv, true);
        GUI.color = prev;
    }

    // Normalize a sprite path to a Resources-relative path (strips leading "Resources/")
    static string ToResourcesPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return path.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)
            ? path.Substring("Resources/".Length) : path;
    }

    Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_spriteCache.TryGetValue(path, out var cached)) return cached;

        string rel = ToResourcesPath(path);
        // Resources.Load<Sprite> works exactly like the game loads it at runtime
        var sp = Resources.Load<Sprite>(rel);
        _spriteCache[path] = sp;
        return sp;
    }

    Sprite LoadSpriteFromSheet(string sheetPath, int index)
    {
        if (string.IsNullOrEmpty(sheetPath) || index < 0) return null;
        string key = $"{sheetPath}#{index}";
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;

        string rel = ToResourcesPath(sheetPath);
        var all = Resources.LoadAll<Sprite>(rel);
        Sprite result = index < all.Length ? all[index] : null;
        _spriteCache[key] = result;
        return result;
    }

    static void DrawEffectDataHint(string type)
    {
        string hint = type switch
        {
            "damage"                      => "damageBase, damageType (0=Physical 1=Arcane 2=Piercing 3=Natural), minimumDamage, maximumDamage, criticalChance, criticalDamageMultiplier, shieldBonus",
            "launch_missile"              => "impactEffectId, missileSpeed, missileScale, missileLifetime, missileSpritePath (or missileSpriteSheet + missileSpriteIndex), missileColor, faceDirection, homing, arcFlight, piercing, drawLine",
            "launch_shotgun"              => "impactEffectId, pelletCount, spreadAngle, missileSpeed, missileScale, missileLifetime, missileSpritePath",
            "launch_boomerang"            => "impactEffectId, arcRadius, sweepSpeed, hitRadius, boomerangScale, boomerangSpritePath, boomerangColor, spinSpeed",
            "search_area"                 => "effectId, radius, maxTargets (-1=all), horizontalArc, searchFromTarget (bool), startingDepth",
            "set"                         => "effectIds: [\"id1\", \"id2\"]  — runs all listed effects in sequence",
            "apply_behavior"              => "behaviorId  (e.g. \"poisoned\", \"rooted\", \"slowed\")",
            "apply_permanent_speed_buff"  => "speedBonus, radius, targetDefinitionId",
            "railgun"                     => "damageBase, damageType, beamRange, beamWidth, beamFadeDuration, beamColor",
            "drain_life"                  => "damage, goldPerDrain, techPerDrain",
            _                             => null
        };
        if (hint != null)
            EditorGUILayout.HelpBox(hint, MessageType.None);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────

    // Shorthand for EditorGUILayout
    static class EF
    {
        public static float FloatField(string l, float v) => EditorGUILayout.FloatField(l, v);
        public static int   IntField(string l, int v)     => EditorGUILayout.IntField(l, v);
        public static Color ColorField(string l, Color v) => EditorGUILayout.ColorField(l, v);
    }

    string TF(string label, string val) => EditorGUILayout.TextField(label, val ?? "");

    string TA(string label, string val, int lines)
    {
        GUILayout.Label(label, EditorStyles.miniLabel);
        return EditorGUILayout.TextArea(val ?? "", GUILayout.MinHeight(lines * 17f));
    }

    string Popup(string label, string current, string[] options)
    {
        int idx = Array.IndexOf(options, current);
        idx = EditorGUILayout.Popup(label, idx < 0 ? 0 : idx, options);
        return idx >= 0 && idx < options.Length ? options[idx] : current;
    }

    string TFDropdown(string label, string val, string[] options)
    {
        EditorGUILayout.BeginHorizontal();
        val = EditorGUILayout.TextField(label, val ?? "");
        if (options.Length > 0)
        {
            int idx = Array.IndexOf(options, val);
            int sel = EditorGUILayout.Popup(idx < 0 ? 0 : idx, options, GUILayout.Width(150));
            if (sel >= 0 && sel < options.Length) val = options[sel];
        }
        EditorGUILayout.EndHorizontal();
        return val;
    }

    void ListItem(string label, bool selected, Action onSelect)
    {
        GUI.backgroundColor = selected ? new Color(0.3f, 0.55f, 1f) : Color.white;
        if (GUILayout.Button(label, new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft }))
            onSelect();
        GUI.backgroundColor = Color.white;
    }

    void Section(string title)
    {
        GUILayout.Space(6);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f), new Color(0.4f, 0.4f, 0.4f, 0.5f));
        GUILayout.Label(title, EditorStyles.boldLabel);
    }

    static void Divider()
    {
        var r = EditorGUILayout.GetControlRect(false, GUILayout.Width(2f), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(r, new Color(0.25f, 0.25f, 0.25f, 1f));
    }

    static void EmptyMsg(string msg) =>
        GUILayout.Label(msg, EditorStyles.centeredGreyMiniLabel);

    static GUIStyle MonoStyle()
    {
        var style = new GUIStyle(EditorStyles.textArea);
        var monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Menlo", "Consolas", "Courier" }, 11);
        if (monoFont != null) style.font = monoFont;
        return style;
    }

    void SaveDeleteBar(Action onSave, Action onDelete)
    {
        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
        if (GUILayout.Button("Save", GUILayout.Height(28)))
        {
            onSave();
            EditorUtility.DisplayDialog("Saved", "Changes written to JSON.", "OK");
        }
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Delete", GUILayout.Width(70), GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("Delete", "Remove this entry and save?", "Delete", "Cancel"))
                onDelete();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ── ID list helpers ───────────────────────────────────────────────────

    string[] AbilityIds()
    {
        var ids = new string[_abilities.Count];
        for (int i = 0; i < _abilities.Count; i++) ids[i] = _abilities[i].id;
        return ids;
    }

    string[] EffectIds()
    {
        var ids = new string[_effects.Count];
        for (int i = 0; i < _effects.Count; i++) ids[i] = _effects[i].id;
        return ids;
    }

    // ── JSON Pre/Deprocess ────────────────────────────────────────────────
    // Mirrors the preprocessor in EffectLibrary and TowerDefinitionLibrary.
    // Preprocess:  "data": { ... }  →  "data": "{ ... }"   (for JsonUtility)
    // Deprocess:   "data": "{ ... }" →  "data": { ... }    (for file output)

    static string Preprocess(string raw)
    {
        if (raw == null) return null;
        const string key = "\"data\"";
        var sb = new StringBuilder(raw.Length + 64);
        int pos = 0;
        while (pos < raw.Length)
        {
            int kp = raw.IndexOf(key, pos, StringComparison.Ordinal);
            if (kp < 0) { sb.Append(raw, pos, raw.Length - pos); break; }

            int cp = kp + key.Length;
            while (cp < raw.Length && raw[cp] != ':') cp++;
            int vs = cp + 1;
            while (vs < raw.Length && char.IsWhiteSpace(raw[vs])) vs++;

            if (vs >= raw.Length || raw[vs] != '{')
            {
                sb.Append(raw, pos, kp - pos + key.Length);
                pos = kp + key.Length;
                continue;
            }

            sb.Append(raw, pos, vs - pos);
            pos = vs;

            int depth = 0; bool inStr = false; int objStart = pos;
            while (pos < raw.Length)
            {
                char c = raw[pos];
                if (inStr) { if (c == '\\') pos++; else if (c == '"') inStr = false; }
                else
                {
                    if (c == '"') inStr = true;
                    else if (c == '{') depth++;
                    else if (c == '}') { depth--; if (depth == 0) { pos++; break; } }
                }
                pos++;
            }

            sb.Append('"');
            for (int i = objStart; i < pos; i++)
            {
                switch (raw[i])
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': break;
                    case '\t': sb.Append("\\t");  break;
                    default:   sb.Append(raw[i]); break;
                }
            }
            sb.Append('"');
        }
        return sb.ToString();
    }

    static string Deprocess(string json)
    {
        if (json == null) return null;
        const string key = "\"data\"";
        var sb = new StringBuilder(json.Length);
        int pos = 0;
        while (pos < json.Length)
        {
            int kp = json.IndexOf(key, pos, StringComparison.Ordinal);
            if (kp < 0) { sb.Append(json, pos, json.Length - pos); break; }

            int cp = kp + key.Length;
            while (cp < json.Length && json[cp] != ':') cp++;
            int vs = cp + 1;
            while (vs < json.Length && char.IsWhiteSpace(json[vs])) vs++;

            if (vs >= json.Length || json[vs] != '"')
            {
                sb.Append(json, pos, kp - pos + key.Length);
                pos = kp + key.Length;
                continue;
            }

            sb.Append(json, pos, vs - pos);
            pos = vs + 1; // skip opening quote

            var data = new StringBuilder();
            while (pos < json.Length)
            {
                char c = json[pos++];
                if (c == '"') break;
                if (c == '\\')
                {
                    char n = pos < json.Length ? json[pos++] : '\0';
                    switch (n)
                    {
                        case '"':  data.Append('"');  break;
                        case '\\': data.Append('\\'); break;
                        case 'n':  data.Append('\n'); break;
                        case 't':  data.Append('\t'); break;
                        default:   data.Append(n);    break;
                    }
                }
                else data.Append(c);
            }
            sb.Append(data);
        }
        return sb.ToString();
    }
}
