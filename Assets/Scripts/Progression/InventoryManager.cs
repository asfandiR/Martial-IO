using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Stores permanent relic inventory and applies passive relic effects.
public class InventoryManager : MonoBehaviour
{
    [SerializeField] private List<RelicData> allRelics = new List<RelicData>(256);

    private const string SaveKey = "meta_relic_inventory_v1";

    [Serializable]
    private class InventorySaveData
    {
        public List<string> ownedRelicIds = new List<string>();
    }

    public static InventoryManager Instance { get; private set; }
    public IReadOnlyList<RelicData> OwnedRelics => ownedRelics;

    public event Action OnInventoryChanged;

    private readonly List<RelicData> ownedRelics = new List<RelicData>(128);
    private readonly HashSet<string> ownedRelicIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> runtimeAppliedRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private int currentPlayerInstanceId = int.MinValue;
    private InventorySaveData saveData = new InventorySaveData();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadRelicCatalogFromResourcesIfNeeded();
        Load();
        RebuildOwnedRelicsFromSave();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        TryApplyOwnedRelicsToCurrentPlayer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public bool AddRelic(RelicData relic)
    {
        if (relic == null) return false;

        string relicId = GetRelicId(relic);
        if (string.IsNullOrWhiteSpace(relicId)) return false;
        if (ownedRelicIdSet.Contains(relicId)) return false;

        ownedRelicIdSet.Add(relicId);
        ownedRelics.Add(relic);
        saveData.ownedRelicIds.Add(relicId);
        Save();

        ApplySingleRelicIfPossible(relic, relicId);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool HasRelic(RelicData relic)
    {
        if (relic == null) return false;
        string relicId = GetRelicId(relic);
        return !string.IsNullOrWhiteSpace(relicId) && ownedRelicIdSet.Contains(relicId);
    }

    public bool HasRelicId(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return false;
        return ownedRelicIdSet.Contains(relicId.Trim());
    }

    public void RefreshKnownRelics(List<RelicData> relicCatalog)
    {
        allRelics.Clear();
        if (relicCatalog != null)
            allRelics.AddRange(relicCatalog);

        RebuildOwnedRelicsFromSave();
        TryApplyOwnedRelicsToCurrentPlayer(forceReapplyForCurrentPlayer: true);
        OnInventoryChanged?.Invoke();
    }

    public void WipeRelics()
    {
        ownedRelics.Clear();
        ownedRelicIdSet.Clear();
        runtimeAppliedRelicIds.Clear();
        saveData = new InventorySaveData();

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        OnInventoryChanged?.Invoke();
    }

    private void LoadRelicCatalogFromResourcesIfNeeded()
    {
        if (allRelics.Count > 0)
            return;

        RelicData[] loaded = Resources.LoadAll<RelicData>("Relics");
        if (loaded == null || loaded.Length == 0)
            return;

        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null)
                allRelics.Add(loaded[i]);
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

        if (saveData == null)
            saveData = new InventorySaveData();

        if (saveData.ownedRelicIds == null)
            saveData.ownedRelicIds = new List<string>();

        for (int i = 0; i < saveData.ownedRelicIds.Count; i++)
        {
            string id = saveData.ownedRelicIds[i];
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
        for (int i = 0; i < allRelics.Count; i++)
        {
            RelicData relic = allRelics[i];
            if (relic == null) continue;
            if (string.Equals(GetRelicId(relic), relicId, StringComparison.OrdinalIgnoreCase))
                return relic;
        }

        return null;
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

            ApplyRelicToRuntimeTargets(relic, player);
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

        ApplyRelicToRuntimeTargets(relic, player);
        runtimeAppliedRelicIds.Add(relicId);
    }

    private static void ApplyRelicToRuntimeTargets(RelicData relic, PlayerController player)
    {
        if (relic == null || player == null) return;

        var health = player.GetComponent<HealthSystem>();
        var weapon = player.GetComponent<WeaponController>();
        var abilities = player.GetComponent<AbilityManager>();

        if (health != null && relic.maxHpMultiplier > 1f)
            health.SetMaxHp(Mathf.Max(1f, health.MaxHp * relic.maxHpMultiplier), refill: false);

        if (relic.moveSpeedMultiplier != 1f)
            player.MultiplyMoveSpeed(relic.moveSpeedMultiplier);

        if (weapon != null)
        {
            if (relic.damageMultiplier != 1f)
                weapon.MultiplyProjectileDamage(relic.damageMultiplier);

            if (relic.projectileSpeedMultiplier != 1f)
                weapon.MultiplyProjectileSpeed(relic.projectileSpeedMultiplier);

            if (relic.critChanceMultiplier != 1f)
                weapon.MultiplyCritChance(relic.critChanceMultiplier);

            if (relic.critDamageMultiplier != 1f)
                weapon.MultiplyCritMultiplier(relic.critDamageMultiplier);
        }

        if (abilities != null && relic.cooldownMultiplier != 1f)
            abilities.MultiplyCooldown(relic.cooldownMultiplier);
    }

    private void Save()
    {
        if (saveData == null)
            saveData = new InventorySaveData();

        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            saveData = new InventorySaveData();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        saveData = JsonUtility.FromJson<InventorySaveData>(json) ?? new InventorySaveData();
    }
}
