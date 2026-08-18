using UnityEngine;

public class RunManager : GameManagerBase
{
    public static RunManager Instance;

    [SerializeField] private RunSettings settings;
    [SerializeField] private WaveManager waveManager;

    [Header("Lives")]
    [SerializeField, ReadOnly] private int currentLives;

    public bool isActive = false;

    protected override bool OnInitialize(GameLevelBootstrap levelBootstrap)
    {
        if (settings == null)
        {
            Debug.LogError("RunManager: RunSettings is not assigned.", this);
            return false;
        }

        if (waveManager == null)
        {
            Debug.LogError("RunManager: WaveManager is not assigned.", this);
            return false;
        }

        if (!Utilities.CreateInstance<RunManager>(ref Instance, this))
        {
            return false;
        }

        return true;
    }

    [InspectorButton]
    public void StartRun()
    {
        currentLives = settings.lives;  
        isActive = true;

        if (waveManager != null)
        {
            waveManager.StartWaves();
        }
    }

    [InspectorButton]
    public void EndRun()
    {
        isActive = false;

        if (waveManager != null)
        {
            waveManager.StopWaves();
        }
    }

    public void LoseLife()
    {
        currentLives--;

        if (currentLives <= 0)
        {
            EndRun();
        }
    }
}