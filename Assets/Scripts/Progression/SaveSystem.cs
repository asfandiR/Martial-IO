using System.Collections.Generic;
using UnityEngine;

// Save/load meta progression.
// Responsibilities:
// - JSON or PlayerPrefs
// - Gold and meta-upgrades
public class SaveSystem : MonoBehaviour
{
    private const string SaveKey = "meta_save_v1";
    public static SaveSystem Instance { get; private set; }

    [SerializeField] private bool cleanPrefs = false;

    [System.Serializable]
    private class SaveData
    {
        public int gold;
        public int bestScore;
        public List<string> purchasedUpgrades = new List<string>();
        public List<string> ownedRelicIds = new List<string>();
    }

    public int Gold => data.gold;
    public int BestScore => data.bestScore;

    private SaveData data = new SaveData();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        data.gold += amount;
        Save();
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (data.gold < amount) return false;

        data.gold -= amount;
        Save();
        return true;
    }

    public bool IsUpgradePurchased(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId)) return false;
        return data.purchasedUpgrades.Contains(upgradeId);
    }

    public bool PurchaseUpgrade(string upgradeId, int cost)
    {
        if (string.IsNullOrWhiteSpace(upgradeId)) return false;
        if (IsUpgradePurchased(upgradeId)) return false;
        if (!SpendGold(cost)) return false;

        data.purchasedUpgrades.Add(upgradeId);
        Save();
        return true;
    }

    public bool TrySetBestScore(int score)
    {
        if (score <= data.bestScore) return false;
        data.bestScore = score;
        Save();
        return true;
    }

    public bool AddOwnedRelicId(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return false;
        relicId = relicId.Trim();

        if (data.ownedRelicIds.Contains(relicId))
            return false;

        data.ownedRelicIds.Add(relicId);
        Save();
        return true;
    }

    public void ClearOwnedRelics()
    {
        data.ownedRelicIds.Clear();
        Save();
    }

    public IReadOnlyList<string> GetOwnedRelicIds()
    {
        return data.ownedRelicIds;
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            data = new SaveData();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        if (data.purchasedUpgrades == null)
            data.purchasedUpgrades = new List<string>();
        if (data.ownedRelicIds == null)
            data.ownedRelicIds = new List<string>();
    }

    public void Wipe()
    {
        data = new SaveData();
        PlayerPrefs.DeleteKey(SaveKey);
    }
    private void OnValidate()
    {
        if (cleanPrefs)
        {
            cleanPrefs = false;
            Wipe();
        }
    }
}
