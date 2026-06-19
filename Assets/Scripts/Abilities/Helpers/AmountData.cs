using System.Collections.Generic;

[System.Serializable]
public class AmountData
{
    public int Base { get; set; }
    public List<Accumulators_Constant> Accumulators { get; set; } = new List<Accumulators_Constant>();
}
