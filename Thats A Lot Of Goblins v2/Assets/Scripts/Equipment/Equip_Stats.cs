using System.Collections.Generic;

// A modifier and whoever added it, so everything from one upgrade can be removed together
public struct Equip_ModifierEntry
{
    public object Source;
    public Equip_Modifier Modifier;
}

// One piece of equipment's final stats. Rebuilt from scratch (base, then adds, then percents, then multiplies)
// whenever the loadout changes, never nudged up and down, so adding and removing upgrades can't drift.
//
// GC: one float array per equipped piece, allocated on equip. Rebuild is main-thread only and allocation free.
public sealed class Equip_Stats
{
    private const int Count = (int)Equip_Stat.Length;

    private static readonly float[] adds = new float[Count];
    private static readonly float[] percents = new float[Count];
    private static readonly float[] multipliers = new float[Count];

    private readonly float[] values = new float[Count];

    public float this[Equip_Stat stat] => values[(int)stat];

    public void Rebuild(Equip_Item item, List<Equip_ModifierEntry> modifiers)
    {
        for (int i = 0; i < Count; i++)
        {
            values[i] = 0f;
            adds[i] = 0f;
            percents[i] = 0f;
            multipliers[i] = 1f;
        }

        if (item.baseStats != null)
        {
            for (int i = 0; i < item.baseStats.Length; i++)
                values[(int)item.baseStats[i].stat] += item.baseStats[i].value;
        }

        for (int i = 0; i < modifiers.Count; i++)
        {
            Equip_Modifier modifier = modifiers[i].Modifier;

            if (!modifier.AppliesTo(item))
                continue;

            int stat = (int)modifier.stat;

            switch (modifier.op)
            {
                case Equip_ModifierOp.Add: adds[stat] += modifier.value; break;
                case Equip_ModifierOp.Percent: percents[stat] += modifier.value; break;
                default: multipliers[stat] *= modifier.value; break;
            }
        }

        for (int i = 0; i < Count; i++)
            values[i] = (values[i] + adds[i]) * (1f + percents[i] * 0.01f) * multipliers[i];
    }
}
