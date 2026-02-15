using UnityEngine;

// ScriptableObject for ability configuration.
// Fields: Icon, Damage, Cooldown, etc.
[CreateAssetMenu(menuName = "Game/Ability Data")]
public class AbilityData : ScriptableObject
{
    public enum AbilityRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public string abilityName;
    public Sprite icon;
    public AbilityRarity rarity = AbilityRarity.Common;
    public float damage = 1f;
    public float cooldown = 1f;
    public float projectileSpeed = 10f;
    public int pierceCount = 1;
    public float projectileLifetime = 3f;
    [Range(0f, 1f)] public float critChance = 0.1f;
    [Min(1f)] public float critMultiplier = 2f;
    public GameObject projectilePrefab;
}
