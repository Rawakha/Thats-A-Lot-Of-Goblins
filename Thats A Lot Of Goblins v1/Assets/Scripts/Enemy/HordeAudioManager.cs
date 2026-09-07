using UnityEngine;

public class HordeAudioManager : MonoBehaviour
{
    public static HordeAudioManager Instance;

    [Header("Hit")]
    [SerializeField] private SoundData hitSound;
    [SerializeField] private float minHitInterval = 0.06f;
    [SerializeField] private int maxHitsPerFrame = 6;

    [Header("Death")]
    [SerializeField] private SoundData deathSound;
    [SerializeField] private float minDeathInterval = 0.08f;

    [Header("Culling")]
    [SerializeField] private Transform listener;
    [SerializeField] private float maxAudibleDistance = 30f;

    private float lastDeathTime = 0f;
    private float lastHitTime = 0f;
    private int hitsThisFrame = 0;

    public bool Initialize(EnemyManager manager)
    {
        if (!Utilities.CreateInstance(ref Instance, this))
            return false;

        return true;
    }

    private void LateUpdate()
    {
        hitsThisFrame = 0;
    }

    public void RequestHit(Vector3 position)
    {
        if (hitsThisFrame >= maxHitsPerFrame) return;
        if (Time.time - lastHitTime < minHitInterval) return;
        if (!IsAudible(position)) return;

        if (!Utilities.TryPlaySound(hitSound, position, additive: true))
            return;

        lastHitTime = Time.time;
        hitsThisFrame++;
    }

    public void RequestDeath(Vector3 position)
    {
        if (Time.time - lastDeathTime < minDeathInterval) return;
        if (!IsAudible(position)) return;

        if (!Utilities.TryPlaySound(deathSound, position, additive: true))
            return;

        lastDeathTime = Time.time;
    }

    private bool IsAudible(Vector3 position)
    {
        if (listener == null) return false;
        return (position - listener.position).sqrMagnitude <= maxAudibleDistance * maxAudibleDistance;
    }
}