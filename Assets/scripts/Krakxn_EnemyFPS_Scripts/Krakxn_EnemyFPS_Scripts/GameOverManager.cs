using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameOverManager
/// ─────────────────────────────────────────────────────────
/// ใช้คู่กับ PlayerHealth + SaveSpawnPillar เดิมของคุณ
/// Logic:
/// - ฟัง Event PlayerHealth.OnDeath
/// - เมื่อ Player ตาย จะรอเวลาสั้น ๆ ให้ SaveSpawnPillar มีโอกาส Respawn ก่อน
/// - ถ้า Player ยังไม่กลับมา active แปลว่าไม่ได้กด Save Spawn Point → แสดง Game Over
/// - ถ้า Player ถูก Respawn กลับมาแล้ว → ไม่แสดง Game Over
///
/// วิธีใช้:
/// 1. สร้าง GameObject ชื่อ GameOverManager
/// 2. ใส่ Script นี้
/// 3. ลาก Player ที่มี PlayerHealth ใส่ช่อง Player Health
/// 4. ลาก Panel Game Over ใส่ช่อง Game Over Panel
/// 5. ตั้ง Game Over Panel ให้ปิดไว้ตอนเริ่มเกม
/// </summary>
public class GameOverManager : MonoBehaviour
{
    [Header("=== References ===")]
    [Tooltip("PlayerHealth ของ Player ถ้าไม่ลากใส่ ระบบจะหา GameObject Tag Player ให้เอง")]
    public PlayerHealth playerHealth;

    [Tooltip("Panel หน้า Game Over เช่น Canvas/Panel ที่มีปุ่ม Restart / Quit")]
    public GameObject gameOverPanel;

    [Header("=== Checkpoint / Respawn Detection ===")]
    [Tooltip("รอกี่วินาทีหลัง Player ตาย เพื่อให้ SaveSpawnPillar Respawn ก่อน ถ้าไม่มี Respawn ค่อย Game Over")]
    public float waitForRespawnBeforeGameOver = 1.3f;

    [Tooltip("ถ้าเปิดไว้ จะ Pause เกมตอน Game Over")]
    public bool pauseGameOnGameOver = true;

    [Tooltip("ถ้าเปิดไว้ จะปลดล็อกเมาส์ตอน Game Over เพื่อให้กด UI ได้")]
    public bool unlockCursorOnGameOver = true;

    [Header("=== Optional Scene Buttons ===")]
    [Tooltip("ชื่อ Scene หน้าเมนู ถ้าใช้ปุ่ม Load Main Menu")]
    public string mainMenuSceneName = "MainMenu";

    private bool isGameOver = false;
    private Coroutine gameOverCheckRoutine;

    private void Awake()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        FindPlayerHealthIfNeeded();
    }

    private void OnEnable()
    {
        FindPlayerHealthIfNeeded();
        SubscribePlayerDeath();
    }

    private void OnDisable()
    {
        UnsubscribePlayerDeath();
    }

    private void FindPlayerHealthIfNeeded()
    {
        if (playerHealth != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void SubscribePlayerDeath()
    {
        if (playerHealth == null) return;

        playerHealth.OnDeath -= HandlePlayerDeath;
        playerHealth.OnDeath += HandlePlayerDeath;
    }

    private void UnsubscribePlayerDeath()
    {
        if (playerHealth == null) return;
        playerHealth.OnDeath -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath()
    {
        if (isGameOver) return;

        if (gameOverCheckRoutine != null)
            StopCoroutine(gameOverCheckRoutine);

        gameOverCheckRoutine = StartCoroutine(CheckGameOverAfterRespawnWindow());
    }

    private IEnumerator CheckGameOverAfterRespawnWindow()
    {
        // ใช้ Realtime เพราะอนาคตอาจมีระบบ slow/pause ตอนตาย
        yield return new WaitForSecondsRealtime(waitForRespawnBeforeGameOver);

        // ถ้า SaveSpawnPillar ทำงานสำเร็จ มันจะ Revive แล้ว SetActive(true) ให้ Player อีกครั้ง
        bool playerRespawned = playerHealth != null && playerHealth.gameObject.activeInHierarchy;

        if (playerRespawned)
        {
            gameOverCheckRoutine = null;
            yield break;
        }

        ShowGameOver();
    }

    public void ShowGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (unlockCursorOnGameOver)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (pauseGameOnGameOver)
            Time.timeScale = 0f;

        Debug.Log("[GameOverManager] Game Over: Player died without saved spawn point.");
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning("[GameOverManager] mainMenuSceneName is empty.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
