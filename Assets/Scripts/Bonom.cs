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
    public bool Grounded = false;
    public Bonom Target = null;

    public List<Transform> Touching = new List<Transform>();

    public bool Alive => Health > 0;
    public int TeamIndex => myTeam == null ? -1 : myTeam.TeamIndex;
    
    
    public void Init(Engine engine, Team team, BonomStats stats)
    {
        Engine = engine;
        myTeam = team;
        Stats = stats;

        Health = Stats.HealthMax;
        Target = null;
        Touching.Clear();
        DeadQued = false;

        myRigidBody = gameObject.GetComponent<Rigidbody>();
        myRigidBody.velocity = Vector3.zero;
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

    private void OnCollisionStay(Collision collision)
    {
        Grounded = collision.transform.tag == "GROUND";
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.tag == "BONOM")
            Touching.Add(collision.transform);
    }

    private void OnCollisionExit(Collision collision)
    {
        Grounded = !(collision.transform.tag == "GROUND");

        if (collision.transform.tag == "BONOM")
            Touching.Remove(collision.transform);
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

        //List<Bonom> enemies = Engine.GetEnemies(myTeam.TeamIndex);

        Bonom closest = null;

        foreach(Bonom enemy in myTeam.Enemies)
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
        if (!Alive || !Grounded)
            return;

        Vector3 bearingVector = Target == null ? myTeam.Flag.transform.position : Target.transform.position;
        bearingVector -= transform.position;
        bearingVector.y = 0;

        Quaternion facing = transform.rotation;
        Quaternion bearing = Quaternion.LookRotation(bearingVector, Vector3.up);

        transform.rotation = Quaternion.Lerp(facing, bearing, Stats.TurnSpeed);
    }

    private void MoveUpdate()
    {
        if (myRigidBody == null ||
            !Alive || !Grounded ||
            (Target != null && Vector3.Distance(Target.transform.position, transform.position) < Stats.AttkRange))
            return;

        Vector3 solution = (Target == null ? myTeam.Flag.transform.position : Target.transform.position) - transform.position;
        float turnFactor = Vector3.Dot(solution, transform.forward);
        foreach (Transform touch in Touching)
            solution -= touch.position - transform.position;
        solution = (Vector3.Normalize(solution) * Stats.MoveSpeed) - myRigidBody.velocity;
        
        solution = Vector3.Normalize(solution) * Stats.MoveAccel * turnFactor;
        myRigidBody.AddForce(solution);

        //myRigidBody.velocity = (!Alive || (Target != null && Vector3.Distance(Target.transform.position, transform.position) < Stats.AttkRange)) ?
        //    Vector3.zero : Vector3.Normalize(transform.forward) * Stats.MoveSpeed;
    }
    private void AttackUpdate()
    {
        if (!Alive || !Grounded)
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

            foreach (Team enemyTeam in Engine.Teams)
                if (enemyTeam != myTeam)
                    enemyTeam.RemoveEnemy(this);

            myTeam.RemoveBonom(this);
        }

        myTeam.DamageHealed += Health - oldHealth;

        if (myRenderer != null)
        {
            long deathLength = DateTime.Now.Ticks - DeathTime.Ticks;
            float lerp = (float)(Engine.BodyExpirationTicks - deathLength) / Engine.BodyExpirationTicks;
            Color newColor = new Color(myTeam.TeamColor.r, myTeam.TeamColor.g, myTeam.TeamColor.b, lerp);
            myRenderer.material.color = newColor;
            //Debug.Log($"Color: {newColor}");
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
