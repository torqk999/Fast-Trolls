using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Engine : MonoBehaviour
{
    public Quadrant[,] QuadMap;
    public int xRoot, zRoot;
    public int xWidth, zWidth;
    public int QuadResolution;
    private float quad_res_inverse;

    public List<Bonom> Dead = new List<Bonom>();
    private List<Bonom> query = new List<Bonom>();

    public Team[] Teams;
    public Transform[] SpawnLocations;
    public BonomStats[] PreSets;

    public float SpawnRadius;
    public float AvoidanceScalar;
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
    public bool debug;
    public Team SelectedTeam => Teams[SelectedTeamIndex];
    public int GetTypeIndex(string requestType)
    {
        for (int i = 0; i < PreSets.Length; i++)
            if (PreSets[i].Type == requestType)
                return i;
        return -1;
    }

    public void GetCoords(Vector3 coordinates, out int xCoord, out int zCoord)
    {
        xCoord = (int)(coordinates.x * quad_res_inverse);
        zCoord = (int)(coordinates.z * quad_res_inverse);
    }
    public Quadrant GetQuad(Vector3 coordinates)
    {
        int xTarget, zTarget;
        GetCoords(coordinates, out xTarget, out zTarget);
        return GetQuad(xTarget, zTarget);
    }
    public Quadrant GetQuad(int xTarget, int zTarget)
    {
        if (xTarget + xRoot < 0 || xTarget + xRoot >= QuadMap.GetLength(0) ||
            zTarget + zRoot < 0 || zTarget + zRoot >= QuadMap.GetLength(1))
        {
            ExpandMap(xTarget + xRoot, zTarget + zRoot);
        }

        return QuadMap[xTarget + xRoot, zTarget + zRoot];
    }
    private void ExpandMap(int x, int z)
    {
        int xInit = QuadMap.GetLength(0);
        int zInit = QuadMap.GetLength(1);
        xWidth = x < 0 ? xInit - x : x >= xInit ? x + 1 : xInit;
        zWidth = z < 0 ? zInit - z : z >= zInit ? z + 1 : zInit;
        int xOffset = x < 0 ? -x : 0;
        int zOffset = z < 0 ? -z : 0;
        xRoot -= x < 0 ? x : 0;
        zRoot -= z < 0 ? z : 0;

        Quadrant[,] newMap = new Quadrant[xWidth, zWidth];

        for(int X = 0; X < xWidth; X++)
            for(int Z = 0; Z < zWidth; Z++)
            {
                newMap[X, Z] =
                (X >= xOffset && X - xOffset < xInit &&
                Z >= zOffset && Z - zOffset < zInit) ?
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

    public void BonomQuery(List<Bonom> query, Vector3 position, int radius = 0)
    {
        query.Clear();
        int xOrigin, zOrigin;
        GetCoords(position, out xOrigin, out zOrigin);
        for (int X = xOrigin - radius; X <= xOrigin + radius; X++)
            for (int Z = zOrigin - radius; Z <= zOrigin + radius; Z++)
                query.AddRange(GetQuad(X, Z));
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
        BonomQuery(query, target.transform.position, attacker.attk_radius);
        foreach (Bonom enemy in query)
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

    public void AddBonomToTeam(Team requestingTeam, bool random = false, bool debug = false)
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

        newBonomObject.transform.position = SpawnLocation(requestingTeam.TeamIndex);
        GetQuad(newBonomObject.transform.position).Add(newBonom);

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
            AddBonomToTeam(team, false, debug);
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
        quad_res_inverse = 1f / QuadResolution;
    }

    // Update is called once per frame
    void Update()
    {
        SpawnUpdate();
    }
}
