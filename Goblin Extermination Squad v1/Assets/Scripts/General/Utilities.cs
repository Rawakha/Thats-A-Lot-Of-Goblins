using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class Utilities
{
    public static class ExecutionOrder
    {
        public const int GameBootstrap = -1000;
        public const int Singletons = -100;
        public const int DependantSingletons = -90;
        public const int Default = 0;
    }

    public enum Toggle
    { 
        Enabled,
        Disabled
    }

    [Serializable]
    public struct Pose
    {
        public Vector3 position; 
        public Quaternion rotation;
    }

    public static void GetReference<T>(Component owner, ref T field) where T : Component
    {
        if (field == null)
            field = owner.GetComponent<T>();
    }

    public static void GetReferenceInChildren<T>(Component owner, ref T field) where T : Component
    {
        if (field == null)
            field = owner.GetComponentInChildren<T>(true);
    }

    public static bool TryPlaySound(SoundData soundData, Vector3 worldPosition)
    {
        if (soundData == null || soundData.soundConfig == null) 
            return false;

        return TryPlaySound(soundData.soundConfig, worldPosition);
    }

    public static bool TryPlaySound(SoundConfig soundConfig, Vector3 worldPosition)
    {
        // An empty slot is skipped quietly, so optional sounds can be left unfilled
        if (soundConfig == null || !soundConfig.HasClips)
            return false;

        if (AudioManager.Instance == null)
            return false;

        AudioManager.Instance.RequestSound(soundConfig, worldPosition);
        return true;
    }

    public static Coroutine Delay(MonoBehaviour owner, Action action, float delay)
    {
        return owner.StartCoroutine(DelayRoutine(action, delay));
    }

    public static IEnumerator DelayRoutine(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);

        action?.Invoke();
    }

    public static int LayerMaskToLayer(LayerMask mask)
    {
        int v = mask.value;
        if (v == 0 || (v & (v - 1)) != 0)
        {
            Debug.LogError("LayerMask must contain exactly one layer");
            return -1;
        }
        return Mathf.RoundToInt(Mathf.Log(v, 2));
    }

    public static bool CreateInstance<T>(ref T instance, T newInstance) where T : UnityEngine.Object
    {
        if (instance == null)
        {
            instance = newInstance;
            return true;
        }

        if (instance == newInstance)
            return true; 

        Debug.LogError($"Multiple instances of {typeof(T).Name} detected. " + $"Existing: {instance.name}, New: {newInstance.name}", newInstance);

        return false;
    }

    public static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        return (mask.value & (1 << go.layer)) != 0;
    }

    public static float Random(Vector2 range)
    {
        return UnityEngine.Random.Range(range.x, range.y);
    }

    public static float Random()
    {
        return Random(new Vector2(0f, 1f));
    }

    public static int Random(Vector2Int range)
    {
        return UnityEngine.Random.Range(range.x, range.y);
    }

    public static int Random(int max)
    {
        return UnityEngine.Random.Range(0, max);
    }

    public static T Random<T>(IReadOnlyList<T> list)
    {
        if (list  == null)
        {
            Debug.LogWarning("Cannot select a random element from a null list.");
            return default;
        }

        if (list.Count == 0)
        {
            Debug.LogWarning("Cannot select a random element from an empty list.");
            return default;
        }

        int index = Random(list.Count);
        return list[index];
    }

    public static T Random<T>(T[] array)
    {
        if (array == null)
        {
            Debug.LogWarning("Cannot select a random element from a null array.");
            return default;
        }

        if (array.Length == 0)
        {
            Debug.LogWarning("Cannot select a random element from an empty array.");
            return default;
        }

        int index = Random(array.Length);
        return array[index];
    }

    public static void ApplyForcePD(Rigidbody rb, Vector3 targetPos, float posStrength, float posDamping)
    {
        Vector3 posError = targetPos - rb.position;
        Vector3 velError = -rb.linearVelocity;
        Vector3 force = (posError * posStrength) + (velError * posDamping);

        rb.AddForce(force, ForceMode.Acceleration);
    }

    public static void ApplyForcePD(Rigidbody rb, Vector3 targetPos, Vector3 forcePosition, float posStrength, float posDamping)
    {
        Vector3 posError = targetPos - rb.position;
        Vector3 velError = -rb.linearVelocity;
        Vector3 force = (posError * posStrength) + (velError * posDamping);

        rb.AddForceAtPosition(force, forcePosition, ForceMode.Acceleration);
    }

    public static void ApplyAxisBasedForcePD(Rigidbody rb, Vector3 targetPos, Vector3 axis, float posStrength, float posDamping)
    {
        axis = axis.normalized;

        float posError = Vector3.Dot(targetPos - rb.position, axis);
        float velError = Vector3.Dot(-rb.linearVelocity, axis);

        float force = (posError * posStrength) + (velError * posDamping);

        rb.AddForce(axis * force, ForceMode.Acceleration);
    }

    public static void ApplyTorquePD(Rigidbody rb, Quaternion targetRot, float rotStrength, float rotDamping)
    {
        Vector3 rotError = Core_Math.ToRotationVector(Core_Math.ShortestDelta(rb.rotation, targetRot));
        Vector3 angularVelError = -rb.angularVelocity;
        Vector3 torque = (rotError * rotStrength) + (angularVelError * rotDamping);

        rb.AddTorque(torque, ForceMode.Acceleration);
    }

    public static Vector3 ScaleUnder(Transform parent, Vector3 worldScale)
    {
        Vector3 parentScale = parent.lossyScale;

        return new Vector3(
            parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
            parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
            parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z);
    }

    #region Math
    public static int Mod(int direction, int maxValue)
    {
        int result = direction % maxValue;
        return result < 0 ? result + maxValue : result;
    }

    public static Quaternion ShortestRotation(Quaternion to, Quaternion from)
    {
        return Quaternion.Normalize(Core_Math.ShortestDelta(from, to));
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Core_Math.Flatten(b - a).magnitude;
    }

    public static bool IsInRange(Vector3 source, Vector3 target, float range)
    {
        float sqrDistance = (target - source).sqrMagnitude;
        return sqrDistance < range * range;
    }

    public static bool IsInRange(Vector3 source, Vector3 target, float sqrRange, out float sqrDistance)
    {
        sqrDistance = (target - source).sqrMagnitude;
        return sqrDistance < sqrRange;
    }

    public static bool IsInHorizontalRange(Vector3 a, Vector3 b, float range)
    {
        float xDistance = a.x - b.x;
        float zDistance = a.z - b.z;

        return xDistance * xDistance + zDistance * zDistance <= range * range;
    }

    public static Vector3 LerpWithArc(Vector3 a, Vector3 b, float height, float t)
    {
        Vector3 p = Vector3.Lerp(a, b, t);                 // straight line
        if (height > 0f) p.y += height * (4f * t * (1f - t)); // optional parabola
        return p;
    }

    public static bool IsInFront(Transform source, Transform target, float cosThreshold = 0f, bool flat = true)
    {
        return IsInFront(source.position, target.position, source.forward, cosThreshold, flat);
    }

    public static bool IsInFront(Vector3 source, Vector3 target, Vector3 forward, float cosThreshold = 0f, bool flat = true)
    {
        Vector3 toTarget;

        if (flat)
        {
            toTarget = Core_Math.FlatDirection(target - source, Vector3.zero);
            forward = Core_Math.FlatDirection(forward, Vector3.zero);
        }
        else
        {
            toTarget = Core_Math.SafeNormalize(target - source, Vector3.zero);
        }

        if (toTarget.sqrMagnitude < 0.0001f)
            return false;

        return Vector3.Dot(forward, toTarget) > cosThreshold;
    }

    public static bool IsInFrontXZ(Transform source, Transform target, float cosThreshold = 0f)
    {
        return IsInFront(source.position, target.position, source.forward, cosThreshold, true);
    }

    #endregion

    #region Editor
    public static void RecordData(UnityEngine.Object data, string label)
    {
#if UNITY_EDITOR
        Undo.RecordObject(data, label);
#endif
    }

    public static void MarkDataDirty(UnityEngine.Object data)
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(data);
#endif
    }
    #endregion

    #region Gizmos
    // A flat fan: two edge lines and an arc between them.
    public static void DrawCone(Vector3 origin, Vector3 forward, float halfAngle, float radius, Color color)
    {
        Gizmos.color = color;

        Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
        Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;

        // Edges
        Gizmos.DrawLine(origin, origin + left * radius);
        Gizmos.DrawLine(origin, origin + right * radius);

        // Arc
        const int segments = 24;
        Vector3 previous = origin + left * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
            Vector3 next = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;

            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
    #endregion
}