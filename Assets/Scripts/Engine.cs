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
    public CameraControl CameraControl;

    public long BodyExpirationTicks;
    public long SpawnDelayTicks;
    public long RegenDelayTicks;
    public bool Named;
    public Team SelectedTeam => Teams[SelectedTeamIndex];

    /*public List<Bonom> GetEnemies(int teamIndex)
    {
        List<Bonom> request_buffer = new List<Bonom>();
        foreach (Team team in Teams)
            if (team.TeamIndex != teamIndex)
                request_buffer.AddRange(team.Members);
        return request_buffer;
    }*/

    public Vector3 SpawnLocation(int teamIndex)
    {
        Vector3 location = SpawnLocations[teamIndex].position;

        location.x += ((UnityEngine.Random.value * 2) - 1) * SpawnRadius;
        location.z += ((UnityEngine.Random.value * 2) - 1) * SpawnRadius;

        return location;
    }

    public void AttackBonom(Bonom attacker, Bonom target)
    {
        AOEDamage(attacker, target);
        attacker.myTeam.KillCount += target.Health <= 0 ? 1 : 0;
    }

    private void DamageBonom(Bonom attacker, Bonom target)
    {
        float dmg = attacker.Stats.AttkDamage;
        target.Health -= dmg;
        target.myTeam.DamageRecieved += dmg;
        attacker.myTeam.DamageDealt += dmg;
    }

    private void KnockBonom(Bonom attacker, Bonom target, Vector3 source)
    {
        if (target.myRigidBody == null ||
            !target.Grounded)
            return;

        Vector3 knockBack = Vector3.Normalize(target.transform.position - source) * attacker.Stats.KnockBack;
        Vector3 knockUp = Vector3.up * attacker.Stats.KnockUp;

        target.myRigidBody.AddForce(knockBack + knockUp, ForceMode.Impulse);
    }

    private void AOEDamage(Bonom attacker, Bonom target)
    {
        //List<Bonom> enemies = GetEnemies(attacker.myTeam.TeamIndex);
        int damagedCount = 0;
        foreach (Bonom enemy in attacker.myTeam.Enemies)
        {
            float distance = Vector3.Distance(enemy.transform.position, target.transform.position);
            if (distance <= attacker.Stats.AttkRadius)
            {
                DamageBonom(attacker, enemy);
                KnockBonom(attacker, enemy, distance == 0 ? attacker.transform.position : target.transform.position);
                damagedCount++;
            }
        }
        Debug.Log($"Total hit: {damagedCount}");
    }

    public BonomStats RandomBonomStats()
    {
        return PreSets[(int)(UnityEngine.Random.value * (PreSets.Length - 1) + .5f)];
    }

    public void GenerateBonom(Team requestingTeam, bool random = false, bool debug = false)
    {
        GameObject newBonomObject;
        Bonom newBonom;

        if (RecycleBonom(out newBonom))
        {
            newBonomObject = newBonom.gameObject;
        }
        else
        {
            newBonomObject = Instantiate(PrefabBonom.gameObject);
            newBonom = newBonomObject.GetComponent<Bonom>();
        }

        newBonomObject.SetActive(true);
        newBonomObject.GetComponent<Renderer>().enabled = debug;
        
        requestingTeam.AddBonom(newBonom, random);

        foreach (Team enemyTeam in Teams)
            if (enemyTeam != requestingTeam)
                enemyTeam.AddEnemy(newBonom);

        newBonomObject.transform.position = SpawnLocation(requestingTeam.TeamIndex);
        if (newBonom.Stats.Prefab != null)
            Instantiate(newBonom.Stats.Prefab, newBonomObject.transform);
        
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
        foreach (Team team in Teams)
        {
            if (team.Members.Count >= MemberCount)
                continue;

            if (team.LastSpawn + new TimeSpan(SpawnDelayTicks) > DateTime.Now)
                continue;

            team.LastSpawn = DateTime.Now;
            GenerateBonom(team, false, true);
        }
    }

    private bool RecycleBonom(out Bonom oldBonom)
    {
        oldBonom = null;
        if (Dead.Count < 1 ||
            Dead[0].DeathTime + new TimeSpan(BodyExpirationTicks) > DateTime.Now)
            return false;
        oldBonom = Dead[0];
        Dead.RemoveAt(0);
        Debug.Log("Recycled!");
        return true;
    }

    /*private void DeadUpdate()
    {
        if (Dead.Count < 1)
            return;

        if (Dead[0] == null ||
            Dead[0].Alive)
        {
            Dead.RemoveAt(0);
            return;
        }

        if (Dead[0].DeathTime + new TimeSpan(BodyExpirationTicks) < DateTime.Now)
        {
            foreach (Team enemyTeam in Teams)
                if (enemyTeam != Dead[0].myTeam)
                    enemyTeam.RemoveEnemy(Dead[0]);

            Dead[0].myTeam.RemoveBonom(Dead[0]);

            //Dead[0].myTeam.Members.Remove(Dead[0]);
            Destroy(Dead[0].gameObject);
            Dead.RemoveAt(0);
        }
    }*/

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

        for (int i = 0; i < SpawnLocations.Length; i++)
        {
            if (SpawnLocations[i] == null)
                continue;

            Color color;
            MeshRenderer spawnMesh = SpawnLocations[i].GetComponent<MeshRenderer>();
            if (spawnMesh == null)
                color = RandomColorGenerator();
            else
                color = spawnMesh.material.color;

            Teams[i] = new Team(this, i, color, Named ? SpawnLocations[i].name : null);
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
        //DeadUpdate();
    }
}
