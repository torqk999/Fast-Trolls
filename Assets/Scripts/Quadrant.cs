using System;
using System.Collections.Generic;

[Serializable]
public class Quadrant : List<Bonom>
{
    public int Xcoord { get; private set; }
    public int Zcoord { get; private set; }

    //public Quadrant(int x, int z)
    //{
    //    Reposition(x, z);
    //}

    public void Reposition(int x, int z)
    {
        Xcoord = x;
        Zcoord = z;
    }

    public void AddBonom(Bonom newBonom)
    {
        newBonom.myQuad = this;
        Add(newBonom);
    }

    public bool RemoveBonom(Bonom newBonom)
    {
        newBonom.myQuad = null;
        return Remove(newBonom);
    }
}
