using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Liikkuminen")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float maxFallSpeed = -25f;      // putoamisnopeuden yläraja

    [Header("Portaat ja esteet")]
    [SerializeField] private float stepHeight = 0.55f;
    [SerializeField] private float slopeLimit = 50f;

    [Header("Turvallisuus")]
    [SerializeField] private float killY = -10f;             // tämän alapuolella pelaaja siirretään takaisin
    [SerializeField] private float maxDeltaTime = 0.033f;    // rajoittaa ruudunpäivityksen pituuden

    [Header("Katselu (valinnainen)")]
    [SerializeField] private bool enableMouseLook = false;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("Muut")]
    public bool canMove = true;   // ZombieAI jäädyttää pelaajan tällä

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch;
    private Vector3 lastSafePosition;
    private float startupTimer = 0.3f;   // odottaa hetken, kun scene on latautunut

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        controller.stepOffset = Mathf.Min(stepHeight, controller.height + controller.radius);
        controller.slopeLimit = slopeLimit;
        controller.skinWidth = 0.05f;
        controller.minMoveDistance = 0f;

        lastSafePosition = transform.position;
    }

    void Start()
    {
        if (enableMouseLook)
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // Pelin ensimmäisinä hetkinä ei liikuta, jotta latauksen aiheuttama nykäys ei vie lattian läpi
        if (startupTimer > 0f)
        {
            startupTimer -= Time.unscaledDeltaTime;
            return;
        }

        if (!canMove) return;

        float dt = Mathf.Min(Time.deltaTime, maxDeltaTime);

        if (enableMouseLook) Look();

        // Pysy maassa
        if (controller.isGrounded)
        {
            if (velocity.y < 0f) velocity.y = -2f;
            lastSafePosition = transform.position;   // tallenna turvallinen paikka
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;
        Vector3 move = transform.right * x + transform.forward * z;
        move = Vector3.ClampMagnitude(move, 1f);

        // Hyppy
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Painovoima ja putoamisnopeuden raja
        velocity.y += gravity * dt;
        velocity.y = Mathf.Max(velocity.y, maxFallSpeed);

        Vector3 total = move * currentSpeed + Vector3.up * velocity.y;
        controller.Move(total * dt);

        // Varmistus: jos pelaaja on silti pudonnut maailman läpi, siirrä hänet takaisin
        if (transform.position.y < killY)
            Respawn();
    }

    void Respawn()
    {
        controller.enabled = false;                          // Character Controllerin siirto vaatii tämän
        transform.position = lastSafePosition + Vector3.up * 0.5f;
        controller.enabled = true;
        velocity = Vector3.zero;
    }

    void Look()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -85f, 85f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}