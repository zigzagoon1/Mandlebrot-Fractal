
using PeterO.Numbers;
using UnityEngine;

public class HelperMethods
{
    public static Vector2 DoubleToFloat2(double d)
    {
        float hi = (float)d;
        float lo = (float)(d - (double)hi);

        return new Vector2(hi, lo);
    }
}
