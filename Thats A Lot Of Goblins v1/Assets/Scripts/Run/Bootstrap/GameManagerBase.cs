using UnityEngine;

public abstract class GameManagerBase : MonoBehaviour
{
    public bool IsInitialised { get; private set; }

    public bool Initialize(GameLevelBootstrap levelBootstrap)
    {
        if (IsInitialised)
            return true;

        bool success = OnInitialize(levelBootstrap);

        IsInitialised = success;
        return IsInitialised;
    }

    protected abstract bool OnInitialize(GameLevelBootstrap levelBootstrap);
}