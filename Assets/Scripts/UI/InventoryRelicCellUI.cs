using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Single cell view for inventory GridLayoutGroup.
public class InventoryRelicCellUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statText;

    public void Bind(RelicData relic)
    {
        if (relic == null)
        {
            if (iconImage != null) iconImage.sprite = null;
            if (nameText != null) nameText.text = "-";
            if (statText != null) statText.text = string.Empty;
            return;
        }

        if (iconImage != null)
            iconImage.sprite = relic.icon;

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(relic.relicName) ? relic.name : relic.relicName;

        if (statText != null)
            statText.text = $"+{InventoryManager.BoostPercentPerRelic:0.#}% {GetStatLabel(relic.boostedStat)}";
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
