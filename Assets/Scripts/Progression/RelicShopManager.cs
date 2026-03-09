using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Relic shop runtime:
// - fills visible shop slots with random relic offers
// - shows only rarity on slot buttons
// - shows full data in detail panel on click
// - buys selected relic via one buy button on detail panel
// - integrates with UIPanelManager for overlay hierarchy
public class RelicShopManager : MonoBehaviour
{
    private const string PanelId = "RelicShopPanel";

    [Header("References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private RectTransform shopSlotsRoot;
    [SerializeField] private InventoryRelicCellUI[] shopCells;

    [Header("Detail Island")]
    [SerializeField] private TMP_Text selectedNameText;
    [SerializeField] private TMP_Text selectedStatText;
    [SerializeField] private TMP_Text selectedRarityText;
    [SerializeField] private TMP_Text selectedPriceText;
    [SerializeField] private TMP_Text selectedDescriptionText;
    [SerializeField] private Button buyButton;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Offer Generation")]
    [SerializeField, Min(1)] private int offerCount = 12;
    [SerializeField] private bool refreshOffersOnEnable = true;
    [SerializeField, Min(0f)] private float commonWeight = 50f;
    [SerializeField, Min(0f)] private float rareWeight = 25f;
    [SerializeField, Min(0f)] private float epicWeight = 12.5f;
    [SerializeField, Min(0f)] private float legendaryWeight = 6.25f;

    private readonly List<RelicData> allRelics = new List<RelicData>(256);
    private readonly List<RelicData> currentOffers = new List<RelicData>(32);
    private RelicData selectedRelic;

    private void Awake()
    {
        ResolveInventoryManager();

        ResolveShopCells();

        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(BuySelectedRelic);
            buyButton.onClick.AddListener(BuySelectedRelic);
        }

        LoadAllRelics();
        SetSelectedRelic(null);
    }

    private void Start()
    {
        // Register panel with the manager
        GameObject shopPanel = gameObject;
        if (UIPanelManager.Instance != null)
            UIPanelManager.Instance.RegisterPanel(PanelId, shopPanel, UIPanelManager.PanelType.Overlay);
    }

    private void OnEnable()
    {
        ResolveInventoryManager();

        if (refreshOffersOnEnable)
            GenerateOffers();

        if (inventoryManager != null)
            inventoryManager.OnWalletChanged += HandleWalletChanged;
    }

    private void OnDisable()
    {
        if (inventoryManager != null)
            inventoryManager.OnWalletChanged -= HandleWalletChanged;
    }

    private void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(BuySelectedRelic);

        if (UIPanelManager.Instance != null)
            UIPanelManager.Instance.UnregisterPanel(PanelId);
    }

    public void GenerateOffers()
    {
        ResolveShopCells();
        LoadAllRelics();

        currentOffers.Clear();

        int targetCount = Mathf.Max(1, offerCount);
        int slotCount = shopCells != null ? shopCells.Length : 0;
        int maxOffers = Mathf.Min(targetCount, slotCount);

        for (int i = 0; i < maxOffers; i++)
        {
            RelicData offer = PickRandomOffer(currentOffers);
            currentOffers.Add(offer);
        }

        for (int i = 0; i < slotCount; i++)
        {
            InventoryRelicCellUI cell = shopCells[i];
            if (cell == null) continue;

            RelicData relic = i < currentOffers.Count ? currentOffers[i] : null;
            cell.BindShopOffer(relic, HandleOfferSelected);
        }

        SetSelectedRelic(null);
    }

    public void BuyRelic(int statIndex, int rarityIndex)
    {
        ResolveInventoryManager();

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

    public void BuySelectedRelic()
    {
        ResolveInventoryManager();

        if (inventoryManager == null)
        {
            SetStatus("Inventory not found");
            return;
        }

        if (selectedRelic == null)
        {
            SetStatus("Select a relic first");
            return;
        }

        var result = inventoryManager.TryBuyRelic(selectedRelic);
        SetStatus(BuildResultMessage(result, selectedRelic.boostedStat, selectedRelic.Rarity));

        if (result == InventoryManager.RelicTransactionResult.Success)
        {
            for (int i = 0; i < currentOffers.Count; i++)
            {
                if (currentOffers[i] == selectedRelic)
                {
                    currentOffers[i] = PickRandomOffer(currentOffers);
                    if (shopCells != null && i < shopCells.Length && shopCells[i] != null)
                        shopCells[i].BindShopOffer(currentOffers[i], HandleOfferSelected);
                    break;
                }
            }

            SetSelectedRelic(null);
        }
        else
        {
            RefreshBuyButtonState();
        }
    }

    public void SellRelic(RelicData relic)
    {
        ResolveInventoryManager();

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

    private void ResolveShopCells()
    {
        bool noConfiguredCells = shopCells == null || shopCells.Length == 0;
        if (!noConfiguredCells)
            return;

        if (shopSlotsRoot != null)
        {
            shopCells = shopSlotsRoot.GetComponentsInChildren<InventoryRelicCellUI>(true);
            return;
        }

        shopCells = GetComponentsInChildren<InventoryRelicCellUI>(true);
    }

    private void LoadAllRelics()
    {
        allRelics.Clear();
        RelicData[] loaded = Resources.LoadAll<RelicData>("Relics");
        if (loaded == null || loaded.Length == 0)
            return;

        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null)
                allRelics.Add(loaded[i]);
        }
    }

    private RelicData PickRandomOffer(List<RelicData> exclude)
    {
        RelicData.RelicRarity rarity = RollRarityByWeight();

        var matching = new List<RelicData>(64);
        var fallback = new List<RelicData>(allRelics.Count);

        for (int i = 0; i < allRelics.Count; i++)
        {
            RelicData relic = allRelics[i];
            if (relic == null) continue;
            if (inventoryManager != null && inventoryManager.HasRelic(relic)) continue;
            if (exclude != null && exclude.Contains(relic)) continue;

            fallback.Add(relic);
            if (relic.Rarity == rarity)
                matching.Add(relic);
        }

        if (matching.Count > 0)
            return matching[Random.Range(0, matching.Count)];

        if (fallback.Count > 0)
            return fallback[Random.Range(0, fallback.Count)];

        if (allRelics.Count == 0)
            return null;

        return allRelics[Random.Range(0, allRelics.Count)];
    }

    private RelicData.RelicRarity RollRarityByWeight()
    {
        float c = Mathf.Max(0f, commonWeight);
        float r = Mathf.Max(0f, rareWeight);
        float e = Mathf.Max(0f, epicWeight);
        float l = Mathf.Max(0f, legendaryWeight);
        float total = c + r + e + l;

        if (total <= 0f)
            return RelicData.RelicRarity.Common;

        float roll = Random.value * total;
        if (roll < c) return RelicData.RelicRarity.Common;
        roll -= c;
        if (roll < r) return RelicData.RelicRarity.Rare;
        roll -= r;
        if (roll < e) return RelicData.RelicRarity.Epic;
        return RelicData.RelicRarity.Legendary;
    }

    private void HandleOfferSelected(RelicData relic)
    {
        SetSelectedRelic(relic);
    }

    private void SetSelectedRelic(RelicData relic)
    {
        selectedRelic = relic;
        UpdateDetailPanel();
        RefreshBuyButtonState();
    }

    private void UpdateDetailPanel()
    {
        if (selectedRelic == null)
        {
            if (selectedNameText != null) selectedNameText.text = "-";
            if (selectedStatText != null) selectedStatText.text = string.Empty;
            if (selectedRarityText != null) selectedRarityText.text = string.Empty;
            if (selectedPriceText != null) selectedPriceText.text = string.Empty;
            if (selectedDescriptionText != null) selectedDescriptionText.text = "Select a relic from the shop list.";
            return;
        }

        if (selectedNameText != null)
            selectedNameText.text = GetRelicName(selectedRelic);

        if (selectedStatText != null)
            selectedStatText.text = $"+{selectedRelic.StatBonusPercent:0.#}% {GetStatLabel(selectedRelic.boostedStat)}";

        if (selectedRarityText != null)
            selectedRarityText.text = selectedRelic.Rarity.ToString();

        if (selectedPriceText != null)
            selectedPriceText.text = $"Price: {selectedRelic.PriceCoins}";

        if (selectedDescriptionText != null)
            selectedDescriptionText.text =
                $"{selectedRelic.Rarity} relic\nBoost: +{selectedRelic.StatBonusPercent:0.#}% {GetStatLabel(selectedRelic.boostedStat)}";
    }

    private void RefreshBuyButtonState()
    {
        if (buyButton == null)
            return;

        bool canBuy = selectedRelic != null
            && inventoryManager != null
            && !inventoryManager.HasRelic(selectedRelic)
            && !inventoryManager.IsInventoryFull
            && inventoryManager.WalletCoins >= selectedRelic.PriceCoins;

        buyButton.interactable = canBuy;
    }

    private void HandleWalletChanged(int _)
    {
        RefreshBuyButtonState();
    }

    private void ResolveInventoryManager()
    {
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance ?? FindFirstObjectByType<InventoryManager>();
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
