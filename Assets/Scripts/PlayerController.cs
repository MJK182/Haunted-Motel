using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Portaat ja esteet")]
    [SerializeField] private float stepHeight = 0.55f;   // hieman yli 0,5, jotta 0,5-korkuiset askelmat ylittyvät varmasti
    [SerializeField] private float slopeLimit = 50f;

    private CharacterController controller;
    private Vector3 velocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Step Offset ei saa olla suurempi kuin Height + Radius, Unity rajoittaa sen muuten itse
        controller.stepOffset = Mathf.Min(stepHeight, controller.height + controller.radius);
        controller.slopeLimit = slopeLimit;
        controller.skinWidth = 0.05f;
        controller.minMoveDistance = 0f;
    }

    void Update()
    {
        // Pysy maassa
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
        Vector3 move = transform.right * x + transform.forward * z;
        move = Vector3.ClampMagnitude(move, 1f);   // viistoliike ei ole nopeampaa

        // Hyppy
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Painovoima
        velocity.y += gravity * Time.deltaTime;

        // Yksi Move-kutsu: vaaka- ja pystyliike yhdessä
        Vector3 total = move * currentSpeed + Vector3.up * velocity.y;
        controller.Move(total * Time.deltaTime);
    }
}