using UnityEngine;

/// <summary>
/// Auto-initializes UIPanelManager on game start.
/// Ensures the manager exists and is properly configured globally.
/// </summary>
public static class UIPanelManagerInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // If manager doesn't exist, create it
        if (UIPanelManager.Instance == null)
        {
            GameObject managerObj = new GameObject("UIPanelManager");
            managerObj.AddComponent<UIPanelManager>();
            Object.DontDestroyOnLoad(managerObj);
            Debug.Log("[UIPanelManager] Manager initialized and set to persist across scenes");
        }
    }
}
