using System.Collections.Generic;
using UnityEngine;

// Global UI panel manager.
// Overlay panels are mutually exclusive; system panels are controlled externally.
public class UIPanelManager : MonoBehaviour
{
    public enum PanelType
    {
        Overlay = 0,
        System = 1
    }

    private static UIPanelManager instance;
    private static bool isShuttingDown;

    public static UIPanelManager Instance
    {
        get
        {
            if (isShuttingDown)
                return null;

            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<UIPanelManager>();
            if (instance != null)
                return instance;

            GameObject managerObject = new GameObject("UIPanelManager");
            instance = managerObject.AddComponent<UIPanelManager>();
            return instance;
        }
        private set => instance = value;
    }

    private class PanelEntry
    {
        public GameObject panel;
        public PanelType type;
    }

    [SerializeField] private bool logDebugInfo;

    private readonly Dictionary<string, PanelEntry> registeredPanels = new Dictionary<string, PanelEntry>(32);
    private readonly List<string> openOrder = new List<string>(32);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    public void RegisterPanel(string panelId, GameObject panel, PanelType type = PanelType.Overlay)
    {
        if (string.IsNullOrWhiteSpace(panelId) || panel == null)
        {
            Debug.LogWarning($"[UIPanelManager] Register failed. panelId='{panelId}', panel={panel}");
            return;
        }

        if (registeredPanels.TryGetValue(panelId, out PanelEntry existingEntry))
        {
            // Allow refresh/rebind for the same id to avoid stale registration.
            existingEntry.panel = panel;
            existingEntry.type = type;

            if (panel.activeSelf)
                MarkAsOpened(panelId);
            else
                RemoveFromOpenOrder(panelId);

            if (logDebugInfo)
                Debug.Log($"[UIPanelManager] Updated panel registration: {panelId} ({type})");
            return;
        }

        registeredPanels.Add(panelId, new PanelEntry
        {
            panel = panel,
            type = type
        });

        if (panel.activeSelf)
            MarkAsOpened(panelId);

        if (logDebugInfo)
            Debug.Log($"[UIPanelManager] Registered panel: {panelId} ({type})");
    }

    public void UnregisterPanel(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
            return;

        if (!registeredPanels.Remove(panelId))
            return;

        RemoveFromOpenOrder(panelId);

        if (logDebugInfo)
            Debug.Log($"[UIPanelManager] Unregistered panel: {panelId}");
    }

    public void OpenPanel(string panelId)
    {
        if (!TryGetValidEntry(panelId, out PanelEntry entry))
            return;

        if (entry.type == PanelType.Overlay)
            CloseAllOverlayPanels(panelId);

        if (!entry.panel.activeSelf)
            entry.panel.SetActive(true);

        MarkAsOpened(panelId);

        if (logDebugInfo)
            Debug.Log($"[UIPanelManager] Opened panel: {panelId}");
    }

    public void ClosePanel(string panelId)
    {
        if (!TryGetValidEntry(panelId, out PanelEntry entry))
            return;

        if (entry.panel.activeSelf)
            entry.panel.SetActive(false);

        RemoveFromOpenOrder(panelId);

        if (logDebugInfo)
            Debug.Log($"[UIPanelManager] Closed panel: {panelId}");
    }

    public void TogglePanel(string panelId)
    {
        if (!TryGetValidEntry(panelId, out PanelEntry entry))
            return;

        if (entry.panel.activeSelf)
            ClosePanel(panelId);
        else
            OpenPanel(panelId);
    }

    public void CloseAllOverlayPanels(string excludePanelId = null)
    {
        List<string> panelIds = new List<string>(registeredPanels.Keys);
        for (int i = 0; i < panelIds.Count; i++)
        {
            string panelId = panelIds[i];
            if (!TryGetValidEntry(panelId, out PanelEntry entry))
                continue;

            if (entry.type != PanelType.Overlay)
                continue;

            if (!string.IsNullOrEmpty(excludePanelId) && panelId == excludePanelId)
                continue;

            if (!entry.panel.activeSelf)
            {
                RemoveFromOpenOrder(panelId);
                continue;
            }

            entry.panel.SetActive(false);
            RemoveFromOpenOrder(panelId);

            if (logDebugInfo)
                Debug.Log($"[UIPanelManager] Auto-closed overlay: {panelId}");
        }
    }

    public void CloseAllPanelsOfType(PanelType type)
    {
        List<string> panelIds = new List<string>(registeredPanels.Keys);
        for (int i = 0; i < panelIds.Count; i++)
        {
            string panelId = panelIds[i];
            if (!TryGetValidEntry(panelId, out PanelEntry entry))
                continue;

            if (entry.type != type)
                continue;

            if (entry.panel.activeSelf)
                entry.panel.SetActive(false);

            RemoveFromOpenOrder(panelId);
        }
    }

    public bool IsPanelActive(string panelId)
    {
        return TryGetValidEntry(panelId, out PanelEntry entry) && entry.panel.activeSelf;
    }

    public string GetActiveOverlayPanelId()
    {
        for (int i = openOrder.Count - 1; i >= 0; i--)
        {
            string panelId = openOrder[i];
            if (!TryGetValidEntry(panelId, out PanelEntry entry))
                continue;

            if (entry.type == PanelType.Overlay && entry.panel.activeSelf)
                return panelId;
        }

        return null;
    }

    public void CloseLastPanel()
    {
        for (int i = openOrder.Count - 1; i >= 0; i--)
        {
            string panelId = openOrder[i];
            if (!TryGetValidEntry(panelId, out PanelEntry entry))
                continue;

            if (!entry.panel.activeSelf)
            {
                RemoveFromOpenOrder(panelId);
                continue;
            }

            ClosePanel(panelId);
            return;
        }
    }

    private bool TryGetValidEntry(string panelId, out PanelEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(panelId))
            return false;

        if (!registeredPanels.TryGetValue(panelId, out entry))
        {
            if (logDebugInfo)
                Debug.LogWarning($"[UIPanelManager] Panel is not registered: {panelId}");
            return false;
        }

        if (entry.panel != null)
            return true;

        registeredPanels.Remove(panelId);
        RemoveFromOpenOrder(panelId);

        if (logDebugInfo)
            Debug.LogWarning($"[UIPanelManager] Removed stale panel registration: {panelId}");

        entry = null;
        return false;
    }

    private void MarkAsOpened(string panelId)
    {
        RemoveFromOpenOrder(panelId);
        openOrder.Add(panelId);
    }

    private void RemoveFromOpenOrder(string panelId)
    {
        for (int i = openOrder.Count - 1; i >= 0; i--)
        {
            if (openOrder[i] == panelId)
                openOrder.RemoveAt(i);
        }
    }
}
