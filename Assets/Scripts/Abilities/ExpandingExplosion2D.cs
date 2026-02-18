using System.Collections.Generic;
using UnityEngine;

// Expanding 2D explosion hitbox with one-time damage per target.
[RequireComponent(typeof(CircleCollider2D))]
public class ExpandingExplosion2D : MonoBehaviour
{
    [SerializeField] private CircleCollider2D hitCollider;
    [SerializeField] private int overlapBufferSize = 64;

    private readonly HashSet<int> hitTargetIds = new HashSet<int>(128);
    private Collider2D[] overlapBuffer;

    private Transform ownerRoot;
    private float damage;
    private float startRadius;
    private float targetRadius;
    private float expandDuration;
    private float totalLifetime;
    private float elapsed;
    private int enemyLayerMask;
    private bool initialized;

    private void Awake()
    {
        if (hitCollider == null)
            hitCollider = GetComponent<CircleCollider2D>();

        if (hitCollider != null)
            hitCollider.isTrigger = true;

        overlapBuffer = new Collider2D[Mathf.Max(8, overlapBufferSize)];
    }

    public void Initialize(
        Transform ownerRoot,
        float damage,
        float startRadius,
        float targetRadius,
        float expandDuration,
        float totalLifetime,
        int enemyLayerMask)
    {
        this.ownerRoot = ownerRoot;
        this.damage = Mathf.Max(0f, damage);
        this.startRadius = Mathf.Max(0f, startRadius);
        this.targetRadius = Mathf.Max(this.startRadius, targetRadius);
        this.expandDuration = Mathf.Max(0.01f, expandDuration);
        this.totalLifetime = Mathf.Max(this.expandDuration, totalLifetime);
        this.enemyLayerMask = enemyLayerMask;

        elapsed = 0f;
        initialized = true;
        hitTargetIds.Clear();

        if (hitCollider != null)
            hitCollider.radius = this.startRadius;

        DealDamageInCurrentRadius();
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;

        if (hitCollider != null)
        {
            float t = Mathf.Clamp01(elapsed / expandDuration);
            hitCollider.radius = Mathf.Lerp(startRadius, targetRadius, t);
        }

        DealDamageInCurrentRadius();

        if (elapsed >= totalLifetime)
            Destroy(gameObject);
    }

    private void DealDamageInCurrentRadius()
    {
        float radius = hitCollider != null ? hitCollider.radius : targetRadius;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyLayerMask,
            useTriggers = true
        };

        int count = Physics2D.OverlapCircle((Vector2)transform.position, radius, filter, overlapBuffer);
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = overlapBuffer[i];
            if (col == null)
                continue;

            if (ownerRoot != null && col.transform.root == ownerRoot)
                continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null)
                continue;

            Component targetComponent = damageable as Component;
            if (targetComponent == null)
                continue;

            int id = targetComponent.GetInstanceID();
            if (!hitTargetIds.Add(id))
                continue;

            damageable.TakeDamage(damage);
        }
    }
}
