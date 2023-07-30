using System;
using System.Collections.Generic;
using UnityEngine;

public class Team
{
    public TeamStats Stats;
    public long LastSpawn;

    public GameObject Flag;
    public GameObject Base;

    public Dictionary<string, Squad> Squads;
    public int MemberCount {get
        {
            int total = 0;
            foreach (Squad squad in Squads.Values)
                total += squad.Members.Count;
            return total;
        } }
    public float DamageDealt = 0;
    public float DamageRecieved = 0;
    public float DamageHealed = 0;
    public int KillCount = 0;

    BonomStats stat_buffer;

    public Team(TeamStats stats)
    {
        Stats = stats;
        Flag = Game.GenerateNewFlag(this);
        Base = Game.GenerateNewBase(this);
        Squads = new Dictionary<string, Squad>();
        float sum = 0;
        int presetCount = Game.Instance.BonomPresets.Length;
        float init = 1f / presetCount;
        Debug.Log(init);

        for (int i = 0; i < presetCount; i++)
        {
            Squads.Add(Game.Instance.BonomPresets[i].Type, new Squad(this, i < presetCount - 1 ? init : 1 - sum));
            sum += init;
        } 
    }

    private BonomStats NeededStats()
    {
        for (int i = 0; i < Game.Instance.BonomPresets.Length; i++)
        {
            Squad check = Squads[Game.Instance.BonomPresets[i].Type];

            if (check.Ratio == 0)
                continue;

            if (MemberCount == 0)
                return Game.Instance.BonomPresets[i];

            float currentRatio = (float)check.Count / MemberCount;

            if (currentRatio < check.Ratio)
                return Game.Instance.BonomPresets[i];    
        }

        return Game.Instance.BonomPresets[0]; // fail-safe
    }

    public Vector3 SpawnLocation()
    {
        Vector3 location = Stats.SpawnLocation.position;

        location.x += ((UnityEngine.Random.value * 2) - 1) * Game.Instance.SpawnRadius;
        location.z += ((UnityEngine.Random.value * 2) - 1) * Game.Instance.SpawnRadius;

        return location;
    }

    public Bonom AddBonom(bool random = false)
    {
        stat_buffer = random ? Game.RandomBonomStats() : NeededStats();
        Bonom newBonom;
        Squad targetSquad = Squads[stat_buffer.Type];

        if (!targetSquad.RecycleBonom(out newBonom))
        {
            newBonom = Game.GenerateNewBonom();
            newBonom.Init(targetSquad, stat_buffer);
        }

        Squads[stat_buffer.Type].Members.Add(newBonom);
        newBonom.gameObject.SetActive(true);
        newBonom.Refresh();

        Game.UIManager.CountUpdate(this);

        return newBonom;
    }

    public Squad this[string type]
    {
        get { return Squads[type]; }

        set { Squads[type] = value; }
    }
}
