using UnityEngine;

/// <summary>
/// FPS Camera Controller
/// วางบน Camera ที่เป็นลูกของ Player
/// - Mouse X หมุนตัว Player
/// - Mouse Y ก้ม/เงยกล้อง
/// - FOV เพิ่มตอน Sprint
/// - Head Bob ตอนเดิน/วิ่ง
/// </summary>
[RequireComponent(typeof(Camera))]
public class FPSCameraController : MonoBehaviour
{
    [Header("=== Mouse Look ===")]
    public float sensitivityX = 2f;
    public float sensitivityY = 2f;
    public bool invertY = false;
    public float pitchClamp = 85f;

    [Header("=== FOV ===")]
    public float normalFOV = 75f;
    public float sprintFOV = 90f;
    public float fovSmoothSpeed = 8f;

    [Header("=== Head Bob ===")]
    public bool enableHeadBob = true;
    public float bobSpeedWalk = 10f;
    public float bobSpeedSprint = 16f;
    public float bobAmountY = 0.05f;
    public float bobAmountX = 0.025f;

    [Header("=== Dash Tilt ===")]
    public float dashTiltAngle = 5f;
    public float tiltSmoothSpeed = 10f;

    [Header("=== References ===")]
    public Transform playerBody;

    private FPSPlayerController playerController;
    private Camera cam;
    private float pitchRotation;
    private float bobTimer;
    private Vector3 bobOffset;
    private Vector3 initialLocalPosition;
    private float currentTilt;

    private void Start()
    {
        cam = GetComponent<Camera>();
        initialLocalPosition = transform.localPosition;

        if (playerBody == null && transform.parent != null)
            playerBody = transform.parent;

        if (playerBody != null)
            playerController = playerBody.GetComponent<FPSPlayerController>();

        cam.fieldOfView = normalFOV;
        LockCursor(true);
    }

    private void Update()
    {
        HandleCursorToggle();
        HandleDashTilt();
        HandleMouseLook();
        HandleFOV();
        HandleHeadBob();
    }

    private void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivityY * (invertY ? 1f : -1f);

        if (playerBody != null)
            playerBody.Rotate(Vector3.up * mouseX);

        pitchRotation = Mathf.Clamp(pitchRotation + mouseY, -pitchClamp, pitchClamp);
        transform.localRotation = Quaternion.Euler(pitchRotation, 0f, currentTilt);
    }

    private void HandleFOV()
    {
        bool sprinting = playerController != null && playerController.IsSprinting && playerController.IsGrounded;
        float targetFOV = sprinting ? sprintFOV : normalFOV;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
    }

    private void HandleHeadBob()
    {
        if (!enableHeadBob) return;

        bool moving = playerController != null
            && (Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f)
            && playerController.IsGrounded
            && !playerController.IsDashing;

        if (moving)
        {
            float speed = playerController.IsSprinting ? bobSpeedSprint : bobSpeedWalk;
            bobTimer += Time.deltaTime * speed;
            bobOffset = new Vector3(
                Mathf.Sin(bobTimer * 0.5f) * bobAmountX,
                Mathf.Sin(bobTimer) * bobAmountY,
                0f);
        }
        else
        {
            bobTimer = 0f;
            bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, Time.deltaTime * 8f);
        }

        transform.localPosition = initialLocalPosition + bobOffset;
    }

    private void HandleDashTilt()
    {
        bool dashing = playerController != null && playerController.IsDashing;
        float targetTilt = dashing ? dashTiltAngle : 0f;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSmoothSpeed * Time.deltaTime);
    }

    private void HandleCursorToggle()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        LockCursor(Cursor.lockState != CursorLockMode.Locked);
    }

    public void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
