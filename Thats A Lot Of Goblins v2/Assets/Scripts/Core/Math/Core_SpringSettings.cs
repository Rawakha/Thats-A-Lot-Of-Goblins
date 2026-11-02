using System;
using UnityEngine;

[Serializable]
public struct Core_SpringSettings
{
    public float strength;
    public float damper;

    public Core_SpringSettings(float strength, float damper)
    {
        this.strength = strength;
        this.damper = damper;
    }
}