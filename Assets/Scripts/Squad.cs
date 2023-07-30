using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Squad
{
    public Team parentTeam;
    public int Count => Members.Count;
    public float Ratio;
    public bool Locked;

    public List<Bonom> Members = new List<Bonom>();
    public List<Bonom> Recycle = new List<Bonom>();
    public List<Vector2> WayPoints = new List<Vector2>();
    public Squad(Team team, float initRatio)
    {
        parentTeam = team;
        Ratio = initRatio;
        Locked = false;
    }
    public Bonom this[int index]
    {
        get { return Members[index]; }
        set { Members[index] = value; }
    }

    public bool RecycleBonom(out Bonom recycled)
    {
        if (Recycle.Count == 0)
        {
            recycled = null;
            return false;
        }

        recycled = Recycle[0];
        Recycle.RemoveAt(0);
        return true;
    }
    public bool KillBonom(Bonom deadBonom)
    {
        if (Members.Remove(deadBonom))
        {
            Recycle.Add(deadBonom);
            return true;
        }

        return false;
    }
    public void ToggleLock()
    {
        Locked = !Locked;
    }
}
