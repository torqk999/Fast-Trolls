using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public class Networking : NetworkBehaviour
{
    public int LobbyHeartBeatDelaySeconds;
    public const string JoinCodeKey = "JoinCode";
    public string HostedRoomName;

    static public int MaxPlayers;
    static public string JoinCode;
    static UnityTransport Transport;
    static List<Lobby> QueryBuffer;
    static Lobby ConnectedLobby;
    static Allocation HostedAllocation;
    static JoinAllocation JoinedAllocation;
    static Dictionary<string, DataObject> LobbySettings;

    private async void Awake()
    {
        Transport = FindObjectOfType<UnityTransport>();
        QueryBuffer = new List<Lobby>();
        LobbySettings = new Dictionary<string, DataObject>();
        await AuthenticateAnonymously();
    }

    private static async Task AuthenticateAnonymously()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async void CreateLobby()
    {
        await CreateAllocation();

        LobbySettings.Remove(JoinCodeKey);
        LobbySettings.Add(JoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, JoinCode));

        var options = new CreateLobbyOptions { Data = LobbySettings };
        ConnectedLobby = await Lobbies.Instance.CreateLobbyAsync(HostedRoomName, MaxPlayers, options);
        StartCoroutine(LobbyHeartBeat(ConnectedLobby.Id, LobbyHeartBeatDelaySeconds));

        SetupHost();
    }

    public async void RunQuery(string continuationToken = null)
    {
        var options = new QueryLobbiesOptions { ContinuationToken = continuationToken };
        var response = await Lobbies.Instance.QueryLobbiesAsync(options);
        QueryBuffer.AddRange(response.Results);
    }

    public async void QuickJoinLobby()
    {
        ConnectedLobby = await Lobbies.Instance.QuickJoinLobbyAsync();
        JoinedAllocation = await RelayService.Instance.JoinAllocationAsync(ConnectedLobby.Data[JoinCodeKey].Value);
        SetupClient();
    }

    private static IEnumerator LobbyHeartBeat(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (ConnectedLobby != null && HostedAllocation != null)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    public async void CreateQuickMatch()
    {
        await CreateAllocation();
        SetupHost();
    }

    public static async Task CreateAllocation()
    {
        HostedAllocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers);
        JoinCode = await RelayService.Instance.GetJoinCodeAsync(HostedAllocation.AllocationId);
    }

    private static void SetupHost()
    {
        Transport.SetHostRelayData(
            HostedAllocation.RelayServer.IpV4,
            (ushort)HostedAllocation.RelayServer.Port,
            HostedAllocation.AllocationIdBytes,
            HostedAllocation.Key,
            HostedAllocation.ConnectionData);

        NetworkManager.Singleton.StartHost();
    }

    public static void SetupClient()
    {
        Transport.SetClientRelayData(
            JoinedAllocation.RelayServer.IpV4,
            (ushort)JoinedAllocation.RelayServer.Port,
            JoinedAllocation.AllocationIdBytes, JoinedAllocation.Key,
            JoinedAllocation.ConnectionData,
            JoinedAllocation.HostConnectionData);

        NetworkManager.Singleton.StartClient();
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
