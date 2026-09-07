using System;
using UnityEngine;

public class RunManager : GameManagerBase
{
    public static RunManager Instance;

    public enum RunPhase
    {
        Unset,
        Idle,
        Spawning,
        Clearing,
        Won,
        Lost
    }

    [SerializeField, ReadOnly] private RunPhase phase;
    [SerializeField] private bool autoStartRun = true;

    [Header("Waves")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private int waveIndex = 0;

    [Header("Lives")]
    [SerializeField, ReadOnly] private int currentLives;
    [SerializeField] private int startingLives = 100;

    [Header("Gold")]
    [SerializeField] private int currentGold = 0;
    [SerializeField] private int startingGold = 200;

    public event Action<RunPhase> OnPhaseChanged;
    public event Action<int> OnLifeLost;
    public event Action<int> OnGoldGained;
    public event Action<int> OnGoldLost;

    protected override bool OnInitialize(GameLevelBootstrap levelBootstrap)
    {
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

    private void Start()
    {
        if (autoStartRun)
            StartRun();
    }

    private void Update()
    {
        if (phase == RunPhase.Clearing)
        {
            if (EnemyManager.Instance.ActiveCount <= 0)
            {
                EndWave();
                return;
            }
        }
    }

    [InspectorButton]
    public void StartRun()
    {
        currentLives = startingLives;
        currentGold = startingGold;
        waveIndex = 0;

        // Update UI
        OnLifeLost?.Invoke(currentLives);
        OnGoldGained?.Invoke(currentGold);

        SetPhase(RunPhase.Idle);
    }

    [InspectorButton]
    public void StartWave()
    {
        if (phase != RunPhase.Idle)
            return;

        waveManager.BeginWave(waveIndex);
        SetPhase(RunPhase.Spawning);
    }

    public void EndWave()
    {
        waveIndex++;
        AddGold(waveManager.CurrentWaveDefinition.goldReward);
        SetPhase(RunPhase.Idle);
    }

    [InspectorButton]
    public void EndRun(bool lost)
    {
        if (lost)
        {
            waveManager.StopSpawning();
            SetPhase(RunPhase.Lost);
        }
        else
        {
            waveManager.StopSpawning();
            SetPhase(RunPhase.Won);
        }
    }

    public void SetPhase(RunPhase next)
    {
        if (phase == next)
            return;

        phase = next;
        OnPhaseChanged?.Invoke(phase);
    }

    public void LoseLife()
    {
        currentLives--;

        if (currentLives <= 0)
        {
            EndRun(true);
        }

        OnLifeLost?.Invoke(currentLives);
    }

    public void AddGold(int gold)
    {
        if (gold <= 0)
            return;

        currentGold += gold;

        OnGoldGained?.Invoke(currentGold);
    }

    public bool TrySpendGold(int gold)
    {
        if (gold < 0)
            return false;

        if (currentGold < gold)
            return false;

        currentGold -= gold;
        OnGoldLost?.Invoke(currentGold);
        return true;
    }
}