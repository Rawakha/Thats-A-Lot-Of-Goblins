using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
#endif

// Editor-authored placement baseline for a generated stone. Retains transform offsets on matching slots.
// Lives under the EditorOnly source group; no runtime callbacks, subscriptions or allocations.
[DisallowMultipleComponent]
public class Level_SourcePiece : MonoBehaviour
{
    [SerializeField, HideInInspector] private string slot;
    [SerializeField, HideInInspector] private Vector3 generatedPosition, generatedScale = Vector3.one;
    [SerializeField, HideInInspector] private Quaternion generatedRotation = Quaternion.identity;
#if UNITY_EDITOR
    public string Slot => slot;
    public void MakeManual() => slot = null;
    public void Place(string id, Vector3 position, Quaternion rotationValue, Vector3 size, bool preserve)
    {
        bool matching = preserve && slot == id;
        Vector3 offset = matching ? transform.localPosition - generatedPosition : Vector3.zero;
        Quaternion rotation = matching ? Quaternion.Inverse(generatedRotation) * transform.localRotation : Quaternion.identity;
        Vector3 scale = matching ? new Vector3(Ratio(transform.localScale.x, generatedScale.x),
            Ratio(transform.localScale.y, generatedScale.y), Ratio(transform.localScale.z, generatedScale.z)) : Vector3.one;
        slot = id; generatedPosition = position; generatedRotation = rotationValue; generatedScale = size;
        transform.localPosition = position + offset;
        transform.localRotation = rotationValue * rotation;
        transform.localScale = Vector3.Scale(size, scale);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
    }
    // A generator supplies only the slot identity and its create/configure operations.
    public static void Sync<TPiece, TLayout>(Level_BakeGroup owner, IReadOnlyList<TLayout> layout,
        Func<TLayout, string> slotOf, Func<TLayout, TPiece> create, Action<TLayout, TPiece> configure)
        where TPiece : Level_SourcePiece
    {
        var desired = new HashSet<string>();
        foreach (var entry in layout)
        {
            string key = slotOf(entry);
            if (string.IsNullOrEmpty(key) || !desired.Add(key))
                throw new InvalidOperationException("Generated source pieces require unique, stable slot IDs.");
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Synchronize Level Pieces");
        try
        {
            Transform folder = owner.Sources.transform;
            var existing = new Dictionary<string, TPiece>();
            foreach (var piece in folder.GetComponentsInChildren<TPiece>(true))
            {
                if (piece.GetComponentInParent<Level_BakeGroup>(true) != owner || string.IsNullOrEmpty(piece.Slot)) continue;
                if (piece.transform.parent == folder && existing.TryAdd(piece.Slot, piece)) continue;
                Undo.RecordObject(piece, "Keep Manual Piece");
                piece.MakeManual();
                PrefabUtility.RecordPrefabInstancePropertyModifications(piece);
            }
            foreach (var entry in layout)
            {
                string key = slotOf(entry);
                if (!existing.TryGetValue(key, out var piece)) piece = create(entry);
                else existing.Remove(key);
                Undo.RecordObject(piece, "Place Level Piece");
                Undo.RecordObject(piece.transform, "Place Level Piece");
                configure(entry, piece);
            }
            foreach (var obsolete in existing.Values)
            {
                while (obsolete.transform.childCount > 0)
                    Undo.SetTransformParent(obsolete.transform.GetChild(0), folder, "Retain Authored Child");
                Undo.DestroyObjectImmediate(obsolete.gameObject);
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }
    private static float Ratio(float value, float baseline) => Mathf.Abs(baseline) > 0.00001f ? value / baseline : 1f;
#endif
}
