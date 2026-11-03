using System;

namespace IncreaseHardMod.Valuables;

/// <summary>One kit-loss debit per valuable, surviving snapshot import and authority transfer.</summary>
internal sealed class TubeLossState
{
    internal bool Committed { get; private set; }
    internal bool Applied { get; private set; }
    internal int Cost { get; private set; }
    internal bool Commit(int cost)
    {
        if(Committed)return false;
        Cost=Math.Max(2000,Math.Min(3000,cost));Committed=true;return true;
    }
    internal int TakePayment(bool host,bool valueReady,float currentValue)
    {
        if(!host||!valueReady||!Committed||Applied||!ValuableDynamics.Finite(currentValue))return 0;
        Applied=true;return (int)Math.Min(Cost,Math.Max(0,currentValue));
    }
    internal bool Import(int cost,bool applied)
    {
        if(cost==0)return !applied&&!Committed;
        if(cost<2000||cost>3000||Committed&&cost!=Cost)return false;
        Cost=cost;Committed=true;Applied|=applied;return true;
    }
}
