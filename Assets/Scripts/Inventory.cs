using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    readonly HashSet<string> items = new HashSet<string>();
    public event System.Action Changed;

    public bool Has(string id) => items.Contains(id);
    public void Add(string id) { if (items.Add(id)) Changed?.Invoke(); }
    public bool Remove(string id) { bool r = items.Remove(id); if (r) Changed?.Invoke(); return r; }
    public IEnumerable<string> All => items;

    void OnGUI()
    {
        if (items.Count == 0) return;
        GUI.Label(new Rect(10, 70, 400, 25), "Tavarat: " + string.Join(", ", items));
    }
}