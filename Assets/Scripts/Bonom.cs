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
    public MeshRenderer myDebugRenderer;
    public MeshRenderer myMainRenderer;
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


    private int aggro_range;
    private int attk_range;
    public int attk_radius;

    private List<Bonom> proxy_query = new List<Bonom>();

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
        DeadQued = false;

        Health = Stats.HealthMax;
        health_max_inverse = 1 / Stats.HealthMax;
        aggro_range = (int)(Stats.AggroRange / Engine.QuadResolution);
        attk_range = (int)(Stats.AttkRange / Engine.QuadResolution);
        attk_radius = (int)(Stats.AttkRadius / Engine.QuadResolution);

        myRigidBody = gameObject.GetComponent<Rigidbody>();
        myRigidBody.velocity = Vector3.zero;
        myCollider = gameObject.GetComponent<Collider>();
        Canvas = transform.GetChild(0).gameObject;
        HealthSlider = Canvas.transform.GetChild(0).GetComponent<Slider>();
        NamePanel = Canvas.transform.GetChild(1).GetComponent<TMP_Text>();
        NamePanel.text = Stats.Type.ToString();

        QuadUpdate();
        MeshInit();
    }
    private void MeshInit()
    {
        if (myMainRenderer != null)
            Destroy(myMainRenderer.gameObject);

        myDebugRenderer = gameObject.GetComponent<MeshRenderer>();
        myDebugRenderer.material.color = myTeam.TeamColor;

        if (Stats.Prefab == null)
            return;

        GameObject newMeshObject = Instantiate(Stats.Prefab, transform.position, transform.rotation, transform);
        newMeshObject.SetActive(true);
        myMainRenderer = newMeshObject.GetComponent<MeshRenderer>();
        myMainRenderer.material.color = myTeam.TeamColor;
    }
    private bool TargetAggroRange(Bonom target)
    {
        buffer_vector0 = target.transform.position;
        buffer_vector1 = transform.position;
        return FastDistance(ref buffer_vector0, ref buffer_vector1) < Math.Pow(Stats.AggroRange, 2);
    }
    private bool TargetAttkRange(Transform target)
    {
        buffer_vector0 = target.position;
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
    private double FastDistance(ref Vector3 v)
    {
        return Math.Pow(v.x, 2) + Math.Pow(v.y, 2) + Math.Pow(v.z, 2);
    }
    private void OnCollisionStay(Collision collision)
    {
        Grounded = collision.transform.tag == "GROUND";
    }
    //private void OnCollisionEnter(Collision collision)
    //{
    //    Grounded = collision.transform.tag == "GROUND";
    //    //if (collision.transform != this && collision.transform.tag == "BONOM")
    //    //    Touching.Add(collision.transform);
    //}
    private void OnCollisionExit(Collision collision)
    {
        Grounded = collision.transform.tag == "GROUND" ? false : Grounded;
    }
    public void BatchUpdate()
    {
        ProxyUpdate();
        TargetUpdate();
        QuadUpdate();
        //AttackUpdate();
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
    private void ProxyUpdate()
    {
        proxy_query.Clear();
        for (int i = 0; i <= Engine.ProxyRadius; i++)
            proxy_query.AddRange(Engine.SearchQuery[i]);
    }
    private void TargetUpdate()
    {
        if (myTarget != null &&
           (!myTarget.Alive ||
            !TargetAggroRange(myTarget)))
            myTarget = null;

        if (myTarget != null)
            Debug.DrawLine(transform.position, myTarget.transform.position, Color.black);

        if (myTarget != null && TargetAttkRange(myTarget.transform))
            return;

        Bonom closest = null;

        for (int i = 0; i <= aggro_range; i++)
            foreach (Bonom bonom in Engine.SearchQuery[i])
            {
                if (bonom.myTeam == myTeam)
                    continue;

                //buffer_vector0 = transform.position;
                //buffer_vector1 = bonom.transform.position;
                //buffer_vector2 = bonom.transform.position;

                if (bonom.Alive &&
                    TargetAggroRange(bonom) &&
                    (closest == null || FastDistanceGreater(closest.transform, bonom.transform, transform)))
                    closest = bonom;
            }

        myTarget = myTarget == null ? closest : closest == null ? myTarget : FastDistanceGreater(closest.transform, myTarget.transform, transform) ? myTarget : closest;
    }
    private void TurnUpdate()
    {
        buffer_vector0 = myTarget == null ? myTeam.Flag.transform.position : myTarget.transform.position;
        buffer_vector0 -= transform.position;
        buffer_vector0.y = 0;

        Quaternion facing = transform.rotation;
        Quaternion bearing = Quaternion.LookRotation(buffer_vector0, Vector3.up);

        transform.rotation = Quaternion.Lerp(facing, bearing, Stats.TurnSpeed);
    }
    private void MoveUpdate()
    {
        if ((myTarget != null && TargetAttkRange(myTarget.transform)) ||
            TargetAttkRange(myTeam.Flag.transform))
            return;

        buffer_vector0 = (myTarget == null ? myTeam.Flag.transform.position : myTarget.transform.position) - transform.position;
        float turnFactor = Vector3.Dot(buffer_vector0, transform.forward);

        //for (int i = 0; i <= 1; i++)
            foreach (Bonom proxy in proxy_query)
            {
                if (proxy == this || proxy == myTarget || !proxy.Alive)
                    continue;

                buffer_vector1 = proxy.transform.position - transform.position;
                buffer_vector0 -= buffer_vector1.normalized * (Engine.AvoidanceScalar / (float)FastDistance(ref buffer_vector1));
            }

        buffer_vector0 = (buffer_vector0.normalized * Stats.MoveSpeed) - myRigidBody.velocity;
        buffer_vector0 = buffer_vector0.normalized * Stats.MoveAccel * turnFactor;

        myRigidBody.AddForce(buffer_vector0);
    }
    private void AttackUpdate()
    {
        if (myTarget == null ||
            LastAttack.Ticks + Stats.AttkDelayTicks > DateTime.Now.Ticks ||
            !myTarget.Alive ||
            !TargetAttkRange(myTarget.transform))
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
        Grounded = Alive ? Grounded : false;
        myCollider.enabled = Alive;

        if (!DeadQued && !Alive)
        {
            DeadQued = true;
            Engine.Dead.Add(this);
            myTeam.RemoveBonom(this);
        }

        myTeam.DamageHealed += Health - oldHealth;
        long deathLength = DateTime.Now.Ticks - DeathTime.Ticks;

        if (myDebugRenderer != null)
        {
            float lerp = (Engine.BodyExpirationTicks - deathLength) * Engine.body_exp_inverse;
            buffer_color = myTeam.TeamColor;
            buffer_color.a = lerp;
            myDebugRenderer.material.color = buffer_color;
        }

        gameObject.SetActive(Engine.BodyExpirationTicks - deathLength > 0);
    }
    #endregion

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        CanvasUpdate();
        LifeUpdate();

        if (!Alive || !Grounded)
            return;

        //QuadUpdate();
        //TargetUpdate();
        MoveUpdate();
        TurnUpdate();
        AttackUpdate();
    }
}
