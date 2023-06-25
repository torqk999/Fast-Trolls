using System;
using System.Collections.Generic;

[Serializable]
public class Quadrant : List<Bonom>
{
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
