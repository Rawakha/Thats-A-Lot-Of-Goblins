using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class RunManagerUI : MonoBehaviour
{
    public static RunManagerUI Instance;
    [SerializeField] private GameObject runUIParent;
    [SerializeField] private Button waveStartButton;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI livesText;

    [Header("Enemies")]
    [SerializeField] private GameObject enemiesPanel;
    [SerializeField] private TextMeshProUGUI enemiesText;

    private bool isEnabled;

    private void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnPhaseChanged += OnPhaseChanged;
            RunManager.Instance.OnGoldGained += OnGoldGained;
            RunManager.Instance.OnGoldLost += OnGoldLost;
            RunManager.Instance.OnLifeLost += OnLifeLost;
        }
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnPhaseChanged -= OnPhaseChanged;
            RunManager.Instance.OnGoldGained -= OnGoldGained;
            RunManager.Instance.OnGoldLost -= OnGoldLost;
            RunManager.Instance.OnLifeLost -= OnLifeLost;
        }
    }

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);
    }

    public void OnPhaseChanged(RunManager.RunPhase currentPhase)
    {
        if (currentPhase == RunManager.RunPhase.Won || currentPhase == RunManager.RunPhase.Lost)
        {
            DisableUI();
            return;
        }

        if (currentPhase == RunManager.RunPhase.Idle)
        {
            waveStartButton.gameObject.SetActive(true);
            // enemiesPanel.gameObject.SetActive(false);

            if (!isEnabled) EnableUI();
        }
        else
        {
            waveStartButton.gameObject.SetActive(false);
            // enemiesPanel.gameObject.SetActive(true);
        }
    }

    private void EnableUI()
    {
        if (runUIParent)
        {
            runUIParent.gameObject.SetActive(true);
        }

        isEnabled = true;
    }

    private void DisableUI()
    {
        if (runUIParent)
        {
            runUIParent.gameObject.SetActive(false);
        }

        isEnabled = false;
    }

    private void OnGoldGained(int gold)
    {
        if (goldText == null)
            return;

        goldText.text = gold.ToString();
    }

    private void OnGoldLost(int gold)
    {
        if (goldText == null)
            return;

        goldText.text = gold.ToString();
    }

    private void OnLifeLost(int lives)
    {
        if (livesText == null)
            return;

        livesText.text = lives.ToString();
    }

    public void SetEnemies(int enemies)
    {
        if (enemiesText == null)
            return;

        enemiesText.text = enemies.ToString();
    }
}