using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Engine : MonoBehaviour
{
    public List<Bonom> Dead = new List<Bonom>();

    public Team[] Teams;
    public Transform[] SpawnLocations;
    public BonomStats[] PreSets;

    public float SpawnRadius;
    //public int SpawnTime;
    public int MemberCount;
    public int SelectedTeamIndex;

    public Bonom PrefabBonom;
    public Flag PrefabFlag;
    public UIManager UIManager;

    public int BodyExpirationSeconds;
    public int SpawnDelaySeconds;
    public bool Named;
    public Team SelectedTeam => Teams[SelectedTeamIndex];

    public List<Bonom> GetEnemies(int teamIndex)
    {
        List<Bonom> request_buffer = new List<Bonom>();
        foreach (Team team in Teams)
            if (team.TeamIndex != teamIndex)
                request_buffer.AddRange(team.Members);
        return request_buffer;
    }

    public Vector3 SpawnLocation(int teamIndex)
    {
        Vector3 location = SpawnLocations[teamIndex].position;

        location.x += ((UnityEngine.Random.value * 2) - 1) * SpawnRadius;
        location.z += ((UnityEngine.Random.value * 2) - 1) * SpawnRadius;

        return location;
    }

    public void AttackBonom(Bonom attacker, Bonom target)
    {
        float dmg = attacker.Stats.AttkDamage;
        target.Health -= dmg;
        target.myTeam.DamageRecieved += dmg;
        attacker.myTeam.DamageDealt += dmg;
        attacker.myTeam.KillCount += target.Health <= 0 ? 1 : 0;
    }

    public BonomStats RandomBonomStats()
    {
        return PreSets[(int)(UnityEngine.Random.value * (PreSets.Length - 1) + .5f)];
    }

    public void GenerateBonom(Team requestingTeam, bool random = false, bool debug = false)
    {
        GameObject newBonomObject = Instantiate(PrefabBonom.gameObject);
        
        newBonomObject.SetActive(true);
        newBonomObject.GetComponent<Renderer>().enabled = debug;
        Bonom newBonom = newBonomObject.GetComponent<Bonom>();
        requestingTeam.AddBonom(newBonom, random);
        newBonomObject.transform.position = SpawnLocation(requestingTeam.TeamIndex);
        if (newBonom.Stats.Prefab != null)
        {
            /*GameObject newBonomMeshObject =*/ Instantiate(newBonom.Stats.Prefab, newBonomObject.transform);
        }
            
    }

    public Flag GenerateTeamFlag(Team requestingTeam)
    {
        GameObject newFlagObject = Instantiate(PrefabFlag.gameObject);
        newFlagObject.SetActive(true);
        Flag newFlag = newFlagObject.GetComponent<Flag>();
        newFlag.Init(this, requestingTeam);
        newFlagObject.transform.position = SpawnLocations[requestingTeam.TeamIndex].position;
        return newFlag;
    }

    private Color RandomColorGenerator()
    {
        return new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
    }

    private void SpawnUpdate()
    {
        foreach(Team team in Teams)
        {
            if (team.Members.Count >= MemberCount)
                continue;

            if (team.LastSpawn + new TimeSpan(0,0,0,SpawnDelaySeconds) > DateTime.Now)
                continue;

            team.LastSpawn = DateTime.Now;
            GenerateBonom(team, false, true);
        }
    }

    private void DeadUpdate()
    {
        if (Dead.Count < 1)
            return;

        if (Dead[0] == null ||
            Dead[0].Alive)
        {
            Dead.RemoveAt(0);
            return;
        }

        if (Dead[0].DeathTime + new TimeSpan(0,0,0,BodyExpirationSeconds) < DateTime.Now)
        {
            Dead[0].myTeam.RemoveBonom(Dead[0]);
            //Dead[0].myTeam.Members.Remove(Dead[0]);
            Destroy(Dead[0].gameObject);
            Dead.RemoveAt(0);
        }
    }

    public void MoveSelectedFlag(Vector3 newLocation)
    {
        Teams[SelectedTeamIndex].Flag.transform.position = newLocation;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (SpawnLocations == null)
            return;

        Teams = new Team[SpawnLocations.Length];

        for(int i = 0; i < SpawnLocations.Length; i++)
        {
            if (SpawnLocations[i] == null)
                continue;

            Color color;
            MeshRenderer spawnMesh = SpawnLocations[i].GetComponent<MeshRenderer>();
            if (spawnMesh == null)
                color = RandomColorGenerator();
            else
                color = spawnMesh.material.color;

            Teams[i] = new Team(this, i, color, Named? SpawnLocations[i].name : null);
        }

        UIManager.PopulateTeamSelectionButtons();
        UIManager.PopulateRatioSliderPanels();
        UIManager.PopulateSquadCounterTexts();
        UIManager.TeamSelection(SelectedTeamIndex);
    }

    // Update is called once per frame
    void Update()
    {
        SpawnUpdate();
        DeadUpdate();
    }
}
