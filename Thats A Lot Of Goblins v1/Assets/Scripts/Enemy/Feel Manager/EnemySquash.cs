using UnityEngine;

[System.Serializable]
public struct SquashSettings
{
    public bool use;
    public float stiffness;
    public float damping;
    public float restValue;
    public float impactStrength;

    [Header("Completion")]
    public float minimumDuration;
    public float settleDistance;
    public float settleVelocity;

    public static SquashSettings Default => new SquashSettings
    {
        use = true,
        stiffness = 200f,
        damping = 15f,
        restValue = 1f,
        impactStrength = 8f,
        minimumDuration = 0.12f,
        settleDistance = 0.01f,
        settleVelocity = 0.05f
    };
}

public static class EnemySquash
{
    public static void Begin(SquashSettings settings, Enemy enemy, bool restart)
    {
        if (enemy == null || !settings.use)
            return;

        if (restart)
        {
            enemy.springValue = settings.restValue;
            enemy.springVelocity = 0f;
        }

        enemy.springVelocity -= settings.impactStrength;
    }

    public static void Update(SquashSettings settings, Enemy enemy, float deltaTime)
    {
        if (enemy == null || !settings.use)
            return;

        float force = (settings.restValue - enemy.springValue) * settings.stiffness - enemy.springVelocity * settings.damping;
        enemy.springVelocity += force * deltaTime;
        enemy.springValue += enemy.springVelocity * deltaTime;

        float rest = Mathf.Max(0.001f, settings.restValue);
        float verticalScale = Mathf.Max(0.05f, enemy.springValue / rest);
        float horizontalScale = 1f / Mathf.Sqrt(verticalScale);

        enemy.feedbackScale = new Vector3(horizontalScale, verticalScale, horizontalScale);
    }

    public static bool IsFinished(SquashSettings settings, Enemy enemy, float elapsed)
    {
        if (enemy == null || !settings.use) 
            return true;

        if (elapsed < settings.minimumDuration)
            return false;

        bool positionSettled = Mathf.Abs(enemy.springValue - settings.restValue) <= settings.settleDistance;
        bool velocitySettled = Mathf.Abs(enemy.springVelocity) <= settings.settleVelocity;

        return positionSettled && velocitySettled;
    }

    public static void Reset(Enemy enemy)
    {
        if (enemy == null) 
            return;

        enemy.springValue = 1f;
        enemy.springVelocity = 0f;
        enemy.feedbackScale = Vector3.one;
    }
}