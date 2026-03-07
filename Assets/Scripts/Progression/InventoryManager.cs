using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Stores permanent relic inventory and applies passive relic effects.
public class InventoryManager : MonoBehaviour
{
    public const float BoostPercentPerRelic = 0.5f;
    public enum RelicTransactionResult
    {
        Success,
        InvalidRelic,
        AlreadyOwned,
        InventoryFull,
        NotEnoughCoins,
        NoMatchingRelic
    }

    public static InventoryManager Instance { get; private set; }
    public IReadOnlyList<RelicData> OwnedRelics => ownedRelics;
    public int MaxRelicCapacity => Mathf.Max(1, maxRelicCapacity);
    public int CurrentRelicCount => ownedRelics.Count;
    public int RemainingRelicCapacity => Mathf.Max(0, MaxRelicCapacity - ownedRelics.Count);
    public bool IsInventoryFull => ownedRelics.Count >= MaxRelicCapacity;
    public int WalletCoins
    {
        get
        {
            ResolveSaveSystem();
            return saveSystem != null ? saveSystem.Gold : 0;
        }
    }

    public event Action OnInventoryChanged;
    public event Action<int> OnWalletChanged;

    private readonly List<RelicData> ownedRelics = new List<RelicData>(128);
    private readonly HashSet<string> ownedRelicIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> runtimeAppliedRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RelicData> relicById = new Dictionary<string, RelicData>(StringComparer.OrdinalIgnoreCase);

    [SerializeField] private SaveSystem saveSystem;
    [SerializeField, Min(1)] private int maxRelicCapacity = 24;
    private int currentPlayerInstanceId = int.MinValue;
    private bool goldEventSubscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveSaveSystem();

        BuildRelicLookupFromResources();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RebuildOwnedRelicsFromSave();
        TryApplyOwnedRelicsToCurrentPlayer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (saveSystem != null)
        {
            saveSystem.OnGoldChanged -= HandleGoldChanged;
            goldEventSubscribed = false;
        }
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public bool AddRelic(RelicData relic)
    {
        return TryAddRelic(relic) == RelicTransactionResult.Success;
    }

    public RelicTransactionResult TryAddRelic(RelicData relic)
    {
        if (relic == null) return RelicTransactionResult.InvalidRelic;
        if (ownedRelics.Contains(relic)) return RelicTransactionResult.AlreadyOwned;

        string relicId = GetRelicId(relic);
        if (string.IsNullOrWhiteSpace(relicId)) return RelicTransactionResult.InvalidRelic;
        if (ownedRelicIdSet.Contains(relicId)) return RelicTransactionResult.AlreadyOwned;
        if (IsInventoryFull) return RelicTransactionResult.InventoryFull;

        ownedRelicIdSet.Add(relicId);
        ownedRelics.Add(relic);
        ResolveSaveSystem();
        saveSystem?.AddOwnedRelicId(relicId);

        ApplySingleRelicIfPossible(relic, relicId);
        OnInventoryChanged?.Invoke();
        return RelicTransactionResult.Success;
    }

    public RelicTransactionResult TryBuyRelic(RelicData.RelicStatType stat, RelicData.RelicRarity rarity)
    {
        RelicData relic = FindShopCandidate(stat, rarity);
        if (relic == null)
            return RelicTransactionResult.NoMatchingRelic;

        return TryBuyRelic(relic);
    }

    public RelicTransactionResult TryBuyRelic(RelicData relic)
    {
        if (relic == null)
            return RelicTransactionResult.InvalidRelic;

        if (HasRelic(relic))
            return RelicTransactionResult.AlreadyOwned;

        if (IsInventoryFull)
            return RelicTransactionResult.InventoryFull;

        ResolveSaveSystem();
        if (saveSystem == null)
            return RelicTransactionResult.NotEnoughCoins;

        int cost = Mathf.Max(0, relic.PriceCoins);
        if (!saveSystem.SpendGold(cost))
            return RelicTransactionResult.NotEnoughCoins;

        RelicTransactionResult addResult = TryAddRelic(relic);
        if (addResult != RelicTransactionResult.Success)
        {
            if (cost > 0)
                saveSystem.AddGold(cost);
            return addResult;
        }

        return RelicTransactionResult.Success;
    }

    public bool SellRelic(RelicData relic)
    {
        if (relic == null) return false;

        string relicId = GetRelicId(relic);
        if (string.IsNullOrWhiteSpace(relicId)) return false;
        if (!ownedRelicIdSet.Remove(relicId)) return false;

        for (int i = ownedRelics.Count - 1; i >= 0; i--)
        {
            if (ownedRelics[i] == null) continue;
            if (string.Equals(GetRelicId(ownedRelics[i]), relicId, StringComparison.OrdinalIgnoreCase))
            {
                ownedRelics.RemoveAt(i);
                break;
            }
        }

        ResolveSaveSystem();
        saveSystem?.ClearOwnedRelics();
        if (saveSystem != null)
        {
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                RelicData owned = ownedRelics[i];
                if (owned == null) continue;
                saveSystem.AddOwnedRelicId(GetRelicId(owned));
            }

            int sellValue = Mathf.Max(0, relic.SellPriceCoins);
            if (sellValue > 0)
                saveSystem.AddGold(sellValue);
        }

        RemoveSingleRelicIfPossible(relic, relicId);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool HasRelic(RelicData relic)
    {
        if (relic == null) return false;
        return ownedRelics.Contains(relic);
    }

    public bool HasRelicId(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return false;
        return ownedRelicIdSet.Contains(relicId.Trim());
    }

    public int GetOwnedCountForStat(RelicData.RelicStatType stat)
    {
        int count = 0;
        for (int i = 0; i < ownedRelics.Count; i++)
        {
            RelicData relic = ownedRelics[i];
            if (relic == null) continue;
            if (relic.boostedStat == stat)
                count++;
        }

        return count;
    }

    public float GetTotalBoostPercent(RelicData.RelicStatType stat)
    {
        float total = 0f;
        for (int i = 0; i < ownedRelics.Count; i++)
        {
            RelicData relic = ownedRelics[i];
            if (relic == null || relic.boostedStat != stat) continue;
            total += relic.StatBonusPercent;
        }

        return total;
    }

    public void WipeRelics()
    {
        ownedRelics.Clear();
        ownedRelicIdSet.Clear();
        runtimeAppliedRelicIds.Clear();
        ResolveSaveSystem();
        saveSystem?.ClearOwnedRelics();
        OnInventoryChanged?.Invoke();
    }

    private void BuildRelicLookupFromResources()
    {
        relicById.Clear();

        RelicData[] loaded = Resources.LoadAll<RelicData>("Relics");
        if (loaded == null || loaded.Length == 0)
            return;

        for (int i = 0; i < loaded.Length; i++)
        {
            RelicData relic = loaded[i];
            if (relic == null) continue;

            string id = GetRelicId(relic);
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!relicById.ContainsKey(id))
                relicById.Add(id, relic);
        }
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        currentPlayerInstanceId = int.MinValue;
        runtimeAppliedRelicIds.Clear();
        TryApplyOwnedRelicsToCurrentPlayer();
    }

    private void RebuildOwnedRelicsFromSave()
    {
        ownedRelics.Clear();
        ownedRelicIdSet.Clear();
        ResolveSaveSystem();

        IReadOnlyList<string> savedRelicIds = saveSystem != null
            ? saveSystem.GetOwnedRelicIds()
            : Array.Empty<string>();

        for (int i = 0; i < savedRelicIds.Count; i++)
        {
            string id = savedRelicIds[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            id = id.Trim();

            if (!ownedRelicIdSet.Add(id))
                continue;

            RelicData relic = FindRelicById(id);
            if (relic != null)
                ownedRelics.Add(relic);
        }
    }

    private RelicData FindRelicById(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return null;

        if (relicById.Count == 0)
            BuildRelicLookupFromResources();

        relicById.TryGetValue(relicId.Trim(), out RelicData relic);
        return relic;
    }

    private static string GetRelicId(RelicData relic)
    {
        if (relic == null) return string.Empty;
        return relic.RelicId;
    }

    private void TryApplyOwnedRelicsToCurrentPlayer(bool forceReapplyForCurrentPlayer = false)
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        int instanceId = player.GetInstanceID();
        if (forceReapplyForCurrentPlayer || instanceId != currentPlayerInstanceId)
        {
            currentPlayerInstanceId = instanceId;
            runtimeAppliedRelicIds.Clear();
        }

        for (int i = 0; i < ownedRelics.Count; i++)
        {
            RelicData relic = ownedRelics[i];
            if (relic == null) continue;

            string relicId = GetRelicId(relic);
            if (string.IsNullOrWhiteSpace(relicId) || runtimeAppliedRelicIds.Contains(relicId))
                continue;

            ApplyRelicToRuntimeTargets(relic, player, apply: true);
            runtimeAppliedRelicIds.Add(relicId);
        }
    }

    private void ApplySingleRelicIfPossible(RelicData relic, string relicId)
    {
        if (relic == null || string.IsNullOrWhiteSpace(relicId))
            return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        int instanceId = player.GetInstanceID();
        if (instanceId != currentPlayerInstanceId)
        {
            currentPlayerInstanceId = instanceId;
            runtimeAppliedRelicIds.Clear();
        }

        if (runtimeAppliedRelicIds.Contains(relicId))
            return;

        ApplyRelicToRuntimeTargets(relic, player, apply: true);
        runtimeAppliedRelicIds.Add(relicId);
    }

    private void RemoveSingleRelicIfPossible(RelicData relic, string relicId)
    {
        if (relic == null || string.IsNullOrWhiteSpace(relicId))
            return;

        if (!runtimeAppliedRelicIds.Contains(relicId))
            return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            runtimeAppliedRelicIds.Remove(relicId);
            return;
        }

        ApplyRelicToRuntimeTargets(relic, player, apply: false);
        runtimeAppliedRelicIds.Remove(relicId);
    }

    private static void ApplyRelicToRuntimeTargets(RelicData relic, PlayerController player, bool apply)
    {
        if (relic == null || player == null) return;

        var health = player.GetComponent<HealthSystem>();
        var weapon = player.GetComponent<WeaponController>();
        var abilities = player.GetComponent<AbilityManager>();
        float delta = Mathf.Max(0f, relic.StatBonusPercent * 0.01f);
        float upMultiplier = 1f + delta;
        float cooldownMultiplier = Mathf.Max(0.01f, 1f - delta);
        float damageHpMultiplier = 1f + delta * 4f;

        float finalUpMultiplier = apply ? upMultiplier : (1f / upMultiplier);
        float finalCooldownMultiplier = apply ? cooldownMultiplier : (1f / cooldownMultiplier);
        float finalHpMultiplier = apply ? damageHpMultiplier : (1f / damageHpMultiplier);

        switch (relic.boostedStat)
        {
            case RelicData.RelicStatType.MaxHp:
                if (health != null)
                    health.SetMaxHp(Mathf.Max(1f, health.MaxHp * finalHpMultiplier), refill: false);
                break;
            case RelicData.RelicStatType.SwordSpeed:
                if (weapon != null)
                    weapon.MultiplySwordOrbitSpeed(finalUpMultiplier);
                break;
            case RelicData.RelicStatType.Damage:
                if (weapon != null)
                {
                    weapon.MultiplyProjectileDamage(finalUpMultiplier);
                    weapon.MultiplySwordDamage(finalUpMultiplier);
                }
                break;
            case RelicData.RelicStatType.Cooldown:
                if (abilities != null)
                    abilities.MultiplyCooldown(Mathf.Max(0.1f, finalCooldownMultiplier));
                break;
            case RelicData.RelicStatType.ProjectileSpeed:
                if (weapon != null)
                    weapon.MultiplyProjectileSpeed(finalUpMultiplier);
                break;
            case RelicData.RelicStatType.CritChance:
                if (weapon != null)
                    weapon.MultiplyCritChance(finalUpMultiplier);
                break;
            case RelicData.RelicStatType.CritDamage:
                if (weapon != null)
                    weapon.MultiplyCritMultiplier(finalUpMultiplier);
                break;
        }
    }

    private RelicData FindShopCandidate(RelicData.RelicStatType stat, RelicData.RelicRarity rarity)
    {
        if (relicById.Count == 0)
            BuildRelicLookupFromResources();

        foreach (var kvp in relicById)
        {
            RelicData relic = kvp.Value;
            if (relic == null) continue;
            if (relic.boostedStat != stat) continue;
            if (relic.Rarity != rarity) continue;
            if (HasRelic(relic)) continue;
            return relic;
        }

        return null;
    }

    private void ResolveSaveSystem()
    {
        if (saveSystem == null)
            saveSystem = SaveSystem.Instance ?? FindFirstObjectByType<SaveSystem>();

        if (saveSystem != null && !goldEventSubscribed)
        {
            saveSystem.OnGoldChanged += HandleGoldChanged;
            goldEventSubscribed = true;
        }
    }

    private void HandleGoldChanged(int value)
    {
        OnWalletChanged?.Invoke(value);
    }
}
