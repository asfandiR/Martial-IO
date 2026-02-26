using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Single cell view for inventory GridLayoutGroup.
public class InventoryRelicCellUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button sellButton;

    private RelicData boundRelic;

    private void Awake()
    {
        if (sellButton != null)
        {
            sellButton.onClick.RemoveListener(SellBoundRelic);
            sellButton.onClick.AddListener(SellBoundRelic);
        }
    }

    private void OnDestroy()
    {
        if (sellButton != null)
            sellButton.onClick.RemoveListener(SellBoundRelic);
    }

    public void Bind(RelicData relic)
    {
        boundRelic = relic;

        if (relic == null)
        {
            if (iconImage != null) iconImage.sprite = null;
            if (nameText != null) nameText.text = "-";
            if (statText != null) statText.text = string.Empty;
            if (rarityText != null) rarityText.text = string.Empty;
            if (priceText != null) priceText.text = string.Empty;
            if (sellButton != null) sellButton.interactable = false;
            return;
        }

        if (iconImage != null)
            iconImage.sprite = relic.icon;

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(relic.relicName) ? relic.name : relic.relicName;

        if (statText != null)
            statText.text = $"+{relic.StatBonusPercent:0.#}% {GetStatLabel(relic.boostedStat)}";

        if (rarityText != null)
            rarityText.text = relic.Rarity.ToString();

        if (priceText != null)
            priceText.text = $"Sell: {relic.SellPriceCoins}";

        if (sellButton != null)
            sellButton.interactable = InventoryManager.Instance != null;
    }

    public void SellBoundRelic()
    {
        if (boundRelic == null) return;
        InventoryManager.Instance?.SellRelic(boundRelic);
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
