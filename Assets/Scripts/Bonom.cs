using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Bonom : MonoBehaviour
{
    public Rigidbody myRigidBody;
    public Collider myCollider;
    public MeshRenderer myRenderer;
    public TMP_Text NamePanel;
    public Slider HealthSlider;
    public GameObject Canvas;

    public Engine Engine;
    public Team myTeam;
    public BonomStats Stats;
    
    public DateTime DeathTime;
    public DateTime LastAttack;
    public float Health;
    private bool DeadQued = false;
    public Bonom Target = null;

    public bool Alive => Health > 0;
    public int TeamIndex => myTeam == null ? -1 : myTeam.TeamIndex;
    
    
    public void Init(Engine engine, Team team, BonomStats stats)
    {
        Engine = engine;
        myTeam = team;
        Stats = stats;

        Health = Stats.HealthMax;
        Target = null;

        myRigidBody = gameObject.GetComponent<Rigidbody>();
        myCollider = gameObject.GetComponent<Collider>();
        myRenderer = gameObject.GetComponent<MeshRenderer>();
        myRenderer.material.color = myTeam.TeamColor;
        Canvas = transform.GetChild(0).gameObject;
        HealthSlider = Canvas.transform.GetChild(0).GetComponent<Slider>();
        NamePanel = Canvas.transform.GetChild(1).GetComponent<TMP_Text>();
        NamePanel.text = Stats.Type.ToString();
    }
    private bool TargetAggro(Bonom target)
    {
        return Vector3.Distance(target.transform.position, transform.position) < Stats.AggroRange;
    }
    private bool TargetAttkRange(Bonom target)
    {
        return Vector3.Distance(target.transform.position, transform.position) < Stats.AttkRange;
    }

    #region Sub-Routines
    private void CanvasUpdate()
    {
        Canvas.transform.rotation = Engine.CameraControl.Camera.rotation;
        HealthSlider.value = Health / Stats.HealthMax;
    }
    private void TargetUpdate()
    { 
        if (!Alive)
        {
            Target = null;
            return;
        }

        if (Target != null &&
           (!Target.Alive ||
            !TargetAggro(Target)))
            Target = null;

        if (Target != null)
            Debug.DrawLine(transform.position, Target.transform.position, Color.black);

        if (Target != null && TargetAttkRange(Target))
            return;

        List<Bonom> enemies = Engine.GetEnemies(myTeam.TeamIndex);

        Bonom closest = null;

        foreach(Bonom enemy in enemies)
        {
            if (enemy.Alive &&
                Vector3.Distance(enemy.transform.position, transform.position) < Stats.AggroRange &&
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
        if (myRigidBody == null)
            return;

        myRigidBody.velocity = (!Alive || (Target != null && Vector3.Distance(Target.transform.position, transform.position) < Stats.AttkRange)) ?
            Vector3.zero : Vector3.Normalize(transform.forward) * Stats.MoveSpeed;
    }
    private void AttackUpdate()
    {
        if (!Alive)
            return;

        if (Target == null ||
            LastAttack.Ticks + Stats.AttkDelayTicks > DateTime.Now.Ticks ||
            !Target.Alive ||
            !TargetAttkRange(Target))
            return;

        Engine.AttackBonom(this, Target);
        LastAttack = DateTime.Now;
    }
    private void LifeUpdate()
    {
        float oldHealth = Health;
        Health += Alive ? Stats.HealthRegen : 0;
        Health = Health < 0 ? 0 : Health > Stats.HealthMax ? Stats.HealthMax : Health;
        DeathTime = Alive ? DateTime.Now : DeathTime;
        DeadQued = Alive ? false : DeadQued;
        myCollider.enabled = Alive;

        if (!DeadQued && !Alive)
        {
            DeadQued = true;
            Engine.Dead.Add(this);
        }
        myTeam.DamageHealed += Health - oldHealth;

        if (myRenderer != null)
        {
            long deathLength = DateTime.Now.Ticks - DeathTime.Ticks;
            float lerp = (float)(Engine.BodyExpirationTicks - deathLength) / Engine.BodyExpirationTicks;
            Color newColor = new Color(myTeam.TeamColor.r, myTeam.TeamColor.g, myTeam.TeamColor.b, lerp);
            myRenderer.material.color = newColor;
            Debug.Log($"Color: {newColor}");
        }   
    }
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Engine == null)
            return;

        MoveUpdate();
        LifeUpdate();
        CanvasUpdate();
        TargetUpdate();
        TurnUpdate();
        AttackUpdate();
    }
}
