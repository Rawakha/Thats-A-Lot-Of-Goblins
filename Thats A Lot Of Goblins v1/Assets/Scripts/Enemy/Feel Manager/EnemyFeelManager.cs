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
        public SquashSettings squashSettings;

        public static FeelSettings DefaultHit => new FeelSettings
        {
            type = FeelType.Hit,
            duration = 0.8f,
            flashSettings = FlashSettings.Default,
            squashSettings = SquashSettings.Default
        };
    }

    [Header("Feedbacks")]
    [SerializeField] private FeelSettings hitFeel = FeelSettings.DefaultHit;
    [SerializeField] private FeelSettings flickFeel = FeelSettings.DefaultHit;
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

            // Squash
            SquashSettings squash = feedbackSettings.squashSettings;
            EnemySquash.Update(squash, enemy, dt);

            // Flash
            FlashSettings flash = feedbackSettings.flashSettings;
            EnemyFlash.Update(flash, enemy, active.elapsed);

            bool squashFinished = EnemySquash.IsFinished(squash, enemy, active.elapsed);
            bool flashFinished = EnemyFlash.IsFinished(flash, active.elapsed);
            bool allEffectsFinished = squashFinished && flashFinished;
            bool maximumDurationReached = active.elapsed >= feedbackSettings.duration;

            if (allEffectsFinished || maximumDurationReached)
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
                bool sameFeel = existingFeedback.settings.type == settings.type;

                existingFeedback.settings = settings;
                existingFeedback.elapsed = 0f;
                existingFeedback.onComplete = onComplete;
                activeFeedbacks[enemy.feedbackIndex] = existingFeedback;
                enemy.inFeedback = true;

                EnemySquash.Begin(settings.squashSettings, enemy, sameFeel);
                EnemyFlash.Update(settings.flashSettings, enemy, 0f);
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
        EnemySquash.Begin(settings.squashSettings, enemy, restart: true);
        EnemyFlash.Update(settings.flashSettings, enemy, 0f);
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
        EnemySquash.Reset(enemy);
        EnemyFlash.Reset(enemy);
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
}