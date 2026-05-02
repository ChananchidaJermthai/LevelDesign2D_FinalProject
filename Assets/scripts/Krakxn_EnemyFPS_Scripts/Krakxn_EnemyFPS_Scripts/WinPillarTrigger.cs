using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put this on the pillar that should show the Win UI when Player touches it.
/// BossDeathSpawnWinPillar can also add and setup this component automatically.
/// </summary>
public class WinPillarTrigger : MonoBehaviour
{
    [Header("Win UI")]
    [SerializeField] private GameObject winPanel;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Win Behaviour")]
    [SerializeField] private bool pauseGameOnWin = true;
    [SerializeField] private bool unlockCursorOnWin = true;
    [SerializeField] private bool disableTriggerAfterWin = true;

    [Header("Optional Scene Loading")]
    [Tooltip("Optional. Put your main menu scene name here if you want to use LoadMainMenu().")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool hasWon;

    public void Setup(GameObject panel, string tagName, bool pauseGame, bool unlockCursor)
    {
        winPanel = panel;
        playerTag = tagName;
        pauseGameOnWin = pauseGame;
        unlockCursorOnWin = unlockCursor;
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasWon)
            return;

        if (!other.CompareTag(playerTag))
            return;

        ShowWin();
    }

    public void ShowWin()
    {
        if (hasWon)
            return;

        hasWon = true;

        if (winPanel != null)
            winPanel.SetActive(true);
        else
            Debug.LogWarning("[WinPillarTrigger] Win Panel is not assigned.", this);

        if (unlockCursorOnWin)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (pauseGameOnWin)
            Time.timeScale = 0f;

        if (disableTriggerAfterWin)
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }
}
