using System;

[Serializable]
public class Squad
{
    public BonomType Type;
    public int Count;
    public float Ratio;
    public bool Locked;

    public Squad(BonomType type, float ratio)
    {
        Type = type;
        Count = 0;
        Ratio = ratio;
        Locked = false;
    }

    public void ToggleLock()
    {
        Locked = !Locked;
    }
}
