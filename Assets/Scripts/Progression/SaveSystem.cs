using System.Collections.Generic;
using UnityEngine;

// Save/load meta progression.
// Responsibilities:
// - JSON or PlayerPrefs
// - Gold and meta-upgrades
public class SaveSystem : MonoBehaviour
{
    private const string SaveKey = "meta_save_v1";

    [System.Serializable]
    private class SaveData
    {
        public int gold;
        public List<string> purchasedUpgrades = new List<string>();
    }

    public int Gold => data.gold;

    private SaveData data = new SaveData();

    private void Awake()
    {
        Load();
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
    }

    public void Wipe()
    {
        data = new SaveData();
        PlayerPrefs.DeleteKey(SaveKey);
    }
}
