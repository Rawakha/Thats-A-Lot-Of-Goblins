using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyFeelManager : MonoBehaviour
{
    public static EnemyFeelManager Instance;

    public enum FeelType
    {
        Hit,
        Launch,
        Death
    }

    private struct ActiveFeedback
    {
        public Enemy enemyData;
        public FeelSettings settings;
        public Action onComplete;

        public float elapsed;
        public bool isFlashing;
    }

    [System.Serializable]
    public struct FeelSettings
    {
        public FeelType type;
        [Min(0.01f)] public float duration;
        public FlashSettings flashSettings;
        public ScaleSettings scaleSettings;
    }

    [System.Serializable]
    public struct FlashSettings
    {
        public bool use;
        [Min(0.01f)] public float duration;
    }

    [System.Serializable]
    public struct ScaleSettings
    {
        public bool use;
        public bool returnToBase;
        [Min(0.01f)] public float duration;
        public float horizontalTargetScale;
        public float verticalTargetScale;
        public Ease horizontalEase;
        public Ease verticalEase;
    }

    [Header("Feedbacks")]
    [SerializeField] private FeelSettings hitFeel;
    [SerializeField] private FeelSettings flickFeel;
    [SerializeField] private FeelSettings deathFeel;

    private List<ActiveFeedback> activeFeedbacks = new List<ActiveFeedback>(256);

    private void Awake()
    {
        Utilities.CreateInstance<EnemyFeelManager>(ref Instance, this);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = activeFeedbacks.Count - 1; i >= 0; i--)
        {
            ActiveFeedback active = activeFeedbacks[i];
            FeelSettings feedbackSettings = active.settings;
            Enemy enemy = active.enemyData;

            if (enemy == null)
            {
                RemoveAtSwap(i);
                continue;
            }

            active.elapsed += dt;
            float normalizedTime = Mathf.Clamp01(active.elapsed / feedbackSettings.duration);

            // Scale
            ScaleSettings scale = feedbackSettings.scaleSettings;
            if (scale.use)
            {
                float scaleNormalizedTime = Mathf.Clamp01(active.elapsed / scale.duration);
                float t = scale.returnToBase
                    ? 1f - Mathf.Abs(scaleNormalizedTime * 2f - 1f)     // 0 → 1 → 0
                    : scaleNormalizedTime;                              // 0 → 1, holds at target

                float horizontal = DOVirtual.EasedValue(1f, scale.horizontalTargetScale, t, scale.horizontalEase);
                float vertical = DOVirtual.EasedValue(1f, scale.verticalTargetScale, t, scale.verticalEase);
                Vector3 multiplier = new Vector3(Mathf.Max(0f, horizontal), Mathf.Max(0f, vertical), Mathf.Max(0f, horizontal));
                enemy.feedbackScale = multiplier;
            }

            // Flash
            FlashSettings flash = feedbackSettings.flashSettings;
            if (flash.use)
            {
                enemy.emissionValue = active.elapsed < flash.duration ? 1f : 0f;
            }
            else
            {
                enemy.emissionValue = 0f;
            }

            if (normalizedTime >= 1f)
            {
                Action onComplete = active.onComplete;
                Remove(enemy);
                onComplete?.Invoke();
            }
            else
            {
                activeFeedbacks[i] = active;
            }
        }
    }

    private FeelSettings GetSettings(FeelType type)
    {
        switch (type) 
        {
            case FeelType.Hit:
                return hitFeel;

            case FeelType.Launch:
                return flickFeel;

            case FeelType.Death: 
                return deathFeel;

            default:
                return hitFeel;
        }
    }

    public void Add(Enemy enemy, FeelType type, Action onComplete = null)
    {
        if (enemy == null)
            return;

        FeelSettings settings = GetSettings(type);
        int index = enemy.feedbackIndex;

        if (index >= 0 && index < activeFeedbacks.Count)
        {
            // Try Get the existing feedback
            ActiveFeedback existingFeedback = activeFeedbacks[index];

            if (existingFeedback.enemyData == enemy)
            {
                // If the feedback is the same, reset the elapsed time
                existingFeedback.settings = settings;
                existingFeedback.elapsed = 0f;
                existingFeedback.onComplete = onComplete;
                activeFeedbacks[enemy.feedbackIndex] = existingFeedback;
                enemy.inFeedback = true;
                return;
            }
        }

        enemy.inFeedback = true;
        enemy.feedbackIndex = activeFeedbacks.Count;

        ActiveFeedback activeFeedback = new ActiveFeedback 
        { 
            enemyData = enemy, 
            settings = settings, 
            elapsed = 0f,
            onComplete = onComplete
        };

        activeFeedbacks.Add(activeFeedback);
    }

    public void Remove(Enemy enemy)
    {
        if (enemy == null || !enemy.inFeedback) 
            return;

        int index = enemy.feedbackIndex;
        bool validIndex = index >= 0 && index < activeFeedbacks.Count;
        bool validFeedback = validIndex && activeFeedbacks[index].enemyData == enemy;

        // If index is out of bounds, or points to wrong enemy feedback
        if (!validFeedback)
        {
            Debug.LogError($"EnemyFeedbackManager: stale feedbackIndex on {enemy.name}", enemy);

            index = -1;

            // Search for the feedback in the list
            for (int i = 0; i < activeFeedbacks.Count; i++)
            {
                if (activeFeedbacks[i].enemyData == enemy)
                {
                    index = i;
                    break;
                }
            }

            // If still not found, reset tracking and return
            if (index < 0)
            {
                ResetTracking(enemy);
                return;
            }
        }

        RemoveAtSwap(index);
        ResetTracking(enemy);
    }

    private void ResetTracking(Enemy enemy)
    {
        enemy.inFeedback = false;
        enemy.feedbackIndex = -1;
        enemy.feedbackScale = Vector3.one;
        enemy.emissionValue = 0f;
    }

    private void RemoveAtSwap(int index)
    {
        int lastIndex = activeFeedbacks.Count - 1;

        if (index != lastIndex)
        {
            ActiveFeedback movedFeedback = activeFeedbacks[lastIndex];
            activeFeedbacks[index] = movedFeedback;

            if (movedFeedback.enemyData != null)
                movedFeedback.enemyData.feedbackIndex = index;
        }

        activeFeedbacks.RemoveAt(lastIndex);
    }
}