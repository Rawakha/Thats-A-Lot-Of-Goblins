using System;
using System.Collections.Generic;
using UnityEngine;

// What sort of thing a target is, so an assist only pulls toward what its weapon mode is meant for
[Flags]
public enum Combat_TargetKind
{
    None = 0,
    Ground = 1,     // goblins, ground vehicles, buildings
    Air = 2         // enemy planes, balloons, flak
}

// What a weapon's aim assist is looking for: something of the given Kinds, within Range of Origin, inside a cone
// (measured flat, round Forward) of CosAngle, preferring what's closest to the middle and nearest. Previous, when
// set, is where the last target was: anything within StickyRadius of it gets Sticky added to its score, so the assist
// doesn't flick about.
public struct Combat_TargetQuery
{
    public Combat_TargetKind Kinds;
    public Vector3 Origin;
    public Vector3 Forward;         // flat, normalised
    public float Range;
    public float CosAngle;          // cos of the cone's half angle
    public bool HasPrevious;
    public Vector3 Previous;
    public float StickyRadius;
    public float Sticky;
}

// Anything that can offer targets to aim assists: the horde, later enemy planes or turrets. Returns its best one of
// the query's Kinds (none, if it has none of those).
public interface Combat_ITargetSource
{
    bool FindTarget(in Combat_TargetQuery query, out Vector3 point, out float score);
}

// Asks every registered source and keeps the best. Sources are whole systems, registered once, not per target.
public static class Combat_Targets
{
    private static readonly List<Combat_ITargetSource> sources = new List<Combat_ITargetSource>(4);

    public static void Register(Combat_ITargetSource source)
    {
        if (source != null && !sources.Contains(source))
            sources.Add(source);
    }

    public static void Unregister(Combat_ITargetSource source)
    {
        sources.Remove(source);
    }

    public static bool Find(in Combat_TargetQuery query, out Vector3 point)
    {
        point = default;
        float best = float.MinValue;
        bool found = false;

        for (int i = 0; i < sources.Count; i++)
        {
            if (!sources[i].FindTarget(query, out Vector3 candidate, out float score) || score <= best)
                continue;

            best = score;
            point = candidate;
            found = true;
        }

        return found;
    }

    // The score every source uses, so they compete fairly: middle of the cone counts most, nearness a little, and
    // sticking to the last target a set bonus. Below the cone's edge or out of range: not a target (returns false).
    public static bool Score(in Combat_TargetQuery query, Vector3 point, out float score)
    {
        score = 0f;
        Vector3 offset = point - query.Origin;
        float distanceSqr = offset.sqrMagnitude;

        if (distanceSqr > query.Range * query.Range)
            return false;

        Vector3 flat = new Vector3(offset.x, 0f, offset.z);
        float flatLength = flat.magnitude;

        // Straight below counts as dead ahead
        float cos = flatLength > 0.001f ? Vector3.Dot(flat / flatLength, query.Forward) : 1f;

        if (cos < query.CosAngle)
            return false;

        score = cos - Mathf.Sqrt(distanceSqr) / Mathf.Max(query.Range, 0.01f) * 0.3f;

        if (query.HasPrevious && (point - query.Previous).sqrMagnitude <= query.StickyRadius * query.StickyRadius)
            score += query.Sticky;

        return true;
    }
}
