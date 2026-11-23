using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class EscapeExit : MonoBehaviour, IInteractable
{
    public string exitName = "Pääovi";
    public string[] requiredItems;
    public bool requiresPower = true;           // vaatii generaattorin käynnissä
    public bool consumeItems = false;
    public string lockedHint = "Ovi on lukossa.";
    public string escapeText = "Pääsit pakoon!";
    public string endSceneName = "";            // tyhjä = lataa nykyinen scene uudelleen
    public float endDelay = 4f;
    public UnityEvent onEscape;                 // esim. elokuva tai ääni

    bool done;
    public string Prompt => exitName;

    public void Interact(Inventory inv)
    {
        if (done) return;

        if (requiresPower && !Generator.PowerOn)
        {
            MessageUI.Show("Sähköt ovat pois päältä. Käynnistä generaattori.");
            return;
        }

        var missing = requiredItems.Where(i => !inv.Has(i)).ToList();
        if (missing.Count > 0)
        {
            MessageUI.Show(lockedHint + " Puuttuu: " + string.Join(", ", missing));
            return;
        }

        done = true;
        if (consumeItems) foreach (var i in requiredItems) inv.Remove(i);
        StartCoroutine(Escape(inv));
    }

    IEnumerator Escape(Inventory inv)
    {
        var pc = inv.GetComponent<PlayerController>();
        if (pc) pc.canMove = false;

        MessageUI.Show(escapeText, endDelay);
        onEscape?.Invoke();

        yield return new WaitForSeconds(endDelay);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(string.IsNullOrEmpty(endSceneName)
            ? SceneManager.GetActiveScene().name : endSceneName);
    }
}