using UnityEngine;

public class EnemyFeedback : MonoBehaviour
{
    public Transform feedbackVisual;

    [Header("Tracking")]
    public bool inFeedback = false;

    [HideInInspector] public int feedbackIndex = -1;
    [HideInInspector] public Vector3 baseScale = Vector3.one;

    public void PlayFlick()
    {
        EnemyFeedbackManager.Instance.Add(this, EnemyFeedbackManager.FeedbackType.Flick);
    }

    public void StopFeedback()
    {
        EnemyFeedbackManager.Instance.Remove(this);

        // Cleanup
        inFeedback = false;
        feedbackIndex = -1;
    }
}