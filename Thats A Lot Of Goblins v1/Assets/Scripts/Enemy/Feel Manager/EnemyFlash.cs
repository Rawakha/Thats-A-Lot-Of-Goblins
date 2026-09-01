using UnityEngine;

[System.Serializable]
public struct FlashSettings
{
    public bool use;
    [Min(0.01f)] public float duration;
    public float remapZero;
    public float remapOne;
    public AnimationCurve curve;

    public static FlashSettings Default => new FlashSettings
    {
        use = true,
        duration = 0.1f,
        remapZero = 0f,
        remapOne = 1f,
        curve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)
    };
}

public static class EnemyFlash
{
    public static void Update(FlashSettings settings, Enemy enemy, float elapsed)
    {
        if (enemy == null)
            return;

        if (!settings.use)
        {
            enemy.emissionValue = 0f;
            return;
        }

        if (elapsed < settings.duration)
        {
            enemy.emissionValue = EvaluateFlash(settings, elapsed);
        }
        else
        {
            enemy.emissionValue = 0f;
        }
    }

    private static float EvaluateFlash(FlashSettings settings, float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / settings.duration);
        float curveValue = settings.curve.Evaluate(t);
        return Mathf.LerpUnclamped(settings.remapZero, settings.remapOne, curveValue);
    }

    public static bool IsFinished(FlashSettings settings, float elapsed) 
    {
        return !settings.use || elapsed >= settings.duration;
    }

    public static void Reset(Enemy enemy)
    {
        if (enemy == null)
            return;

        enemy.emissionValue = 0f;
    }
}