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
    public float ThreatThreshold;
    public float AggroRange;
    public float AttkRange;
    public float AttkRadius;
    public int AttkSpeed;
    public float AttkDamage;
    public float HealthMax;
    public float HealthRegen;
    public float MoveSpeed;
    public float TurnSpeed;

    public BonomStats(
        BonomType bonomType,
        Sprite bonomSprite,
        GameObject prefab,
        int rowPriority,
        float threatThreshold,
        float aggroRange,
        float attkRange,
        float attkRadius,
        float attkDamage,
        float healthMax,
        float healthRegen,
        float moveSpeed,
        float turnSpeed,
        int attkSpeed)
    {
        Type = bonomType;
        Sprite = bonomSprite;
        Prefab = prefab;
        RowPriority = rowPriority;
        ThreatThreshold = threatThreshold;
        AggroRange = aggroRange;
        AttkRange = attkRange;
        AttkRadius = attkRadius;
        AttkSpeed = attkSpeed;
        AttkDamage = attkDamage;
        HealthMax = healthMax;
        HealthRegen = healthRegen;
        MoveSpeed = moveSpeed;
        TurnSpeed = turnSpeed;
    }
}
