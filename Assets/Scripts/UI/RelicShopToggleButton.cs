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
        if (state != GameManager.GameState.Gameplay && shopPanel != null && shopPanel.activeSelf)
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
}
