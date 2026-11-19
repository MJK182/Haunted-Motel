using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint; // valinnainen

    private CharacterController controller;
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Jos spawnPointia ei ole annettu, käytetään paikkaa, jossa peli alkoi
        if (spawnPoint != null)
        {
            startPosition = spawnPoint.position;
            startRotation = spawnPoint.rotation;
        }
        else
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }
    }

    public void Respawn()
    {
        controller.enabled = false;   // pakollinen, muuten siirto ei toimi
        transform.SetPositionAndRotation(startPosition, startRotation);
        controller.enabled = true;
    }
}