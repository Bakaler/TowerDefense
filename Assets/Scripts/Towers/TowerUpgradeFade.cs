using UnityEngine;

/// <summary>
/// Tier-upgrade transition. A ghost of the previous tier's visuals (sprite or running
/// animation, plus turret) is drawn beneath the tower while the new tier's sprites fade
/// in on the live renderers; the ghost is removed once the fade completes.
/// Runs on unscaled time so upgrades bought while paused still resolve.
/// </summary>
public class TowerUpgradeFade : MonoBehaviour
{
    const float Duration = 0.8f;

    SpriteRenderer _main, _turret;
    GameObject     _ghost;
    Color          _mainTarget, _turretTarget;
    int            _mainOrder, _turretOrder;
    float          _t;

    /// <summary>Call right after the tower's visuals switched to the new tier.</summary>
    public static void Play(TowerInfo info, int oldTier)
    {
        if (info == null) return;
        if (TowerDefinitionLibrary.Instance == null ||
            !TowerDefinitionLibrary.Instance.TryGet(info.definitionId, out var def)) return;

        // Rapid successive upgrades: finish the previous transition instantly
        var existing = info.GetComponent<TowerUpgradeFade>();
        if (existing != null) existing.Complete();

        info.gameObject.AddComponent<TowerUpgradeFade>().Begin(def, oldTier);
    }

    void Begin(TowerDefinition def, int oldTier)
    {
        _main = GetComponent<SpriteRenderer>();
        if (_main == null) { Destroy(this); return; }
        var turretT = transform.Find("Turret");
        _turret = turretT != null ? turretT.GetComponent<SpriteRenderer>() : null;

        // ── Ghost of the previous tier, beneath the fading new visuals ──
        _ghost = new GameObject("[UpgradeGhost]");
        _ghost.transform.SetParent(transform, false);
        _ghost.transform.localPosition = Vector3.zero;

        var gsr              = _ghost.AddComponent<SpriteRenderer>();
        gsr.sortingLayerName = _main.sortingLayerName;
        gsr.sortingOrder     = _main.sortingOrder;
        gsr.color            = def.tintColor;

        if (def.animFps > 0f)
        {
            var frames = Resources.LoadAll<Sprite>(TowerFactory.ResolveTieredPath(def.id, oldTier, def.spritePath));
            if (frames != null && frames.Length > 0)
            {
                gsr.sprite = frames[0];
                if (frames.Length > 1)
                    _ghost.AddComponent<SpriteAnimator>().Setup(frames, def.animFps);
            }
        }
        else
        {
            gsr.sprite = TowerFactory.ResolveTieredSprite(def.id, oldTier, def.spritePath);
        }

        if (_turret != null)
        {
            var oldTurretSprite = TowerFactory.ResolveTieredSprite(def.id + "_turret", oldTier, def.turretSpritePath);
            if (oldTurretSprite != null)
            {
                var tGO = new GameObject("Turret");
                tGO.transform.SetParent(_ghost.transform, false);
                tGO.transform.localRotation = _turret.transform.localRotation;   // keep current aim
                var tsr              = tGO.AddComponent<SpriteRenderer>();
                tsr.sprite           = oldTurretSprite;
                tsr.color            = _turret.color;
                tsr.sortingLayerName = _turret.sortingLayerName;
                tsr.sortingOrder     = _main.sortingOrder + 1;
            }
        }

        // ── Live renderers move above the ghost and fade in from transparent ──
        _mainOrder         = _main.sortingOrder;
        _mainTarget        = _main.color;
        _main.sortingOrder = _mainOrder + 2;
        _main.color        = WithAlpha(_mainTarget, 0f);

        if (_turret != null)
        {
            _turretOrder         = _turret.sortingOrder;
            _turretTarget        = _turret.color;
            _turret.sortingOrder = _mainOrder + 3;
            _turret.color        = WithAlpha(_turretTarget, 0f);
        }
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime / Duration;
        float a = Mathf.Clamp01(_t);
        if (_main   != null) _main.color   = WithAlpha(_mainTarget,   _mainTarget.a   * a);
        if (_turret != null) _turret.color = WithAlpha(_turretTarget, _turretTarget.a * a);
        if (_t >= 1f) Complete();
    }

    void Complete()
    {
        if (_main   != null) { _main.color   = _mainTarget;   _main.sortingOrder   = _mainOrder; }
        if (_turret != null) { _turret.color = _turretTarget; _turret.sortingOrder = _turretOrder; }
        if (_ghost  != null) Destroy(_ghost);
        Destroy(this);
    }

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
