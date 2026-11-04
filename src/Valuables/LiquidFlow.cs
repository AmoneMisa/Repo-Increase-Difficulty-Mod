using System;

namespace IncreaseHardMod.Valuables;

internal static class LiquidFlow
{
    internal static float SignedRate(float up)=>ValuableDynamics.Finite(up)&&Math.Abs(up)>.1f?
        (float)(Math.Sign(up)*Math.Pow(Math.Min(1,Math.Abs(up)),1.5)):0;
    internal static float Advance(float upper,float up,float dt,float seconds)
    {
        if(!ValuableDynamics.Finite(upper)||!ValuableDynamics.Finite(up)||!ValuableDynamics.Finite(dt)||seconds<=0)return upper;
        if(Math.Abs(up)<.1f)return upper;
        double flow=SignedRate(up)*Math.Max(0,dt)/seconds;
        return (float)Math.Max(0,Math.Min(1,upper-flow));
    }
    internal static bool Flowing(float upper,float up)=>Math.Abs(up)>.1f&&(up>0?upper>.002f:upper<.998f);
}
