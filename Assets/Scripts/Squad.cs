using System;

[Serializable]
public class Squad
{
    public int TypeIndex;
    public int Count;
    public float Ratio;
    public bool Locked;

    public Squad(int type, float ratio)
    {
        TypeIndex = type;
        Count = 0;
        Ratio = ratio;
        Locked = false;
    }

    public void ToggleLock()
    {
        Locked = !Locked;
    }
}
