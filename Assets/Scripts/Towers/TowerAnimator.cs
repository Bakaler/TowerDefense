using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a sprite-sheet animation on the tower's own SpriteRenderer when it attacks,
/// then restores the original sprite. Add to tower prefabs alongside Turret.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TowerAnimator : MonoBehaviour
{
    private SpriteRenderer _sr;
    private Sprite         _originalSprite;
    private Coroutine      _activeAnim;

    void Awake()
    {
        _sr             = GetComponent<SpriteRenderer>();
        _originalSprite = _sr.sprite;
    }

    public void PlayAttack(Ability_Effect ability)
    {
        if (string.IsNullOrEmpty(ability.attackSheetPath) || ability.attackFrameCount <= 0) return;

        if (_activeAnim != null) StopCoroutine(_activeAnim);
        _activeAnim = StartCoroutine(PlaySheet(ability));
    }

    IEnumerator PlaySheet(Ability_Effect ability)
    {
        var tex = Resources.Load<Texture2D>(ability.attackSheetPath);
        if (tex == null)
        {
            Debug.LogWarning($"[TowerAnimator] Texture not found at '{ability.attackSheetPath}'");
            yield break;
        }

        int fw = tex.width / ability.attackFrameCount;
        int fh = tex.height;
        var frames = new Sprite[ability.attackFrameCount];
        for (int i = 0; i < ability.attackFrameCount; i++)
            frames[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), new Vector2(0.5f, 0.5f), fw);

        if (ability.attackScale != 1f)
            transform.localScale = Vector3.one * ability.attackScale;

        float spf   = 1f / ability.attackFps;
        float timer = 0f;
        int   frame = 0;
        _sr.sprite  = frames[0];

        while (frame < ability.attackFrameCount)
        {
            timer += Time.deltaTime;
            int next = Mathf.FloorToInt(timer / spf);
            if (next != frame && next < ability.attackFrameCount)
            {
                frame      = next;
                _sr.sprite = frames[frame];
            }
            yield return null;
        }

        _sr.sprite          = _originalSprite;
        transform.localScale = Vector3.one;
        _activeAnim          = null;
    }
}
