using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 3.5f;
    public float runSpeed = 6f;
    public float mouseSensitivity = 2f;
    public float gravity = -20f;
    public Transform cameraTransform; // Pelaajan lapsena oleva kamera

    CharacterController cc;
    float pitch;
    float velocityY;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameEnded) return;

        // Katselu
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Liikkuminen (WASD, Shift = juoksu)
        Vector3 input = transform.right * Input.GetAxis("Horizontal")
                      + transform.forward * Input.GetAxis("Vertical");
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        if (cc.isGrounded && velocityY < 0f) velocityY = -2f;
        velocityY += gravity * Time.deltaTime;

        cc.Move((Vector3.ClampMagnitude(input, 1f) * speed + Vector3.up * velocityY) * Time.deltaTime);
    }
}

