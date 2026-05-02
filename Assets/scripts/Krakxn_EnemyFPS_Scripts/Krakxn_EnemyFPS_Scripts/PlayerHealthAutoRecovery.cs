using UnityEngine;

/// <summary>
/// เพิ่มระบบเลือด Recovery ให้ PlayerHealth โดยไม่แก้ PlayerHealth.cs เดิม
/// วิธีใช้: ใส่ Component นี้ไว้ที่ Player ตัวเดียวกับ PlayerHealth
/// - ถ้า HP ลดลง จะนับว่าเพิ่งโดนดาเมจ
/// - ถ้าไม่โดนดาเมจครบ recoveryDelayAfterDamage จะเรียก PlayerHealth.Heal() อัตโนมัติ
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealthAutoRecovery : MonoBehaviour
{
    [Header("=== Reference ===")]
    public PlayerHealth playerHealth;

    [Header("=== Auto Recovery ===")]
    public bool enableRecovery = true;
    [Tooltip("ต้องไม่โดนดาเมจกี่วินาทีก่อนเริ่มฟื้นเลือด")]
    public float recoveryDelayAfterDamage = 5f;
    [Tooltip("เลือดที่ฟื้นต่อวินาที")]
    public float recoveryPerSecond = 8f;
    [Tooltip("เปิด = ฟื้นจนเต็ม / ปิด = ฟื้นได้ถึง Recovery Max Percent")]
    public bool recoverToFullHealth = true;
    [Range(0f, 1f)] public float recoveryMaxPercent = 1f;

    private float lastHealth;
    private float lastDamageTime;
    private bool initialized;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        InitializeState();

        if (playerHealth != null)
            playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void Start()
    {
        InitializeState();
    }

    private void Update()
    {
        if (!enableRecovery || playerHealth == null) return;
        if (playerHealth.IsDead) return;
        if (playerHealth.currentHealth <= 0f) return;

        float maxRecoverHealth = recoverToFullHealth
            ? playerHealth.maxHealth
            : playerHealth.maxHealth * recoveryMaxPercent;

        if (playerHealth.currentHealth >= maxRecoverHealth) return;
        if (Time.time < lastDamageTime + recoveryDelayAfterDamage) return;

        playerHealth.Heal(recoveryPerSecond * Time.deltaTime);
    }

    private void InitializeState()
    {
        if (initialized || playerHealth == null) return;

        lastHealth = playerHealth.currentHealth;
        lastDamageTime = Time.time;
        initialized = true;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (currentHealth < lastHealth)
            lastDamageTime = Time.time;

        lastHealth = currentHealth;
    }
}
