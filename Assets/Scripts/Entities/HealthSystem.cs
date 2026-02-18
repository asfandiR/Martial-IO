using System;
using UnityEngine;

// Health management for entities.
// Responsibilities:
// - TakeDamage / Heal
// - Death events
public class HealthSystem : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHp = 10f;
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private float incomingFlatDamageReduction = 0f;

    public float MaxHp => maxHp;
    public float CurrentHp { get; private set; }
    public bool IsDead { get; private set; }
    public float IncomingFlatDamageReduction => incomingFlatDamageReduction;

    public event Action<float> OnDamage;
    public event Action<float> OnHeal;
    public event Action OnDeath;

    private void Awake()
    {
        CurrentHp = Mathf.Max(1f, maxHp);
        IsDead = false;
    }

    public void SetMaxHp(float value, bool refill)
    {
        maxHp = Mathf.Max(1f, value);
        if (refill)
            CurrentHp = maxHp;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        if (amount <= 0f) return;

        amount = Mathf.Max(0f, amount - incomingFlatDamageReduction);
        if (amount <= 0f) return;

        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        OnDamage?.Invoke(amount);
        if (showDamageNumbers && DamageNumberManager.Instance != null)
            DamageNumberManager.Instance.SpawnDamage(GetDamageNumberPosition(), amount);

        if (CurrentHp <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        if (amount <= 0f) return;

        CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
        OnHeal?.Invoke(amount);
    }

    public void SetIncomingFlatDamageReduction(float amount)
    {
        incomingFlatDamageReduction = Mathf.Max(0f, amount);
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnDeath?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    public void Revive(float hp)
    {
        IsDead = false;
        CurrentHp = Mathf.Clamp(hp, 1f, maxHp);
    }

    private Vector3 GetDamageNumberPosition()
    {
        Collider2D col2D = GetComponentInChildren<Collider2D>();
        if (col2D != null)
            return col2D.bounds.center;

        Collider col3D = GetComponentInChildren<Collider>();
        if (col3D != null)
            return col3D.bounds.center;

        return transform.position;
    }
}
