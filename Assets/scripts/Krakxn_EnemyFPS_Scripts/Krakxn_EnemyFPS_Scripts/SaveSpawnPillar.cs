using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SaveSpawnPillar — เสา Checkpoint สำหรับ FPS Game
/// ──────────────────────────────────────────────────
/// วิธีใช้งาน:
///   1. สร้าง GameObject เป็นเสา ใส่ Script นี้
///   2. ใส่ Collider แบบ Trigger บน GameObject เดียวกัน
///   3. สร้าง World Space Canvas ลูก → ใส่ interactPromptCanvas
///   4. ใน SpawnManager (หรือ GameManager) ให้ Subscribe OnPlayerDeath แล้วเรียก SpawnManager.RespawnPlayer()
///   5. สร้าง SpawnManager.cs แล้ว assign ได้เลย หรือใช้แบบ standalone ก็ได้
/// 
/// Dependencies:
///   - PlayerHealth.cs    (TakeDamage / Heal / OnDeath / Revive)
///   - FPSPlayerController.cs (SetStamina)
/// </summary>
public class SaveSpawnPillar : MonoBehaviour
{
    // ──────────────────────────── Inspector ────────────────────────────

    [Header("=== Interact Settings ===")]
    [Tooltip("ระยะที่ Player เข้ามาแล้วจะแสดง UI Prompt")]
    public float interactRadius = 3f;
    [Tooltip("ปุ่มกด Interact")]
    public KeyCode interactKey = KeyCode.E;

    [Header("=== Spawn Point ===")]
    [Tooltip("ถ้าไม่ได้ Assign จะ Spawn ที่ตำแหน่งเสาเอง")]
    public Transform spawnPoint;

    [Header("=== World Space UI ===")]
    [Tooltip("Canvas (World Space) ที่แนบไว้กับเสา")]
    public Canvas interactPromptCanvas;
    [Tooltip("Text '[E] บันทึกจุด Spawn' — ใช้ TextMeshPro หรือ Legacy Text ก็ได้")]
    public GameObject promptTextObject;
    [Tooltip("แสดงเมื่อ Activate แล้ว เช่น 'บันทึกแล้ว ✔'")]
    public GameObject activatedTextObject;
    [Tooltip("ไอคอน / Glow ที่เปลี่ยนเมื่อ Activate")]
    public Renderer pillarRenderer;
    public Color inactiveColor = new Color(0.4f, 0.4f, 1f);
    public Color activeColor = new Color(1f, 0.85f, 0.1f);

    [Header("=== Audio (Optional) ===")]
    public AudioSource audioSource;
    public AudioClip activateSound;
    public AudioClip respawnSound;

    [Header("=== Feedback ===")]
    [Tooltip("วินาทีที่จะแสดง 'บันทึกแล้ว' ก่อนกลับเป็น Prompt ปกติ")]
    public float activatedMessageDuration = 2f;

    // ──────────────────────────── Private ────────────────────────────

    private bool isActivated = false;
    private bool playerInRange = false;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private FPSPlayerController playerController;

    // Static → เก็บ checkpoint ล่าสุดแบบ Global ระหว่าง Scene
    private static SaveSpawnPillar currentActivePillar;

    // ──────────────────────────── Unity ────────────────────────────

    private void Awake()
    {
        // ซ่อน UI ตั้งต้น
        SetPromptVisible(false);
        SetActivatedMessageVisible(false);
        UpdatePillarVisual();
    }

    private void Update()
    {
        if (!playerInRange || playerTransform == null) return;

        // ตรวจระยะจริงทุก Frame (กรณี Trigger ไม่ตรง)
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > interactRadius + 0.5f)
        {
            HidePrompt();
            return;
        }

        if (Input.GetKeyDown(interactKey) && !isActivated)
        {
            Activate();
        }
    }

    // ──────────────────────────── Trigger ────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerTransform = other.transform;
        playerHealth = other.GetComponent<PlayerHealth>();
        playerController = other.GetComponent<FPSPlayerController>();
        playerInRange = true;

        // ถ้า Activate แล้วไม่ต้องแสดง Prompt
        if (!isActivated)
            ShowPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        HidePrompt();
    }

    // ──────────────────────────── Core Logic ────────────────────────────

    /// <summary>Player กด E → บันทึกจุด Spawn</summary>
    private void Activate()
    {
        // ยกเลิก Active จาก Pillar เก่า
        if (currentActivePillar != null && currentActivePillar != this)
            currentActivePillar.Deactivate();

        currentActivePillar = this;
        isActivated = true;

        UpdatePillarVisual();
        PlaySound(activateSound);
        StartCoroutine(ShowActivatedFeedback());

        // Subscribe OnDeath เพื่อ Respawn
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandlePlayerDeath;   // กันซ้ำ
            playerHealth.OnDeath += HandlePlayerDeath;
        }

        Debug.Log($"[SaveSpawnPillar] Checkpoint activated: {gameObject.name}");
    }

    /// <summary>เมื่อมี Pillar ใหม่ถูก Activate แทน</summary>
    private void Deactivate()
    {
        isActivated = false;

        if (playerHealth != null)
            playerHealth.OnDeath -= HandlePlayerDeath;

        UpdatePillarVisual();
    }

    /// <summary>เรียกจาก PlayerHealth.OnDeath → ทำ Respawn</summary>
    private void HandlePlayerDeath()
    {
        if (playerHealth == null) return;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        // รอ 1 วินาทีก่อน Respawn (ใส่ Death VFX / animation ได้ตรงนี้)
        yield return new WaitForSecondsRealtime(1f);

        // หา Player จาก Tag ในกรณีที่ Reference หาย
        if (playerHealth == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerHealth = p.GetComponent<PlayerHealth>();
                playerController = p.GetComponent<FPSPlayerController>();
                playerTransform = p.transform;
            }
        }

        if (playerHealth == null) yield break;

        // ── Unsubscribe ก่อน Revive เพื่อกัน OnDeath ยิงซ้ำระหว่าง Revive ──
        playerHealth.OnDeath -= HandlePlayerDeath;

        // ── Teleport ──
        Vector3 targetPos = spawnPoint != null ? spawnPoint.position : transform.position + Vector3.up * 0.1f;

        // ต้อง Disable CharacterController ก่อน Teleport
        CharacterController cc = playerHealth.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerHealth.transform.position = targetPos;

        if (cc != null) cc.enabled = true;

        // ── Restore Stats (Revive ล้าง isInvincible ด้วยแล้ว) ──
        playerHealth.Revive(fullHealth: true);
        playerController?.SetStamina(playerController.maxStamina);

        // ── Re-subscribe OnDeath สำหรับครั้งต่อไป ──
        playerHealth.OnDeath += HandlePlayerDeath;

        PlaySound(respawnSound);

        Debug.Log($"[SaveSpawnPillar] Player respawned at: {gameObject.name}");
    }

    // ──────────────────────────── UI Helpers ────────────────────────────

    private void ShowPrompt()
    {
        if (interactPromptCanvas != null) interactPromptCanvas.gameObject.SetActive(true);
        SetPromptVisible(true);
        SetActivatedMessageVisible(false);
    }

    private void HidePrompt()
    {
        if (interactPromptCanvas != null) interactPromptCanvas.gameObject.SetActive(false);
        SetPromptVisible(false);
        SetActivatedMessageVisible(false);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptTextObject != null) promptTextObject.SetActive(visible);
    }

    private void SetActivatedMessageVisible(bool visible)
    {
        if (activatedTextObject != null) activatedTextObject.SetActive(visible);
    }

    private IEnumerator ShowActivatedFeedback()
    {
        SetPromptVisible(false);
        SetActivatedMessageVisible(true);

        yield return new WaitForSeconds(activatedMessageDuration);

        SetActivatedMessageVisible(false);
        // ไม่แสดง Prompt ปกติอีก เพราะ Activate แล้ว
    }

    private void UpdatePillarVisual()
    {
        if (pillarRenderer == null) return;
        pillarRenderer.material.color = isActivated ? activeColor : inactiveColor;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    // ──────────────────────────── Gizmos ────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
        Gizmos.DrawSphere(transform.position, interactRadius);

        if (spawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
            Gizmos.DrawLine(transform.position, spawnPoint.position);
        }
    }
}