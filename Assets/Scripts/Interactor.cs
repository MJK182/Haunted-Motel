using UnityEngine;

public class Interactor : MonoBehaviour
{
    public Camera cam;
    public float range = 3f;
    public KeyCode key = KeyCode.E;
    public LayerMask mask = ~0;

    Inventory inv;
    IInteractable current;

    void Start()
    {
        inv = GetComponentInParent<Inventory>();
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        current = null;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range, mask, QueryTriggerInteraction.Collide))
            current = hit.collider.GetComponentInParent<IInteractable>();

        if (current != null && Input.GetKeyDown(key))
            current.Interact(inv);
    }

    void OnGUI()
    {
        if (current == null) return;
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 40), "[E] " + current.Prompt, style);
    }
}