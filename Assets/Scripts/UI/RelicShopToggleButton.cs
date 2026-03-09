using UnityEngine;

// Opens/closes relic shop panel from UI button.
// Works with UIPanelManager overlay rules.
public class RelicShopToggleButton : MonoBehaviour
{
    private const string PanelId = "RelicShopPanel";

    [SerializeField] private GameObject shopPanel;
    [SerializeField] private bool pauseGameplayWhileOpen = true;
    [SerializeField] private bool closeOnStart = true;

    private bool pausedByThisUI;
    private InventoryToggleButton cachedInventoryToggleButton;

    private void Start()
    {
        if (shopPanel == null)
            return;

        EnsureRegistered();

        if (closeOnStart)
            shopPanel.SetActive(false);
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

    public void ToggleShop()
    {
        if (shopPanel == null)
            return;

        SetShopOpen(!shopPanel.activeSelf);
    }

    public void SetShopOpen(bool isOpen)
    {
        if (shopPanel == null)
            return;

        if (isOpen)
            CloseInventoryIfOpen();

        bool wasOpen = shopPanel.activeSelf;

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
            shopPanel.SetActive(isOpen);
        }

        HandlePauseState(isOpen);

        if (wasOpen != isOpen)
            SoundManager.Instance?.PlaySfx(isOpen ? GameSfxId.UiOpen : GameSfxId.UiClose);
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        if (shopPanel == null || !shopPanel.activeSelf)
            return;

        if (state == GameManager.GameState.Gameplay)
            return;

        // Keep panel open when this UI itself put the game in Pause.
        if (state == GameManager.GameState.Pause && pausedByThisUI)
            return;

        SetShopOpen(false);
    }

    private void HandlePauseState(bool shopOpen)
    {
        if (!pauseGameplayWhileOpen || GameManager.Instance == null)
            return;

        if (shopOpen)
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
        if (UIPanelManager.Instance != null && shopPanel != null)
            UIPanelManager.Instance.RegisterPanel(PanelId, shopPanel, UIPanelManager.PanelType.Overlay);
    }

    private void CloseInventoryIfOpen()
    {
        if (cachedInventoryToggleButton == null)
            cachedInventoryToggleButton = FindFirstObjectByType<InventoryToggleButton>();

        if (cachedInventoryToggleButton != null && cachedInventoryToggleButton.IsInventoryOpen)
            cachedInventoryToggleButton.SetInventoryOpen(false);
    }

    public bool IsShopOpen => shopPanel != null && shopPanel.activeSelf;
}
