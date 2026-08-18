using System.Collections.Generic;
using UnityEngine;

public class EnemyContactManager : GameManagerBase
{
    public static EnemyContactManager Instance;

    [Header("Contact Settings")]
    [SerializeField] private LayerMask validImpactLayers;

    private static bool subscribed = false;

    private readonly Dictionary<EntityId, Enemy> contactEnemies = new Dictionary<EntityId, Enemy>(256);

    protected override bool OnInitialize(GameLevelBootstrap levelBootstrap)
    {
        if (!Utilities.CreateInstance<EnemyContactManager>(ref Instance, this))
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
        Physics.ContactEvent -= OnContactEvent;
        subscribed = false;
    }

    private void OnContactEvent(PhysicsScene scene, Unity.Collections.NativeArray<ContactPairHeader>.ReadOnly headerArray)
    {
        for (int i = 0; i < headerArray.Length; i++)
        {
            ContactPairHeader header = headerArray[i];

            if (!TryGetContactEnemy(header, out Enemy enemy))
                continue;

            for (int j = 0; j < header.pairCount; j++)
            {
                ref readonly ContactPair pair = ref header.GetContactPair(j);
                
                if (!pair.isCollisionEnter)
                    continue;

                if (!IsValidImpactLayer(pair.collider) && !IsValidImpactLayer(pair.otherCollider))
                    continue;

                enemy.Kill();

                break;
            }
        }
    }

    private bool TryGetContactEnemy(ContactPairHeader header, out Enemy enemy)
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

    public void Add(Enemy enemy)
    {
        if (enemy == null)
            return;

        EntityId bodyID = enemy.body.GetEntityId();
        contactEnemies[bodyID] = enemy;
    }

    public void Remove(Enemy enemy)
    {
        EntityId bodyID = enemy.body.GetEntityId();
        contactEnemies.Remove(bodyID);
    }
}