using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

// Author a visual choice on a GameObject under the goblin. Each option is a direct child root containing meshes;
// an empty root means None. Set percentages, choose Head/Body, and use the custom inspector to preview an option.
// Preview active states never control spawn chances. Groups are independent and must not be nested.
// GC: serialized lists/objects at authoring/load; no runtime callbacks or subscriptions on this component.
[DisallowMultipleComponent]
[AddComponentMenu("Goblins/Horde/Variation Group")]
public class Horde_VariationGroup : MonoBehaviour
{
    public enum Anchor { Head, Body }

    [Serializable]
    public class Option
    {
        public string name = "Option";
        [Tooltip("Direct child containing this option's visual objects. Empty means no additional visual.")]
        public GameObject root;
        [Min(0f)] public float weight = 1f;
    }

    [Tooltip("Animation anchor for all this group's meshes. Their authored position is preserved.")]
    public Anchor anchor = Anchor.Head;
    public List<Option> options = new List<Option>();
    [HideInInspector] public int preview;

    public static float Weight(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);

    // Inspector-only caller: keep the edited chance exact and preserve ratios among the other options.
    // If all others were zero, split the remainder evenly. Existing relative weights remain compatible.
    // Mutates the caller's temporary array; no allocations or subscriptions here.
    public static void SetPercentage(float[] weights, int selected, float percentage)
    {
        if (selected < 0 || selected >= weights.Length) return;
        if (weights.Length == 1) { weights[0] = 100f; return; }
        percentage = Mathf.Clamp(Weight(percentage), 0f, 100f);
        double others = 0;
        for (int i = 0; i < weights.Length; i++)
            if (i != selected) others += Weight(weights[i]);
        float remaining = 100f - percentage;
        for (int i = 0; i < weights.Length; i++)
            if (i != selected)
                weights[i] = others > 0
                    ? (float)(remaining * Weight(weights[i]) / others)
                    : remaining / (weights.Length - 1);
        weights[selected] = percentage;
    }

    public bool Validate(out string reason)
    {
        if (transform.parent != null && transform.parent.GetComponentInParent<Horde_VariationGroup>(true) != null)
        {
            reason = "Variation groups cannot be nested. Put independent groups beside each other.";
            return false;
        }
        for (int i = 0; i < options.Count; i++)
        {
            Option option = options[i];
            if (option == null) continue;
            if (option.root != null && option.root.transform.parent != transform)
            {
                reason = "Every option root must be a direct child of this group.";
                return false;
            }
            for (int j = 0; j < i; j++)
                if (option.root != null && options[j] != null && options[j].root == option.root)
                {
                    reason = "Each option needs a different root.";
                    return false;
                }
        }
        reason = null;
        return true;
    }
}

// Baked choices for one render group. Snapshot weights once; roll once per spawn and swap back with the goblin.
// GC: dictionaries/weight arrays and NativeLists at bake, NativeList capacity growth on spawning only.
// No per-frame allocations or subscriptions. Draw only reads choices; the render group owns/disposes this.
public sealed class Horde_Variations : IDisposable
{
    private readonly Dictionary<Transform, Vector2Int> roots = new Dictionary<Transform, Vector2Int>();
    private readonly Horde_VariationGroup[] groups;
    private readonly float[][] weights;
    private readonly NativeList<int>[] choices;
    private readonly Transform prefabRoot;
    public int Count => groups.Length;

    public Horde_Variations(GameObject prefab, int capacity)
    {
        prefabRoot = prefab.transform;
        var valid = new List<Horde_VariationGroup>();
        foreach (Horde_VariationGroup group in prefab.GetComponentsInChildren<Horde_VariationGroup>(true))
        {
            if (!group.enabled) continue;
            if (!group.Validate(out string reason))
            {
                Debug.LogError($"{group.name}: {reason} Group ignored by render bake.", group);
                continue;
            }
            valid.Add(group);
        }
        groups = valid.ToArray();
        weights = new float[groups.Length][];
        choices = new NativeList<int>[groups.Length];
        for (int g = 0; g < groups.Length; g++)
        {
            var options = groups[g].options;
            weights[g] = new float[options.Count];
            choices[g] = new NativeList<int>(capacity, Allocator.Persistent);
            float largest = 0f;
            for (int o = 0; o < options.Count; o++)
            {
                var option = options[o];
                if (option == null) continue;
                weights[g][o] = Horde_VariationGroup.Weight(option.weight);
                largest = Mathf.Max(largest, weights[g][o]);
                if (option.root != null) roots.Add(option.root.transform, new Vector2Int(g, o));
            }
            // Normalise large weights before summing, so a valid authoring weight cannot overflow the roll.
            if (largest > 0f)
                for (int o = 0; o < weights[g].Length; o++) weights[g][o] /= largest;
        }
    }

    public Vector2Int Resolve(Transform part)
    {
        for (Transform current = part; current != null; current = current.parent)
        {
            if (roots.TryGetValue(current, out Vector2Int option)) return option;
            if (current == prefabRoot) break;
        }
        return new Vector2Int(-1, -1);
    }

    // Only option roots may be inactive for preview; deliberate inactive descendants remain excluded.
    public bool Included(Transform part)
    {
        for (Transform current = part; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf && !roots.ContainsKey(current)) return false;
            if (current == prefabRoot) break;
        }
        return true;
    }

    public bool OnHead(int group) => groups[group].anchor == Horde_VariationGroup.Anchor.Head;
    public bool Matches(int goblin, int group, int option) => group < 0 || choices[group][goblin] == option;

    public static int Choose(float[] weights, float roll)
    {
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += weights[i];
        if (total <= 0f) return -1;
        float remaining = Mathf.Clamp(roll, 0f, 0.99999994f) * total;
        int last = -1;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0f) continue;
            last = i;
            if (remaining < weights[i]) return i;
            remaining -= weights[i];
        }
        return last;
    }

    public void Add(uint seed)
    {
        var random = new Unity.Mathematics.Random(seed | 1u);
        for (int g = 0; g < groups.Length; g++) choices[g].Add(Choose(weights[g], random.NextFloat()));
    }

    public void Remove(int index)
    {
        for (int g = 0; g < groups.Length; g++) choices[g].RemoveAtSwapBack(index);
    }

    public void Dispose()
    {
        for (int g = 0; g < groups.Length; g++) choices[g].Dispose();
    }
}
