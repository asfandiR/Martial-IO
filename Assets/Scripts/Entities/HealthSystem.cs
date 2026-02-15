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

    public float MaxHp => maxHp;
    public float CurrentHp { get; private set; }
    public bool IsDead { get; private set; }

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

        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        OnDamage?.Invoke(amount);

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
}
