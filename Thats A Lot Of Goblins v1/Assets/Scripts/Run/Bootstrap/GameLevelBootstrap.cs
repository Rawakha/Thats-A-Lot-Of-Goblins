using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.GameBootstrap)]
public class GameLevelBootstrap : MonoBehaviour
{
    public static GameLevelBootstrap Instance;

    [Header("Core")]
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private RunManager runManager;

    [Header("Initialisation Order")]
    [SerializeField] private GameManagerBase[] managerInitializationOrder;

    private void Awake()
    {
        if (!ValidateManagers())
        {
            AbortBootstrap();
            return;
        }

        InitializeManagers();
    }

    private bool ValidateManagers()
    {
        bool isValid = true;

        if (!ContainsManager(audioManager))
        {
            Debug.LogError("GameBootstrap: AudioManager is not included in managerInitializationOrder.", this);
            isValid = false;
        }

        if (!ContainsManager(enemyManager))
        {
            Debug.LogError("GameBootstrap: EnemyManager is not included in managerInitializationOrder.", this);
            isValid = false;
        }

        if (!ContainsManager(runManager))
        {
            Debug.LogError("GameBootstrap: RunManager is not included in managerInitializationOrder.", this);
            isValid = false;
        }

        if (!ValidateDuplicateManagers())
        {
            isValid = false;
        }

        return isValid;
    }

    private bool ValidateDuplicateManagers()
    {
        bool isValid = true;
        HashSet<GameManagerBase> seen = new HashSet<GameManagerBase>();

        for (int i = 0; i < managerInitializationOrder.Length; i++)
        {
            GameManagerBase manager = managerInitializationOrder[i];

            if (manager == null)
                continue;

            if (!seen.Add(manager))
            {
                Debug.LogError($"GameBootstrap: Duplicate manager in initialization order: {manager.name}", manager);
                isValid = false;
            }
        }

        return isValid;
    }

    private bool InitializeManagers()
    {
        for (int i = 0; i < managerInitializationOrder.Length; i++)
        {
            GameManagerBase manager = managerInitializationOrder[i];
            if (manager == null)
                continue;

            if (!manager.Initialize(this))
            {
                Debug.LogError($"GameBootstrap: Manager failed to initialize: {manager.name}", manager);
                return false;
            }
        }

        return true;
    }

    private void AbortBootstrap()
    {
        Debug.LogError("GameBootstrap: Initialization Failed. Game Startup Aborted", this);

        enabled = false;

#if UNITY_EDITOR
        Debug.Break();
#endif
    }

    private bool ContainsManager(GameManagerBase manager)
    {
        if (manager == null || managerInitializationOrder == null)
            return false;

        for (int i = 0; i < managerInitializationOrder.Length; i++)
        {
            if (managerInitializationOrder[i] == manager)
                return true;
        }

        return false;
    }

    [InspectorButton]
    public void GetManagers()
    {
        if (audioManager == null) audioManager = FindAnyObjectByType<AudioManager>();
        if (enemyManager == null) enemyManager = FindAnyObjectByType<EnemyManager>();
        if (runManager == null) runManager = FindAnyObjectByType<RunManager>();
    }
}