using UnityEngine;

// Opens/closes inventory panel from UI button.
public class InventoryToggleButton : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private bool pauseGameplayWhileOpen = true;
    [SerializeField] private bool closeOnStart = true;

    private bool pausedByThisUI;

    private void Start()
    {
        if (closeOnStart && inventoryPanel != null)
            inventoryPanel.SetActive(false);
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

        bool wasOpen = inventoryPanel.activeSelf;
        inventoryPanel.SetActive(isOpen);
        HandlePauseState(isOpen);

        if (wasOpen != isOpen)
            SoundManager.Instance?.PlaySfx(isOpen ? GameSfxId.UiOpen : GameSfxId.UiClose);
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        if (state != GameManager.GameState.Gameplay && inventoryPanel != null && inventoryPanel.activeSelf)
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
}
