using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Engine : MonoBehaviour
{
    public Quadrant[,] QuadMap;
    public int xRoot, zRoot;
    public int xWidth, zWidth;
    public int QuadResolution;
    private int xWorkCurrent, zWorkCurrent;
    private int xWorkPrevious, zWorkPrevious;
    private int good_batches;
    private float quad_res_inverse;

    public List<Bonom> Dead = new List<Bonom>();
    public List<Bonom>[] SearchQuery;
    public List<Bonom>[] AOEQuery;

    public Team[] Teams;
    public Transform[] SpawnLocations;
    public BonomStats[] PreSets;

    public float SpawnRadius;
    public float AvoidanceMax;
    public float WorkHighlightHeight;
    public int SearchQueryRadius;
    public int AOEQueryRadius;
    public int ProxyRadius;
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
    public bool MapWorkActive;
    public bool MapInit;
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

    public Quadrant GetQuad(Vector3 coordinates, bool expand = true)
    {
        int xTarget, zTarget;
        GetCoords(coordinates, out xTarget, out zTarget);
        return GetQuad(xTarget, zTarget, expand);
    }
    public Quadrant GetQuad(int xTarget, int zTarget, bool expand = true, bool literal = false)
    {
        int xFinal = xTarget + (literal ? 0 : xRoot);
        int zFinal = zTarget + (literal ? 0 : zRoot);

        if (xFinal < 0 || xFinal >= QuadMap.GetLength(0) ||
            zFinal < 0 || zFinal >= QuadMap.GetLength(1))
        {
            if (expand)
            {
                ExpandMap(xFinal, zFinal);
                xFinal = xTarget + (literal ? 0 : xRoot);
                zFinal = zTarget + (literal ? 0 : zRoot);
            }
                
            else
                return null;
        }

        return QuadMap[xFinal, zFinal];
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

        for (int X = 0; X < xWidth; X++)
            for (int Z = 0; Z < zWidth; Z++)
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
    private void BonomQuery(List<Bonom>[] lists, int xOrigin, int zOrigin, int radius = 0, bool literal = false)
    {
        Quadrant searching = null;

        for (int i = 0; i < lists.Length; i++)
            lists[i].Clear();

        for (int X = xOrigin - radius; X <= xOrigin + radius; X++)
            for (int Z = zOrigin - radius; Z <= zOrigin + radius; Z++)
            {
                searching = GetQuad(X, Z, false, literal);
                if (searching == null)
                    continue;

                int radiusX = Math.Abs(xOrigin - X);
                int radiusZ = Math.Abs(zOrigin - Z);
                int radiusIndex = radiusX > radiusZ ? radiusX : radiusZ;
                lists[radiusIndex].AddRange(searching);
            }
    }
    public void AttackBonom(Bonom attacker, Bonom target)
    {
        if (attacker.Stats.AttkRadius > 0)
            AOEDamage(attacker, target);
        else
        {
            DamageBonom(attacker, target);
            KnockBonom(attacker, target, target.transform.position);
        } 
    }
    private void DamageBonom(Bonom attacker, Bonom target)
    {
        if (!target.Alive)
            return;

        float dmg = attacker.Stats.AttkDamage;
        target.Health -= dmg;
        target.myTeam.DamageRecieved += dmg;
        attacker.myTeam.DamageDealt += dmg;
        attacker.myTeam.KillCount += target.Alive ? 0 : 1;
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
        int xCoord, zCoord;
        GetCoords(target.transform.position, out xCoord, out zCoord);
        BonomQuery(AOEQuery, xCoord, zCoord, attacker.attk_radius);

        for (int i = 0; i <= attacker.attk_radius; i++)
            foreach (Bonom enemy in AOEQuery[i])
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
    private async void MapWorkUpdate()
    {
        while(MapWorkActive)
        {
            if (QuadMap == null)
            {
                Debug.LogError("Null map");
                MapWorkActive = false;
                continue;
            }

            if (QuadMap.Length < 1)
            {
                Debug.Log("No MapWork");
                await Task.Yield();
                continue;
            }

            Quadrant workingQuadrant = null;
            xWorkPrevious = xWorkCurrent;
            zWorkPrevious = zWorkCurrent;

            for (int i = 0; i < QuadMap.Length; i++)
            {
                AdvanceWorkCoords();
                workingQuadrant = GetQuad(xWorkCurrent, zWorkCurrent, false, true);
                if (workingQuadrant == null)
                {
                    Debug.LogError(
                        $"bad work coord: ({xWorkCurrent},{zWorkCurrent})\n" +
                        $"QuadMap Size: ({QuadMap.GetLength(0)},{QuadMap.GetLength(1)})\n" +
                        $"consecutive good batches: {good_batches}");
                    good_batches = 0;
                    xWorkCurrent = 0;
                    zWorkCurrent = 0;
                    MapWorkActive = false;
                    break;
                }
                if (workingQuadrant.Count > 0)
                    break;
            }

            if (!MapWorkActive)
                continue;

            good_batches++;

            BonomQuery(SearchQuery, xWorkCurrent, zWorkCurrent, SearchQueryRadius, true);
            for (int i = workingQuadrant.Count - 1; i > -1; i--)
                workingQuadrant[i].BatchUpdate();

            DrawWorkingQuadFrame(xWorkCurrent, zWorkCurrent, Color.red);
            DrawWorkingQuadFrame(xWorkPrevious, zWorkPrevious, Color.blue);
            await Task.Yield();
        }
    }

    private void AdvanceWorkCoords()
    {
        zWorkCurrent++;                                                         // Single Tick
        xWorkCurrent += zWorkCurrent >= QuadMap.GetLength(1) ? 1 : 0;           // Tick from Z roll-over
        zWorkCurrent = zWorkCurrent >= QuadMap.GetLength(1) ? 0 : zWorkCurrent; // Z roll-over
        xWorkCurrent = xWorkCurrent >= QuadMap.GetLength(0) ? 0 : xWorkCurrent; // X roll-over
    }

    Vector3 o;
    Vector3 x;
    Vector3 z;
    Vector3 O;

    private void DrawWorkingQuadFrame(int xCoord, int zCoord, Color color)
    {
        o.x = QuadResolution * (xCoord - xRoot); o.z = QuadResolution * (zCoord - zRoot);
        x.x = QuadResolution * (xCoord - xRoot + 1); x.z = QuadResolution * (zCoord - zRoot);
        z.x = QuadResolution * (xCoord - xRoot); z.z = QuadResolution * (zCoord - zRoot + 1);
        O.x = QuadResolution * (xCoord - xRoot + 1); O.z = QuadResolution * (zCoord - zRoot + 1);

        Debug.DrawLine(o, x, color);
        Debug.DrawLine(o, z, color);
        Debug.DrawLine(O, x, color);
        Debug.DrawLine(O, z, color);
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

        SearchQueryRadius = ProxyRadius;
        foreach(BonomStats preset in PreSets)
        {
            int agro = (int)(preset.AggroRange / QuadResolution);
            int splash = (int)(preset.AttkRadius / QuadResolution);
            SearchQueryRadius = agro > SearchQueryRadius ? agro : SearchQueryRadius;
            AOEQueryRadius = splash > AOEQueryRadius ? splash : AOEQueryRadius;
        }

        SearchQuery = new List<Bonom>[SearchQueryRadius + 1]; // +1 for origin
        for (int i = 0; i < SearchQuery.Length; i++)
            SearchQuery[i] = new List<Bonom>();

        AOEQuery = new List<Bonom>[AOEQueryRadius + 1];
        for (int i = 0; i < AOEQuery.Length; i++)
            AOEQuery[i] = new List<Bonom>();

        o.y = WorkHighlightHeight;
        x.y = WorkHighlightHeight;
        z.y = WorkHighlightHeight;
        O.y = WorkHighlightHeight;

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

        MapWorkActive = true;
        MapWorkUpdate();
        //StartCoroutine(MapWorkUpdate());
    }

    // Update is called once per frame
    void Update()
    {
        SpawnUpdate();
    }
}
