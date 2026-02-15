using UnityEngine;

// Facade kept for compatibility with existing references.
[RequireComponent(typeof(ProjectileAttackController))]
[RequireComponent(typeof(SwordOrbitController))]
public class WeaponController : MonoBehaviour
{
    [SerializeField] private ProjectileAttackController projectileController;
    [SerializeField] private SwordOrbitController swordController;

    public float ProjectileDamageMultiplier => projectileController != null ? projectileController.ProjectileDamageMultiplier : 1f;
    public float ProjectileSpeedMultiplier => projectileController != null ? projectileController.ProjectileSpeedMultiplier : 1f;
    public float ProjectileLifetimeMultiplier => projectileController != null ? projectileController.ProjectileLifetimeMultiplier : 1f;
    public float CritChanceMultiplier => projectileController != null ? projectileController.CritChanceMultiplier : 1f;
    public float CritDamageMultiplier => projectileController != null ? projectileController.CritDamageMultiplier : 1f;
    public float PierceMultiplier => projectileController != null ? projectileController.PierceMultiplier : 1f;

    private void Awake()
    {
        if (projectileController == null)
            projectileController = GetComponent<ProjectileAttackController>();
        if (swordController == null)
            swordController = GetComponent<SwordOrbitController>();
    }

    public void SetFireButtonHeld(bool value)
    {
        if (projectileController != null)
            projectileController.SetFireButtonHeld(value);
    }

    public void FireOnceFromButton()
    {
        if (projectileController != null)
            projectileController.FireOnceFromButton();
    }

    public void MultiplyProjectileDamage(float multiplier)
    {
        if (projectileController != null)
            projectileController.MultiplyProjectileDamage(multiplier);
    }

    public void MultiplyProjectileSpeed(float multiplier)
    {
        if (projectileController != null)
            projectileController.MultiplyProjectileSpeed(multiplier);
    }

    public void MultiplyProjectileLifetime(float multiplier)
    {
        if (projectileController != null)
            projectileController.MultiplyProjectileLifetime(multiplier);
    }

    public void MultiplyCritChance(float multiplier)
    {
        if (projectileController != null)
            projectileController.MultiplyCritChance(multiplier);
    }

    public void MultiplyCritMultiplier(float multiplier)
    {
        if (projectileController != null)
            projectileController.MultiplyCritMultiplier(multiplier);
    }

    public void MultiplyPierce(float multiplier)
    {
        if (projectileController != null)
            projectileController.MultiplyPierce(multiplier);
    }

    public void AddSwordSectorAngle(float angleDelta)
    {
        if (swordController != null)
            swordController.AddSwordSectorAngle(angleDelta);
    }

    public void AddSwordRadius(float radiusDelta)
    {
        if (swordController != null)
            swordController.AddSwordRadius(radiusDelta);
    }

    public void HandleAbilityLearned(AbilityData ability)
    {
        if (swordController != null)
            swordController.HandleAbilityLearned(ability);
    }
}
