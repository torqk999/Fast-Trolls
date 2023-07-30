using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.Rendering;
using Unity.Netcode;

public partial class Bonom : MonoBehaviour
{
    public Rigidbody RigidBody;
    public Collider Collider;
    public MeshRenderer myDebugRenderer;
    public Renderer myMainRenderer;
    public Animator myAnimator;
    public BonomCanvas myCanvas;

    public Team myTeam => mySquad == null ? null : mySquad.parentTeam;
    public Squad mySquad;
    public Quadrant myQuad;
    public Bonom myTarget;
    private Bonom Closest;
    public BonomStats Stats;

    
    public long DeathTime;
    public long LastAttack;
    public float Health;

    public bool Recycled;
    public bool Grounded;
    public bool IN_RANGE;

    public Vector3 MOVE_VECTOR;

    private int aggro_range;
    private int attk_range;
    public int attk_radius;

    //private List<Bonom> proxy_query = new List<Bonom>();

    private float health_max_inverse;

    private static double Avoidance;
    private static Vector2 v2_buff_0;
    private static Vector2 v2_buff_1;
    private static Vector2 v2_buff_2;
    private static Vector3 v3_buff_0;
    private static Color buffer_color;

    public float HealthPercent => Health * health_max_inverse;
    public bool Alive => Health > 0;
    public bool InPlay => transform.position.y > Game.Instance.DeathHeight;

    public void Init(Squad squad, BonomStats stats)
    {
        Stats = stats;
        mySquad = squad;

        health_max_inverse = 1 / Stats.HealthMax;
        aggro_range = (int)(Stats.AggroRange / Game.QuadResolution) + 1;
        attk_range = (int)(Stats.AttkRange / Game.QuadResolution) + 1;
        attk_radius = (int)(Stats.AttkRadius / Game.QuadResolution) + 1;

        RigidBody = gameObject.GetComponent<Rigidbody>();
        Collider = gameObject.GetComponent<Collider>();

        QuadUpdate();
        MeshInit();
    }
    public void Refresh()
    {
        myTarget = null;
        Recycled = false;
        Health = Stats.HealthMax;
        RigidBody.velocity = Vector3.zero;
        transform.position = myTeam == null ? transform.position : myTeam.SpawnLocation();
        Collider.enabled = true;
        gameObject.GetComponentInChildren<ParticleSystem>().Play();
    }

    private void MeshInit()
    {
        if (myMainRenderer != null)
            Destroy(myMainRenderer.gameObject);

        myDebugRenderer = gameObject.GetComponent<MeshRenderer>();
        myDebugRenderer.enabled = Game.Instance.debug;
        myDebugRenderer.material.color = myTeam.Stats.TeamColor;

        GameObject newMeshObject = Instantiate(Stats.Prefab == null ? Game.Instance.PrefabDefaultBonom : Stats.Prefab, transform.position - new Vector3(0, .9f, 0), transform.rotation, transform);
        newMeshObject.SetActive(true);
        myAnimator = newMeshObject.GetComponent<Animator>();
        myMainRenderer = myAnimator.transform.GetChild(0).GetComponent<Renderer>();
        myMainRenderer.material.color = myTeam.Stats.TeamColor;

    }
    private void WalkAnimation()
    {
        if (myAnimator == null)
            return;

        if (RigidBody.velocity.magnitude > 1 && Fast.FastDistance(transform, Game.CameraControl.Camera) < Game.anim_distance_squared)
        {
            myAnimator.SetBool("isWalking", true);
        }

        else
        {
            myAnimator.SetBool("isWalking", false);
        }
    }

    private bool TargetAggroRange(Bonom target)
    {
        return Fast.FastDistance(transform, target.transform) < Math.Pow(Stats.AggroRange, 2);
    }
    private bool TargetAttkRange(Transform target)
    {
        return Fast.FastDistance(transform, target) < Math.Pow(Stats.AttkRange, 2);
    }

    //private void OnCollisionStay(Collision collision)
    //{
    //    Grounded = collision.transform.tag == "GROUND" ? true : Grounded;
    //}
    private void OnCollisionEnter(Collision collision)
    {
        Grounded = collision.transform.tag == "GROUND" ? true : Grounded;
    }
    private void OnCollisionExit(Collision collision)
    {
        Grounded = collision.transform.tag == "GROUND" ? false : Grounded;
    }

    public void BatchUpdate()
    {
        TargetUpdate();
        QuadUpdate();
    }
    public void NetworkUpdate()
    {

    }
    public void SingleUpdate()
    {
        LifeUpdate();

        if (!Grounded)
            return;

        WalkAnimation();
        IN_RANGE = MoveUpdate();
        TurnUpdate();
        AttackUpdate();
    }

    #region Sub-Routines
    private void QuadUpdate()
    {
        if (!Alive)
        {
            if (myQuad != null)
                myQuad.RemoveBonom(this);
            return;
        }

        Quadrant check = Game.GetQuad(transform.position);
        if (check != myQuad)
        {
            if (myQuad != null)
                myQuad.RemoveBonom(this);
            check.AddBonom(this);
        }
    }
    private void TargetUpdate()
    {
        if (!Alive ||
            myQuad == null)
        {
            myTarget = null;
            return;
        }

        if (myTarget != null &&
           (!myTarget.Alive ||
            !TargetAggroRange(myTarget)))
            myTarget = null;

        

        if (myTarget != null && TargetAttkRange(myTarget.transform))
            return;

        Game.QuadRadiusProcess(myQuad, aggro_range, FindNewTarget);

        myTarget = myTarget == null ? Closest : Closest == null ? myTarget : Fast.FastDistanceGreater(Closest.transform, myTarget.transform, transform) ? myTarget : Closest;
    }
    private void TurnUpdate()
    {
        v3_buff_0 = myTarget == null ? myTeam.Flag.transform.position : myTarget.transform.position;
        Fast.Write3to2(ref v3_buff_0, ref v2_buff_0);
        Fast.WritePosToBuffer(transform, ref v2_buff_1);
        //v2_buff_1 = transform.position;
        v2_buff_0 -= v2_buff_1;
        //v2_buff_0.y = 0;

        Quaternion facing = transform.rotation;
        Fast.Write2to3(ref v2_buff_0, ref v3_buff_0);
        Quaternion bearing = Quaternion.LookRotation(v3_buff_0, Vector3.up);

        transform.rotation = Quaternion.Lerp(facing, bearing, Stats.TurnSpeed);
    }
    private bool MoveUpdate()
    {
        if (myQuad == null)
        {
            MOVE_VECTOR = Vector3.zero;
            return false;
        }

        if ((myTarget != null && TargetAttkRange(myTarget.transform)) ||
            TargetAttkRange(myTeam.Flag.transform))
        {
            MOVE_VECTOR = Vector3.zero;

            if (!IN_RANGE)
                Debug.DrawLine(transform.position, myTarget != null ? myTarget.transform.position : myTeam.Flag.transform.position, Color.black, 0.1f);

            return true;
        }


        v3_buff_0 = (myTarget == null ? myTeam.Flag.transform.position : myTarget.transform.position) - transform.position;
        Fast.Write3to2(ref v3_buff_0, ref v2_buff_2);
        v3_buff_0 = transform.forward;
        Fast.Write3to2(ref v3_buff_0, ref v2_buff_1);
        float turnFactor = Vector2.Dot(v2_buff_2, v2_buff_1);
        Avoidance = Fast.FastDistance(ref v2_buff_2);
        double avoidMaxSquared = Game.Instance.AvoidanceMax * Game.Instance.AvoidanceMax;
        Avoidance = Avoidance > avoidMaxSquared ? avoidMaxSquared : Avoidance;

        Game.QuadAreaProcess(myQuad, Game.Instance.ProxyRadius, AddAvoidanceDisplacement);
            
        v3_buff_0 = RigidBody.velocity;
        Fast.Write3to2(ref v3_buff_0, ref v2_buff_1);
        v2_buff_2 = (v2_buff_2.normalized * Stats.MoveSpeed) - v2_buff_1;
        float scale = Stats.MoveAccel * turnFactor;
        //scale = scale < MIN_VECTOR_MAGNITUDE ? MIN_VECTOR_MAGNITUDE : scale;
        v2_buff_2 = v2_buff_2.normalized * scale;
        Fast.Write2to3(ref v2_buff_2, ref v3_buff_0);

        MOVE_VECTOR = v3_buff_0;
        //myRigidBody.velocity = v3_buff_0;
        RigidBody.AddForce(v3_buff_0, ForceMode.Acceleration);
        return false;
    }
    private void AttackUpdate()
    {
        if (myTarget == null ||
            LastAttack + Stats.AttkDelayTicks > Game.GameTime)
            return;

        if (!myTarget.Alive ||
            !TargetAggroRange(myTarget))
        {
            myTarget = null;
            return;
        }

        if (Stats.AttkRadius > 0)
            AOEAttack();
        else
            Game.AttackBonom(this, myTarget);

        LastAttack = Game.GameTime;
    }
    private void AOEAttack()
    {
        Game.QuadAreaProcess(myQuad, attk_radius, AOEQuadProc);
    }
    private void LifeUpdate()
    {
        float oldHealth = Health;
        Health += Alive ? (Stats.HealthRegen * Game.health_reg_inverse) : 0;
        Health = Health < 0 ? 0 : Health > Stats.HealthMax ? Stats.HealthMax : Health;
        DeathTime = Alive ? Game.GameTime : DeathTime;
        Recycled = Alive ? false : Recycled;
        Grounded = Alive ? Grounded : false;
        Collider.enabled = Alive;

        myTeam.DamageHealed += Health - oldHealth;

        if (!Alive)
            RenderFade(); // <<< NEEDS WORK

        if (!Recycled && !InPlay)
        {
            Recycled = true;
            mySquad.KillBonom(this);
            gameObject.SetActive(false);
        }
    }

    private bool FindNewTarget(Quadrant quad)
    {
        Closest = null;

        foreach (Bonom bonom in quad)
        {
            if (bonom.myTeam == myTeam)
                continue;

            if (bonom.Alive &&
                TargetAggroRange(bonom) &&
                (Closest == null || Fast.FastDistanceGreater(Closest.transform, bonom.transform, transform)))
                Closest = bonom;
        }

        return Closest != null;
    }
    private void AddAvoidanceDisplacement(Quadrant quad)
    {
        foreach (Bonom proxy in quad)
        {
            if (proxy == this || proxy == myTarget || !proxy.Alive)
                continue;

            v3_buff_0 = proxy.transform.position - transform.position;
            Fast.Write3to2(ref v3_buff_0, ref v2_buff_1);
            v2_buff_2 -= v2_buff_1.normalized * (float)(Avoidance / Fast.FastDistance(ref v2_buff_1));
        }
    }
    private void AOEQuadProc(Quadrant quad)
    {
        foreach (Bonom enemy in quad)
        {
            float distance = Vector3.Distance(enemy.transform.position, myTarget.transform.position);
            if (distance <= Stats.AttkRadius)
            {
                Game.AttackBonom(this, enemy);
            }
        }
    }
    #endregion

    void RenderFade()
    {
        return;

        long deathLength = Game.GameTime - DeathTime;
        float lerp = (Game.Instance.BodyExpirationTicks - deathLength) * Game.body_exp_inverse;
        buffer_color = myTeam.Stats.TeamColor;
        buffer_color.a = lerp;

        myDebugRenderer.material.color = buffer_color;
        myMainRenderer.material.color = buffer_color;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //CanvasUpdate();
        //LifeUpdate();

        //if (!Alive || !Grounded)
        //    return;

        //QuadUpdate();
        //TargetUpdate();
        //MoveUpdate();
        //TurnUpdate();
        //AttackUpdate();
    }
}
