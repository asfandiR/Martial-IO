using System.Collections.Generic;
using UnityEngine;

public enum AbilityTag
{
    None,
    // Projectile Families
    Crossbowman,
    Archer,
    Aeromancer,
    Cryomancer,
    Druid,
    Pyromancer,
    Warlock,
    // Sword Families
    Swordsman,
    Berserker,
    Paladin,
    Blacksmith,
    Spearman,
    // Special
    Demon,
    Debuff,
    Priest,
    Buff
}

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
    [Header("Tags")]
    public List<AbilityTag> tags = new List<AbilityTag>();

    private void OnValidate()
    {
        if (tags == null) tags = new List<AbilityTag>();
        if (string.IsNullOrWhiteSpace(abilityName)) return;

        AutoTag("Crossbowman", AbilityTag.Crossbowman);
        AutoTag("Archer", AbilityTag.Archer);
        AutoTag("Aeromancer", AbilityTag.Aeromancer);
        AutoTag("Cryomancer", AbilityTag.Cryomancer);
        AutoTag("Druid", AbilityTag.Druid);
        AutoTag("Pyromancer", AbilityTag.Pyromancer);
        AutoTag("Warlock", AbilityTag.Warlock);

        AutoTag("Swordsman", AbilityTag.Swordsman);
        AutoTag("Berserker", AbilityTag.Berserker);
        AutoTag("Paladin", AbilityTag.Paladin);
        AutoTag("Blacksmith", AbilityTag.Blacksmith);
        AutoTag("Spearman", AbilityTag.Spearman);

        AutoTag("Demon", AbilityTag.Demon);
        AutoTag("Debuff", AbilityTag.Debuff);
        AutoTag("Priest", AbilityTag.Priest);
        AutoTag("Buff", AbilityTag.Buff);
    }

    private void AutoTag(string token, AbilityTag tag)
    {
        if (abilityName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (!tags.Contains(tag)) tags.Add(tag);
        }
    }
}
