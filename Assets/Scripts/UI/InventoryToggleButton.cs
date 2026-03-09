using UnityEngine;

// Opens/closes inventory panel from UI button.
// Integrates with UIPanelManager to handle overlay panel hierarchy.
public class InventoryToggleButton : MonoBehaviour
{
    private const string PanelId = "InventoryPanel";

    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private bool pauseGameplayWhileOpen = true;
    [SerializeField] private bool closeOnStart = true;

    private bool pausedByThisUI;
    private RelicShopToggleButton cachedShopToggleButton;

    private void Start()
    {
        if (inventoryPanel != null)
        {
            EnsureRegistered();

            if (closeOnStart)
                inventoryPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    public void ToggleInventory()
    {
        if (inventoryPanel == null) return;

        SetInventoryOpen(!inventoryPanel.activeSelf);
    }

    public void SetInventoryOpen(bool isOpen)
    {
        if (inventoryPanel == null) return;

        if (isOpen)
            CloseShopIfOpen();

        bool wasOpen = inventoryPanel.activeSelf;
        
        if (UIPanelManager.Instance != null)
        {
            EnsureRegistered();

            if (isOpen)
                UIPanelManager.Instance.OpenPanel(PanelId);
            else
                UIPanelManager.Instance.ClosePanel(PanelId);
        }
        else
        {
            inventoryPanel.SetActive(isOpen);
        }

        HandlePauseState(isOpen);

        if (wasOpen != isOpen)
            SoundManager.Instance?.PlaySfx(isOpen ? GameSfxId.UiOpen : GameSfxId.UiClose);
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        if (inventoryPanel == null || !inventoryPanel.activeSelf)
            return;

        if (state == GameManager.GameState.Gameplay)
            return;

        // Keep panel open when this UI itself put the game in Pause.
        if (state == GameManager.GameState.Pause && pausedByThisUI)
            return;

        SetInventoryOpen(false);
    }

    private void HandlePauseState(bool inventoryOpen)
    {
        if (!pauseGameplayWhileOpen || GameManager.Instance == null)
            return;

        if (inventoryOpen)
        {
            if (GameManager.Instance.CurrentState == GameManager.GameState.Gameplay)
            {
                GameManager.Instance.Pause();
                pausedByThisUI = true;
            }
        }
        else if (pausedByThisUI && GameManager.Instance.CurrentState == GameManager.GameState.Pause)
        {
            GameManager.Instance.Resume();
            pausedByThisUI = false;
        }
    }

    private void EnsureRegistered()
    {
        if (UIPanelManager.Instance != null && inventoryPanel != null)
            UIPanelManager.Instance.RegisterPanel(PanelId, inventoryPanel, UIPanelManager.PanelType.Overlay);
    }

    private void CloseShopIfOpen()
    {
        if (cachedShopToggleButton == null)
            cachedShopToggleButton = FindFirstObjectByType<RelicShopToggleButton>();

        if (cachedShopToggleButton != null && cachedShopToggleButton.IsShopOpen)
            cachedShopToggleButton.SetShopOpen(false);
    }

    public bool IsInventoryOpen => inventoryPanel != null && inventoryPanel.activeSelf;
}
