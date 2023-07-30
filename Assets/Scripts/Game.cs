using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Game
{
    public enum GameState
    {
        OFFLINE,
        ONLINE,
        LOBBY,
        MATCH
    }

    static public GameState CurrentGamestate;

    static public Instance Instance;
    static public UIManager UIManager;
    static public CameraControl CameraControl;

    static public int[,] QueryLookUp;
    static public Quadrant[,] QuadMap;
    static public List<Bonom> AllBonoms;

    static public Team[] Teams;
    static public Team SelectedTeam;

    static private Vector3 o;
    static private Vector3 x;
    static private Vector3 z;
    static private Vector3 O;

    static private int xRoot, zRoot;
    static private int xWidth, zWidth;
    static private int xWorkCurrent, zWorkCurrent;
    static private int xWorkPrevious, zWorkPrevious;
    static private int good_batches;
    static private int BonomWorkPrevious = 0;
    static private float quad_res_inverse;

    static public int QuadResolution { get; private set; }
    static public float health_reg_inverse { get; private set; }
    static public float body_exp_inverse { get; private set; }
    static public int SearchQueryRadius { get; private set; }
    static public int AOEQueryRadius { get; private set; }
    static public float anim_distance_squared => Instance.AnimationDistance * Instance.AnimationDistance;
    static public Camera MainCamera => CameraControl.Camera.GetComponent<Camera>();

    static public long GameTime;

    static public void InitializeGame(Instance instance)
    {
        if (instance.TeamPresets == null || instance.TeamPresets.Length < 2 ||
            instance.BonomPresets == null || instance.BonomPresets.Length == 0)
            return;

        Instance = instance;
        QuadResolution = instance.QuadResolution;
        UIManager = GameObject.Find("GameUI").GetComponent<UIManager>();
        CameraControl = GameObject.Find("CameraBoom").GetComponent<CameraControl>();

        if (UIManager == null)
            return;

        Instance.BonomWorkActive = true;
        Instance.MapWorkActive = true;

        health_reg_inverse = 1f / Instance.RegenDelayTicks; // Runtime-update?
        body_exp_inverse = 1f / Instance.BodyExpirationTicks; // Runtime-update?
        quad_res_inverse = 1f / Instance.QuadResolution;

        QuadMap = new Quadrant[0, 0];
        Teams = new Team[Instance.TeamPresets.Length];
        AllBonoms = new List<Bonom>();

        foreach (BonomStats preset in Instance.BonomPresets)
        {
            int agro = (int)(preset.AggroRange / Instance.QuadResolution) + 1;
            int splash = (int)(preset.AttkRadius / Instance.QuadResolution) + 1;
            SearchQueryRadius = agro > SearchQueryRadius ? agro : SearchQueryRadius;
            AOEQueryRadius = splash > AOEQueryRadius ? splash : AOEQueryRadius;
        }

        int lookUpTableRadius = Instance.ProxyRadius;
        lookUpTableRadius = lookUpTableRadius > SearchQueryRadius ? lookUpTableRadius : SearchQueryRadius;
        lookUpTableRadius = lookUpTableRadius > AOEQueryRadius ? lookUpTableRadius : AOEQueryRadius;

        bool directionY = false;
        bool negative = false;

        int xPos = 0;
        int yPos = 0;
        int delta = 1;
        int displaced = 0;

        int lookUpCount = (2 * (lookUpTableRadius - 1)) + 1;
        lookUpCount *= lookUpCount;

        QueryLookUp = new int[lookUpCount, 2];

        for (int index = 0; index < lookUpCount; index++)
        {
            QueryLookUp[index, 0] = xPos;
            QueryLookUp[index, 1] = yPos;
            Debug.Log($"Lookup ({index}): {QueryLookUp[index, 0]},{QueryLookUp[index, 1]}\n");

            xPos += (directionY ? 0 : 1) * (negative ? 1 : -1);
            yPos += (directionY ? 1 : 0) * (negative ? 1 : -1);

            displaced++;

            if (displaced == delta)
            {
                negative = directionY ? !negative : negative;
                delta += directionY ? 1 : 0;
                directionY = !directionY;
                displaced = 0;
            }
        }

        for (int i = 0; i < Teams.Length; i++)
        {
            Instance.TeamPresets[i].TeamColor = Instance.RandomTeamColors ? RandomColorGenerator() : Instance.TeamPresets[i].TeamColor;
            Teams[i] = new Team(Instance.TeamPresets[i]);
        }

        SelectedTeam = Teams[0];
        GameTime = 0;
        UIManager.UIinit();

        for (int i = 0; i < Teams.Length; i++)
            Instance.StartCoroutine(BonomWork(i));

        Instance.StartCoroutine(TeamWork());
        Instance.StartCoroutine(MapWork());

        Debug.Log("Done Initializing!");
    }
    static public void TerminateGame()
    {
        if (Instance == null)
            return;
    }
    static public void ExitToWindows()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
    }

    #region Query
    static public void GetNonLiteralCoords(Vector3 coordinates, out int xCoord, out int zCoord)
    {
        xCoord = (int)(coordinates.x * quad_res_inverse);
        zCoord = (int)(coordinates.z * quad_res_inverse);
    }
    static public Quadrant GetQuad(int xStart, int zStart, int lookUpIndex)
    {
        if (QueryLookUp == null || lookUpIndex > QueryLookUp.GetLength(0))
            return null;
        return GetQuad(xStart + QueryLookUp[lookUpIndex, 0], zStart + QueryLookUp[lookUpIndex, 1], false, true);
    }
    static public Quadrant GetQuad(Vector3 coordinates, bool expand = true)
    {
        int xTarget, zTarget;
        GetNonLiteralCoords(coordinates, out xTarget, out zTarget);
        return GetQuad(xTarget, zTarget, expand);
    }
    static public Quadrant GetQuad(int xTarget, int zTarget, bool expand = true, bool literal = false)
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

    static private void ExpandMap(int x, int z)
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
                bool outOfBounds =
                    (X >= xOffset && X - xOffset < xInit &&
                     Z >= zOffset && Z - zOffset < zInit);
                newMap[X, Z] = outOfBounds ? QuadMap[X - xOffset, Z - zOffset] : new Quadrant();
                newMap[X, Z].Reposition(X, Z);
            }

        QuadMap = newMap;
    }
    /*static private void BonomQuery(List<Bonom>[] lists, int xOrigin, int zOrigin, int radius = 0, bool literal = false)
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
    */

    public delegate void WorkProcess(Quadrant quad);
    public delegate bool SearchProcess(Quadrant quad);
    public static void QuadRadiusProcess(Quadrant center, int radius, SearchProcess process)
    {
        bool finished = false;

        for (int i = 0; i < radius; i++) // For each Radius
        {
            int outer = (2 * (i - 1)) + 1;
            int inner = outer - 2;
            inner = inner > 0 ? inner : 0;
            inner *= inner;
            outer *= outer;
            int j_max = outer - inner; // Count of just the ring */

            /*int j_max = (2 * (i - 1)) + 1;
            j_max *= j_max; // Area bound by radius */


            for (int j = 0; j < j_max; j++)
            {
                Quadrant searchQuad = GetQuad(center.Xcoord, center.Zcoord, j);
                if (searchQuad == null)
                    continue;

                finished = process(searchQuad);
            }

            if (finished)
                break;
        }
    }
    public static void QuadAreaProcess(Quadrant center, int radius, WorkProcess process)
    {
        for (int i = 0; i < radius; i++) // For each Radius
        {
            /*int outer = (2 * (i - 1)) + 1;
            int inner = outer - 2;
            inner = inner > 0 ? inner : 0;
            inner *= inner;
            outer *= outer;
            int j_max = outer - inner; // Count of just the ring */

            int j_max = (2 * (i - 1)) + 1;
            j_max *= j_max; // Area bound by radius */

            for (int j = 0; j < j_max; j++)
            {
                Quadrant searchQuad = Game.GetQuad(center.Xcoord, center.Zcoord, j);
                if (searchQuad == null)
                    continue;

                process(searchQuad);
            }
        }
    }
    #endregion

    #region Combat
    static public void AttackBonom(Bonom attacker, Bonom target)
    {
        DamageBonom(attacker, target);
        KnockBonom(attacker, target, target.transform.position);
    }

    static private void DamageBonom(Bonom attacker, Bonom target)
    {
        if (!target.Alive)
            return;

        float dmg = attacker.Stats.AttkDamage;
        target.Health -= dmg;
        target.myTeam.DamageRecieved += dmg;
        attacker.myTeam.DamageDealt += dmg;
        attacker.myTeam.KillCount += target.Alive ? 0 : 1;
    }
    static private void KnockBonom(Bonom attacker, Bonom target, Vector3 source)
    {
        if (target.RigidBody == null ||
            !target.Grounded)
            return;

        Vector3 knockBack = Vector3.Normalize(target.transform.position - source) * attacker.Stats.KnockBack;
        Vector3 knockUp = Vector3.up * attacker.Stats.KnockUp;

        target.RigidBody.AddForce(knockBack + knockUp, ForceMode.Impulse);
    }

    #endregion

    #region Generation
    static public BonomStats RandomBonomStats()
    {
        return Instance.BonomPresets[(int)(UnityEngine.Random.value * (Instance.BonomPresets.Length - 1) + .5f)];
    }
    static public void AddBonomToTeam(Team requestingTeam, bool random = false)
    {
        Bonom newBonom = requestingTeam.AddBonom(random);
        GetQuad(newBonom.transform.position).Add(newBonom);
    }
    static public Bonom GenerateNewBonom()
    {
        Bonom newBonom = UnityEngine.Object.Instantiate(Instance.PrefabBonomContainer.gameObject, Instance.transform).GetComponent<Bonom>();
        AllBonoms.Add(newBonom);
        BonomCanvas newCanvas = UnityEngine.Object.Instantiate(UIManager.PrefabBonomCanvasPanel, UIManager.BonomHealthBarContainer).GetComponent<BonomCanvas>();
        newCanvas.Init(newBonom);
        return newBonom;
    }
    static public GameObject GenerateNewFlag(Team team)
    {
        GameObject newFlag = UnityEngine.Object.Instantiate(team.Stats.PrefabCustomFlag == null ? Instance.PrefabDefaultFlag : team.Stats.PrefabCustomFlag, team.Stats.SpawnLocation, false);
        newFlag.GetComponent<Renderer>().material.color = team.Stats.TeamColor;
        return newFlag;
    }
    static public GameObject GenerateNewBase(Team team)
    {
        GameObject newBase = UnityEngine.Object.Instantiate(team.Stats.PrefabCustomBase == null ? Instance.PrefabDefaultBase : team.Stats.PrefabCustomBase, team.Stats.SpawnLocation, false);
        //newFlag.GetComponent<Renderer>().material.color = team.Stats.TeamColor;
        return newBase;
    }
    static public void MoveSelectedFlag(Vector3 newLocation)
    {
        SelectedTeam.Flag.transform.position = newLocation;
    }

    static private Color RandomColorGenerator()
    {
        return new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
    }
    #endregion

    #region WorkLoad Entry-Points
    static private /*async void*/IEnumerator MapWork()
    {
        while (Instance.MapWorkActive)
        {
            if (QuadMap.Length < 1)
            {
                Debug.Log("No MapWork");
                yield return new WaitForFixedUpdate();
                //await Task.Yield();
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
                    Instance.MapWorkActive = false;
                    break;
                }

                if (workingQuadrant.Count > 0)
                    break;
            }

            if (!Instance.MapWorkActive)
                continue;

            good_batches++;

            for (int i = workingQuadrant.Count - 1; i > -1; i--)
                workingQuadrant[i].BatchUpdate();

            DrawWorkingQuadFrame(xWorkCurrent, zWorkCurrent, Color.red);
            DrawWorkingQuadFrame(xWorkPrevious, zWorkPrevious, Color.blue);

            yield return new WaitForEndOfFrame();
        }
    }
    static private IEnumerator BonomWork(int TeamIndex)
    {
        while (Instance.BonomWorkActive)
        {
            if (Instance.GamePause)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

            int targetIndex = 0;

            for (int i = 0; i < Instance.MaxBonomWork && i < AllBonoms.Count; i++)
            {
                targetIndex = (BonomWorkPrevious + i) % (AllBonoms.Count);
                AllBonoms[targetIndex].SingleUpdate();
            }

            BonomWorkPrevious = targetIndex + 1;

            yield return new WaitForEndOfFrame();
        }
    }
    static private IEnumerator TeamWork()
    {
        //int workCount = 0;
        while (Instance.BonomWorkActive)
        {
            if (Instance.GamePause)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

            foreach (Team team in Teams)
            {
                if (team.MemberCount >= Instance.MaxMemberCount)
                    continue;

                if (team.LastSpawn + Instance.SpawnDelayTicks > GameTime)
                    continue;

                team.LastSpawn = GameTime;

                //workCount++;
                //Debug.Log($"WorkCount: {workCount}");
                AddBonomToTeam(team, false);
            }

            yield return new WaitForFixedUpdate();
        }
    }
    static private void AdvanceWorkCoords()
    {
        zWorkCurrent++;                                                         // Single Tick
        xWorkCurrent += zWorkCurrent >= QuadMap.GetLength(1) ? 1 : 0;           // Tick from Z roll-over
        zWorkCurrent = zWorkCurrent >= QuadMap.GetLength(1) ? 0 : zWorkCurrent; // Z roll-over
        xWorkCurrent = xWorkCurrent >= QuadMap.GetLength(0) ? 0 : xWorkCurrent; // X roll-over
    }
    static private void DrawWorkingQuadFrame(int xCoord, int zCoord, Color color)
    {
        o.x = Instance.QuadResolution * (xCoord - xRoot); o.z = Instance.QuadResolution * (zCoord - zRoot);
        x.x = Instance.QuadResolution * (xCoord - xRoot + 1); x.z = Instance.QuadResolution * (zCoord - zRoot);
        z.x = Instance.QuadResolution * (xCoord - xRoot); z.z = Instance.QuadResolution * (zCoord - zRoot + 1);
        O.x = Instance.QuadResolution * (xCoord - xRoot + 1); O.z = Instance.QuadResolution * (zCoord - zRoot + 1);

        o.y = Instance.WorkHighlightHeight;
        x.y = Instance.WorkHighlightHeight;
        z.y = Instance.WorkHighlightHeight;
        O.y = Instance.WorkHighlightHeight;

        Debug.DrawLine(o, x, color);
        Debug.DrawLine(o, z, color);
        Debug.DrawLine(O, x, color);
        Debug.DrawLine(O, z, color);
    }
    #endregion
}
