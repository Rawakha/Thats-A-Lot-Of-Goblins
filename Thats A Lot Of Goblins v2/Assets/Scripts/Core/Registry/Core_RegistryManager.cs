using System.Collections.Generic;
using UnityEngine;

// Base for managers that track registered objects, with swap-back removal (O(1), no shifting).
// Order is not stable and indices change on removal, so never hold an index outside the registry.
//
// Parallel data: override OnRegistered to append your per-item data at the given index, and
// OnRemoved to mirror the swap (NativeList.RemoveAtSwapBack(index) does exactly that).
//
// Iterating: wrap loops in BeginIteration/EndIteration (try/finally) so Unregister calls made
// during the loop are deferred until EndIteration. Items registered during a loop are appended
// and only visited if the loop re-reads Count.
//
// GC: no per-frame allocations. Lists are pre-sized from initialCapacity; growing past it allocates.
public abstract class Core_RegistryManager<T> : MonoBehaviour where T : class, Core_IRegistrable
{
    [SerializeField, Min(1)] private int initialCapacity = 64;

    protected List<T> items;
    private List<T> pendingRemovals;
    private int iterationDepth;

    public int Count => items == null ? 0 : items.Count;
    public T this[int index] => items[index];

    public void Register(T item)
    {
        if (item == null)
            return;

        EnsureCreated();

        if (item.RegistryIndex != Core_Registry.Unregistered)
        {
            // Unregistered then re-registered inside one iteration: cancel the pending removal
            if (pendingRemovals.Remove(item))
                return;

            Debug.LogWarning($"{name}: {item} is already registered (here or in another registry).", this);
            return;
        }

        int index = items.Count;
        items.Add(item);
        item.RegistryIndex = index;
        OnRegistered(item, index);
    }

    public void Unregister(T item)
    {
        if (item == null || items == null)
            return;

        int index = item.RegistryIndex;
        if (index < 0 || index >= items.Count || !ReferenceEquals(items[index], item))
            return;

        if (iterationDepth > 0)
        {
            if (!pendingRemovals.Contains(item))
                pendingRemovals.Add(item);
            return;
        }

        RemoveAtSwapBack(index);
    }

    public void BeginIteration()
    {
        iterationDepth++;
    }

    public void EndIteration()
    {
        if (iterationDepth == 0 || --iterationDepth > 0)
            return;

        // Nothing has registered yet, so nothing can be pending (the lists are made on first Register)
        if (pendingRemovals == null)
            return;

        for (int i = 0; i < pendingRemovals.Count; i++)
            Unregister(pendingRemovals[i]);

        pendingRemovals.Clear();
    }

    // Called after the item is appended at index
    protected virtual void OnRegistered(T item, int index) { }

    // Called after the swap: the item that was at lastIndex now lives at index (unless they were equal),
    // and lastIndex is gone. Mirror this on any parallel data.
    protected virtual void OnRemoved(T removed, int index, int lastIndex) { }

    protected virtual void OnDestroy()
    {
        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
            items[i].RegistryIndex = Core_Registry.Unregistered;

        items.Clear();
        pendingRemovals.Clear();
    }

    private void RemoveAtSwapBack(int index)
    {
        T removed = items[index];
        int lastIndex = items.Count - 1;

        if (index != lastIndex)
        {
            T moved = items[lastIndex];
            items[index] = moved;
            moved.RegistryIndex = index;
        }

        items.RemoveAt(lastIndex);
        removed.RegistryIndex = Core_Registry.Unregistered;
        OnRemoved(removed, index, lastIndex);
    }

    // Lazy so items can register before this manager's Awake runs
    private void EnsureCreated()
    {
        if (items != null)
            return;

        items = new List<T>(initialCapacity);
        pendingRemovals = new List<T>(Mathf.Max(4, initialCapacity / 4));
    }
}
