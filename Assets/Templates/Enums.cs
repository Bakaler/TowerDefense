using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enums : MonoBehaviour
{
}

public enum DamageType
{
    Elemental,
    Arcane,
    Physical,
    Piercing,
    Poison,
    Pure,
}

public enum BalanceType
{
    Elemental,
    Arcane,
    Physical,
    /// <summary>Counts toward all three balance types (contribution split evenly).</summary>
    All,
}