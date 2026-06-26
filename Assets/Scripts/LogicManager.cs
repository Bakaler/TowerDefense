using UnityEngine;

/// <summary>
/// Owns the player's lives.
/// Lives start at startingLives and decrease when enemies reach the terminus.
/// When lives hit zero, WaveManager is notified which triggers the Game Over state.
/// </summary>
public class LogicManager : MonoBehaviour
{
    [Header("Config")]
    public int startingLives = 20;

    // ── State ─────────────────────────────────────────────────────────
    public float lives    { get; private set; }
    public bool  gameOver { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        lives = startingLives;
    }

    public void ResetToStart(int overrideLives = -1)
    {
        lives    = overrideLives >= 0 ? overrideLives : startingLives;
        gameOver = false;
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Change lives by delta (negative to subtract).
    /// Triggers game over when lives reach zero.
    /// </summary>
    public void UpdateLives(float delta)
    {
        if (gameOver) return;

        lives += delta;

        if (lives <= 0f)
        {
            lives = 0f;
            Lose();
        }
    }

    // ── Private ───────────────────────────────────────────────────────

    void Lose()
    {
        if (gameOver) return;
        gameOver = true;
        WaveManager.Instance?.NotifyGameOver();
    }
}
