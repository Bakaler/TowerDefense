using UnityEngine;

[CreateAssetMenu(fileName = "NewAccumulator", menuName = "Accumulator/AccumulatorConstant")]
public class Accumulators_Constant : ScriptableObject
{
    public int amount;
    public ApplicationRule application_rule;
}

public enum ApplicationRule
{
    Add,
    Multiply,
    AddativeMultiply,
}
