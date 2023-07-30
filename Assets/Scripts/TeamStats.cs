using System;
using UnityEngine;

[Serializable]
public struct TeamStats
{
    public string TeamName;
    public Color TeamColor;
    public Transform SpawnLocation;
    public GameObject PrefabCustomBase;
    public GameObject PrefabCustomFlag;
}
