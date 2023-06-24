using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Bonom : MonoBehaviour
{
    public Rigidbody myRigidBody;
    public MeshRenderer myRenderer;
    public TMP_Text NamePanel;
    public Slider HealthSlider;

    public Engine Engine;
    public Team myTeam;
    public BonomStats Stats;

    public DateTime DeathTime;
    public float Health;
    public bool Alive => Health > 0;
    private bool DeadQued = false;
    public int TeamIndex => myTeam == null ? -1 : myTeam.TeamIndex;
    public int AttackTimer = 0;
    public Bonom Target = null;
    
    public void Init(Engine engine, Team team, BonomStats stats)
    {
        Engine = engine;
        myTeam = team;
        Stats = stats;

        Health = Stats.HealthMax;
        Target = null;
        AttackTimer = 0;

        myRigidBody = gameObject.GetComponent<Rigidbody>();
        
        HealthSlider = transform.GetChild(0).GetChild(0).GetComponent<Slider>();
        NamePanel = transform.GetChild(0).GetChild(1).GetComponent<TMP_Text>();
        NamePanel.text = Stats.Type.ToString();

        myRenderer = gameObject.GetComponent<MeshRenderer>();
        myRenderer.material.color = myTeam.TeamColor;
    }

    private bool TargetAggro(Bonom target)
    {
        return Vector3.Distance(target.transform.position, transform.position) < Stats.AggroRange;
    }

    private bool TargetAttkRange(Bonom target)
    {
        return Vector3.Distance(target.transform.position, transform.position) < Stats.AttkRange;
    }

    private void CanvasUpdate()
    {
        HealthSlider.value = Health / Stats.HealthMax;
    }

    private void TargetUpdate()
    { 
        if (Target != null &&
            (!Target.Alive ||
            !TargetAggro(Target)))
            Target = null;

        if (Target != null && TargetAttkRange(Target))
            return;

        List<Bonom> enemies = Engine.GetEnemies(myTeam.TeamIndex);

        Bonom closest = null;

        foreach(Bonom enemy in enemies)
        {
            if (Vector3.Distance(enemy.transform.position, transform.position) < Stats.AggroRange &&
                (closest == null || Vector3.Distance(closest.transform.position, transform.position) > Vector3.Distance(enemy.transform.position, transform.position)))
                closest = enemy;
        }

        Target = Target == null || Vector3.Distance(Target.transform.position, transform.position) > Vector3.Distance(closest.transform.position, transform.position) ? closest : Target;
    }

    private void TurnUpdate()
    {
        if (!Alive)
            return;

        Vector3 target = Target == null ? myTeam.Flag.transform.position : Target.transform.position;

        Quaternion facing = transform.rotation;
        Quaternion bearing = Quaternion.LookRotation(target - transform.position, Vector3.up);

        transform.rotation = Quaternion.Lerp(facing, bearing, Stats.TurnSpeed);
    }

    private void MoveUpdate()
    {
        if (myRigidBody == null ||
            !Alive ||
            (Target != null && Vector3.Distance(Target.transform.position, transform.position) < Stats.AttkRange))
            return;

        myRigidBody.velocity = Vector3.Normalize(transform.forward) * Stats.MoveSpeed;
    }

    private void AttackUpdate()
    {
        AttackTimer--;
        AttackTimer = AttackTimer <= 0 ? 0 : AttackTimer;

        if (Target == null ||
            AttackTimer > 0 ||
            !Target.Alive ||
            !TargetAttkRange(Target))
            return;

        Engine.AttackBonom(this, Target);
        AttackTimer = Stats.AttkSpeed;
    }

    private void LifeUpdate()
    {
        float oldHealth = Health;
        Health += Alive ? Stats.HealthRegen : 0;
        Health = Health < 0 ? 0 : Health > Stats.HealthMax ? Stats.HealthMax : Health;
        DeathTime = Alive ? DateTime.Now : DeathTime;
        DeadQued = Alive ? false : DeadQued;

        if (!DeadQued && !Alive)
        {
            DeadQued = true;
            Engine.Dead.Add(this);
        }
        myTeam.DamageHealed += Health - oldHealth;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        MoveUpdate();
        LifeUpdate();
        CanvasUpdate();

        if (Engine == null ||
            !Alive)
            return;

        TargetUpdate();
        TurnUpdate();
        AttackUpdate();
    }
}
