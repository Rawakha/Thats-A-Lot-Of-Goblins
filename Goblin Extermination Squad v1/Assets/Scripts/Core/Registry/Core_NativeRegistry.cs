using System;
using Unity.Collections;

// Stable reference to an entry in a Core_NativeRegistry. Survives swap-back; goes stale once removed.
// default (Generation 0) is never valid.
public struct Core_Handle : IEquatable<Core_Handle>
{
    public int Id;
    public int Generation;

    public static Core_Handle Invalid => default;

    public bool Equals(Core_Handle other) => Id == other.Id && Generation == other.Generation;
    public override bool Equals(object obj) => obj is Core_Handle other && Equals(other);
    public override int GetHashCode() => (Id * 397) ^ Generation;
}

// Burst-compatible handle registry for struct-of-arrays data (horde, carryables, render instances).
// The owner keeps its data in parallel NativeLists indexed by dense index, and mirrors every removal:
//
//   int index = registry.Remove(handle);
//   if (index >= 0) { positions.RemoveAtSwapBack(index); velocities.RemoveAtSwapBack(index); ... }
//
// Dense indices change on removal, so anything outside the owner holds a Core_Handle, never an index.
// When removing several entries by dense index (e.g. from a job's output), go in descending index order.
//
// GC: none. Growth reallocates unmanaged memory only; pre-size with initialCapacity.
public struct Core_NativeRegistry : IDisposable
{
    private NativeList<int> denseToId;
    private NativeList<int> idToDense;
    private NativeList<int> generations;
    private NativeList<int> freeIds;

    public Core_NativeRegistry(int initialCapacity, Allocator allocator)
    {
        denseToId = new NativeList<int>(initialCapacity, allocator);
        idToDense = new NativeList<int>(initialCapacity, allocator);
        generations = new NativeList<int>(initialCapacity, allocator);
        freeIds = new NativeList<int>(initialCapacity, allocator);
    }

    public bool IsCreated => denseToId.IsCreated;
    public int Count => denseToId.Length;

    // New entry goes at denseIndex == old Count; the owner appends its data there
    public Core_Handle Add(out int denseIndex)
    {
        int id;
        if (freeIds.Length > 0)
        {
            id = freeIds[freeIds.Length - 1];
            freeIds.RemoveAtSwapBack(freeIds.Length - 1);
        }
        else
        {
            id = idToDense.Length;
            idToDense.Add(-1);
            generations.Add(1);
        }

        denseIndex = denseToId.Length;
        denseToId.Add(id);
        idToDense[id] = denseIndex;

        return new Core_Handle { Id = id, Generation = generations[id] };
    }

    // Dense index for the handle, or -1 if it's stale
    public int IndexOf(Core_Handle handle)
    {
        if ((uint)handle.Id >= (uint)idToDense.Length || generations[handle.Id] != handle.Generation)
            return -1;

        return idToDense[handle.Id];
    }

    public bool Contains(Core_Handle handle) => IndexOf(handle) >= 0;

    public Core_Handle HandleAt(int denseIndex)
    {
        int id = denseToId[denseIndex];
        return new Core_Handle { Id = id, Generation = generations[id] };
    }

    // Returns the dense index removed (mirror it with RemoveAtSwapBack on your data), or -1 if stale
    public int Remove(Core_Handle handle)
    {
        int index = IndexOf(handle);
        if (index >= 0)
            RemoveAtSwapBack(index);

        return index;
    }

    public void RemoveAtSwapBack(int denseIndex)
    {
        int id = denseToId[denseIndex];

        denseToId.RemoveAtSwapBack(denseIndex);
        if (denseIndex < denseToId.Length)
            idToDense[denseToId[denseIndex]] = denseIndex;

        Release(id);
    }

    // Removes everything; all outstanding handles go stale. Clear your parallel data too.
    public void Clear()
    {
        for (int i = 0; i < denseToId.Length; i++)
            Release(denseToId[i]);

        denseToId.Clear();
    }

    public void Dispose()
    {
        if (denseToId.IsCreated) denseToId.Dispose();
        if (idToDense.IsCreated) idToDense.Dispose();
        if (generations.IsCreated) generations.Dispose();
        if (freeIds.IsCreated) freeIds.Dispose();
    }

    private void Release(int id)
    {
        idToDense[id] = -1;
        generations[id]++;
        freeIds.Add(id);
    }
}
