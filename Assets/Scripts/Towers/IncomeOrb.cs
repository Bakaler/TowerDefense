using System.Collections;
using UnityEngine;

/// <summary>
/// Gentle bob animation for an income orb sitting above an IncomeTower slot.
/// Call Pop() to play the collect burst then self-destruct.
/// </summary>
public class IncomeOrb : MonoBehaviour
{
    public float bobAmplitude = 0.07f;
    public float bobSpeed     = 1.8f;
    public float popDuration  = 0.18f;  // seconds for the pop animation
    public float popScale     = 2.2f;   // how much bigger before fading

    private Vector3 _basePos;
    private float   _phase;
    private bool    _popping;

    void Start()
    {
        _basePos = transform.position;
        _phase   = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        if (_popping) return;
        float bob = Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude;
        transform.position = _basePos + Vector3.up * bob;
    }

    /// <summary>Scale up, fade out, then destroy. Call instead of Destroy().</summary>
    public void Pop()
    {
        _popping = true;
        StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        var sr           = GetComponent<SpriteRenderer>();
        Vector3 startScale = transform.localScale;
        Vector3 endScale   = startScale * popScale;
        float elapsed      = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / popDuration;

            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - t);

            yield return null;
        }

        Destroy(gameObject);
    }
}
