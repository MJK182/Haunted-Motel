public interface IInteractable
{
    string Prompt { get; }
    void Interact(Inventory inv);
}