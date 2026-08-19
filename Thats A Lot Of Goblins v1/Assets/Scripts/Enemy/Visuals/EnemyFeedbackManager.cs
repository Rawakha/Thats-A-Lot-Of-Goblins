using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyFeedbackManager : MonoBehaviour
{
    public static EnemyFeedbackManager Instance;

    public enum FeedbackType
    {
        Flick,
        Death
    }

    private struct ActiveFeedback
    {
        public EnemyFeedback enemyFeedback;
        public FeedbackSettings settings;
        public float elapsed;
        public Action onComplete;
    }

    [System.Serializable]
    public struct FeedbackSettings
    {
        public FeedbackType type;
        public ScaleSettings scaleSettings;
    }

    [System.Serializable]
    public struct ScaleSettings
    {
        public bool use;
        [Min(0.01f)] public float duration;
        public AnimationCurve horizontalScale; // X and Z
        public AnimationCurve verticalScale;   // Y
    }

    [Header("Feedbacks")]
    [SerializeField] private FeedbackSettings flickFeedback;
    [SerializeField] private FeedbackSettings deathFeedback;

    private List<ActiveFeedback> activeFeedbacks = new List<ActiveFeedback>(256);

    private void Awake()
    {
        Utilities.CreateInstance<EnemyFeedbackManager>(ref Instance, this);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = activeFeedbacks.Count - 1; i >= 0; i--)
        {
            ActiveFeedback active = activeFeedbacks[i];
            FeedbackSettings feedbackSettings = active.settings;
            EnemyFeedback enemyFeedback = active.enemyFeedback;

            if (enemyFeedback == null || enemyFeedback.feedbackVisual == null)
            {
                RemoveAtSwap(i);
                continue;
            }

            active.elapsed += dt;

            // Scale
            ScaleSettings scale = feedbackSettings.scaleSettings;
            float normalizedTime = Mathf.Clamp01(active.elapsed / scale.duration);
            float horizontal = scale.horizontalScale != null ? scale.horizontalScale.Evaluate(normalizedTime) : 1f;
            float vertical = scale.verticalScale != null ? scale.verticalScale.Evaluate(normalizedTime) : 1f;
            Vector3 multiplier = new Vector3(Mathf.Max(0f, horizontal), Mathf.Max(0f, vertical), Mathf.Max(0f, horizontal));
            enemyFeedback.feedbackVisual.localScale = Vector3.Scale(enemyFeedback.baseScale, multiplier);

            if (normalizedTime >= 1f)
            {
                Action onComplete = active.onComplete;
                Remove(enemyFeedback);
                onComplete?.Invoke();
            }
            else
            {
                activeFeedbacks[i] = active;
            }
        }
    }

    private FeedbackSettings GetSettings(FeedbackType type)
    {
        switch (type) 
        {
            case FeedbackType.Flick:
                return flickFeedback;

            case FeedbackType.Death: 
                return deathFeedback;

            default:
                return flickFeedback;
        }
    }

    public void Add(EnemyFeedback enemyFeedback, FeedbackType type, Action onComplete = null)
    {
        if (enemyFeedback == null)
            return;

        FeedbackSettings settings = GetSettings(type);
        int index = enemyFeedback.feedbackIndex;

        if (index >= 0 && index < activeFeedbacks.Count)
        {
            // Try Get the existing feedback
            ActiveFeedback existingFeedback = activeFeedbacks[index];

            if (existingFeedback.enemyFeedback == enemyFeedback)
            {
                // If the feedback is the same, reset the elapsed time
                existingFeedback.settings = settings;
                existingFeedback.elapsed = 0f;
                existingFeedback.onComplete = onComplete;
                activeFeedbacks[enemyFeedback.feedbackIndex] = existingFeedback;
                enemyFeedback.inFeedback = true;
                return;
            }
        }

        enemyFeedback.inFeedback = true;
        enemyFeedback.feedbackIndex = activeFeedbacks.Count;

        ActiveFeedback activeFeedback = new ActiveFeedback 
        { 
            enemyFeedback = enemyFeedback, 
            settings = settings, 
            elapsed = 0f,
            onComplete = onComplete
        };

        activeFeedbacks.Add(activeFeedback);
    }

    public void Remove(EnemyFeedback enemyFeedback)
    {
        if (enemyFeedback == null || !enemyFeedback.inFeedback) 
            return;

        int index = enemyFeedback.feedbackIndex;
        bool validIndex = index >= 0 && index < activeFeedbacks.Count;
        bool validFeedback = validIndex && activeFeedbacks[index].enemyFeedback == enemyFeedback;

        // If index is out of bounds, or points to wrong enemy feedback
        if (!validFeedback)
        {
            Debug.LogError($"EnemyFeedbackManager: stale feedbackIndex on {enemyFeedback.name}", enemyFeedback);

            index = -1;

            // Search for the feedback in the list
            for (int i = 0; i < activeFeedbacks.Count; i++)
            {
                if (activeFeedbacks[i].enemyFeedback == enemyFeedback)
                {
                    index = i;
                    break;
                }
            }

            // If still not found, reset tracking and return
            if (index < 0)
            {
                ResetTracking(enemyFeedback);
                return;
            }
        }

        RemoveAtSwap(index);
        ResetTracking(enemyFeedback);
    }

    private void ResetTracking(EnemyFeedback enemyFeedback)
    {
        enemyFeedback.inFeedback = false;
        enemyFeedback.feedbackIndex = -1;

        if (enemyFeedback.feedbackVisual != null)
        {
            enemyFeedback.feedbackVisual.localScale = enemyFeedback.baseScale;
        }
    }

    private void RemoveAtSwap(int index)
    {
        int lastIndex = activeFeedbacks.Count - 1;

        if (index != lastIndex)
        {
            ActiveFeedback movedFeedback = activeFeedbacks[lastIndex];
            activeFeedbacks[index] = movedFeedback;

            if (movedFeedback.enemyFeedback != null)
                movedFeedback.enemyFeedback.feedbackIndex = index;
        }

        activeFeedbacks.RemoveAt(lastIndex);
    }
}