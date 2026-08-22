using UnityEngine;
using System.Collections.Generic;

public class HordeAudioManager : MonoBehaviour
{
    public static HordeAudioManager Instance;

    public bool Initialize(EnemyManager manager)
    {
        if (!Utilities.CreateInstance(ref Instance, this))
            return false;

        return true;
    }
}