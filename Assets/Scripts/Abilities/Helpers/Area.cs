using System;
using UnityEngine;

[Serializable]
public class Area
{
    [Header("Shape")]
    public float radius = 5f;
    public float startingDepth = 0f;
    public int maxTargets = -1;
    public float radiusBonus = 0f;
    public float castOffset = 0f;

    [Header("Arc")]
    [Range(0f, 360f)] public float horizontalArc = 360f;

    [Header("Facing")]
    public float facingAdjustment = 0f;

    [Header("Effect")]
    public Effect effect;
}
