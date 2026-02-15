using UnityEngine;
using UnityEngine.EventSystems;

// Binds UI button events to weapon fire controls.
public class FireButtonInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private WeaponController weaponController;

    private void Awake()
    {
        if (weaponController == null)
            weaponController = Object.FindFirstObjectByType<WeaponController>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (weaponController != null)
            weaponController.SetFireButtonHeld(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (weaponController != null)
            weaponController.SetFireButtonHeld(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (weaponController != null)
            weaponController.FireOnceFromButton();
    }
}
