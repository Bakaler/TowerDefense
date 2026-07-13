using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window for the game's data JSON: towers, abilities, effects, behaviors,
/// projectiles, units, minions, sounds, and achievements.
/// Tab layout: list panel on left, detail form on right.
/// </summary>
public class DataEditorWindow : EditorWindow
{
    // ── Tabs ──────────────────────────────────────────────────────────────
    enum Tab { Towers, Abilities, Effects, Behaviors, Projectiles, Units, Minions, Sounds, Achievements, Modifiers }
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

    // ── Behavior state ────────────────────────────────────────────────────
    readonly List<BehaviorDefinition> _behaviors = new();
    int _bIdx = -1;

    static readonly string[] StackRuleOptions = { "refresh", "stack", "none" };

    // ── Projectile state ──────────────────────────────────────────────────
    readonly List<ProjectileDefinition> _projectiles = new();
    int _pIdx = -1;

    // ── Unit state ────────────────────────────────────────────────────────
    readonly List<UnitDefinition> _units = new();
    int _uIdx = -1;

    // ── Minion state ──────────────────────────────────────────────────────
    readonly List<MinionDefinition> _minions = new();
    int _mIdx = -1;

    // ── Sound state (_sIdx == -2 selects the Events map) ──────────────────
    readonly List<SoundDefinition>  _sounds      = new();
    readonly List<SoundEventEntry>  _soundEvents = new();
    int _sIdx = -1;

    // ── Achievement state ─────────────────────────────────────────────────
    readonly List<AchievementDefinition> _achievements = new();
    int _achIdx = -1;

    // ── Modifier state (shared Definitions/modifier_columns.json) ─────────
    readonly List<ModifierColumn> _modColumns = new();
    int _modCol = -1;   // selected column
    int _modOpt = -1;   // selected option within the column

    [Serializable]
    class ModifierColumnsWrapper { public ModifierColumn[] modifierColumns; }

    // Must match the effectType switch in LevelManager.ApplyModifiers and the
    // runtime ModifierSelection lookups. Public so LevelEditorWindow shares the list.
    public static readonly string[] ModifierEffectTypes =
    {
        "compound",
        "StartingGold", "StartingLives", "StartingTech", "BonusTowerSlots",
        "FullRefund", "TowerSpeedMult", "TowerRangeMult", "TowerDamageMult",
        "PhysicalDamageMult", "ElementalDamageMult", "ArcaneSpdMult", "ElementalSpdMult",
        "AuraRadiusMult", "AuraMult", "AuraSlowEnemies",
        "BountyPerKill", "ForgiveFirstLeak", "LeakDamageReduction", "LastStand",
        "BonusDrones", "BonusShotgunBullets", "BasicDoubleTap",
        "GoldPerWave", "LivesDrainPerWave",
        "BasicTowerDamageMult", "BasicTowerFireRateMult", "FreeFirstBasicTower",
        "FreeFirstIncomeTower",
    };

    // Must match the condition switch in AchievementManager
    static readonly string[] AchievementConditionOptions =
    {
        "LevelStars", "TotalStars", "TotalKills", "TowersBuilt",
        "WavesCleared", "GoldEarned", "FlawlessVictory", "MonoTypeVictory"
    };

    static readonly string[] BusOptions      = { "music", "combat", "ui", "ambient" };
    static readonly string[] WaveOptions     = { "none", "sine", "square", "triangle", "saw", "noise", "drone" };
    static readonly string[] PickModeOptions = { "shuffle", "random" };

    // ── Scroll positions ──────────────────────────────────────────────────
    Vector2 _listScroll, _detailScroll;

    // ── List search & sort ────────────────────────────────────────────────
    // Search matches the entry's serialized JSON, so every field is searchable.
    // Sort works on any public string/float/int/bool field of the tab's def type.
    string _search    = "";
    string _sortField = "";   // empty = file order
    bool   _sortDesc;
    readonly Dictionary<Type, string[]> _sortFieldCache = new();

    /// <summary>Reordering only makes sense on the unfiltered, file-ordered view.</summary>
    bool ViewIsReorderable => string.IsNullOrEmpty(_search) && string.IsNullOrEmpty(_sortField);

    Type TabDefType() => _tab switch
    {
        Tab.Towers       => typeof(TowerDefinition),
        Tab.Abilities    => typeof(AbilityDefinition),
        Tab.Effects      => typeof(EffectDefinition),
        Tab.Behaviors    => typeof(BehaviorDefinition),
        Tab.Projectiles  => typeof(ProjectileDefinition),
        Tab.Units        => typeof(UnitDefinition),
        Tab.Minions      => typeof(MinionDefinition),
        Tab.Sounds       => typeof(SoundDefinition),
        Tab.Achievements => typeof(AchievementDefinition),
        Tab.Modifiers    => typeof(ModifierDef),
        _                => null,
    };

    string[] SortFieldsFor(Type t)
    {
        if (t == null) return Array.Empty<string>();
        if (_sortFieldCache.TryGetValue(t, out var cached)) return cached;

        var names = new List<string>();
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (f.FieldType == typeof(string) || f.FieldType == typeof(float) ||
                f.FieldType == typeof(int)    || f.FieldType == typeof(bool))
                names.Add(f.Name);
        names.Sort(StringComparer.Ordinal);

        var arr = names.ToArray();
        _sortFieldCache[t] = arr;
        return arr;
    }

    static int CompareFieldValues(object a, object b)
    {
        if (a is string sa && b is string sb) return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
        if (a is IComparable ca && b != null) return ca.CompareTo(b);
        return 0;
    }

    /// <summary>Indices of list entries matching the search, ordered per the sort settings.</summary>
    List<int> BuildView<T>(List<T> list)
    {
        var view = new List<int>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            if (!string.IsNullOrEmpty(_search))
            {
                string hay = JsonUtility.ToJson(list[i]);
                if (hay.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            }
            view.Add(i);
        }

        if (!string.IsNullOrEmpty(_sortField))
        {
            var fi = typeof(T).GetField(_sortField, BindingFlags.Instance | BindingFlags.Public);
            if (fi != null)
            {
                view.Sort((a, b) =>
                {
                    int c = CompareFieldValues(fi.GetValue(list[a]), fi.GetValue(list[b]));
                    return c != 0 ? c : a.CompareTo(b);   // stable: fall back to file order
                });
                if (_sortDesc) view.Reverse();
            }
        }
        return view;
    }

    void DrawSearchSortBar()
    {
        EditorGUILayout.BeginHorizontal();
        _search = EditorGUILayout.TextField(_search);
        if (GUILayout.Button("×", GUILayout.Width(20)))
        {
            _search = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        var fields = SortFieldsFor(TabDefType());
        var opts   = new string[fields.Length + 1];
        opts[0]    = "Sort: file order";
        for (int i = 0; i < fields.Length; i++) opts[i + 1] = "Sort: " + fields[i];

        EditorGUILayout.BeginHorizontal();
        int idx = Array.IndexOf(fields, _sortField) + 1;   // 0 = file order
        int sel = EditorGUILayout.Popup(idx, opts);
        if (sel != idx) _sortField = sel <= 0 ? "" : fields[sel - 1];
        if (!string.IsNullOrEmpty(_sortField) &&
            GUILayout.Button(_sortDesc ? "↓" : "↑", GUILayout.Width(22)))
            _sortDesc = !_sortDesc;
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    // ── Sprite cache ──────────────────────────────────────────────────────
    readonly Dictionary<string, Sprite> _spriteCache = new();

    // ── Layout ────────────────────────────────────────────────────────────
    const float ListW = 200f;

    static readonly string[] BalanceOptions  = { "Physical", "Elemental", "Arcane", "All" };
    static readonly string[] TargetingOptions = Enum.GetNames(typeof(TargetingMode));
    static readonly string[] EffectTypeOptions =
    {
        "damage", "launch", "search_area", "set", "apply_behavior",
        "apply_permanent_speed_buff", "drain_life"
    };
    static readonly string[] MovementOptions = { "straight", "homing", "arc", "orbit" };

    // ── Open ──────────────────────────────────────────────────────────────

    [MenuItem("TowerDefense/Data Editor")]
    public static void Open()
    {
        var w = GetWindow<DataEditorWindow>("Data Editor");
        w.minSize = new Vector2(750f, 400f);
        w.Show();
    }

    void OnEnable()
    {
        titleContent = NeonTab.Title("Data Editor", NeonTab.Cyan);
        EditorApplication.delayCall += () => NeonTab.ColorTitleBar("Data Editor", NeonTab.Cyan);
        EnsureRegistriesLoaded();
        LoadAll();
    }

    void OnFocus() => NeonTab.ColorTitleBar("Data Editor", NeonTab.Cyan);

    void OnDisable()
    {
        if (_effectForm is ScriptableObject so) DestroyImmediate(so);
        _effectForm    = null;
        _effectFormIdx = -1;
    }

    // ── Registry bootstrap ────────────────────────────────────────────────
    // The Effect/Component/Validator registries self-populate via
    // [RuntimeInitializeOnLoadMethod] which only fires in play mode.
    // Invoke the same static Register() methods here so the editor can offer
    // dropdowns and reflect data templates in edit mode.
    static bool _registriesLoaded;

    static void EnsureRegistriesLoaded()
    {
        if (_registriesLoaded && ComponentRegistry.All.Count > 0 && EffectRegistry.All.Count > 0)
            return;

        // Every registration in the codebase is a static parameterless Register()
        // declared on an Effect, TargetValidator, or MonoBehaviour subclass.
        InvokeRegisters(TypeCache.GetTypesDerivedFrom<Effect>());
        InvokeRegisters(TypeCache.GetTypesDerivedFrom<TargetValidator>());
        InvokeRegisters(TypeCache.GetTypesDerivedFrom<MonoBehaviour>());

        _registriesLoaded = true;
        int validators = 0; foreach (var _ in TargetValidatorRegistry.Prefixes) validators++;
        Debug.Log($"[DataEditor] Registries loaded — {EffectRegistry.All.Count} effect type(s), " +
                  $"{ComponentRegistry.All.Count} component key(s), {validators} validator prefix(es).");
    }

    static void InvokeRegisters(IEnumerable<Type> types)
    {
        foreach (var t in types)
        {
            var m = t.GetMethod("Register",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null, Type.EmptyTypes, null);
            if (m == null) continue;
            try { m.Invoke(null, null); }
            catch { /* registries skip duplicates themselves */ }
        }
    }

    // ── Loading ───────────────────────────────────────────────────────────

    void LoadAll()
    {
        _spriteCache.Clear();
        _texturePaths = null;   // rescan sprite-path dropdown options
        LoadTowers();
        LoadAbilities();
        LoadEffects();
        LoadProjectiles();
        LoadUnits();
        LoadMinions();
        LoadBehaviors();
        LoadSounds();
        LoadAchievements();
        LoadModifiers();
    }

    // ── Modifiers ─────────────────────────────────────────────────────────

    void LoadModifiers()
    {
        _modColumns.Clear(); _modCol = -1; _modOpt = -1;
        string text = ReadFile("modifier_columns");
        if (text == null) return;
        var wrapper = JsonUtility.FromJson<ModifierColumnsWrapper>(text);
        if (wrapper?.modifierColumns != null) _modColumns.AddRange(wrapper.modifierColumns);
    }

    void SaveModifiers()
    {
        var wrapper = new ModifierColumnsWrapper { modifierColumns = _modColumns.ToArray() };
        WriteFile("modifier_columns", JsonUtility.ToJson(wrapper, true));
    }

    // ── Achievements ──────────────────────────────────────────────────────

    void LoadAchievements()
    {
        _achievements.Clear(); _achIdx = -1;
        string text = ReadFile("achievements");
        if (text == null) return;
        var col = JsonUtility.FromJson<AchievementDefinitionList>(text);
        if (col?.achievements != null) _achievements.AddRange(col.achievements);
    }

    void SaveAchievements()
    {
        var col = new AchievementDefinitionList { achievements = _achievements.ToArray() };
        WriteFile("achievements", JsonUtility.ToJson(col, true));
    }

    void LoadSounds()
    {
        _sounds.Clear(); _soundEvents.Clear(); _sIdx = -1;
        string text = ReadFile("sounds");
        if (text == null) return;
        var col = JsonUtility.FromJson<SoundDefinitionCollection>(text);
        if (col?.sounds != null) _sounds.AddRange(col.sounds);
        if (col?.events != null) _soundEvents.AddRange(col.events);
    }

    void SaveSounds()
    {
        var col = new SoundDefinitionCollection { sounds = _sounds.ToArray(), events = _soundEvents.ToArray() };
        WriteFile("sounds", JsonUtility.ToJson(col, true));
    }

    string[] SoundIds()
    {
        var ids = new string[_sounds.Count];
        for (int i = 0; i < _sounds.Count; i++) ids[i] = _sounds[i].id;
        return ids;
    }

    // ── Behaviors ─────────────────────────────────────────────────────────
    [Serializable] class BehaviorCollection { public BehaviorDefinition[] behaviors; }

    void LoadBehaviors()
    {
        _behaviors.Clear(); _bIdx = -1;
        string text = ReadFile("behaviors");
        if (text == null) return;
        var col = JsonUtility.FromJson<BehaviorCollection>(text);
        if (col?.behaviors != null) _behaviors.AddRange(col.behaviors);
    }

    void SaveBehaviors()
    {
        var col = new BehaviorCollection { behaviors = _behaviors.ToArray() };
        WriteFile("behaviors", JsonUtility.ToJson(col, true));
    }

    string[] BehaviorIds()
    {
        var ids = new string[_behaviors.Count];
        for (int i = 0; i < _behaviors.Count; i++) ids[i] = _behaviors[i].id;
        return ids;
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

    void LoadProjectiles()
    {
        _projectiles.Clear(); _pIdx = -1;
        string text = ReadFile("projectiles");
        if (text == null) return;
        var col = JsonUtility.FromJson<ProjectileDefinitionCollection>(text);
        if (col?.projectiles != null) _projectiles.AddRange(col.projectiles);
    }

    void LoadUnits()
    {
        _units.Clear(); _uIdx = -1;
        string text = ReadFile("units");
        if (text == null) return;
        var col = JsonUtility.FromJson<UnitDefinitionCollection>(Preprocess(text));
        if (col?.units != null) _units.AddRange(col.units);
    }

    void LoadMinions()
    {
        _minions.Clear(); _mIdx = -1;
        string text = ReadFile("minions");
        if (text == null) return;
        var col = JsonUtility.FromJson<MinionDefinitionCollection>(text);
        if (col?.minions != null) _minions.AddRange(col.minions);
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
        // Normalize every entry against its type's template: missing fields get
        // defaults, stale/unknown keys are dropped.
        foreach (var e in _effects)
        {
            var form = BuildForm(e.type, e.data);
            if (form == null) continue;   // unknown type — leave data untouched
            e.data = SerializeForm(form);
            if (form is ScriptableObject so) DestroyImmediate(so);
        }

        var col = new EffectDefinitionCollection { effects = _effects.ToArray() };
        WriteFile("effects", Deprocess(JsonUtility.ToJson(col, true)));
    }

    void SaveProjectiles()
    {
        var col = new ProjectileDefinitionCollection { projectiles = _projectiles.ToArray() };
        WriteFile("projectiles", JsonUtility.ToJson(col, true));
    }

    void SaveUnits()
    {
        var col = new UnitDefinitionCollection { units = _units.ToArray() };
        WriteFile("units", Deprocess(JsonUtility.ToJson(col, true)));
    }

    void SaveMinions()
    {
        var col = new MinionDefinitionCollection { minions = _minions.ToArray() };
        WriteFile("minions", JsonUtility.ToJson(col, true));
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

        // Saving during Play mode: the libraries loaded their definitions at
        // scene start, so re-read — newly spawned units pick up the edit live.
        if (Application.isPlaying && name == "units")
            UnitDefinitionLibrary.Instance?.Reload();
        if (Application.isPlaying && name == "behaviors")
            BehaviorLibrary.Instance?.Reload();

        Debug.Log($"[DataEditor] Saved {name}.json");
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────

    void OnGUI()
    {
        NeonTab.DrawStrip(NeonTab.Cyan);
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

        if (GUILayout.Toggle(_tab == Tab.Towers,      "Towers",      EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Towers;
        if (GUILayout.Toggle(_tab == Tab.Abilities,   "Abilities",   EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Abilities;
        if (GUILayout.Toggle(_tab == Tab.Effects,     "Effects",     EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Effects;
        if (GUILayout.Toggle(_tab == Tab.Behaviors,   "Behaviors",   EditorStyles.toolbarButton, GUILayout.Width(75)))  _tab = Tab.Behaviors;
        if (GUILayout.Toggle(_tab == Tab.Projectiles, "Projectiles", EditorStyles.toolbarButton, GUILayout.Width(80)))  _tab = Tab.Projectiles;
        if (GUILayout.Toggle(_tab == Tab.Units,       "Units",       EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Units;
        if (GUILayout.Toggle(_tab == Tab.Minions,     "Minions",     EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Minions;
        if (GUILayout.Toggle(_tab == Tab.Sounds,      "Sounds",      EditorStyles.toolbarButton, GUILayout.Width(70)))  _tab = Tab.Sounds;
        if (GUILayout.Toggle(_tab == Tab.Achievements, "Achievements", EditorStyles.toolbarButton, GUILayout.Width(90))) _tab = Tab.Achievements;
        if (GUILayout.Toggle(_tab == Tab.Modifiers,    "Modifiers",    EditorStyles.toolbarButton, GUILayout.Width(75))) _tab = Tab.Modifiers;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60))) { EnsureRegistriesLoaded(); LoadAll(); }

        EditorGUILayout.EndHorizontal();
    }

    // ── List Panel ────────────────────────────────────────────────────────

    void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(ListW));
        DrawSearchSortBar();
        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

        switch (_tab)
        {
            case Tab.Towers:      DrawTowerList();      break;
            case Tab.Abilities:   DrawAbilityList();    break;
            case Tab.Effects:     DrawEffectList();     break;
            case Tab.Behaviors:   DrawBehaviorList();   break;
            case Tab.Projectiles: DrawProjectileList(); break;
            case Tab.Units:       DrawUnitList();       break;
            case Tab.Minions:      DrawMinionList();      break;
            case Tab.Sounds:       DrawSoundList();       break;
            case Tab.Achievements: DrawAchievementList(); break;
            case Tab.Modifiers:    DrawModifierList();    break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawTowerList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_towers))
        {
            string label = string.IsNullOrEmpty(_towers[i].displayName) ? _towers[i].id : _towers[i].displayName;
            int idx = i;
            int m = ListItem(label, _tIdx == i, () => _tIdx = idx, null, ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_towers, moveIdx, moveDir, ref _tIdx)) SaveTowers();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Tower"))
        {
            _towers.Add(new TowerDefinition { id = "new_tower", displayName = "New Tower" });
            _tIdx = _towers.Count - 1;
        }
        if (_tIdx >= 0 && _tIdx < _towers.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_towers[_tIdx]);
            c.id += "_copy"; c.displayName += " Copy";
            _towers.Add(c);
            _tIdx = _towers.Count - 1;
        }
    }

    void DrawAbilityList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_abilities))
        {
            string label = string.IsNullOrEmpty(_abilities[i].displayName) ? _abilities[i].id : _abilities[i].displayName;
            int idx = i;
            int m = ListItem(label, _aIdx == i, () => _aIdx = idx, null, ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        // The validators list is parallel to _abilities — move both together
        if (moveIdx >= 0 && MoveEntry(_abilities, moveIdx, moveDir, ref _aIdx))
        {
            int dummy = -1;
            MoveEntry(_aValidators, moveIdx, moveDir, ref dummy);
            SaveAbilities();
        }
        GUILayout.Space(4);
        if (GUILayout.Button("+ Ability"))
        {
            _abilities.Add(new AbilityDefinition { id = "new_ability", displayName = "New Ability", cooldownDuration = 1f, range = 5f, fireArc = 360f });
            _aValidators.Add(new List<string>());
            _aIdx = _abilities.Count - 1;
        }
        if (_aIdx >= 0 && _aIdx < _abilities.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_abilities[_aIdx]);
            c.id += "_copy"; c.displayName += " Copy";
            _abilities.Add(c);
            _aValidators.Add(_aIdx < _aValidators.Count ? new List<string>(_aValidators[_aIdx]) : new List<string>());
            _aIdx = _abilities.Count - 1;
        }
    }

    void DrawEffectList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_effects))
        {
            var e = _effects[i];
            string name = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;
            string label = $"{name}\n<size=10><color=#aaa>{e.type}</color></size>";
            int idx = i;
            int m = ListItem(label, _eIdx == i, () => { _eIdx = idx; _effectRawMode = false; }, RichListStyle(), ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_effects, moveIdx, moveDir, ref _eIdx)) SaveEffects();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Effect"))
        {
            _effects.Add(new EffectDefinition { id = "new_effect", displayName = "New Effect", type = "damage", chance = 1f, data = "{}" });
            _eIdx = _effects.Count - 1;
        }
        if (_eIdx >= 0 && _eIdx < _effects.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_effects[_eIdx]);
            c.id += "_copy"; c.displayName += " Copy";
            _effects.Add(c);
            _eIdx = _effects.Count - 1;
        }
    }

    void DrawProjectileList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_projectiles))
        {
            var p = _projectiles[i];
            string name = string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName;
            string label = $"{name}\n<size=10><color=#aaa>{p.movement}</color></size>";
            int idx = i;
            int m = ListItem(label, _pIdx == i, () => _pIdx = idx, RichListStyle(), ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_projectiles, moveIdx, moveDir, ref _pIdx)) SaveProjectiles();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Projectile"))
        {
            _projectiles.Add(new ProjectileDefinition { id = "new_projectile", displayName = "New Projectile" });
            _pIdx = _projectiles.Count - 1;
        }
        if (_pIdx >= 0 && _pIdx < _projectiles.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_projectiles[_pIdx]);
            c.id += "_copy"; c.displayName += " Copy";
            _projectiles.Add(c);
            _pIdx = _projectiles.Count - 1;
        }
    }

    void DrawUnitList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_units))
        {
            string label = string.IsNullOrEmpty(_units[i].displayName) ? _units[i].id : _units[i].displayName;
            int idx = i;
            int m = ListItem(label, _uIdx == i, () => _uIdx = idx, null, ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_units, moveIdx, moveDir, ref _uIdx)) SaveUnits();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Unit"))
        {
            _units.Add(new UnitDefinition { id = "new_unit", displayName = "New Unit" });
            _uIdx = _units.Count - 1;
        }
        if (_uIdx >= 0 && _uIdx < _units.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_units[_uIdx]);
            c.id += "_copy"; c.displayName += " Copy";
            _units.Add(c);
            _uIdx = _units.Count - 1;
        }
    }

    void DrawMinionList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_minions))
        {
            string label = string.IsNullOrEmpty(_minions[i].displayName) ? _minions[i].id : _minions[i].displayName;
            int idx = i;
            int m = ListItem(label, _mIdx == i, () => _mIdx = idx, null, ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_minions, moveIdx, moveDir, ref _mIdx)) SaveMinions();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Minion"))
        {
            _minions.Add(new MinionDefinition { id = "new_minion", displayName = "New Minion" });
            _mIdx = _minions.Count - 1;
        }
        if (_mIdx >= 0 && _mIdx < _minions.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_minions[_mIdx]);
            c.id += "_copy"; c.displayName += " Copy";
            _minions.Add(c);
            _mIdx = _minions.Count - 1;
        }
    }

    void DrawSoundList()
    {
        // Pseudo-entry for the event → sound map
        GUI.backgroundColor = _sIdx == -2 ? new Color(0.3f, 0.55f, 1f) : new Color(0.85f, 0.75f, 0.4f);
        if (GUILayout.Button("⚡ Game Events", new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft }))
            _sIdx = -2;
        GUI.backgroundColor = Color.white;
        GUILayout.Space(4);

        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_sounds))
        {
            var s = _sounds[i];
            string name  = string.IsNullOrEmpty(s.displayName) ? s.id : s.displayName;
            string sub   = s.clips != null && s.clips.Length > 0 ? $"{s.clips.Length} clip(s)" : $"synth:{s.synthWave}";
            string label = $"{name}\n<size=10><color=#aaa>{s.bus} · {sub}</color></size>";
            int idx = i;
            int m = ListItem(label, _sIdx == i, () => _sIdx = idx, RichListStyle(), ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_sounds, moveIdx, moveDir, ref _sIdx)) SaveSounds();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Sound"))
        {
            _sounds.Add(new SoundDefinition { id = "new_sound", displayName = "New Sound", synthWave = "sine" });
            _sIdx = _sounds.Count - 1;
        }
        if (_sIdx >= 0 && _sIdx < _sounds.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_sounds[_sIdx]);
            c.id += "_copy"; c.displayName += " Copy";
            _sounds.Add(c);
            _sIdx = _sounds.Count - 1;
        }
    }

    // ── Modifier List & Detail ────────────────────────────────────────────
    // Edits the SHARED columns (Definitions/modifier_columns.json) shown when a
    // level has no modifierColumns of its own. Per-level overrides live in the
    // level JSONs.

    void DrawModifierList()
    {
        for (int c = 0; c < _modColumns.Count; c++)
        {
            // Column header: reorder + delete
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Column {c + 1}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            int colMove = 0;
            if (GUILayout.Button("▲", GUILayout.Width(20))) colMove = -1;
            if (GUILayout.Button("▼", GUILayout.Width(20))) colMove = +1;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(20)) &&
                EditorUtility.DisplayDialog("Delete Column",
                    $"Delete column {c + 1} and its {(_modColumns[c].options?.Length ?? 0)} option(s)?", "Delete", "Cancel"))
            {
                _modColumns.RemoveAt(c);
                if (_modCol == c) { _modCol = -1; _modOpt = -1; }
                else if (_modCol > c) _modCol--;
                SaveModifiers();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                return;   // list changed — redraw next frame
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (colMove != 0)
            {
                int to = c + colMove;
                if (to >= 0 && to < _modColumns.Count)
                {
                    (_modColumns[c], _modColumns[to]) = (_modColumns[to], _modColumns[c]);
                    if (_modCol == c) _modCol = to;
                    else if (_modCol == to) _modCol = c;
                    SaveModifiers();
                }
                return;
            }

            // Options in this column
            var opts = new List<ModifierDef>(_modColumns[c].options ?? Array.Empty<ModifierDef>());
            int moveIdx = -1, moveDir = 0;
            foreach (int i in BuildView(opts))
            {
                var o = opts[i];
                string name = string.IsNullOrEmpty(o.displayName) ? o.id : o.displayName;
                string sub  = o.subEffects != null && o.subEffects.Length > 0
                    ? $"compound ({o.subEffects.Length})" : o.effectType;
                string label = $"{name}\n<size=10><color=#aaa>{sub}</color></size>";
                int cc = c, ii = i;
                int m = ListItem(label, _modCol == c && _modOpt == i,
                    () => { _modCol = cc; _modOpt = ii; }, RichListStyle(), ViewIsReorderable);
                if (m != 0) { moveIdx = i; moveDir = m; }
            }
            if (moveIdx >= 0)
            {
                int sel = _modCol == c ? _modOpt : -1;
                if (MoveEntry(opts, moveIdx, moveDir, ref sel))
                {
                    _modColumns[c].options = opts.ToArray();
                    if (_modCol == c) _modOpt = sel;
                    SaveModifiers();
                }
            }

            if (GUILayout.Button("+ Option"))
            {
                opts.Add(new ModifierDef { id = $"c{c + 1}_new", displayName = "New Modifier" });
                _modColumns[c].options = opts.ToArray();
                _modCol = c;
                _modOpt = opts.Count - 1;
            }
            GUILayout.Space(10);
        }

        if (GUILayout.Button("+ Column"))
        {
            _modColumns.Add(new ModifierColumn());
            _modCol = _modColumns.Count - 1;
            _modOpt = -1;
        }
    }

    void DrawModifierDetail()
    {
        if (_modCol < 0 || _modCol >= _modColumns.Count ||
            _modColumns[_modCol].options == null ||
            _modOpt < 0 || _modOpt >= _modColumns[_modCol].options.Length)
        {
            EmptyMsg("Select a modifier option on the left, or press + Option / + Column.");
            return;
        }
        var m = _modColumns[_modCol].options[_modOpt];

        Section("Identity");
        m.id          = TF("ID",           m.id);
        m.displayName = TF("Display Name", m.displayName);
        m.description = TA("Description",  m.description, 3);

        Section("Effect");
        EditorGUILayout.HelpBox(
            "Single effect: pick an Effect Type and Value.\n" +
            "Compound: set type 'compound' and add sub-effects — they apply instead of this modifier's own type/value.",
            MessageType.None);
        m.effectType = TFDropdown("Effect Type", m.effectType, ModifierEffectTypes);
        m.value      = EF.FloatField("Value", m.value);

        Section("Sub-Effects");
        var subs = new List<SubEffectDef>(m.subEffects ?? Array.Empty<SubEffectDef>());
        int removeSub = -1;
        for (int i = 0; i < subs.Count; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Sub-Effect {i}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeSub = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            subs[i].id         = TF("ID", subs[i].id);
            subs[i].effectType = TFDropdown("Effect Type", subs[i].effectType, ModifierEffectTypes);
            subs[i].value      = EF.FloatField("Value", subs[i].value);
            EditorGUILayout.EndVertical();
        }
        if (removeSub >= 0) subs.RemoveAt(removeSub);
        if (GUILayout.Button("+ Sub-Effect"))
            subs.Add(new SubEffectDef { id = $"{m.id}_sub{subs.Count}" });
        m.subEffects = subs.ToArray();

        SaveDeleteBar(SaveModifiers, () =>
        {
            var opts = new List<ModifierDef>(_modColumns[_modCol].options);
            opts.RemoveAt(_modOpt);
            _modColumns[_modCol].options = opts.ToArray();
            _modOpt = Mathf.Clamp(_modOpt - 1, 0, opts.Count - 1);
            if (opts.Count == 0) _modOpt = -1;
            SaveModifiers();
        });
    }

    // ── Achievement List & Detail ─────────────────────────────────────────

    void DrawAchievementList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_achievements))
        {
            var a = _achievements[i];
            string name  = string.IsNullOrEmpty(a.title) ? a.id : a.title;
            string label = $"{name}\n<size=10><color=#aaa>{a.conditionType}{(a.hidden ? " · hidden" : "")}</color></size>";
            int idx = i;
            int m = ListItem(label, _achIdx == i, () => _achIdx = idx, RichListStyle(), ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_achievements, moveIdx, moveDir, ref _achIdx)) SaveAchievements();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Achievement"))
        {
            _achievements.Add(new AchievementDefinition { id = "new_achievement", title = "New Achievement", conditionType = "LevelStars", levelIndex = 1, minStars = 1 });
            _achIdx = _achievements.Count - 1;
        }
        if (_achIdx >= 0 && _achIdx < _achievements.Count && GUILayout.Button("+ Copy Selected"))
        {
            var c = CloneDef(_achievements[_achIdx]);
            c.id += "_copy"; c.title += " Copy";
            _achievements.Add(c);
            _achIdx = _achievements.Count - 1;
        }
    }

    void DrawAchievementDetail()
    {
        if (_achIdx < 0 || _achIdx >= _achievements.Count)
        {
            EmptyMsg("Select an achievement from the list, or click + Achievement.");
            return;
        }
        var a = _achievements[_achIdx];

        DrawSpritePreview(ResolveAchievementPreview(a), Color.white);

        Section("Identity");
        a.id          = TF("ID",          a.id);
        a.title       = TF("Title",       a.title);
        a.description = TA("Description", a.description, 3);
        a.hidden      = EF.Toggle("Hidden until earned", a.hidden);

        Section("Icon");
        a.iconPath  = TFSprite("Icon Path",  a.iconPath,  "Art/");
        a.iconSheet = TFSprite("Icon Sheet", a.iconSheet, "Art/");
        a.iconIndex = EF.IntField("Icon Index", a.iconIndex);

        Section("Condition");
        EditorGUILayout.HelpBox(
            "Save-state (re-checked on progress): LevelStars, TotalStars, TotalKills, TowersBuilt, WavesCleared, GoldEarned.\n" +
            "Run conditions (checked at victory): FlawlessVictory, MonoTypeVictory.", MessageType.None);
        a.conditionType = Popup("Condition Type", a.conditionType, AchievementConditionOptions);

        switch (a.conditionType)
        {
            case "LevelStars":
                a.levelIndex = EF.IntField("Level Index (1-based)", a.levelIndex);
                a.minStars   = EF.IntField("Min Stars",             a.minStars);
                break;
            case "TotalStars":
                a.minStars   = EF.IntField("Min Total Stars", a.minStars);
                break;
            case "TotalKills":
            case "TowersBuilt":
            case "WavesCleared":
            case "GoldEarned":
                a.count      = EF.IntField("Count", a.count);
                break;
            case "MonoTypeVictory":
                a.balanceType = Popup("Balance Type", a.balanceType, BalanceOptions);
                a.minBalance  = EF.FloatField("Min Balance Score (0 = any)", a.minBalance);
                break;
            // FlawlessVictory needs no parameters
        }

        SaveDeleteBar(SaveAchievements, () => { _achievements.RemoveAt(_achIdx); _achIdx = Mathf.Clamp(_achIdx - 1, 0, _achievements.Count - 1); SaveAchievements(); });
    }

    Sprite ResolveAchievementPreview(AchievementDefinition a)
    {
        if (!string.IsNullOrEmpty(a.iconPath))
        {
            var sp = LoadSprite(a.iconPath);
            if (sp != null) return sp;
        }
        return LoadSpriteFromSheet(a.iconSheet, a.iconIndex);
    }

    void DrawSoundDetail()
    {
        if (_sIdx == -2) { DrawSoundEvents(); return; }
        if (_sIdx < 0 || _sIdx >= _sounds.Count)
        {
            EmptyMsg("Select a sound, the Game Events map, or click + Sound.");
            return;
        }
        var s = _sounds[_sIdx];

        Section("Identity");
        s.id          = TF("ID",           s.id);
        s.displayName = TF("Display Name", s.displayName);

        Section("Clips (empty = synth fallback)");
        DrawClipList(s);
        s.pickMode = Popup("Pick Mode", s.pickMode, PickModeOptions);

        Section("Playback");
        s.bus         = Popup("Bus", s.bus, BusOptions);
        s.volume      = EF.FloatField("Volume",       s.volume);
        s.pitch       = EF.FloatField("Pitch",        s.pitch);
        s.pitchJitter = EF.FloatField("Pitch Jitter", s.pitchJitter);
        s.loop        = EF.Toggle(    "Loop (music/ambient)", s.loop);

        Section("Discipline");
        s.minInterval = EF.FloatField("Min Interval (s)",     s.minInterval);
        s.maxVoices   = EF.IntField(  "Max Voices (0=∞)",     s.maxVoices);
        s.priority    = EF.IntField(  "Priority",             s.priority);

        Section("Synth Fallback");
        s.synthWave = Popup("Waveform", s.synthWave, WaveOptions);
        if (s.synthWave != "none")
        {
            s.synthFreq     = EF.FloatField("Frequency (Hz)",      s.synthFreq);
            s.synthFreqEnd  = EF.FloatField("End Frequency (0=—)", s.synthFreqEnd);
            s.synthDuration = EF.FloatField("Duration (s)",        s.synthDuration);
            s.synthAttack   = EF.FloatField("Attack (s)",          s.synthAttack);
        }

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("▶ Preview", GUILayout.Height(24))) PreviewSound(s);
        if (GUILayout.Button("■ Stop", GUILayout.Width(70), GUILayout.Height(24))) StopPreview();
        EditorGUILayout.EndHorizontal();

        SaveDeleteBar(SaveSounds, () => { _sounds.RemoveAt(_sIdx); _sIdx = Mathf.Clamp(_sIdx - 1, 0, _sounds.Count - 1); SaveSounds(); });
    }

    void DrawSoundEvents()
    {
        Section("Game Events → Sounds");
        EditorGUILayout.HelpBox("Code fires these named events; which sound plays is data.\nKnown events: ui_click, select, tower_place, enemy_death, wave_start, wave_clear, victory, defeat, life_lost, tier_unlock, music_game, music_menu.", MessageType.None);

        int removeIdx = -1;
        for (int i = 0; i < _soundEvents.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _soundEvents[i].eventId = EditorGUILayout.TextField(_soundEvents[i].eventId, GUILayout.Width(140));
            _soundEvents[i].soundId = TextWithIdPopup(_soundEvents[i].soundId, SoundIds());
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeIdx = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (removeIdx >= 0) _soundEvents.RemoveAt(removeIdx);
        if (GUILayout.Button("+ Event Mapping")) _soundEvents.Add(new SoundEventEntry { eventId = "", soundId = "" });

        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
        if (GUILayout.Button("Save", GUILayout.Height(28)))
        {
            SaveSounds();
            EditorUtility.DisplayDialog("Saved", "Changes written to JSON.", "OK");
        }
        GUI.backgroundColor = Color.white;
    }

    // ── Clip list with drag-and-drop AudioClip picker ─────────────────────

    void DrawClipList(SoundDefinition s)
    {
        var clips = new List<string>(s.clips ?? Array.Empty<string>());
        int removeIdx = -1;

        var warnStyle = new GUIStyle(EditorStyles.miniLabel);
        warnStyle.normal.textColor = new Color(1f, 0.6f, 0.3f);

        for (int i = 0; i < clips.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            clips[i] = EditorGUILayout.TextField(clips[i] ?? "");

            var loaded = string.IsNullOrEmpty(clips[i])
                ? null
                : Resources.Load<AudioClip>(ToResourcesPath(clips[i]));

            var picked = (AudioClip)EditorGUILayout.ObjectField(loaded, typeof(AudioClip), false, GUILayout.Width(150));
            if (picked != loaded && picked != null)
            {
                string resPath = AssetPathToResourcesPath(AssetDatabase.GetAssetPath(picked));
                if (resPath != null) { clips[i] = resPath; loaded = picked; }
                else ShowNotification(new GUIContent("Clip must live under a Resources folder."));
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeIdx = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(clips[i]) && loaded == null)
                GUILayout.Label($"   ⚠ not found — paths are Resources-relative, e.g. \"Audio/SFX/{Path.GetFileName(clips[i])}\"", warnStyle);
        }
        if (removeIdx >= 0) clips.RemoveAt(removeIdx);
        if (GUILayout.Button("+ Clip")) clips.Add("");

        s.clips = clips.ToArray();
    }

    /// <summary>"Assets/Resources/Audio/SFX/x.wav" → "Audio/SFX/x". Null if not under Resources.</summary>
    static string AssetPathToResourcesPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        int r = assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (r < 0) return null;
        string p = assetPath.Substring(r + "/Resources/".Length);
        int dot = p.LastIndexOf('.');
        return dot > 0 ? p.Substring(0, dot) : p;
    }

    // ── Sound preview (editor-only playback via AudioUtil reflection) ─────

    void PreviewSound(SoundDefinition s)
    {
        AudioClip clip = null;
        string source = null;
        if (s.clips != null)
            foreach (var path in s.clips)
            {
                if (string.IsNullOrEmpty(path)) continue;
                clip = Resources.Load<AudioClip>(ToResourcesPath(path));
                if (clip != null) { source = path; break; }
            }
        if (clip == null)
        {
            clip = AudioSynth.GetClip(s);
            source = clip != null ? $"synth:{s.synthWave}" : null;
        }
        if (clip == null) { ShowNotification(new GUIContent("No clip found and no synth waveform set.")); return; }

        if (PlayPreviewClip(clip))
            ShowNotification(new GUIContent($"▶ {source}"), 0.8f);
        else
            ShowNotification(new GUIContent("Preview unavailable in this Unity version — check console."));
    }

    void StopPreview() => StopAllPreviewClips();

    static Type FindAudioUtil()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("UnityEditor.AudioUtil");
            if (t != null) return t;
        }
        return null;
    }

    static bool PlayPreviewClip(AudioClip clip)
    {
        var util = FindAudioUtil();
        if (util == null) { Debug.LogWarning("[DataEditor] UnityEditor.AudioUtil not found — sound preview disabled."); return false; }

        const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var name in new[] { "PlayPreviewClip", "PlayClip" })
        {
            var m = util.GetMethod(name, Flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (m != null) { m.Invoke(null, new object[] { clip, 0, false }); return true; }
            m = util.GetMethod(name, Flags, null, new[] { typeof(AudioClip) }, null);
            if (m != null) { m.Invoke(null, new object[] { clip }); return true; }
        }
        Debug.LogWarning("[DataEditor] AudioUtil found but no known preview method — sound preview disabled.");
        return false;
    }

    static void StopAllPreviewClips()
    {
        var util = FindAudioUtil();
        if (util == null) return;
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var name in new[] { "StopAllPreviewClips", "StopAllClips" })
        {
            var m = util.GetMethod(name, Flags, null, Type.EmptyTypes, null);
            if (m != null) { m.Invoke(null, null); return; }
        }
    }

    // ── Detail Panel ──────────────────────────────────────────────────────

    void DrawDetail()
    {
        EditorGUILayout.BeginVertical();
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

        switch (_tab)
        {
            case Tab.Towers:      DrawTowerDetail();      break;
            case Tab.Abilities:   DrawAbilityDetail();    break;
            case Tab.Effects:     DrawEffectDetail();     break;
            case Tab.Behaviors:   DrawBehaviorDetail();   break;
            case Tab.Projectiles: DrawProjectileDetail(); break;
            case Tab.Units:       DrawUnitDetail();       break;
            case Tab.Minions:      DrawMinionDetail();      break;
            case Tab.Sounds:       DrawSoundDetail();       break;
            case Tab.Achievements: DrawAchievementDetail(); break;
            case Tab.Modifiers:    DrawModifierDetail();    break;
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
        t.balanceWeight   = EF.IntField(  "Balance Weight",    t.balanceWeight);
        t.unique          = EF.Toggle(    "Unique (only one)", t.unique);
        t.detectorTier    = EF.IntField(  "Detector Tier (0=never)", t.detectorTier);
        t.defaultTargeting  = Popup("Targeting Prio 1", string.IsNullOrEmpty(t.defaultTargeting)  ? "Furthest" : t.defaultTargeting,  TargetingOptions);
        t.defaultTargeting2 = Popup("Targeting Prio 2", string.IsNullOrEmpty(t.defaultTargeting2) ? "Furthest" : t.defaultTargeting2, TargetingOptions);

        Section("Ability");
        t.fireAbilityId   = TFDropdown("Fire Ability ID", t.fireAbilityId, AbilityIds());
        t.displayDamage   = EF.FloatField("Display Damage (override)",   t.displayDamage);
        t.displayCooldown = EF.FloatField("Display Cooldown (override)", t.displayCooldown);

        Section("Audio");
        t.placeSoundId   = TFDropdown("Place Sound (empty = default)",   t.placeSoundId,   SoundIds());
        t.sellSoundId    = TFDropdown("Sell Sound (empty = default)",    t.sellSoundId,    SoundIds());
        t.upgradeSoundId = TFDropdown("Upgrade Sound (empty = default)", t.upgradeSoundId, SoundIds());

        Section("Upgrades");
        t.maxTier               = EF.IntField(  "Max Tier",                t.maxTier);
        t.towerTier             = EF.IntField(  "Tower Tier (research)",   t.towerTier);
        t.upgradeStatMultiplier = EF.FloatField("Stat Multiplier / Tier",  t.upgradeStatMultiplier);
        t.rangePerTier          = EF.FloatField("Extra Range / Tier",      t.rangePerTier);

        Section("Visuals");
        t.spritePath       = TFSprite("Sprite Path",        t.spritePath,       "Art/Towers/");
        t.turretSpritePath = TFSprite("Turret Sprite Path", t.turretSpritePath, "Art/Towers/");
        t.spriteSheet      = TFSprite("Sprite Sheet",       t.spriteSheet,      "Art/Towers/");
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
            DrawComponentEntry(comps[i]);
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

        Section("Audio");
        a.fireSoundId = TFDropdown("Fire Sound ID", a.fireSoundId, SoundIds());

        Section("Cooldown");
        a.cooldownDuration = EF.FloatField("Cooldown (s)", a.cooldownDuration);

        Section("Timing Phases");
        EditorGUILayout.HelpBox("All timings are usually 0 unless you need animation sync.", MessageType.None);
        a.prepare_time     = EF.FloatField("Prepare Time",      a.prepare_time);
        a.cast_start_time  = EF.FloatField("Cast Start Time",   a.cast_start_time);
        a.cast_finish_time = EF.FloatField("Cast Finish Time",  a.cast_finish_time);
        a.finish_time      = EF.FloatField("Finish Time",       a.finish_time);

        Section("Target Validators");
        EditorGUILayout.HelpBox("\"no_behavior:poisoned\" skips targets that already have the behavior.\n\"affectable:slowed\" also skips targets immune to it (e.g. boss_immunity).", MessageType.None);
        int removeIdx = -1;
        var prefixes = ValidatorPrefixes();
        for (int i = 0; i < validators.Count; i++)
        {
            string cur    = validators[i] ?? "";
            int    colon  = cur.IndexOf(':');
            string prefix = colon >= 0 ? cur.Substring(0, colon) : cur;
            string param  = colon >= 0 ? cur.Substring(colon + 1) : "";

            EditorGUILayout.BeginHorizontal();
            if (prefixes.Length > 0)
            {
                int pIdx  = Array.IndexOf(prefixes, prefix);
                int shown = pIdx < 0 ? 0 : pIdx;
                int sel   = EditorGUILayout.Popup(shown, prefixes, GUILayout.Width(110));
                if (sel != shown || pIdx >= 0) prefix = prefixes[sel];
            }
            else
            {
                prefix = EditorGUILayout.TextField(prefix, GUILayout.Width(110));
            }
            param = TextWithIdPopup(param, BehaviorIds());
            validators[i] = prefix + ":" + param;

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
        e.type   = Popup("Type", e.type, EffectTypeKeys());
        e.chance = EF.FloatField("Chance (0–1)", e.chance);

        Section("Data");
        var form = GetEffectForm(e);
        if (form == null)
        {
            EditorGUILayout.HelpBox($"Unknown effect type '{e.type}' — editing raw JSON.", MessageType.Warning);
            _effectRawMode = true;
        }

        if (form != null)
            _effectRawMode = GUILayout.Toggle(_effectRawMode, "Edit raw JSON", EditorStyles.miniButton, GUILayout.Width(110));

        if (_effectRawMode)
        {
            if (string.IsNullOrEmpty(e.data)) e.data = "{}";
            string edited = EditorGUILayout.TextArea(e.data, MonoStyle(), GUILayout.MinHeight(140));
            if (edited != e.data)
            {
                e.data = edited;
                _effectFormIdx = -1;   // raw edit — rebuild the form next time it's shown
            }
        }
        else if (form != null)
        {
            GUILayout.Label("Fields reflected from the effect type — unknown JSON keys are dropped on save.", EditorStyles.miniLabel);
            DrawEffectForm(form);
            e.data = SerializeForm(form);
        }

        SaveDeleteBar(SaveEffects, () => { _effects.RemoveAt(_eIdx); _eIdx = Mathf.Clamp(_eIdx - 1, 0, _effects.Count - 1); SaveEffects(); });
    }

    // ── Effect data form (reflection-driven) ──────────────────────────────
    // The form model is the same thing the runtime fills from JSON:
    //  - a nested [Serializable] data class when the effect uses one
    //    (Effect_Set.EffectSetData, Effect_Search_Area.SearchAreaData), or
    //  - the Effect subclass itself (fields declared on the subclass only).
    // New effect types get a working form automatically.

    object _effectForm;
    string _effectFormType;
    int    _effectFormIdx = -1;
    bool   _effectRawMode;

    object GetEffectForm(EffectDefinition e)
    {
        if (_effectForm != null && _effectFormIdx == _eIdx && _effectFormType == e.type)
            return _effectForm;

        if (_effectForm is ScriptableObject old) DestroyImmediate(old);
        _effectForm     = BuildForm(e.type, e.data);
        _effectFormType = e.type;
        _effectFormIdx  = _eIdx;
        return _effectForm;
    }

    static object BuildForm(string effectType, string dataJson)
    {
        if (!EffectRegistry.TryGet(effectType, out var type)) return null;
        string json = string.IsNullOrEmpty(dataJson) ? "{}" : dataJson;

        var nested = FindNestedDataType(type);
        if (nested != null)
        {
            object obj;
            try { obj = JsonUtility.FromJson(json, nested); }
            catch { obj = null; }
            return obj ?? Activator.CreateInstance(nested);
        }

        var so = ScriptableObject.CreateInstance(type);
        so.hideFlags = HideFlags.HideAndDontSave;
        try { JsonUtility.FromJsonOverwrite(json, so); } catch { }
        return so;
    }

    static Type FindNestedDataType(Type effectType)
    {
        foreach (var nt in effectType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            if (nt.IsClass && nt.IsDefined(typeof(SerializableAttribute), false))
                return nt;
        return null;
    }

    static List<FieldInfo> FormFields(object form)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance;
        if (form is ScriptableObject) flags |= BindingFlags.DeclaredOnly;   // skip Effect base fields

        var fields = new List<FieldInfo>();
        foreach (var f in form.GetType().GetFields(flags))
            if (IsSupportedFieldType(f.FieldType))
                fields.Add(f);
        return fields;
    }

    static bool IsSupportedFieldType(Type t) =>
        t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(string) ||
        t == typeof(Color) || t.IsEnum || t == typeof(string[]) || t == typeof(List<string>);

    void DrawEffectForm(object form)
    {
        foreach (var f in FormFields(form))
        {
            var    t     = f.FieldType;
            string label = ObjectNames.NicifyVariableName(f.Name);
            object v     = f.GetValue(form);

            if      (t == typeof(float))  f.SetValue(form, EditorGUILayout.FloatField(label, (float)v));
            else if (t == typeof(int))    f.SetValue(form, EditorGUILayout.IntField(label, (int)v));
            else if (t == typeof(bool))   f.SetValue(form, EditorGUILayout.Toggle(label, (bool)v));
            else if (t == typeof(Color))  f.SetValue(form, EditorGUILayout.ColorField(label, (Color)v));
            else if (t.IsEnum)            f.SetValue(form, EditorGUILayout.EnumPopup(label, (Enum)v));
            else if (t == typeof(string)) f.SetValue(form, DrawIdAwareString(label, f.Name, (string)v));
            else if (t == typeof(string[]))
            {
                var list = new List<string>((string[])v ?? Array.Empty<string>());
                DrawStringList(label, f.Name, list);
                f.SetValue(form, list.ToArray());
            }
            else if (t == typeof(List<string>))
            {
                var list = (List<string>)v ?? new List<string>();
                DrawStringList(label, f.Name, list);
                f.SetValue(form, list);
            }
        }
    }

    // Field names that reference other definitions get an id dropdown
    string DrawIdAwareString(string label, string fieldName, string value)
    {
        var options = IdOptionsFor(fieldName);
        return options != null
            ? TFDropdown(label, value ?? "", options)
            : TF(label, value);
    }

    string[] IdOptionsFor(string fieldName)
    {
        string n = fieldName.ToLowerInvariant();
        if (n.Contains("effectid"))     return EffectIds();
        if (n.Contains("projectileid")) return ProjectileIds();
        if (n.Contains("behaviorid"))   return BehaviorIds();
        if (n.Contains("minionid"))     return MinionIds();
        if (n.Contains("soundid"))      return SoundIds();
        if (n.Contains("unitid"))       return UnitIds();
        if (n.Contains("spritepath") || n.Contains("spritesheet") ||
            n.Contains("sheetpath")  || n.Contains("texturepath"))
            return AllTexturePaths();
        return null;
    }

    string[] UnitIds()
    {
        var ids = new string[_units.Count];
        for (int i = 0; i < _units.Count; i++) ids[i] = _units[i].id;
        return ids;
    }

    void DrawStringList(string label, string fieldName, List<string> list)
    {
        GUILayout.Label(label, EditorStyles.miniLabel);
        int removeIdx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            list[i] = DrawIdAwareString($"[{i}]", fieldName, list[i]);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeIdx = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (removeIdx >= 0) list.RemoveAt(removeIdx);
        if (GUILayout.Button($"+ {label} entry")) list.Add("");
    }

    // ── Form → data JSON ──────────────────────────────────────────────────

    static string SerializeForm(object form)
    {
        var sb = new StringBuilder("{ ");
        bool first = true;
        foreach (var f in FormFields(form))
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append('"').Append(f.Name).Append("\": ");
            AppendJsonValue(sb, f.GetValue(form), f.FieldType);
        }
        sb.Append(" }");
        return sb.ToString();
    }

    static void AppendJsonValue(StringBuilder sb, object v, Type t)
    {
        if (t == typeof(float))       sb.Append(((float)v).ToString(System.Globalization.CultureInfo.InvariantCulture));
        else if (t == typeof(int))    sb.Append((int)v);
        else if (t == typeof(bool))   sb.Append((bool)v ? "true" : "false");
        else if (t.IsEnum)            sb.Append(Convert.ToInt32(v));
        else if (t == typeof(string)) AppendJsonString(sb, (string)v);
        else if (t == typeof(Color))
        {
            var c = (Color)v;
            sb.Append("{\"r\": ").Append(c.r.ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(", \"g\": ").Append(c.g.ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(", \"b\": ").Append(c.b.ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(", \"a\": ").Append(c.a.ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append('}');
        }
        else if (t == typeof(string[]) || t == typeof(List<string>))
        {
            var items = v as IEnumerable<string> ?? Array.Empty<string>();
            sb.Append('[');
            bool firstItem = true;
            foreach (var s in items)
            {
                if (!firstItem) sb.Append(", ");
                firstItem = false;
                AppendJsonString(sb, s);
            }
            sb.Append(']');
        }
        else sb.Append("null");
    }

    static void AppendJsonString(StringBuilder sb, string s)
    {
        sb.Append('"');
        if (!string.IsNullOrEmpty(s))
            foreach (char c in s)
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:   sb.Append(c);      break;
                }
        sb.Append('"');
    }

    string[] EffectTypeKeys()
    {
        if (EffectRegistry.All.Count == 0) return EffectTypeOptions;
        var keys = new List<string>(EffectRegistry.All.Keys);
        keys.Sort();
        return keys.ToArray();
    }

    // ── Projectile Detail ─────────────────────────────────────────────────

    void DrawProjectileDetail()
    {
        if (_pIdx < 0 || _pIdx >= _projectiles.Count)
        {
            EmptyMsg("Select a projectile from the list, or click + Projectile.");
            return;
        }
        var p = _projectiles[_pIdx];

        DrawSpritePreview(ResolveProjectilePreview(p), p.color);

        Section("Identity");
        p.id          = TF("ID",           p.id);
        p.displayName = TF("Display Name", p.displayName);
        p.description = TA("Description",  p.description, 2);

        Section("Movement");
        p.movement      = Popup("Movement", p.movement, MovementOptions);
        p.speed         = EF.FloatField("Speed",            p.speed);
        p.lifetime      = EF.FloatField("Lifetime (s, 0=∞)", p.lifetime);
        p.faceDirection = EF.Toggle(    "Face Direction",   p.faceDirection);

        if (p.movement == "orbit")
        {
            Section("Orbit (boomerang)");
            p.arcRadius  = EF.FloatField("Max Arc Radius (reach = 2×)", p.arcRadius);
            p.sweepSpeed = EF.FloatField("Sweep Speed (°/s)", p.sweepSpeed);
            p.spinSpeed  = EF.FloatField("Spin Speed (°/s)",  p.spinSpeed);
        }

        Section("Hit Behavior");
        p.hitRadius        = EF.FloatField("Hit Radius",         p.hitRadius);
        p.pierce           = EF.Toggle(    "Pierce",             p.pierce);
        if (p.pierce || p.movement == "orbit")
        {
            p.maxHits        = EF.IntField("Max Hits (0 = ∞)",   p.maxHits);
            if (p.maxHits > 0)
                p.maxHitsPerTier = EF.IntField("Max Hits / Upgrade", p.maxHitsPerTier);
        }
        p.blockedByShields = EF.Toggle(    "Blocked By Shields", p.blockedByShields);
        if (p.blockedByShields)
            p.shieldAbsorb = EF.FloatField("Shield Absorb Dmg",  p.shieldAbsorb);
        p.drawImpactLine   = EF.Toggle(    "Draw Impact Line",   p.drawImpactLine);
        p.impactSoundId    = TFDropdown("Impact Sound ID", p.impactSoundId, SoundIds());

        Section("Visuals");
        p.scale        = EF.FloatField("Scale",         p.scale);
        p.spritePath   = TFSprite("Sprite Path (sheet = anim frames)", p.spritePath);
        p.animFps      = EF.FloatField("Anim FPS (0 = static)",  p.animFps);
        p.spriteSheet  = TFSprite("Sprite Sheet",  p.spriteSheet);
        p.spriteIndex  = EF.IntField(  "Sprite Index",  p.spriteIndex);
        p.color        = EF.ColorField("Color",         p.color);
        p.sortingLayer = TF("Sorting Layer", p.sortingLayer);
        p.sortingOrder = EF.IntField(  "Sorting Order", p.sortingOrder);

        SaveDeleteBar(SaveProjectiles, () => { _projectiles.RemoveAt(_pIdx); _pIdx = Mathf.Clamp(_pIdx - 1, 0, _projectiles.Count - 1); SaveProjectiles(); });
    }

    // ── Unit Detail ───────────────────────────────────────────────────────

    void DrawUnitDetail()
    {
        if (_uIdx < 0 || _uIdx >= _units.Count)
        {
            EmptyMsg("Select a unit from the list, or click + Unit.");
            return;
        }
        var u = _units[_uIdx];

        DrawSpritePreview(ResolveUnitPreview(u), u.tintColor);

        Section("Identity");
        u.id          = TF("ID",           u.id);
        u.displayName = TF("Display Name", u.displayName);
        u.description = TA("Description",  u.description, 3);

        Section("Stats");
        u.life             = EF.FloatField("Life",              u.life);
        u.shield           = EF.FloatField("Shield (0 = none)", u.shield);
        u.speed            = EF.FloatField("Speed",             u.speed);
        u.physicalDefense  = EF.IntField(  "Physical Defense",  u.physicalDefense);
        u.elementalDefense = EF.IntField(  "Elemental Defense", u.elementalDefense);
        u.arcanaDefense    = EF.IntField(  "Arcana Defense",    u.arcanaDefense);
        u.deathBlow        = EF.IntField(  "Death Blow (lives)", u.deathBlow);

        Section("Physics");
        u.colliderRadius = EF.FloatField("Collider Radius",       u.colliderRadius);
        u.layer          = EF.IntField(  "Layer (0=auto Enemy)",  u.layer);

        Section("Movement");
        u.rotateToMovement  = EF.Toggle(    "Rotate To Movement", u.rotateToMovement);
        u.spriteAngleOffset = EF.FloatField("Sprite Angle Offset (180 = art flipped)", u.spriteAngleOffset);

        Section("Visuals");
        u.spritePath  = TFSprite("Sprite Path",  u.spritePath,  "Art/Units/", "Art/Enemies/");
        u.spriteSheet = TFSprite("Sprite Sheet", u.spriteSheet, "Art/Units/", "Art/Enemies/");
        u.spriteIndex = EF.IntField(  "Sprite Index", u.spriteIndex);
        u.scale       = EF.FloatField("Scale",        u.scale);
        u.tintColor   = EF.ColorField("Tint Color",   u.tintColor);
        u.debugColor  = EF.ColorField("Debug Color",  u.debugColor);

        Section("Animation");
        u.animSheet      = TFSprite("Walk Sheet",  u.animSheet,      "Art/Units/", "Art/Enemies/");
        u.animFps        = EF.FloatField("Walk FPS",   u.animFps);
        u.animDeathSheet = TFSprite("Death Sheet", u.animDeathSheet, "Art/Units/", "Art/Enemies/");
        u.animDeathFps   = EF.FloatField("Death FPS",  u.animDeathFps);
        u.animReverse    = EF.Toggle(    "Reverse Frames (sheet authored backwards)", u.animReverse);

        Section("Audio");
        u.deathSoundId = TFDropdown("Death Sound (empty = default)", u.deathSoundId, SoundIds());

        Section("Starting Behaviors");
        EditorGUILayout.HelpBox("Behavior ids applied permanently at spawn (e.g. immunities, shields).", MessageType.None);
        var behaviors = new List<string>(u.startingBehaviors ?? Array.Empty<string>());
        int removeBehavior = -1;
        for (int i = 0; i < behaviors.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            behaviors[i] = TextWithIdPopup(behaviors[i], BehaviorIds());
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeBehavior = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (removeBehavior >= 0) behaviors.RemoveAt(removeBehavior);
        if (GUILayout.Button("+ Behavior")) behaviors.Add("");
        u.startingBehaviors = behaviors.ToArray();

        Section("Abilities");
        EditorGUILayout.HelpBox("Ability ids cast on cooldown (cleanses, barriers, zaps). Preferred over components.", MessageType.None);
        var unitAbilities = new List<string>(u.abilities ?? Array.Empty<string>());
        int removeAbility = -1;
        for (int i = 0; i < unitAbilities.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            unitAbilities[i] = TextWithIdPopup(unitAbilities[i], AbilityIds());
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeAbility = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (removeAbility >= 0) unitAbilities.RemoveAt(removeAbility);
        if (GUILayout.Button("+ Ability")) unitAbilities.Add("");
        u.abilities = unitAbilities.ToArray();

        Section("Tags");
        EditorGUILayout.HelpBox("Targeting tags read by towers: \"high_prio\" (support units), \"boss\".", MessageType.None);
        var unitTags = new List<string>(u.tags ?? Array.Empty<string>());
        int removeTag = -1;
        for (int i = 0; i < unitTags.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            unitTags[i] = EditorGUILayout.TextField(unitTags[i] ?? "");
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeTag = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (removeTag >= 0) unitTags.RemoveAt(removeTag);
        if (GUILayout.Button("+ Tag")) unitTags.Add("");
        u.tags = unitTags.ToArray();

        Section("Components");
        if (u.components == null) u.components = Array.Empty<ComponentEntry>();
        var comps = new List<ComponentEntry>(u.components);
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
            DrawComponentEntry(comps[i]);
            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }
        if (removeIdx >= 0) comps.RemoveAt(removeIdx);
        if (GUILayout.Button("+ Component")) comps.Add(new ComponentEntry { key = "", data = "{}" });
        u.components = comps.ToArray();

        SaveDeleteBar(SaveUnits, () => { _units.RemoveAt(_uIdx); _uIdx = Mathf.Clamp(_uIdx - 1, 0, _units.Count - 1); SaveUnits(); });
    }

    // ── Minion Detail ─────────────────────────────────────────────────────

    void DrawMinionDetail()
    {
        if (_mIdx < 0 || _mIdx >= _minions.Count)
        {
            EmptyMsg("Select a minion from the list, or click + Minion.");
            return;
        }
        var m = _minions[_mIdx];

        DrawSpritePreview(ResolveMinionPreview(m), m.tintColor);

        Section("Identity");
        m.id          = TF("ID",           m.id);
        m.displayName = TF("Display Name", m.displayName);
        m.description = TA("Description",  m.description, 2);

        Section("Brain");
        EditorGUILayout.HelpBox("State machine: Wander → Engage (locked on target until it dies) → Return → Rest.", MessageType.None);
        m.moveSpeed    = EF.FloatField("Wander Speed",       m.moveSpeed);
        m.engageSpeed  = EF.FloatField("Engage Speed",       m.engageSpeed);
        m.returnSpeed  = EF.FloatField("Return Speed",       m.returnSpeed);
        m.orbitDist    = EF.FloatField("Orbit Distance",     m.orbitDist);
        m.wanderRadius = EF.FloatField("Wander Radius",      m.wanderRadius);
        m.restDuration = EF.FloatField("Rest Duration (s)",  m.restDuration);

        Section("Attack");
        m.attackCooldown = EF.FloatField("Attack Cooldown (s)", m.attackCooldown);
        m.projectileId   = TFDropdown("Projectile ID",    m.projectileId,   ProjectileIds());
        m.impactEffectId = TFDropdown("Impact Effect ID", m.impactEffectId, EffectIds());

        Section("Visuals");
        m.scale        = EF.FloatField("Scale",         m.scale);
        m.spritePath   = TFSprite("Sprite Path (sheet ok)", m.spritePath);
        m.animFps      = EF.FloatField("Anim FPS",      m.animFps);
        m.tintColor    = EF.ColorField("Tint Color",    m.tintColor);
        m.sortingLayer = TF("Sorting Layer", m.sortingLayer);
        m.sortingOrder = EF.IntField(  "Sorting Order", m.sortingOrder);

        SaveDeleteBar(SaveMinions, () => { _minions.RemoveAt(_mIdx); _mIdx = Mathf.Clamp(_mIdx - 1, 0, _minions.Count - 1); SaveMinions(); });
    }

    // ── Behavior List & Detail ────────────────────────────────────────────

    void DrawBehaviorList()
    {
        int moveIdx = -1, moveDir = 0;
        foreach (int i in BuildView(_behaviors))
        {
            string label = string.IsNullOrEmpty(_behaviors[i].displayName) ? _behaviors[i].id : _behaviors[i].displayName;
            int idx = i;
            int m = ListItem(label, _bIdx == i, () => _bIdx = idx, null, ViewIsReorderable);
            if (m != 0) { moveIdx = i; moveDir = m; }
        }
        if (moveIdx >= 0 && MoveEntry(_behaviors, moveIdx, moveDir, ref _bIdx)) SaveBehaviors();
        GUILayout.Space(4);
        if (GUILayout.Button("+ Behavior"))
        {
            _behaviors.Add(new BehaviorDefinition { id = "new_behavior", displayName = "New Behavior" });
            _bIdx = _behaviors.Count - 1;
        }
    }

    void DrawBehaviorDetail()
    {
        if (_bIdx < 0 || _bIdx >= _behaviors.Count)
        {
            EditorGUILayout.HelpBox("Select a behavior on the left, or press + Behavior.", MessageType.Info);
            return;
        }
        var b = _behaviors[_bIdx];

        Section("Identity");
        b.id          = TF("ID",           b.id);
        b.displayName = TF("Display Name", b.displayName);
        b.description = TA("Description (shown in enemy panel tooltip)", b.description, 2);
        b.iconPath    = TFSprite("Icon Path (empty = tinted circle)", b.iconPath);

        Section("Type & Stacking");
        b.behaviorType = (BehaviorType)EditorGUILayout.EnumPopup("Behavior Type", b.behaviorType);
        b.duration     = EF.FloatField("Duration (s)", b.duration);
        int sr = Array.IndexOf(StackRuleOptions, b.stackRule); if (sr < 0) sr = 0;
        b.stackRule    = StackRuleOptions[EditorGUILayout.Popup("Stack Rule", sr, StackRuleOptions)];
        if (b.stackRule == "stack")
            b.maxStacks = EF.IntField("Max Stacks (0 = unlimited)", b.maxStacks);

        Section("Stat Modifiers");
        b.moveSpeedMultiplier    = EF.FloatField("Move Speed Multiplier",  b.moveSpeedMultiplier);
        b.damageTakenMultiplier  = EF.FloatField("Damage Taken Multiplier (2 = +100%)", b.damageTakenMultiplier);
        b.tintColor              = EF.ColorField("Unit Tint",              b.tintColor);

        Section("Periodic Tick");
        EditorGUILayout.HelpBox("Tick Interval 0 = no ticks. Tick Behavior applies another behavior every tick (e.g. cloak cycle → invisible).", MessageType.None);
        b.tickInterval   = EF.FloatField("Tick Interval (s)", b.tickInterval);
        b.tickDamage     = EF.FloatField("Tick Damage",       b.tickDamage);
        b.tickDamageType = (int)(DamageType)EditorGUILayout.EnumPopup("Tick Damage Type", (DamageType)b.tickDamageType);
        b.tickBehaviorId = TFDropdown("Tick Behavior ID", b.tickBehaviorId, BehaviorIds());

        Section("Shield Bubble");
        b.shieldHp     = EF.FloatField("Shield HP (0 = none)", b.shieldHp);
        b.shieldRadius = EF.FloatField("Shield Radius",        b.shieldRadius);

        Section("Immunities");
        EditorGUILayout.HelpBox("BehaviorType names this behavior blocks while active (e.g. Slowed, Rooted).", MessageType.None);
        var immunities = new List<string>(b.immunities ?? Array.Empty<string>());
        int removeImm  = -1;
        string[] typeNames = Enum.GetNames(typeof(BehaviorType));
        for (int i = 0; i < immunities.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            immunities[i] = TextWithIdPopup(immunities[i], typeNames);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24))) removeImm = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (removeImm >= 0) immunities.RemoveAt(removeImm);
        if (GUILayout.Button("+ Immunity")) immunities.Add("");
        b.immunities = immunities.ToArray();

        Section("On Death");
        b.onDeathEffectId = TFDropdown("On-Death Effect ID", b.onDeathEffectId, EffectIds());

        Section("Audio");
        b.applySoundId = TFDropdown("Apply Sound ID", b.applySoundId, SoundIds());

        Section("Impact VFX (plays once on apply)");
        b.impactSheetPath  = TFSprite("Sheet Path",     b.impactSheetPath, "Art/VFX/", "Art/Effects/");
        b.impactFrameCount = EF.IntField("Frame Count", b.impactFrameCount);
        b.impactFps        = EF.FloatField("FPS",       b.impactFps);
        b.impactScale      = EF.FloatField("Scale",     b.impactScale);

        Section("Duration VFX (loops while active)");
        b.durationSheetPath  = TFSprite("Sheet Path",     b.durationSheetPath, "Art/VFX/", "Art/Effects/");
        b.durationFrameCount = EF.IntField("Frame Count", b.durationFrameCount);
        b.durationFps        = EF.FloatField("FPS",       b.durationFps);
        b.durationScale      = EF.FloatField("Scale",     b.durationScale);

        SaveDeleteBar(SaveBehaviors, () => { _behaviors.RemoveAt(_bIdx); _bIdx = Mathf.Clamp(_bIdx - 1, 0, _behaviors.Count - 1); SaveBehaviors(); });
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

    // Single-sprite preview used by the Projectile / Unit / Minion tabs.
    void DrawSpritePreview(Sprite sprite, Color tint)
    {
        if (sprite == null) return;

        const float Size = 96f;
        const float Pad  = 6f;

        GUILayout.Space(Pad);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(Pad);

        float maxDim = Mathf.Max(1f, sprite.textureRect.width, sprite.textureRect.height);
        float ppu    = Size / maxDim;

        var boxRect = EditorGUILayout.GetControlRect(false, Size, GUILayout.Width(Size));
        EditorGUI.DrawRect(boxRect, new Color(0.08f, 0.08f, 0.12f, 1f));
        BlitSprite(sprite, boxRect, tint, ppu);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(Pad);
    }

    Sprite ResolveProjectilePreview(ProjectileDefinition p)
    {
        if (!string.IsNullOrEmpty(p.spritePath))
        {
            var sp = LoadSprite(p.spritePath);
            if (sp == null) sp = LoadSpriteFromSheet(p.spritePath, 0);
            if (sp != null) return sp;
        }
        return LoadSpriteFromSheet(p.spriteSheet, p.spriteIndex);
    }

    Sprite ResolveUnitPreview(UnitDefinition u)
    {
        if (!string.IsNullOrEmpty(u.animSheet))
        {
            var sp = LoadSpriteFromSheet(u.animSheet, 0);
            if (sp != null) return sp;
        }
        if (!string.IsNullOrEmpty(u.spriteSheet) && u.spriteIndex >= 0)
        {
            var sp = LoadSpriteFromSheet(u.spriteSheet, u.spriteIndex);
            if (sp != null) return sp;
        }
        return LoadSprite(u.spritePath);
    }

    Sprite ResolveMinionPreview(MinionDefinition m)
    {
        if (string.IsNullOrEmpty(m.spritePath)) return null;
        var sp = LoadSpriteFromSheet(m.spritePath, 0);
        return sp != null ? sp : LoadSprite(m.spritePath);
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

    // ── Texture path scan (dropdown options for sprite/sheet fields) ──────
    // All Texture2D assets under Resources as Resources-relative paths.
    // '/' nests the popup into folder submenus for free. Rescans on Reload.
    static string[] _texturePaths;

    static string[] AllTexturePaths()
    {
        if (_texturePaths != null) return _texturePaths;
        var list = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" }))
        {
            var res = AssetPathToResourcesPath(AssetDatabase.GUIDToAssetPath(guid));
            if (res != null) list.Add(res);
        }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        _texturePaths = list.ToArray();
        return _texturePaths;
    }

    static string[] TexturePathsUnder(params string[] prefixes)
    {
        var all = AllTexturePaths();
        if (prefixes == null || prefixes.Length == 0) return all;
        var list = new List<string>();
        foreach (var p in all)
            foreach (var pre in prefixes)
                if (p.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) { list.Add(p); break; }
        return list.ToArray();
    }

    /// <summary>Sprite/sheet path field: free text + a popup of textures found under
    /// the given Resources folders (empty = every texture under Resources).</summary>
    string TFSprite(string label, string val, params string[] prefixes) =>
        TFDropdown(label, val, TexturePathsUnder(prefixes));

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

    // ── UI Helpers ────────────────────────────────────────────────────────

    // Shorthand for EditorGUILayout
    static class EF
    {
        public static float FloatField(string l, float v) => EditorGUILayout.FloatField(l, v);
        public static int   IntField(string l, int v)     => EditorGUILayout.IntField(l, v);
        public static Color ColorField(string l, Color v) => EditorGUILayout.ColorField(l, v);
        public static bool  Toggle(string l, bool v)      => EditorGUILayout.Toggle(l, v);
    }

    string TF(string label, string val) => EditorGUILayout.TextField(label, val ?? "");

    /// <summary>Text field + id popup combo (no label). Caller wraps in a horizontal group.
    /// "—" = empty; only writes on an actual selection change.</summary>
    string TextWithIdPopup(string value, string[] options, float popupWidth = 150f)
    {
        value = EditorGUILayout.TextField(value ?? "");
        if (options.Length > 0)
        {
            var opts = new string[options.Length + 1];
            opts[0] = "—";
            Array.Copy(options, 0, opts, 1, options.Length);
            int idx = Array.IndexOf(options, value) + 1;
            int sel = EditorGUILayout.Popup(idx, opts, GUILayout.Width(popupWidth));
            if (sel != idx) value = sel <= 0 ? "" : opts[sel];
        }
        return value;
    }

    /// <summary>Component entry editor: key dropdown from ComponentRegistry + data JSON.
    /// Picking a key with empty data prefills the component's Data template with defaults.</summary>
    void DrawComponentEntry(ComponentEntry entry)
    {
        if (ComponentRegistry.All.Count == 0)
            EditorGUILayout.HelpBox("ComponentRegistry is empty — component key dropdown unavailable (check console).", MessageType.Warning);

        string prevKey = entry.key ?? "";
        entry.key = TFDropdown("Key", prevKey, ComponentKeys());

        if (entry.key != prevKey && (string.IsNullOrEmpty(entry.data) || entry.data.Trim() == "{}"))
            entry.data = ComponentDataTemplate(entry.key);

        entry.data = TAAuto("Data (JSON)", string.IsNullOrEmpty(entry.data) ? "{}" : entry.data);
    }

    /// <summary>Word-wrapped text area that grows with its content (min 4, max 40 lines).</summary>
    string TAAuto(string label, string val)
    {
        val ??= "";
        GUILayout.Label(label, EditorStyles.miniLabel);
        int lines = 1;
        foreach (char c in val) if (c == '\n') lines++;
        var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        return EditorGUILayout.TextArea(val, style, GUILayout.MinHeight(Mathf.Clamp(lines + 1, 4, 40) * 15f));
    }

    /// <summary>Default data JSON for a component key, reflected from its nested [Serializable] Data class.</summary>
    static string ComponentDataTemplate(string key)
    {
        if (!ComponentRegistry.TryGet(key, out var type)) return "{}";
        var dataType = FindNestedDataType(type);
        if (dataType == null) return "{}";
        try { return JsonUtility.ToJson(Activator.CreateInstance(dataType), true); }
        catch { return "{}"; }
    }

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

    // Text field + id popup. "—" = empty; the popup only writes on an actual selection
    // change, so optional empty fields aren't force-filled just by being drawn.
    string TFDropdown(string label, string val, string[] options)
    {
        EditorGUILayout.BeginHorizontal();
        val = EditorGUILayout.TextField(label, val ?? "");
        if (options.Length > 0)
        {
            var opts = new string[options.Length + 1];
            opts[0] = "—";
            Array.Copy(options, 0, opts, 1, options.Length);
            int idx = Array.IndexOf(options, val) + 1;   // 0 = "—" when not found
            int sel = EditorGUILayout.Popup(idx, opts, GUILayout.Width(150));
            if (sel != idx) val = sel <= 0 ? "" : opts[sel];
        }
        EditorGUILayout.EndHorizontal();
        return val;
    }

    /// <summary>
    /// Selectable list row with ▲/▼ reorder buttons.
    /// Returns 0 (no move), -1 (move up), or +1 (move down).
    /// </summary>
    int ListItem(string label, bool selected, Action onSelect, GUIStyle style = null, bool arrows = true)
    {
        int move = 0;
        EditorGUILayout.BeginHorizontal();

        if (arrows)
        {
            var arrowStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 8,
                padding  = new RectOffset(0, 0, 0, 0),
                margin   = new RectOffset(1, 1, 4, 1),
            };
            if (GUILayout.Button("▲", arrowStyle, GUILayout.Width(16), GUILayout.Height(16))) move = -1;
            if (GUILayout.Button("▼", arrowStyle, GUILayout.Width(16), GUILayout.Height(16))) move = +1;
        }

        GUI.backgroundColor = selected ? new Color(0.3f, 0.55f, 1f) : Color.white;
        var s = style ?? new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
        if (GUILayout.Button(label, s)) onSelect();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        return move;
    }

    /// <summary>Swaps list[idx] with its neighbor in dir, keeping the selection on the moved item.</summary>
    static bool MoveEntry<T>(List<T> list, int idx, int dir, ref int selIdx)
    {
        int to = idx + dir;
        if (idx < 0 || idx >= list.Count || to < 0 || to >= list.Count) return false;
        (list[idx], list[to]) = (list[to], list[idx]);
        if (selIdx == idx) selIdx = to;
        else if (selIdx == to) selIdx = idx;
        return true;
    }

    static GUIStyle RichListStyle() =>
        new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = true, fixedHeight = 0 };

    /// <summary>Deep copy via JSON round-trip — safe for nested arrays (components, colors).</summary>
    static T CloneDef<T>(T src) => JsonUtility.FromJson<T>(JsonUtility.ToJson(src));

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

    string[] ProjectileIds()
    {
        var ids = new string[_projectiles.Count];
        for (int i = 0; i < _projectiles.Count; i++) ids[i] = _projectiles[i].id;
        return ids;
    }

    string[] MinionIds()
    {
        var ids = new string[_minions.Count];
        for (int i = 0; i < _minions.Count; i++) ids[i] = _minions[i].id;
        return ids;
    }

    string[] ComponentKeys()
    {
        var keys = new List<string>(ComponentRegistry.All.Keys);
        keys.Sort();
        return keys.ToArray();
    }

    string[] ValidatorPrefixes()
    {
        var keys = new List<string>(TargetValidatorRegistry.Prefixes);
        keys.Sort();
        return keys.ToArray();
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
