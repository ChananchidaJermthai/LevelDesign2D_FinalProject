using System.Reflection;
using UnityEngine;

/// <summary>
/// Kill Zone Trigger
/// ใส่ Script นี้ไว้กับ GameObject ที่มี Collider และเปิด Is Trigger
/// เมื่อ Player ตกลงมาโดน Trigger นี้ จะลด HP ของ Player จนเหลือ 0 ผ่าน PlayerHealth.TakeDamage()
/// </summary>
[RequireComponent(typeof(Collider))]
public class KillZoneTrigger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Tag ของ Player")]
    public string playerTag = "Player";

    [Header("Kill Damage")]
    [Tooltip("ใช้ดาเมจจาก Max Health ของ Player เพื่อให้เลือดเหลือ 0")]
    public bool usePlayerMaxHealthAsDamage = true;

    [Tooltip("ใช้เมื่อไม่ต้องการคำนวณจาก Max Health")]
    public float fixedKillDamage = 999999f;

    [Tooltip("บวกเพิ่มจาก Max Health เพื่อให้มั่นใจว่าตาย")]
    public float damageBuffer = 10f;

    [Header("Invincibility")]
    [Tooltip("เปิดไว้เพื่อให้ Kill Zone ฆ่า Player ได้ แม้กำลัง Dash หรือมี Invincibility Frame")]
    public bool ignorePlayerInvincibility = true;

    [Header("Debug")]
    public bool logKill = true;

    private Collider triggerCollider;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("[KillZoneTrigger] Collider ยังไม่ได้เปิด Is Trigger — เปิดให้อัตโนมัติ", this);
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // หา PlayerHealth จาก Object ที่ชน หรือ Parent ของมัน
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) return;

        // กันไม่ให้ศัตรูหรือ object อื่นที่มี PlayerHealth แปลก ๆ โดนโดยไม่ตั้งใจ
        bool isPlayer = other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag) || playerHealth.CompareTag(playerTag);
        if (!isPlayer) return;

        KillPlayer(playerHealth);
    }

    private void KillPlayer(PlayerHealth playerHealth)
    {
        if (playerHealth == null) return;

        if (ignorePlayerInvincibility)
        {
            // ใช้ Public API ก่อน
            playerHealth.SetInvincible(false);

            // เผื่อ PlayerHealth กำลังอยู่ใน Invincibility Frame หลังโดนโจมตี
            // ไม่ได้แก้ PlayerHealth.cs เดิม แค่ปิดค่า private ชั่วคราวเพื่อให้ Fall Kill ทำงานแน่นอน
            SetPrivateBool(playerHealth, "isInvincible", false);
            SetPrivateBool(playerHealth, "isInvincibleForced", false);
        }

        float damage = usePlayerMaxHealthAsDamage
            ? Mathf.Max(playerHealth.maxHealth, playerHealth.currentHealth) + damageBuffer
            : fixedKillDamage;

        playerHealth.TakeDamage(damage);

        if (logKill)
            Debug.Log($"[KillZoneTrigger] Player hit kill zone. Damage = {damage}", this);
    }

    private void SetPrivateBool(PlayerHealth playerHealth, string fieldName, bool value)
    {
        FieldInfo field = typeof(PlayerHealth).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(playerHealth, value);
        }
    }
}
