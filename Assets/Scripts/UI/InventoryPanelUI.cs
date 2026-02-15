using System.Text;
using TMPro;
using UnityEngine;

// Simple inventory text renderer for relic list.
public class InventoryPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text relicListText;

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
        if (relicListText == null)
            return;

        var inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            relicListText.text = "Inventory manager not found.";
            return;
        }

        var owned = inventory.OwnedRelics;
        if (owned == null || owned.Count == 0)
        {
            relicListText.text = "No relics yet.";
            return;
        }

        StringBuilder sb = new StringBuilder(owned.Count * 32);
        for (int i = 0; i < owned.Count; i++)
        {
            var relic = owned[i];
            if (relic == null) continue;
            string shownName = string.IsNullOrWhiteSpace(relic.relicName) ? relic.name : relic.relicName;
            sb.Append(i + 1).Append(". ").AppendLine(shownName);
        }

        relicListText.text = sb.ToString().TrimEnd();
    }

    private void Subscribe()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
    }
}
