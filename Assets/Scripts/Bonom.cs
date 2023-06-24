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
    public Quadrant myQuad;
    public Bonom myTarget;
    public BonomStats Stats;
    
    public DateTime DeathTime;
    public DateTime LastAttack;
    public float Health;
    
    private bool DeadQued = false;
    public bool Grounded = false;
    
    public List<Transform> Touching = new List<Transform>();

    private int aggro_radius;
    private int attk_radius;
    private float health_max_inverse;
    private Vector3 buffer_vector0;
    private Vector3 buffer_vector1;
    private Vector3 buffer_vector2;
    private Color buffer_color;

    public bool Alive => Health > 0;
    public int TeamIndex => myTeam == null ? -1 : myTeam.TeamIndex;
    
    public void Init(Engine engine, Team team, BonomStats stats)
    {
        Engine = engine;
        Stats = stats;
        myTeam = team;
        myTarget = null;
        //myQuad = myQuad == null ? Engine.GetQuad(transform.position) : myQuad;
        Health = Stats.HealthMax;
        health_max_inverse = 1 / Stats.HealthMax;
        
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
    private bool TargetAggroRange(Bonom target)
    {
        buffer_vector0 = target.transform.position;
        buffer_vector1 = transform.position;
        return FastDistance(ref buffer_vector0, ref buffer_vector1) < Math.Pow(Stats.AggroRange, 2);
    }
    private bool TargetAttkRange(Bonom target)
    {
        buffer_vector0 = target.transform.position;
        buffer_vector1 = transform.position;
        return FastDistance(ref buffer_vector0, ref buffer_vector1) < Math.Pow(Stats.AttkRange, 2);
    }
    private bool FastDistanceGreater(Transform a, Transform b, Transform source)
    {
        buffer_vector0 = source.position;
        buffer_vector1 = a.position;
        buffer_vector2 = b.position;
        return FastDistanceGreater(ref buffer_vector1, ref buffer_vector2, ref buffer_vector0);
    }
    private bool FastDistanceGreater(ref Vector3 a, ref Vector3 b, ref Vector3 source)
    {
        return FastDistance(ref a, ref source) > FastDistance(ref b, ref source);
    }
    //private bool FastDistanceGreater(Vector3 a, Vector3 b, float c)
    //{
    //    return FastDistance(ref a, ref b) > Math.Pow(c, 2);
    //}
    private double FastDistance(ref Vector3 a, ref Vector3 b)
    {
        return Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2) + Math.Pow(a.z - b.z, 2);
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
    private void QuadUpdate()
    {
        Quadrant check = Engine.GetQuad(transform.position);
        if (check != myQuad)
        {
            if (myQuad != null)
                myQuad.RemoveBonom(this);
            check.AddBonom(this);
        }
    }
    private void CanvasUpdate()
    {
        Canvas.transform.rotation = Engine.CameraControl.Camera.rotation;
        HealthSlider.value = Health * health_max_inverse;// Stats.HealthMax;
    }
    private void TargetUpdate()
    { 
        if (!Alive)
        {
            myTarget = null;
            return;
        }

        if (myTarget != null &&
           (!myTarget.Alive ||
            !TargetAggroRange(myTarget)))
            myTarget = null;

        if (myTarget != null)
            Debug.DrawLine(transform.position, myTarget.transform.position, Color.black);

        if (myTarget != null && TargetAttkRange(myTarget))
            return;

        Bonom closest = null;

        foreach(Bonom enemy in myTeam.Enemies)
        {
            buffer_vector0 = transform.position;
            buffer_vector1 = enemy.transform.position;
            buffer_vector2 = enemy.transform.position;

            if (enemy.Alive &&
                TargetAggroRange(enemy) &&
                (closest == null || FastDistanceGreater(closest.transform, enemy.transform, transform)/*FastDistance(closest.transform.position, transform.position) > FastDistance(enemy.transform.position, transform.position)*/))
                closest = enemy;
        }

        myTarget = myTarget == null || FastDistanceGreater(closest.transform, myTarget.transform, transform)/*FastDistance(Target.transform.position, transform.position) > FastDistance(closest.transform.position, transform.position)*/ ? closest : myTarget;
    }
    private void TurnUpdate()
    {
        if (!Alive || !Grounded)
            return;

        buffer_vector0 = myTarget == null ? myTeam.Flag.transform.position : myTarget.transform.position;
        buffer_vector0 -= transform.position;
        buffer_vector0.y = 0;

        Quaternion facing = transform.rotation;
        Quaternion bearing = Quaternion.LookRotation(buffer_vector0, Vector3.up);

        transform.rotation = Quaternion.Lerp(facing, bearing, Stats.TurnSpeed);
    }
    private void MoveUpdate()
    {
        if (myRigidBody == null ||
            !Alive || !Grounded ||
            (myTarget != null && TargetAttkRange(myTarget)/*FastDistance(Target.transform.position, transform.position) < Math.Pow(Stats.AttkRange,2)*/))
            return;

        buffer_vector0 = (myTarget == null ? myTeam.Flag.transform.position : myTarget.transform.position) - transform.position;
        float turnFactor = Vector3.Dot(buffer_vector0, transform.forward);
        foreach (Transform touch in Touching)
            buffer_vector0 -= touch.position - transform.position;

        buffer_vector0 = (buffer_vector0.normalized * Stats.MoveSpeed) - myRigidBody.velocity;
        buffer_vector0 = buffer_vector0.normalized * Stats.MoveAccel * turnFactor;

        //buffer_vector0 = (Vector3.Normalize(buffer_vector0) * Stats.MoveSpeed) - myRigidBody.velocity;
        //buffer_vector0 = Vector3.Normalize(buffer_vector0) * Stats.MoveAccel * turnFactor;

        myRigidBody.AddForce(buffer_vector0);
    }
    private void AttackUpdate()
    {
        if (!Alive || !Grounded)
            return;

        if (myTarget == null ||
            LastAttack.Ticks + Stats.AttkDelayTicks > DateTime.Now.Ticks ||
            !myTarget.Alive ||
            !TargetAttkRange(myTarget))
            return;

        Engine.AttackBonom(this, myTarget);
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
            float lerp = (Engine.BodyExpirationTicks - deathLength) * Engine.body_exp_inverse;/// Engine.BodyExpirationTicks;
            buffer_color = myTeam.TeamColor;
            buffer_color.a = lerp;//= new Color(myTeam.TeamColor.r, myTeam.TeamColor.g, myTeam.TeamColor.b, lerp);
            myRenderer.material.color = buffer_color;
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

        CanvasUpdate();
        QuadUpdate();
        LifeUpdate();
        TargetUpdate();

        MoveUpdate();
        TurnUpdate();
        AttackUpdate();
    }
}
