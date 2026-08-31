using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class RunEndUI : MonoBehaviour
{
    public static RunEndUI Instance;

    [SerializeField] private GameObject runEndUIParent;
    [SerializeField] private Image resultIcon;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Win")]
    [SerializeField] private Sprite winImage;
    [SerializeField] private string[] winTextMessages;

    [Header("Lose")]
    [SerializeField] private Sprite loseImage;
    [SerializeField] private string[] loseTextMessages;

    private bool isEnabled;

    private void OnEnable()
    {
        RunManager.Instance.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        RunManager.Instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);
    }

    private void OnPhaseChanged(RunManager.RunPhase phase)
    {
        if (phase == RunManager.RunPhase.Won || phase == RunManager.RunPhase.Lost)
        {
            EnableUI(phase);
            return;
        }

        DisableUI();
    }

    private void EnableUI(RunManager.RunPhase phase)
    {
        bool won = phase == RunManager.RunPhase.Won;

        if (resultIcon != null)
        {
            Sprite image = won ? winImage : loseImage;
            resultIcon.sprite = image;
        }

        if (resultText != null)
        {
            string message = Utilities.Random(won ? winTextMessages : loseTextMessages);
            resultText.text = message;
        }

        if (runEndUIParent != null)
        {
            runEndUIParent.SetActive(true);
        }

        isEnabled = true;
    }

    private void DisableUI()
    {
        if (runEndUIParent != null)
        {
            runEndUIParent.SetActive(false);
        }

        isEnabled = false;
    }
}