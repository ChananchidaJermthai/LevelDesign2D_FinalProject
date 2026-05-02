using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FPS Player Controller สำหรับ Unity 3D
/// - W/A/S/D : เดิน
/// - Shift   : Sprint ใช้ Stamina
/// - Space   : Jump
/// - F       : Dash ใช้ Stamina
/// - Mouse Look ใช้ FPSCameraController แยก
/// ต้องมี CharacterController บน Player
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FPSPlayerController : MonoBehaviour
{
    [Header("=== Movement ===")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float acceleration = 12f;

    [Header("=== Jump ===")]
    [Tooltip("ใช้เป็นความสูงของการกระโดดโดยประมาณ เช่น 1.5 - 2.5")]
    public float jumpForce = 1.8f;

    [Tooltip("ใช้เป็นค่าบวก เช่น 30 หรือ 40 ไม่ต้องใส่ติดลบ")]
    public float gravity = 40f;

    [Tooltip("ตัวคูณ Gravity ตอนกำลังตก ยิ่งมากยิ่งตกเร็ว")]
    public float fallGravityMultiplier = 2.5f;

    [Tooltip("ตัวคูณ Gravity ตอนปล่อย Space ระหว่างกำลังกระโดดขึ้น ทำให้กระโดดสั้นลง")]
    public float lowJumpGravityMultiplier = 2f;

    [Tooltip("จำกัดความเร็วตกสูงสุด")]
    public float maxFallSpeed = 80f;

    public int maxJumps = 1;

    [Header("=== Dash ===")]
    public KeyCode dashKey = KeyCode.F;
    public float dashDistance = 6f;
    public float dashSpeed = 30f;
    public float dashCooldown = 1.2f;
    public bool dashInvincible = true;

    [Header("=== Stamina ===")]
    public float maxStamina = 100f;
    [SerializeField] private float currentStamina;

    [Tooltip("Stamina ที่เสียต่อวินาทีตอนกด Shift วิ่ง")]
    public float sprintStaminaDrainPerSecond = 18f;

    [Tooltip("Stamina ที่เสียหนึ่งครั้งตอน Dash")]
    public float dashStaminaCost = 25f;

    [Tooltip("Stamina ที่ฟื้นต่อวินาที")]
    public float staminaRecoveryPerSecond = 22f;

    [Tooltip("หลังหยุดใช้ Stamina ต้องรอกี่วินาทีก่อนเริ่มฟื้น")]
    public float staminaRecoveryDelay = 0.8f;

    [Tooltip("ถ้า Stamina หมด จะวิ่ง/แดชไม่ได้จนกว่าจะฟื้นถึง % นี้ของ Max Stamina")]
    [Range(0f, 1f)] public float exhaustedRecoveryPercent = 0.25f;

    [Tooltip("เปิด = เดินปกติแล้ว Stamina ยังฟื้นได้ / ปิด = ต้องหยุดนิ่งถึงจะฟื้น")]
    public bool recoverStaminaWhileMoving = true;

    [Header("=== Stamina UI ===")]
    [Tooltip("Slider ที่ใช้เป็นหลอด Stamina ตั้งค่าเหมือน Health Bar ได้เลย")]
    public Slider staminaBarSlider;
    [Tooltip("Image Fill ของ Stamina Bar ถ้ามี")]
    public Image staminaBarFill;
    public Color fullStaminaColor = Color.cyan;
    public Color lowStaminaColor = Color.yellow;
    public Color exhaustedStaminaColor = Color.red;

    [Header("=== Ground Check ===")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    [Tooltip("เลือกเฉพาะ Layer พื้นเท่านั้น เช่น Ground ห้ามเลือก Player หรือ Everything")]
    public LayerMask groundLayer;

    [Header("=== References ===")]
    public Transform cameraTransform;

    private CharacterController controller;
    private PlayerHealth playerHealth;

    private Vector3 verticalVelocity;
    private Vector3 smoothMoveVelocity;
    private Vector3 currentMove;

    private int jumpsRemaining;
    private bool isGrounded;
    private bool isDashing;
    private bool isSprintingInternal;
    private bool isStaminaExhausted;
    private float dashTimer;
    private float lastStaminaUseTime = -999f;

    // เก็บผล OverlapSphere แบบไม่สร้าง garbage ทุกเฟรม
    private readonly Collider[] groundHits = new Collider[12];

    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool IsSprinting => isSprintingInternal;
    public bool IsStaminaExhausted => isStaminaExhausted;
    public float CurrentStamina => currentStamina;
    public float StaminaPercent => maxStamina <= 0f ? 0f : Mathf.Clamp01(currentStamina / maxStamina);
    public float DashCooldownNormalized => dashCooldown <= 0f ? 1f : Mathf.Clamp01(1f - dashTimer / dashCooldown);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("_GroundCheck");
            gc.transform.SetParent(transform);
            groundCheck = gc.transform;
        }

        // สำคัญ: วาง GroundCheck ไว้ที่ใต้เท้าจริงของ CharacterController
        // สูตรนี้รองรับทั้งกรณี Center Y = 1 และ Center Y = 0
        if (groundCheck != null && groundCheck.name == "_GroundCheck")
        {
            float bottomY = controller.center.y - (controller.height * 0.5f) + 0.05f;
            groundCheck.localPosition = new Vector3(controller.center.x, bottomY, controller.center.z);
        }

        currentStamina = Mathf.Clamp(maxStamina, 0f, maxStamina);
        isStaminaExhausted = false;
        UpdateStaminaBar();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;

        CheckGround();

        if (!isDashing)
        {
            HandleMovement();
            HandleJump();
            ApplyGravity();

            Vector3 totalMove = currentMove + Vector3.up * verticalVelocity.y;
            controller.Move(totalMove * Time.deltaTime);
        }
        else
        {
            isSprintingInternal = false;
        }

        HandleDashInput();
        HandleStaminaRecovery();

        if (dashTimer > 0f)
            dashTimer -= Time.deltaTime;
    }

    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool hasMoveInput = Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f;
        bool wantsSprint = Input.GetKey(KeyCode.LeftShift) && v > 0f && hasMoveInput;
        bool canSprint = wantsSprint && CanUseStamina();

        isSprintingInternal = canSprint;

        float targetSpeed = isSprintingInternal ? sprintSpeed : walkSpeed;

        if (isSprintingInternal)
        {
            SpendStamina(sprintStaminaDrainPerSecond * Time.deltaTime);
        }

        Transform moveReference = cameraTransform != null ? cameraTransform : transform;

        Vector3 forward = moveReference.forward;
        Vector3 right = moveReference.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 targetMove = (forward * v + right * h).normalized * targetSpeed;
        currentMove = Vector3.SmoothDamp(currentMove, targetMove, ref smoothMoveVelocity, 1f / acceleration);
    }

    private void HandleJump()
    {
        if (isGrounded && verticalVelocity.y < 0f)
        {
            jumpsRemaining = maxJumps;
        }

        if (Input.GetKeyDown(KeyCode.Space) && jumpsRemaining > 0)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpForce * 2f * gravity);
            jumpsRemaining--;
            isGrounded = false;
        }
    }

    private void ApplyGravity()
    {
        // ถ้าอยู่พื้น ให้กดลงนิด ๆ เพื่อให้ CharacterController เกาะพื้น
        if (isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
            return;
        }

        float gravityThisFrame = gravity;

        // ขาลง: เพิ่มแรงตกให้ลงไวขึ้น โดยไม่ต้องเพิ่มความสูงกระโดด
        if (verticalVelocity.y < 0f)
        {
            gravityThisFrame *= fallGravityMultiplier;
        }
        // ขาขึ้นแต่ปล่อย Space: ทำให้กระโดดเตี้ยลง/ควบคุมง่ายขึ้น
        else if (verticalVelocity.y > 0f && !Input.GetKey(KeyCode.Space))
        {
            gravityThisFrame *= lowJumpGravityMultiplier;
        }

        verticalVelocity.y -= gravityThisFrame * Time.deltaTime;
        verticalVelocity.y = Mathf.Max(verticalVelocity.y, -maxFallSpeed);
    }

    private void HandleDashInput()
    {
        if (!Input.GetKeyDown(dashKey)) return;
        if (isDashing || dashTimer > 0f) return;
        if (!CanDashWithStamina()) return;

        SpendStamina(dashStaminaCost);
        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        isSprintingInternal = false;
        dashTimer = dashCooldown;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Transform moveReference = cameraTransform != null ? cameraTransform : transform;

        Vector3 forward = moveReference.forward;
        Vector3 right = moveReference.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 dashDirection = (forward * v + right * h);
        if (dashDirection.sqrMagnitude < 0.01f)
            dashDirection = forward;
        dashDirection.Normalize();

        if (dashInvincible && playerHealth != null)
            playerHealth.SetInvincible(true);

        float distanceTravelled = 0f;
        while (distanceTravelled < dashDistance)
        {
            float step = dashSpeed * Time.deltaTime;
            step = Mathf.Min(step, dashDistance - distanceTravelled);

            // ให้ยังมีแรงดึงลงเล็กน้อยระหว่าง Dash เพื่อไม่ให้ลอยค้างง่าย
            controller.Move((dashDirection * step) + Vector3.down * 0.02f);
            distanceTravelled += step;

            yield return null;
        }

        if (dashInvincible && playerHealth != null)
            playerHealth.SetInvincible(false);

        verticalVelocity.y = -2f;
        isDashing = false;
    }

    private void HandleStaminaRecovery()
    {
        if (isDashing || isSprintingInternal) return;
        if (Time.time < lastStaminaUseTime + staminaRecoveryDelay) return;
        if (currentStamina >= maxStamina) return;

        if (!recoverStaminaWhileMoving)
        {
            bool hasMoveInput = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;
            if (hasMoveInput) return;
        }

        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecoveryPerSecond * Time.deltaTime);

        if (isStaminaExhausted && currentStamina >= maxStamina * exhaustedRecoveryPercent)
            isStaminaExhausted = false;

        UpdateStaminaBar();
    }

    private bool CanUseStamina()
    {
        return maxStamina > 0f && !isStaminaExhausted && currentStamina > 0f;
    }

    private bool CanDashWithStamina()
    {
        if (dashStaminaCost <= 0f) return true;
        if (maxStamina <= 0f) return false;
        if (isStaminaExhausted) return false;
        return currentStamina >= dashStaminaCost;
    }

    private void SpendStamina(float amount)
    {
        if (amount <= 0f || maxStamina <= 0f) return;

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;

        if (currentStamina <= 0f)
            isStaminaExhausted = true;

        UpdateStaminaBar();
    }

    public void RestoreStamina(float amount)
    {
        if (amount <= 0f || maxStamina <= 0f) return;

        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);

        if (isStaminaExhausted && currentStamina >= maxStamina * exhaustedRecoveryPercent)
            isStaminaExhausted = false;

        UpdateStaminaBar();
    }

    public void SetStamina(float value)
    {
        currentStamina = Mathf.Clamp(value, 0f, maxStamina);

        if (currentStamina <= 0f)
            isStaminaExhausted = true;
        else if (currentStamina >= maxStamina * exhaustedRecoveryPercent)
            isStaminaExhausted = false;

        UpdateStaminaBar();
    }

    private void UpdateStaminaBar()
    {
        float pct = StaminaPercent;

        if (staminaBarSlider != null)
        {
            staminaBarSlider.minValue = 0f;
            staminaBarSlider.maxValue = 1f;
            staminaBarSlider.value = pct;
        }

        if (staminaBarFill != null)
        {
            staminaBarFill.color = isStaminaExhausted
                ? exhaustedStaminaColor
                : Color.Lerp(lowStaminaColor, fullStaminaColor, pct);
        }
    }

    private void CheckGround()
    {
        bool controllerGrounded = controller.isGrounded;
        bool sphereGrounded = false;

        if (groundCheck != null && groundLayer.value != 0)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                groundCheck.position,
                groundCheckRadius,
                groundHits,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = groundHits[i];
                if (hit == null) continue;

                // กันไม่ให้ GroundCheck ตรวจเจอ Collider ของตัว Player เองหรือลูกของ Player
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                sphereGrounded = true;
                break;
            }

            for (int i = 0; i < hitCount; i++)
                groundHits[i] = null;
        }

        // ถ้าไม่ได้ตั้ง Ground Layer จะใช้ CharacterController.isGrounded อย่างเดียว
        isGrounded = controllerGrounded || sphereGrounded;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
