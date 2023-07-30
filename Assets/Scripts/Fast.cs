using System;
using UnityEngine;

public static class Fast
{
    private static Vector2 v2_buff_0;
    private static Vector2 v2_buff_1;
    private static Vector2 v2_buff_2;
    private static Vector3 v3_buff_0;

    public static bool FastDistanceGreater(Transform a, Transform b, Transform source = null)
    {
        return FastDistance(a, source) > FastDistance(b, source);
    }
    public static bool FastDistanceGreater(ref Vector2 a, ref Vector2 b)
    {
        v2_buff_0 = Vector2.zero;
        return FastDistanceGreater(ref a, ref b, ref v2_buff_0);
    }
    public static bool FastDistanceGreater(ref Vector2 a, ref Vector2 b, ref Vector2 source)
    {
        return FastDistance(ref a, ref source) > FastDistance(ref b, ref source);
    }
    public static void WritePosToBuffer(Transform transform, ref Vector2 v2)
    {
        if (transform == null)
        {
            v2 = Vector2.zero;
            return;
        }

        v3_buff_0 = transform.position;
        Write3to2(ref v3_buff_0, ref v2);
    }
    public static void Write3to2(ref Vector3 v3, ref Vector2 v2)
    {
        v2.x = v3.x;
        v2.y = v3.z;
    }
    public static void Write2to3(ref Vector2 v2, ref Vector3 v3)
    {
        v3.x = v2.x;
        v3.y = 0;
        v3.z = v2.y;
    }
    //private bool FastDistanceGreater(Vector3 a, Vector3 b, float c)
    //{
    //    return FastDistance(ref a, ref b) > Math.Pow(c, 2);
    //}
    public static double FastDistance(Transform a, Transform b = null)
    {
        WritePosToBuffer(a, ref v2_buff_0);
        WritePosToBuffer(b, ref v2_buff_1);
        return FastDistance(ref v2_buff_0, ref v2_buff_1);
    }
    public static double FastDistance(ref Vector2 v)
    {
        v2_buff_0 = Vector2.zero;
        return FastDistance(ref v, ref v2_buff_0);
    }
    public static double FastDistance(ref Vector2 a, ref Vector2 b)
    {
        double xDelta = a.x - b.x;
        xDelta *= xDelta;
        double result = a.y - b.y;
        result *= result;
        result += xDelta;
        //double result = Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2);
        result = double.IsNaN(result) ? 0 : result;
        return result; 
    }
}

