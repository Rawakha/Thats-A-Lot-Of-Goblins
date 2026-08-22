using System.Collections.Generic;
using UnityEngine;

public class EnemyAirborneManager : MonoBehaviour
{
    public static EnemyAirborneManager Instance;

    [SerializeField] private float maxAirborneTime = 5f;

    [Header("Contact Settings")]
    [SerializeField] private LayerMask validImpactLayers;

    [Header("Scream")]
    [SerializeField] private SoundConfig screamConfig;
    [SerializeField] private float minScreamSpeed = 2f;
    [SerializeField] private float minHeight = 0f;
    [SerializeField] private float maxHeight = 6f;
    [SerializeField] private float minVolume = 0.1f;

    private static bool subscribed = false;

    private readonly List<Airborne> airborne = new List<Airborne>(256);
    private readonly Dictionary<EntityId, EnemyData> contactEnemies = new Dictionary<EntityId, EnemyData>(256);

    private struct Airborne
    {
        public EnemyData enemy;
        public float startTime;
    }

    public bool Initialize(EnemyManager manager)
    {
        if (!Utilities.CreateInstance<EnemyAirborneManager>(ref Instance, this))
        {
            return false;
        }

        if (!subscribed)
        {
            Physics.ContactEvent += OnContactEvent;
            subscribed = true;
        }

        return true;
    }

    private void OnEnable()
    {
        if (subscribed)
            return;

        Physics.ContactEvent += OnContactEvent;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;

        Physics.ContactEvent -= OnContactEvent;
        subscribed = false;
    }

    private void Update()
    {
        float now = Time.time;

        for (int i = airborne.Count - 1; i >= 0; i--)
        {
            Airborne a = airborne[i];
            EnemyData e = a.enemy;

            if (e == null || e.body == null)
            {
                RemoveAtSwap(i);
                continue;
            }

            // Airborne Timeout
            if (now - a.startTime > maxAirborneTime)
            {
                EnemyStateMachine.Set(e, EnemyState.Dying);
                continue;
            }

            float speedSqr = e.body.linearVelocity.sqrMagnitude;
            if (speedSqr < minScreamSpeed * minScreamSpeed)
            {
                StopScream(e);
                continue;
            }

            if (e.screamHandle != 0)
            {
                float height = e.body.position.y;
                float t = Mathf.InverseLerp(minHeight, maxHeight, height);
                float volume = Mathf.Lerp(minVolume, 1f, t);

                AudioManager.Instance.SetTrackedVolume(e.screamHandle, volume);
            }
        }
    }

    #region Scream

    private void StartScream(EnemyData e)
    {
        if (e == null || e.body == null)
            return;

        int handle = AudioManager.Instance.RequestTracked(screamConfig, e.transform);
        if (handle == 0)
            return;

        e.screamHandle = handle;
    }

    private void StopScream(EnemyData e)
    {
        if (e == null || e.screamHandle == 0)
            return;

        AudioManager.Instance.StopTracked(e.screamHandle);
        e.screamHandle = 0;
    }

    #endregion

    #region Contacts
    private void OnContactEvent(PhysicsScene scene, Unity.Collections.NativeArray<ContactPairHeader>.ReadOnly headerArray)
    {
        for (int i = 0; i < headerArray.Length; i++)
        {
            ContactPairHeader header = headerArray[i];

            if (!TryGetContactEnemy(header, out EnemyData enemy))
                continue;

            for (int j = 0; j < header.pairCount; j++)
            {
                ref readonly ContactPair pair = ref header.GetContactPair(j);
                
                if (!pair.isCollisionEnter)
                    continue;

                if (!IsValidImpactLayer(pair.collider) && !IsValidImpactLayer(pair.otherCollider))
                    continue;

                EnemyStateMachine.Set(enemy, EnemyState.Dying);

                break;
            }
        }
    }

    private bool TryGetContactEnemy(ContactPairHeader header, out EnemyData enemy)
    {
        if (contactEnemies.TryGetValue(header.bodyEntityId, out enemy))
            return true;

        return contactEnemies.TryGetValue(header.otherBodyEntityId, out enemy);
    }

    private bool IsValidImpactLayer(Collider hitCollider)
    {
        if (hitCollider == null) 
            return false;

        int layer = hitCollider.gameObject.layer;
        return (validImpactLayers.value & (1 << layer)) != 0;
    }
    #endregion

    public void Add(EnemyData enemy, float launchSpeed)
    {
        if (enemy == null || enemy.airborneIndex >= 0)
            return;

        airborne.Add(new Airborne
        {
            enemy = enemy,
            startTime = Time.time
        });

        if (enemy.collider != null)
        {
            enemy.collider.providesContacts = true;
            contactEnemies[enemy.body.GetEntityId()] = enemy;
        }

        if (launchSpeed >= minScreamSpeed)
        {
            StartScream(enemy);
        }

        enemy.airborneIndex = airborne.Count - 1;
    }

    public void Remove(EnemyData enemy)
    {
        if (enemy == null || enemy.airborneIndex < 0)
            return;

        int idx = enemy.airborneIndex;

        if (idx >= airborne.Count || airborne[idx].enemy != enemy)
        {
            Debug.LogError($"EnemyAirborneManager: stale airborneIndex on {enemy.name}", enemy);
            for (int i = 0; i < airborne.Count; i++)
            {
                if (airborne[i].enemy == enemy) 
                { 
                    idx = i; break; 
                }
            }
        }

        RemoveAtSwap(idx);
        Cleanup(enemy);
    }

    private void Cleanup(EnemyData enemy)
    {
        if (enemy.collider != null)
        {
            enemy.collider.providesContacts = false;
        }

        contactEnemies.Remove(enemy.body.GetEntityId());
        StopScream(enemy);
        enemy.airborneIndex = -1;
    }

    private void RemoveAtSwap(int index)
    {
        int last = airborne.Count - 1;

        if (index != last)
        {
            Airborne moved = airborne[last];
            airborne[index] = moved;
            if (moved.enemy != null) 
                moved.enemy.airborneIndex = index;
        }

        airborne.RemoveAt(last);
    }
}