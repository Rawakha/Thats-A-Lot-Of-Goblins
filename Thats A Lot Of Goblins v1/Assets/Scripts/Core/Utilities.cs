using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public struct PoseData
    {
        public Vector3 position; 
        public Quaternion rotation;
    }

    public static bool TryPlaySound(SoundData soundData, Vector3 worldPosition, bool additive = false)
    {
        if (soundData == null || soundData.soundConfig == null) 
            return false;

        if (AudioManager.Instance == null)
            return false;

        AudioManager.Instance.RequestSound(soundData.soundConfig, worldPosition, additive);
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

    public static void CreateInstance<T>(ref T instance, T newInstance) where T : UnityEngine.Object
    {
        if (instance == null)
        {
            instance = newInstance;
            return;
        }

        if (instance != newInstance)
        {
            Debug.LogError(
                $"Multiple instances of {typeof(T).Name} detected. " +
                $"Existing: {instance.name}, New: {newInstance.name}",
                newInstance
            );
        }
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
        Quaternion q = targetRot * Quaternion.Inverse(rb.rotation);
        q.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        Vector3 rotError = axis * Mathf.Deg2Rad * angle;
        Vector3 angularVelError = -rb.angularVelocity;
        Vector3 torque = (rotError * rotStrength) + (angularVelError * rotDamping);

        rb.AddTorque(torque, ForceMode.Acceleration);
    }

    #region Math
    public static int Mod(int direction, int maxValue)
    {
        int result = direction % maxValue;
        return result < 0 ? result + maxValue : result;
    }

    public static Quaternion ShortestRotation(Quaternion to, Quaternion from)
    {
        Quaternion q = to * Quaternion.Inverse(from);
        if (q.w < 0f) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
        return q.normalized;
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public static Vector3 LerpWithArc(Vector3 a, Vector3 b, float height, float t)
    {
        Vector3 p = Vector3.Lerp(a, b, t);                 // straight line
        if (height > 0f) p.y += height * (4f * t * (1f - t)); // optional parabola
        return p;
    }

    public static bool IsInFront(Transform source, Transform target, float cosThreshold = 0f)
    {
        Vector3 toTarget = (target.position - source.position).normalized;
        float dot = Vector3.Dot(target.forward, toTarget);
        return dot > cosThreshold;
    }

    public static bool IsInFrontXZ(Transform source, Transform target, float cosThreshold = 0f)
    {
        Vector3 f = source.forward;
        f.y = 0f;
        f.Normalize();

        Vector3 toTarget = target.position - source.position;
        toTarget.y = 0f;
        toTarget.Normalize();

        float dot = Vector3.Dot(f, toTarget);
        return dot > cosThreshold;
    }

    #endregion
}