using UnityEngine;

[CreateAssetMenu(menuName = "Game/Relic Data")]
public class RelicData : ScriptableObject
{
    public enum RelicRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public enum RelicStatType
    {
        MaxHp,
        SwordSpeed,
        Damage,
        Cooldown,
        ProjectileSpeed,
        CritChance,
        CritDamage
    }

    [SerializeField] private string relicId;
    [SerializeField] private RelicRarity rarity = RelicRarity.Common;
    [SerializeField] private bool autoAssignRarityFromId = true;
    public string relicName;
    public Sprite icon;
    public RelicStatType boostedStat = RelicStatType.Damage;

    public string RelicId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(relicId))
                return relicId.Trim();

            return name;
        }
    }

    public RelicRarity Rarity => autoAssignRarityFromId ? GetStableRarityFromId(RelicId) : rarity;
    public float StatBonusPercent => GetStatBonusPercent(Rarity);
    public int PriceCoins => GetPriceCoins(Rarity);
    public int SellPriceCoins => PriceCoins;

    public static float GetStatBonusPercent(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Rare: return 1f;
            case RelicRarity.Epic: return 2f;
            case RelicRarity.Legendary: return 4f;
            default: return 0.5f;
        }
    }

    public static int GetPriceCoins(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Rare: return 2;
            case RelicRarity.Epic: return 4;
            case RelicRarity.Legendary: return 8;
            default: return 1;
        }
    }

    private static RelicRarity GetStableRarityFromId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return RelicRarity.Common;

        int hash = Mathf.Abs(GetStableHash(id.Trim()));
        switch (hash % 4)
        {
            case 1: return RelicRarity.Rare;
            case 2: return RelicRarity.Epic;
            case 3: return RelicRarity.Legendary;
            default: return RelicRarity.Common;
        }
    }

    private static int GetStableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++)
                hash = (hash * 31) + value[i];
            return hash;
        }
    }
}
