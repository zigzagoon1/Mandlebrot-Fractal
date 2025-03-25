
using PeterO.Numbers;
using UnityEngine;

public class HelperMethods
{
    /// <summary>
    /// Converts an Extended Decimal (EDecimal) value to a Vector2. 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static Vector2 ToFloat2(EDecimal value)
    {
        double valueAsDouble = (double)value;

        float hi = (float)(valueAsDouble);
        float lo = (float)(valueAsDouble - (double)hi);

        if (hi == 0f && lo == 0f)
        {
            Debug.LogWarning($"[ToFloat2] Warning: value {value} underflowed to 0");
        }

        return new Vector2(hi, lo);
    }
}
