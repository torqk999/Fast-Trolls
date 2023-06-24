using System;
using Unity;
using UnityEngine;

public enum BonomType
{
    Melee,
    Ranged,
    Mage
}

[Serializable]
public struct BonomStats
{
    public BonomType Type;
    public Sprite Sprite;
    public GameObject Prefab;

    public int RowPriority;
    public long AttkDelayTicks;
    public float AttkRange;
    public float AttkRadius;
    public float AttkDamage;
    public float KnockBack;
    public float KnockUp;
    public float ThreatThreshold;
    public float AggroRange;
    public float HealthMax;
    public float HealthRegen;
    public float MoveSpeed;
    public float MoveAccel;
    public float TurnSpeed;

    public BonomStats(
        BonomType bonomType,
        Sprite bonomSprite,
        GameObject prefab,

        int rowPriority,
        long attkDelayTicks,
        float attkRange,
        float attkRadius,
        float attkDamage,
        float knockBack,
        float knockUp,
        float threatThreshold,
        float aggroRange,
        float healthMax,
        float healthRegen,
        float moveSpeed,
        float moveAccel,
        float turnSpeed
        )
    {
        Type = bonomType;
        Sprite = bonomSprite;
        Prefab = prefab;

        RowPriority = rowPriority;
        AttkDelayTicks = attkDelayTicks;
        AttkRange = attkRange;
        AttkRadius = attkRadius;
        AttkDamage = attkDamage;
        KnockBack = knockBack;
        KnockUp = knockUp;
        ThreatThreshold = threatThreshold;
        AggroRange = aggroRange;
        HealthMax = healthMax;
        HealthRegen = healthRegen;
        MoveSpeed = moveSpeed;
        MoveAccel = moveAccel;
        TurnSpeed = turnSpeed;
    }
}
