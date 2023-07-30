using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIManager : MonoBehaviour
{
    public enum UIState
    {
        MAIN,
        MATCH,
        GAME,
        LOGIN_USER
    }

    public Button[] TeamSelectors;
    public Dictionary<string, BonomRatioPanel> RatioPanelsBin;
    public Dictionary<string, TMP_Text> SquadCountsBin;

    public TMP_Text GameTimeText;
    public TMP_Text SelectedNameSplash;
    public TMP_Text SelectedTeamStats;

    #region Panels
    public PanelWrangler MainMenu;
    public PanelWrangler MatchMaking;
    public PanelWrangler GameHUD;
    PanelWrangler[] GameMenus;
    public PanelWrangler HUD_FullPanel;
    public Transform Debugging;
    #endregion

    #region Containers
    public Transform LobbyJoinContainer;
    public Transform TeamSelectContainer;
    public Transform RatioPanelContainer;
    public Transform SquadCountsContainer;
    public Transform BonomHealthBarContainer;
    #endregion

    #region Pre-fabs

    public Button PrefabTeamSelectButton;
    public GameObject PrefabRatioSliderPanel;
    public GameObject PrefabSquadCountText;
    public GameObject PrefabBonomCanvasPanel;
    public Button PrefabLobbyJoin;

    public Sprite Locked;
    public Sprite Unlocked;
    #endregion

    public UIState CurrentUIState;
    public float ClickRayCastDistance;
    public Vector3 FlagGroundOffset;
    public int StatRefreshDelaySeconds;
    public DateTime LastStatRefresh;
    public const string BlockerTag = "UI_BLOCK";

    #region Initialization
    public void UIinit()
    {
        PopulateRatioSliderPanels();
        PopulateSquadCounterTexts();

        PopulateTeamSelectionButtons();
        SyncUItoCurrentlySelectedTeam();
    }

    private void PopulateTeamSelectionButtons()
    {
        if (Game.Teams == null || Game.Teams.Length < 1)
            return;

        TeamSelectors = new Button[Game.Teams.Length];
        for (int i = TeamSelectContainer.childCount - 1; i > -1; i--)
            Destroy(TeamSelectContainer.GetChild(i).gameObject);

        for (int i = 0; i < TeamSelectors.Length; i++)
        {
            TeamSelectors[i] = Instantiate(PrefabTeamSelectButton, TeamSelectContainer);
            TeamSelectors[i].gameObject.SetActive(true);
            TeamSelectors[i].GetComponent<Image>().color = Game.Teams[i].Stats.TeamColor;
            TeamSelectors[i].transform.GetChild(0).GetComponent<TMP_Text>().text = Game.Teams[i].Stats.TeamName;
            TeamSelectors[i].tag = BlockerTag;
            int tempInt = i;
            TeamSelectors[i].onClick.AddListener(() => TeamSelection(tempInt));
        }

        Debug.Log("Team Selection buttons made!");
    }
    private void PopulateRatioSliderPanels()

    {
        Debug.Log($"Presets Length: {Game.Instance.BonomPresets.Length}");

        RatioPanelsBin = new Dictionary<string, BonomRatioPanel>();

        for (int i = 0; i < Game.Instance.BonomPresets.Length; i++)
        {
            Debug.Log("Making Panel...");
            BonomRatioPanel newPanel = Instantiate(PrefabRatioSliderPanel, RatioPanelContainer).GetComponent<BonomRatioPanel>();
            RatioPanelsBin.Add(Game.Instance.BonomPresets[i].Type, newPanel);
            newPanel.gameObject.SetActive(true);
            newPanel.gameObject.tag = BlockerTag;
            newPanel.Init(Game.Instance.BonomPresets[i]);
        }
    }
    private void PopulateSquadCounterTexts()
    {
        SquadCountsBin = new Dictionary<string, TMP_Text>();//TMP_Text[Engine.PreSets.Length];

        for (int i = 0; i < Game.Instance.BonomPresets.Length; i++)
        {
            TMP_Text newText = Instantiate(PrefabSquadCountText, SquadCountsContainer).GetComponent<TMP_Text>();
            newText.gameObject.SetActive(true);
            SquadCountsBin.Add(Game.Instance.BonomPresets[i].Type, newText);
        }
    }
    #endregion

    #region Synchronization
    private void SyncUItoCurrentlySelectedTeam()
    {
        SelectedNameSplash.text = Game.SelectedTeam.Stats.TeamName;
        SyncAllBonomSliders();
        SyncAllBonomCounts();
    }
    private void SyncAllBonomCounts()
    {
        foreach (string typeKey in SquadCountsBin.Keys)
            SyncBonomTypeCount(typeKey);
    }
    private void SyncBonomTypeCount(string type)
    {
        Squad targetSquad = Game.SelectedTeam.Squads[type];
        SquadCountsBin[type].text = $"{type}:{targetSquad.Count}";
    }
    private void SyncAllBonomSliders()
    {
        foreach (string typeKey in RatioPanelsBin.Keys)
            SyncBonomTypeSlider(typeKey);
    }
    private void SyncBonomTypeSlider(string type)
    {
        //int index = ;
        RatioPanelsBin[type].Sync(Game.SelectedTeam.Squads[type]);
    }
    #endregion

    #region External Updates
    public void ChangeUIState(int i = 0)
    {
        UIState oldState = CurrentUIState;

        CurrentUIState = (UIState)i;

        if (CurrentUIState != UIState.GAME &&
            oldState == UIState.GAME)
            Game.CameraControl.GoHome();

        if (CurrentUIState == UIState.GAME &&
            oldState != UIState.GAME)
            Game.CameraControl.GoBack();

        for (int j = 0; j < GameMenus.Length; j++)
        {
            GameMenus[j].SetState((int)CurrentUIState == j);
        }
            
    }
    public void CountUpdate(Team alteredTeam)
    {
        if (Game.SelectedTeam != alteredTeam)
            return;

        SyncAllBonomCounts();
    }
    public void RatioSliderLockToggle(BonomRatioPanel alteredPanel)
    {
        Squad targetSquad = Game.SelectedTeam.Squads[alteredPanel.Type];
        targetSquad.ToggleLock();
        SyncBonomTypeSlider(alteredPanel.Type);
    }
    public void RatioSliderUpdate(BonomRatioPanel alteredPanel)
    {
        List<Squad> unlocked = new List<Squad>();
        float reserved = 0;
        float unlockedSum = 0;
        float adjustedDelta = alteredPanel.Delta * .1f;
        Squad targetSquad = null;

        foreach (Squad squad in Game.SelectedTeam.Squads.Values)
        {
            if (squad == alteredPanel.Squad)
            {
                targetSquad = squad;

                if (targetSquad.Locked)
                {
                    alteredPanel.RollBack();
                    return;
                }

                continue;
            }

            if (squad.Locked)
                reserved += squad.Ratio;

            else
                unlocked.Add(squad);
        }

        if (targetSquad == null)
            return;

        int preSign = Math.Sign(adjustedDelta);
        adjustedDelta = alteredPanel.Slider.value > 1 - reserved ? (1 - reserved) - alteredPanel.Slider.value : adjustedDelta;
        int postSign = Math.Sign(adjustedDelta);
        adjustedDelta = preSign == postSign ? adjustedDelta : 0;

        foreach (Squad squad in unlocked)
        {
            squad.Ratio -= adjustedDelta / unlocked.Count;
            squad.Ratio = squad.Ratio < 0 ? 0 : squad.Ratio;
            unlockedSum += squad.Ratio;
        }

        targetSquad.Ratio = 1 - (reserved + unlockedSum);

        SyncAllBonomSliders();
    }
    #endregion

    #region Internal Updates
    private void TeamSelection(int teamIndex)
    {
        Game.SelectedTeam = Game.Teams[teamIndex];
        SyncUItoCurrentlySelectedTeam();
    }
    private void GameTimeUpdate()
    {
        if (GameTimeText == null)
            return;

        GameTimeText.text = $"GameTime:\n{Game.GameTime}";
    }
    private void SelectedTeamStatsRefresh()
    {
        if (LastStatRefresh + new TimeSpan(0,0,StatRefreshDelaySeconds) > DateTime.Now)
            return;

        LastStatRefresh = DateTime.Now;
        SelectedTeamStats.text = Game.SelectedTeam == null ? "No Team Selected..." : $"DmgDealt: {Game.SelectedTeam.DamageDealt}  DmgRecieved: {Game.SelectedTeam.DamageRecieved} \nDmgHealed: {Game.SelectedTeam.DamageHealed}  KillCount: {Game.SelectedTeam.KillCount}";
    }
    private void NumButtonPressCheck()
    {
        for (int i = 0; i < TeamSelectors.Length; i++)
            if (Input.GetKeyDown((KeyCode)(i + 48)))
            {
                TeamSelection(i);
                return;
            }
    }
    private void MouseClickCheck()
    {
        List<RaycastResult> pointerResults = new List<RaycastResult>();
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        EventSystem.current.RaycastAll(pointerEventData, pointerResults);

        foreach (RaycastResult pointerResult in pointerResults)
            if (pointerResult.gameObject.tag == BlockerTag)
                return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit result;
            if (!Physics.Raycast(ray, out result, ClickRayCastDistance))
                return;

            Bonom selected = result.transform.GetComponent<Bonom>();
            if (selected == null)
                return;

            Game.CameraControl.SocketCamera(selected.transform);
        }

        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit result;
            if (!Physics.Raycast(ray, out result, ClickRayCastDistance))
                return;

            Vector3 newFlagLocation = result.point;
            //newFlagLocation.y = 0;
            newFlagLocation += FlagGroundOffset;
            Game.MoveSelectedFlag(newFlagLocation);
        }
    }
    private void HotKeyCheck()
    {
        if (Input.GetButtonDown("MainMenu"))
            ChangeUIState();

        //if (Input.GetButtonDown("Quit"))
        //    QuitGame();
    }
    #endregion

    void Start()
    {
        GameMenus = new PanelWrangler[] { MainMenu, MatchMaking, GameHUD };
    }

    private void Update()
    {
        if (Game.Instance == null)
            return;

        BonomHealthBarContainer.gameObject.SetActive(Game.Instance.HealthBars);

        GameTimeUpdate();
        MouseClickCheck();
        HotKeyCheck();
        NumButtonPressCheck();
        SelectedTeamStatsRefresh();
    }
}
