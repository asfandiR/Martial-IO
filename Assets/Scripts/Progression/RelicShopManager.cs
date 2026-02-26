using TMPro;
using UnityEngine;

// Simple shop API for UI buttons: buy relics by stat + rarity using the player's wallet.
public class RelicShopManager : MonoBehaviour
{
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private TMP_Text statusText;

    private void Awake()
    {
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance ?? FindFirstObjectByType<InventoryManager>();
    }

    public void BuyRelic(int statIndex, int rarityIndex)
    {
        if (inventoryManager == null)
        {
            SetStatus("Inventory not found");
            return;
        }

        if (!System.Enum.IsDefined(typeof(RelicData.RelicStatType), statIndex)
            || !System.Enum.IsDefined(typeof(RelicData.RelicRarity), rarityIndex))
        {
            SetStatus("Invalid shop selection");
            return;
        }

        var stat = (RelicData.RelicStatType)statIndex;
        var rarity = (RelicData.RelicRarity)rarityIndex;

        var result = inventoryManager.TryBuyRelic(stat, rarity);
        SetStatus(BuildResultMessage(result, stat, rarity));
    }

    public void SellRelic(RelicData relic)
    {
        if (inventoryManager == null || relic == null)
        {
            SetStatus("Sell failed");
            return;
        }

        bool sold = inventoryManager.SellRelic(relic);
        SetStatus(sold ? $"Sold: {GetRelicName(relic)} (+{relic.SellPriceCoins})" : "Sell failed");
    }

    private string BuildResultMessage(InventoryManager.RelicTransactionResult result, RelicData.RelicStatType stat, RelicData.RelicRarity rarity)
    {
        switch (result)
        {
            case InventoryManager.RelicTransactionResult.Success:
                return $"Bought {rarity} {GetStatLabel(stat)} ({RelicData.GetPriceCoins(rarity)} coins)";
            case InventoryManager.RelicTransactionResult.InventoryFull:
                return "Inventory is full";
            case InventoryManager.RelicTransactionResult.NotEnoughCoins:
                return "Not enough coins";
            case InventoryManager.RelicTransactionResult.NoMatchingRelic:
                return $"No {rarity} {GetStatLabel(stat)} relic available";
            case InventoryManager.RelicTransactionResult.AlreadyOwned:
                return "Relic already owned";
            default:
                return "Purchase failed";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private static string GetRelicName(RelicData relic)
    {
        return string.IsNullOrWhiteSpace(relic.relicName) ? relic.name : relic.relicName;
    }

    private static string GetStatLabel(RelicData.RelicStatType stat)
    {
        switch (stat)
        {
            case RelicData.RelicStatType.MaxHp: return "Max HP";
            case RelicData.RelicStatType.SwordSpeed: return "Sword Speed";
            case RelicData.RelicStatType.Damage: return "Damage";
            case RelicData.RelicStatType.Cooldown: return "Cooldown";
            case RelicData.RelicStatType.ProjectileSpeed: return "Projectile Speed";
            case RelicData.RelicStatType.CritChance: return "Crit Chance";
            case RelicData.RelicStatType.CritDamage: return "Crit Damage";
            default: return stat.ToString();
        }
    }
}
