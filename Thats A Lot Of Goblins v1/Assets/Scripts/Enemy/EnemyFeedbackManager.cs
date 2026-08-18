using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class EnemyFeedbackManager : MonoBehaviour
{
    public static EnemyFeedbackManager Instance;

    public enum FeedbackType
    {
        Flick
    }

    private struct ActiveFeedback
    {
        public EnemyFeedback enemyFeedback;
        public FeedbackSettings settings;
        public float elapsed;
    }

    [System.Serializable]
    public struct FeedbackSettings
    {
        public FeedbackType type;
        public SquishSettings squishSettings;
    }

    [System.Serializable]
    public struct SquishSettings
    {
        [Min(0.01f)] public float duration;
        [Range(0f, 0.9f)] public float verticalStrength;
        [Range(0f, 0.9f)] public float horizontalStrength;

        public AnimationCurve shape;
    }

    [Header("Feedbacks")]
    [SerializeField] private FeedbackSettings flickFeedback;

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
            EnemyFeedback enemyFeedback = active.enemyFeedback;

            if (enemyFeedback == null || enemyFeedback.feedbackVisual == null)
            {
                RemoveAtSwap(i);
                continue;
            }

            active.elapsed += dt;

            // Squish
            SquishSettings squishSettings = active.settings.squishSettings;
            float duration = squishSettings.duration;
            float normalizedTime = Mathf.Clamp01(active.elapsed / duration);
            float curveValue = squishSettings.shape != null ?  squishSettings.shape.Evaluate(normalizedTime) : 0f;

            float horizontalScale = 1f - curveValue * squishSettings.horizontalStrength;
            float verticalScale = 1f + curveValue * squishSettings.verticalStrength;
            Vector3 multiplier = new Vector3(Mathf.Max(0.05f, horizontalScale), Mathf.Max(0.05f, verticalScale), Mathf.Max(0.05f, horizontalScale));

            enemyFeedback.feedbackVisual.localScale = Vector3.Scale(enemyFeedback.baseScale, multiplier);

            if (normalizedTime >= 1f)
            {
                Remove(enemyFeedback);
            }
            else
            {
                // ActiveFeedback is a struct, so assign it back.
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

            default:
                return flickFeedback;
        }
    }

    public void Add(EnemyFeedback enemyFeedback, FeedbackType type)
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
            elapsed = 0f 
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