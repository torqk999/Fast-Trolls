using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIManager : MonoBehaviour
{
    public Transform TeamSelectContainer;
    public Transform RatioPanelContainer;
    public Transform SquadCountsContainer;


    public Button[] TeamSelectors;
    public BonomRatioPanel[] RatioPanels;
    public TMP_Text[] SquadCounts;
    public Engine Engine;

    public TMP_Text SelectedNameSplash;
    public TMP_Text SelectedTeamStats;
    public Button PrefabTeamSelectButton;
    public GameObject PrefabRatioSliderPanel;
    public GameObject PrefabSquadCountText;

    public Sprite Locked;
    public Sprite Unlocked;

    public float ClickRayCastDistance;
    public Vector3 FlagGroundOffset;

    public int StatRefreshDelaySeconds;
    public DateTime LastStatRefresh;

    public const string BlockerTag = "UI_BLOCK";

    public void PopulateTeamSelectionButtons()
    {
        if (Engine.Teams == null || Engine.Teams.Length < 1)
            return;

        TeamSelectors = new Button[Engine.Teams.Length];
        for (int i = TeamSelectContainer.childCount - 1; i > -1; i--)
            Destroy(TeamSelectContainer.GetChild(i).gameObject);

        for (int i = 0; i < TeamSelectors.Length; i++)
        {
            TeamSelectors[i] = Instantiate(PrefabTeamSelectButton, TeamSelectContainer);
            TeamSelectors[i].gameObject.SetActive(true);
            TeamSelectors[i].GetComponent<Image>().color = Engine.Teams[i].TeamColor;
            TeamSelectors[i].transform.GetChild(0).GetComponent<TMP_Text>().text = Engine.Teams[i].TeamName;
            TeamSelectors[i].tag = BlockerTag;
            int tempInt = i;
            TeamSelectors[i].onClick.AddListener(() => TeamSelection(tempInt));
        }
    }

    public void PopulateRatioSliderPanels()
    {
        Debug.Log($"Presets Length: {Engine.PreSets.Length}");

        RatioPanels = new BonomRatioPanel[Engine.PreSets.Length];

        for (int i = 0; i < RatioPanels.Length; i++)
        {
            Debug.Log("Making Panel...");
            RatioPanels[i] = Instantiate(PrefabRatioSliderPanel, RatioPanelContainer).GetComponent<BonomRatioPanel>();
            RatioPanels[i].gameObject.SetActive(true);
            RatioPanels[i].gameObject.tag = BlockerTag;
            RatioPanels[i].Init(this, Engine.PreSets[i], Engine.SelectedTeam.Squads[i]);
        }
    }

    public void PopulateSquadCounterTexts()
    {
        SquadCounts = new TMP_Text[Engine.PreSets.Length];

        for (int i = 0; i < SquadCounts.Length; i++)
        {
            SquadCounts[i] = Instantiate(PrefabSquadCountText, SquadCountsContainer).GetComponent<TMP_Text>();
            SquadCounts[i].gameObject.SetActive(true);
        }
    }

    private void SyncCounts()
    {
        for (int i = 0; i < SquadCounts.Length; i++)
            SyncCount(i);
    }

    private void SyncCount(int index)
    {
        Squad targetSquad = Engine.SelectedTeam.Squads[index];
        SquadCounts[index].text = $"{Engine.PreSets[index].Type}:{targetSquad.Count}";
    }

    private void SyncSliders()
    {
        for (int i = 0; i < RatioPanels.Length; i++)
            RatioPanels[i].Sync(Engine.SelectedTeam.Squads[i]);
    }

    private void SyncSlider(int type)
    {
        int index = Engine.SelectedTeam.SquadIndex(type);
        RatioPanels[index].Sync(Engine.SelectedTeam.Squads[index]);
    }

    public void TeamSelection(int teamIndex)
    {
        Engine.SelectedTeamIndex = teamIndex;
        SelectedNameSplash.text = Engine.Teams[Engine.SelectedTeamIndex].TeamName;
        SyncSliders();
        SyncCounts();
    }

    public void CountUpdate(Team alteredTeam)
    {
        if (Engine.SelectedTeam != alteredTeam)
            return;

        SyncCounts();
    }
    public void RatioSliderLockToggle(BonomRatioPanel alteredPanel)
    {
        Squad targetSquad = Engine.SelectedTeam[alteredPanel.TypeIndex];
        targetSquad.ToggleLock();
        SyncSlider(alteredPanel.TypeIndex);
    }
    public void RatioSliderUpdate(BonomRatioPanel alteredPanel)
    {
        List<Squad> unlocked = new List<Squad>();
        float reserved = 0;
        float unlockedSum = 0;
        float adjustedDelta = alteredPanel.Delta * .1f;
        Squad targetSquad = null;

        foreach (Squad squad in Engine.SelectedTeam.Squads)
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

        SyncSliders();
    }
 
    private void StatUpdate()
    {
        if (LastStatRefresh + new TimeSpan(0,0,StatRefreshDelaySeconds) > DateTime.Now)
            return;

        LastStatRefresh = DateTime.Now;
        SelectedTeamStats.text = $"DmgDealt: {Engine.SelectedTeam.DamageDealt}  DmgRecieved: {Engine.SelectedTeam.DamageRecieved} \nDmgHealed: {Engine.SelectedTeam.DamageHealed}  KillCount: {Engine.SelectedTeam.KillCount}";
    }

    private void NumberCheck()
    {
        for (int i = 0; i < TeamSelectors.Length; i++)
            if (Input.GetKeyDown((KeyCode)(i + 48)))
            {
                TeamSelection(i);
                return;
            }
    }

    private void ClickCheck()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
            pointerEventData.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> pointerResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, pointerResults);

            foreach(RaycastResult pointerResult in pointerResults)
                if (pointerResult.gameObject.tag == BlockerTag)
                    return;
                
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit result;
            if (!Physics.Raycast(ray, out result, ClickRayCastDistance))
                return;

            Vector3 newFlagLocation = result.point;
            newFlagLocation.y = 0;
            newFlagLocation += FlagGroundOffset;
            Engine.MoveSelectedFlag(newFlagLocation);
        }
    }

    void Start()
    {

    }

    private void Update()
    {
        ClickCheck();
        NumberCheck();
        StatUpdate();
    }

    
}
