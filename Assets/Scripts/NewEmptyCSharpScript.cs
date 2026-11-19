using UnityEngine;

public class ItemPickup : MonoBehaviour, IInteractable
{
    public string itemId = "Pääavain";
    public string Prompt => "Ota " + itemId;

    public void Interact(Inventory inv)
    {
        inv.Add(itemId);
        MessageUI.Show("Löysit: " + itemId);
        Destroy(gameObject);
    }
}