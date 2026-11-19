using UnityEngine;
using UnityEngine.Events;

public class Generator : MonoBehaviour, IInteractable
{
    public string requiredItem = "Vipu";
    public bool consumeItem = true;
    public Light[] lightsToEnable;      // valinnainen: valot, jotka syttyvät
    public AudioSource runSound;        // valinnainen: generaattorin ääni
    public UnityEvent onPowerOn;        // valinnainen: esim. ääni tai elokuva

    public static bool PowerOn { get; private set; }

    void Awake()
    {
        PowerOn = false;                // nollaa tilan, kun scene alkaa alusta
        foreach (var l in lightsToEnable) if (l) l.enabled = false;
    }

    public string Prompt => PowerOn ? "Generaattori käy" : "Käynnistä generaattori";

    public void Interact(Inventory inv)
    {
        if (PowerOn)
        {
            MessageUI.Show("Generaattori käy jo.");
            return;
        }

        if (!inv.Has(requiredItem))
        {
            MessageUI.Show("Generaattorista puuttuu " + requiredItem.ToLower() + ".");
            return;
        }

        if (consumeItem) inv.Remove(requiredItem);

        PowerOn = true;
        MessageUI.Show("Generaattori käynnistyi. Sähköt ovat päällä!");

        foreach (var l in lightsToEnable) if (l) l.enabled = true;
        if (runSound) runSound.Play();
        onPowerOn?.Invoke();
    }
}