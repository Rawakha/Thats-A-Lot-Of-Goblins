using System.Collections.Generic;
using UnityEngine;

// One impact. Plain data so hits can be batched into native lists for upgrade hooks.
public struct Combat_Hit
{
    public Vector3 Point;
    public Vector3 Normal;
    public Vector3 Velocity;
    public float Damage;

    // Area damage radius around Point, for goblins near an impact
    public float Radius;

    // Which equipment fired it (its slot for now)
    public int Source;
    public int ProjectileType;

    // True if it hit a Combat_IDamageable, false if it hit the ground or scenery
    public bool HitDamageable;
}

// Real objects that take hits directly: tanks, enemy planes, buildings. Put it on the collider's object or on its
// Rigidbody's object.
public interface Combat_IDamageable
{
    void TakeDamage(in Combat_Hit hit);
}

// Systems that take area damage at impact points without colliders: the horde (goblins have no colliders, so
// raycasts can't find them).
public interface Combat_IAreaDamageable
{
    void TakeAreaDamage(in Combat_Hit hit);
}

// Routes area damage from impacts that didn't hit a damageable to every area system.
// Receivers are whole systems (the horde), registered once at startup, not per object, so this is a plain list.
// A blast: everything within KillRadius of Point is blown apart; everything out to FlingRadius takes Damage and is
// thrown away from it (FlingOut sideways, FlingUp upward, both weaker toward the edge). Velocity is the bomb's, for
// receivers that want to know which way it was going.
public struct Combat_Explosion
{
    public Vector3 Point;
    public Vector3 Velocity;
    public float KillRadius;
    public float FlingRadius;
    public float Damage;
    public float FlingOut;
    public float FlingUp;
    public int Source;
}

// Systems that react to blasts: the horde (blown apart and flung), later destruction
public interface Combat_IExplosionReceiver
{
    void TakeExplosion(in Combat_Explosion explosion);
}

public static class Combat_Damage
{
    private static readonly List<Combat_IAreaDamageable> areaReceivers = new List<Combat_IAreaDamageable>(4);
    private static readonly List<Combat_IExplosionReceiver> explosionReceivers = new List<Combat_IExplosionReceiver>(4);

    public static void RegisterExplosions(Combat_IExplosionReceiver receiver)
    {
        if (receiver != null && !explosionReceivers.Contains(receiver))
            explosionReceivers.Add(receiver);
    }

    public static void UnregisterExplosions(Combat_IExplosionReceiver receiver)
    {
        explosionReceivers.Remove(receiver);
    }

    public static void ApplyExplosion(in Combat_Explosion explosion)
    {
        for (int i = 0; i < explosionReceivers.Count; i++)
            explosionReceivers[i].TakeExplosion(explosion);
    }

    public static void RegisterArea(Combat_IAreaDamageable receiver)
    {
        if (receiver != null && !areaReceivers.Contains(receiver))
            areaReceivers.Add(receiver);
    }

    public static void UnregisterArea(Combat_IAreaDamageable receiver)
    {
        areaReceivers.Remove(receiver);
    }

    public static void ApplyArea(in Combat_Hit hit)
    {
        for (int i = 0; i < areaReceivers.Count; i++)
            areaReceivers[i].TakeAreaDamage(hit);
    }
}
