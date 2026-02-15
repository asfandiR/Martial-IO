using UnityEngine;

// Base projectile logic.
// Responsibilities:
// - Movement
// - Piercing
// - Lifetime
public class ProjectileBase : MonoBehaviour
{
    private float speed;
    private float damage;
    private float critChance;
    private float critMultiplier;
    private int pierceLeft;
    private float lifetime;
    private float lifeTimer;
    private Transform owner;

    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;
    private float lifetimeMultiplier = 1f;
    private float critChanceMultiplier = 1f;
    private float critDamageMultiplier = 1f;
    private float pierceMultiplier = 1f;

    private Vector3 moveDirection = Vector3.right;

    public void SetCombatMultipliers(
        float damageMul,
        float speedMul,
        float lifetimeMul,
        float critChanceMul,
        float critDamageMul,
        float pierceMul)
    {
        damageMultiplier = Mathf.Max(0.1f, damageMul);
        speedMultiplier = Mathf.Max(0.1f, speedMul);
        lifetimeMultiplier = Mathf.Max(0.1f, lifetimeMul);
        critChanceMultiplier = Mathf.Max(0f, critChanceMul);
        critDamageMultiplier = Mathf.Max(0.1f, critDamageMul);
        pierceMultiplier = Mathf.Max(0.1f, pierceMul);
    }

    public void Init(AbilityData data, Vector3 direction, Transform ownerTransform)
    {
        if (data == null) return;

        speed = Mathf.Max(0.1f, data.projectileSpeed * speedMultiplier);
        damage = Mathf.Max(0f, data.damage * damageMultiplier);
        critChance = Mathf.Clamp01(data.critChance * critChanceMultiplier);
        critMultiplier = Mathf.Max(1f, data.critMultiplier * critDamageMultiplier);
        pierceLeft = Mathf.Max(1, Mathf.RoundToInt(data.pierceCount * pierceMultiplier));
        lifetime = Mathf.Max(0.1f, data.projectileLifetime * lifetimeMultiplier);
        lifeTimer = 0f;
        owner = ownerTransform;

        if (direction.sqrMagnitude > 0.001f)
        {
            moveDirection = direction.normalized;
            moveDirection.z = 0f;

            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void Update()
    {
        transform.position += moveDirection * (speed * Time.deltaTime);

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
            Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.transform.root == owner.root) return;

        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        float finalDamage = damage;
        if (Random.value <= critChance)
            finalDamage *= critMultiplier;

        damageable.TakeDamage(finalDamage);
        pierceLeft -= 1;

        if (pierceLeft <= 0)
            Despawn();
    }

    private void Despawn()
    {
        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }
}
