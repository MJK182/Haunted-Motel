using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F;
    [SerializeField] private bool startsOn = false;
    [SerializeField] private AudioSource clickSound; // valinnainen

    private Light flashlight;

    void Awake()
    {
        flashlight = GetComponent<Light>();
        flashlight.enabled = startsOn;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        flashlight.enabled = !flashlight.enabled;

        if (clickSound != null)
            clickSound.Play();
    }
}
