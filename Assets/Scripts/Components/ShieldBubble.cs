using UnityEngine;

/// <summary>
/// Attached to a child GO on a shielded unit. Intercepts projectiles whose definition
/// has blockedByShields (projectiles.json); the shield absorbs def.shieldAbsorb damage.
/// </summary>
public class ShieldBubble : MonoBehaviour
{
    public float shieldHp    = 40f;
    public float maxShieldHp = 40f;

    private CircleCollider2D _col;
    private SpriteRenderer   _sr;

    public static ShieldBubble AddTo(GameObject target, float hp, float radius)
    {
        // Avoid duplicates
        var existing = target.GetComponentInChildren<ShieldBubble>();
        if (existing != null) { existing.shieldHp = Mathf.Max(existing.shieldHp, hp); return existing; }

        var go = new GameObject("ShieldBubble");
        go.transform.SetParent(target.transform, false);
        go.layer = target.layer;

        var col     = go.AddComponent<CircleCollider2D>();
        col.radius  = radius;
        col.isTrigger = true;

        // Visual ring
        var lr               = go.AddComponent<LineRenderer>();
        lr.loop              = true;
        lr.positionCount     = 36;
        lr.startWidth        = 0.06f;
        lr.endWidth          = 0.06f;
        lr.useWorldSpace     = false;
        lr.sortingLayerName  = "Units";
        lr.sortingOrder      = 25;
        lr.sharedMaterial          = RuntimeMaterials.SpriteDefault;
        lr.startColor        = new Color(0.3f, 0.7f, 1f, 0.85f);
        lr.endColor          = new Color(0.3f, 0.7f, 1f, 0.85f);
        for (int i = 0; i < 36; i++)
        {
            float a = i / 36f * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        var shield       = go.AddComponent<ShieldBubble>();
        shield.shieldHp  = hp;
        shield.maxShieldHp = hp;
        shield._col      = col;
        return shield;
    }

    /// <summary>Called by projectiles on impact. Returns true if the shield absorbed the hit.</summary>
    public bool AbsorbHit(float damage)
    {
        shieldHp -= damage;
        UpdateVisual();
        if (shieldHp <= 0f)
        {
            Destroy(gameObject);
            return true;
        }
        return true;
    }

    void UpdateVisual()
    {
        var lr = GetComponent<LineRenderer>();
        if (lr == null) return;
        float t = shieldHp / maxShieldHp;
        var col = Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.6f), new Color(0.3f, 0.7f, 1f, 0.85f), t);
        lr.startColor = col;
        lr.endColor   = col;
    }
}
