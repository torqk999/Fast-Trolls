using UnityEngine;

public class Instance : MonoBehaviour
{
    [Header("Settings")]
    public int QuadResolution = 10;
    public int ProxyRadius;
    public int MaxMemberCount;
    
    public float DeathHeight;
    public float SpawnRadius;
    public float AvoidanceMax;
    public float AnimationDistance;
    public float HalfBonomHeight;

    public long RegenDelayTicks;
    public long BodyExpirationTicks;
    public long SpawnDelayTicks;
    public long CanvasDelayTicks;

    public int MaxBonomWork;
    public int GameSpeed;
    public bool GamePause;

    [Header("Presets")]
    public BonomStats[] BonomPresets;
    public TeamStats[] TeamPresets;

    [Header("Prefabs")]
    public Bonom PrefabBonomContainer;
    public GameObject PrefabDefaultBonom;
    public GameObject PrefabDefaultFlag;
    public GameObject PrefabDefaultBase;
    
    [Header("Debugging")]
    public bool Named;
    public bool debug;
    public bool RandomTeamColors;
    public bool MapWorkActive;
    public bool BonomWorkActive;
    public bool MapInit;
    public bool HealthBars;

    public float WorkHighlightHeight;

    void Start()
    {
        Game.InitializeGame(this); // Migrate to gameLobby code...
    }
    void OnApplicationQuit()
    {
        MapWorkActive = false;
        BonomWorkActive = false;
    }

    private void FixedUpdate()
    {
        Game.GameTime += GamePause ? 0 : GameSpeed;
    }
}
