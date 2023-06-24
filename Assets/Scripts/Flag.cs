using UnityEngine;

public class Flag : MonoBehaviour
{
    public Engine Engine;
    public Team myTeam;

    public void Init(Engine engine, Team team)
    {
        Engine = engine;
        myTeam = team;
        gameObject.GetComponent<MeshRenderer>().material.color = team.TeamColor;
    }
}
