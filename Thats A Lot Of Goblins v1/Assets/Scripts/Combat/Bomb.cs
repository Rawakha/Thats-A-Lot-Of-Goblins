using System.Collections.Generic;
using UnityEngine;

public class Bomb : Grabbable
{
    [Header("Detonation")]
    [SerializeField] private float armDelay = 0.15f;
    [SerializeField] private float minImpactSpeed = 1.5f;

    [Header("Explosion")]
    [SerializeField] private float blastForce = 40f;
    [SerializeField] private float upwardBias = 0.5f;
    [SerializeField] private float damage = 100f;
    [SerializeField] private float damageRadius = 5f;
    [SerializeField] private float launchRadius = 7f;

    [Header("Feedback")]
    [SerializeField] private SoundConfig armedSound;
    [SerializeField] private SoundData explosionSound;
    [SerializeField] private ParticleSystem explosionParticle;

    [Header("Tracking")]
    [SerializeField, ReadOnly] private bool isArmed;
    [SerializeField, ReadOnly] private bool hasExploded;

    private float armedAt;
    private int armedSoundIndex = -1;

    private readonly List<Enemy> blastHits = new(256);

    private void OnCollisionEnter(Collision collision)
    {
        if (!isArmed || hasExploded) return;
        if (Time.time < armedAt + armDelay) return;

        if (collision.relativeVelocity.sqrMagnitude < minImpactSpeed * minImpactSpeed)
            return;

        Explode(collision.GetContact(0).point);
    }

    protected override void OnReleasedInternal(Vector3 velocity)
    {
        Arm();
    }

    public void Arm()
    {
        if (hasExploded)
            return;

        isArmed = true;
        armedAt = Time.time;

        // Sound
        if (armedSound != null)
        {
            armedSoundIndex = AudioManager.Instance.RequestTracked(armedSound, transform);
        }
    }

    private void Explode(Vector3 origin)
    {
        if (hasExploded)
            return;

        hasExploded = true;
        isArmed = false;

        float radius = Mathf.Max(damageRadius, launchRadius);
        int count = EnemyGrid.Instance.QueryRadius(origin, launchRadius, blastHits);
        EnemyGrid.Instance.DebugDrawQuery(origin, launchRadius);

        for (int i = 0; i < count; i++)
        {
            Enemy e = blastHits[i];
            if (e == null || e.state != EnemyState.Walking) 
                continue;

            Vector3 away = e.body.worldCenterOfMass - origin;
            float distance = away.magnitude;

            away.y = 0f;
            if (away.sqrMagnitude < 0.001f)
            {
                away = Random.insideUnitSphere;
                away.y = 0f;
            }
            away.Normalize();

            if (distance <= damageRadius)
            {
                float falloff = 1f - Mathf.Clamp01(distance / damageRadius);
                EnemyHealthManager.Instance.Damage(e, damage * falloff, origin);
            }
            else if (distance <= launchRadius)
            {
                float falloff = 1f - Mathf.Clamp01(distance / launchRadius);
                Vector3 dir = (away + Vector3.up * upwardBias).normalized;
                EnemyStateMachine.Launch(e, dir, blastForce * falloff);
            }
        }

        PlayFeedback(origin);
        gameObject.SetActive(false);
    }

    private void PlayFeedback(Vector3 origin)
    {
        if (explosionParticle != null)
        {
            ParticleSystem e = Instantiate(explosionParticle, origin, Quaternion.identity);
            e.Play();
        }

        if (armedSoundIndex != -1)
        {
            AudioManager.Instance.StopTracked(armedSoundIndex);
        }

        Utilities.TryPlaySound(explosionSound, origin, additive: true);
    }
}