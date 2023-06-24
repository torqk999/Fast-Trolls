using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

public class Engine : MonoBehaviour
{
    public Quadrant[,] QuadMap;
    private int xRoot, zRoot;
    public int QuadResolution;

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
    public float body_exp_inverse;
    public long SpawnDelayTicks;
    public long RegenDelayTicks;
    public bool Named;
    public Team SelectedTeam => Teams[SelectedTeamIndex];

    public Quadrant GetQuad(Vector3 coordinates)
    {
        int xTarget = ((int)coordinates.x / QuadResolution) + xRoot;
        int zTarget = ((int)coordinates.z / QuadResolution) + zRoot;

        if (xTarget < 0 || xTarget >= QuadMap.GetLength(0) ||
            zTarget < 0 || zTarget >= QuadMap.GetLength(1))
        {
            ExpandMap(xTarget, zTarget);
            xTarget = ((int)coordinates.x / QuadResolution) + xRoot;
            zTarget = ((int)coordinates.z / QuadResolution) + zRoot;
        }

        return QuadMap[xTarget, zTarget];
    }
    private void ExpandMap(int x, int z)
    {
        int xInit = QuadMap.GetLength(0);
        int zInit = QuadMap.GetLength(1);
        int xWidth = x < 0 ? xInit - x : x >= xInit ? x : xInit;
        int zWidth = z < 0 ? zInit - z : z >= zInit ? z : zInit;
        int xOffset = x < 0 ? -x : 0;
        int zOffset = z < 0 ? -z : 0;
        xRoot -= x < 0 ? x : 0;
        zRoot -= z < 0 ? z : 0;

        Quadrant[,] newMap = new Quadrant[xWidth, zWidth];

        for(int X = 0; X < xWidth; X++)
            for(int Z = 0; Z < zWidth; Z++)
            {
                newMap[X, Z] =
                (X >= xOffset && X < xOffset + xWidth && Z >= zOffset && Z < zOffset + zWidth) ?
                QuadMap[X - xOffset, Z - zOffset] :
                new Quadrant();
            }

        QuadMap = newMap;
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
        GetQuad(newBonomObject.transform.position).Add(newBonom);

        if (newBonom.Stats.Prefab != null)
        {
            GameObject newMeshObject = Instantiate(newBonom.Stats.Prefab, newBonomObject.transform.position, newBonomObject.transform.rotation, newBonomObject.transform);
            newMeshObject.SetActive(true);
            newMeshObject.GetComponent<Renderer>().material.color = newBonom.myTeam.TeamColor;
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

    public void MoveSelectedFlag(Vector3 newLocation)
    {
        Teams[SelectedTeamIndex].Flag.transform.position = newLocation;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (SpawnLocations == null)
            return;

        QuadMap = new Quadrant[0, 0];
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

        body_exp_inverse = 1f / BodyExpirationTicks;
    }

    // Update is called once per frame
    void Update()
    {
        SpawnUpdate();
    }
}
