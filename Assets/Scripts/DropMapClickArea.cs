using UnityEngine;
using UnityEngine.EventSystems;

public class DropMapClickArea : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private DropSelectionHUD owner;

    public void SetOwner(DropSelectionHUD newOwner)
    {
        owner = newOwner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
            owner.SelectDropPoint(eventData.position, eventData.pressEventCamera);
    }
}
