using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Dynamic relic inventory renderer: expandable grid + total boost summary.
public class InventoryPanelUI : MonoBehaviour
{
    [Header("Left Panel")]
    [SerializeField] private TMP_Text totalBoostText;
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private TMP_Text capacityText;

    [Header("Grid")]
    [SerializeField] private RectTransform gridContent;
    [SerializeField] private InventoryRelicCellUI cellPrefab;
    [SerializeField] private TMP_Text emptyText;

    private readonly List<InventoryRelicCellUI> spawnedCells = new List<InventoryRelicCellUI>(128);

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Refresh()
    {
        var inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            SetText(totalBoostText, "Inventory manager not found.");
            SetText(emptyText, "Inventory manager not found.");
            ClearCells();
            return;
        }

        BuildTotalBoostText(inventory);
        BuildWalletAndCapacityText(inventory);
        BuildGrid(inventory);
    }

    private void BuildTotalBoostText(InventoryManager inventory)
    {
        if (totalBoostText == null)
            return;

        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine("Total Relic Boosts");
        AppendStat(sb, inventory, RelicData.RelicStatType.MaxHp, "Max HP");
        AppendStat(sb, inventory, RelicData.RelicStatType.SwordSpeed, "Sword Speed");
        AppendStat(sb, inventory, RelicData.RelicStatType.Damage, "Damage");
        AppendStat(sb, inventory, RelicData.RelicStatType.Cooldown, "Cooldown");
        AppendStat(sb, inventory, RelicData.RelicStatType.ProjectileSpeed, "Projectile Speed");
        AppendStat(sb, inventory, RelicData.RelicStatType.CritChance, "Crit Chance");
        AppendStat(sb, inventory, RelicData.RelicStatType.CritDamage, "Crit Damage");
        totalBoostText.text = sb.ToString().TrimEnd();
    }

    private static void AppendStat(StringBuilder sb, InventoryManager inventory, RelicData.RelicStatType stat, string label)
    {
        int count = inventory.GetOwnedCountForStat(stat);
        float percent = inventory.GetTotalBoostPercent(stat);
        sb.Append(label).Append(": +").Append(percent.ToString("0.#")).Append("%");
        sb.Append(" (").Append(count).AppendLine(")");
    }

    private void BuildWalletAndCapacityText(InventoryManager inventory)
    {
        if (walletText != null)
            walletText.text = $"Coins: {inventory.WalletCoins}";

        if (capacityText != null)
            capacityText.text = $"Relics: {inventory.CurrentRelicCount}/{inventory.MaxRelicCapacity}";
    }

    private void BuildGrid(InventoryManager inventory)
    {
        ClearCells();

        var owned = inventory.OwnedRelics;
        bool hasAny = owned != null && owned.Count > 0;
        if (emptyText != null)
            emptyText.gameObject.SetActive(!hasAny);

        if (!hasAny || cellPrefab == null || gridContent == null)
            return;

        for (int i = 0; i < owned.Count; i++)
        {
            RelicData relic = owned[i];
            if (relic == null) continue;

            InventoryRelicCellUI cell = Instantiate(cellPrefab, gridContent);
            cell.Bind(relic);
            spawnedCells.Add(cell);
        }
    }

    private void ClearCells()
    {
        for (int i = 0; i < spawnedCells.Count; i++)
        {
            if (spawnedCells[i] != null)
                Destroy(spawnedCells[i].gameObject);
        }

        spawnedCells.Clear();
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    private void Subscribe()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += Refresh;
            InventoryManager.Instance.OnWalletChanged += HandleWalletChanged;
        }
    }

    private void Unsubscribe()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
            InventoryManager.Instance.OnWalletChanged -= HandleWalletChanged;
        }
    }

    private void HandleWalletChanged(int _)
    {
        Refresh();
    }
}
