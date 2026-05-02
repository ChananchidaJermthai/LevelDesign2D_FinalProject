using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put this on an empty GameObject in the scene.
/// Logic:
/// 1. Wait until the assigned Boss GameObject is destroyed.
/// 2. Spawn a Win Pillar prefab at the assigned spawn point.
/// 3. Add/setup WinPillarTrigger on that spawned pillar so the player can touch it to show Win UI.
/// 
/// This script is additive. It does not modify Boss, EnemyHealth, PlayerHealth, or Spawn Point scripts.
/// </summary>
public class BossDeathSpawnWinPillar : MonoBehaviour
{
    [Header("Boss Check")]
    [Tooltip("Drag the Boss GameObject here. When this object is Destroyed, the pillar will spawn.")]
    [SerializeField] private GameObject boss;

    [Tooltip("How often to check if the Boss was destroyed.")]
    [SerializeField] private float checkInterval = 0.15f;

    [Header("Spawn Pillar")]
    [Tooltip("Prefab of the pillar that will appear after the Boss is destroyed.")]
    [SerializeField] private GameObject winPillarPrefab;

    [Tooltip("Position/rotation where the win pillar will spawn.")]
    [SerializeField] private Transform pillarSpawnPoint;

    [Tooltip("If true, the pillar will use the spawn point rotation.")]
    [SerializeField] private bool useSpawnPointRotation = true;

    [Tooltip("Delay after Boss is destroyed before spawning the pillar.")]
    [SerializeField] private float spawnDelay = 0f;

    [Header("Win UI")]
    [Tooltip("The Win UI panel that should appear when Player touches the spawned pillar.")]
    [SerializeField] private GameObject winPanel;

    [Tooltip("Player tag used by WinPillarTrigger.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Pause the game when Win UI appears.")]
    [SerializeField] private bool pauseGameOnWin = true;

    [Tooltip("Unlock and show cursor when Win UI appears.")]
    [SerializeField] private bool unlockCursorOnWin = true;

    [Header("Auto Trigger Setup")]
    [Tooltip("If true, this script will add WinPillarTrigger to the spawned pillar if it does not already have one.")]
    [SerializeField] private bool autoAddWinPillarTrigger = true;

    [Tooltip("If the spawned pillar has no Collider, add a BoxCollider trigger automatically.")]
    [SerializeField] private bool autoAddTriggerCollider = true;

    [Tooltip("Size of the auto-created trigger collider.")]
    [SerializeField] private Vector3 autoTriggerSize = new Vector3(2f, 3f, 2f);

    [Tooltip("Center of the auto-created trigger collider.")]
    [SerializeField] private Vector3 autoTriggerCenter = new Vector3(0f, 1.5f, 0f);

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool pillarSpawned;
    private GameObject spawnedPillar;

    private void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        if (boss == null)
        {
            Debug.LogWarning("[BossDeathSpawnWinPillar] Boss is not assigned. Please drag Boss GameObject into the Boss field.", this);
            return;
        }

        if (winPillarPrefab == null)
        {
            Debug.LogWarning("[BossDeathSpawnWinPillar] Win Pillar Prefab is not assigned.", this);
            return;
        }

        if (pillarSpawnPoint == null)
        {
            Debug.LogWarning("[BossDeathSpawnWinPillar] Pillar Spawn Point is not assigned.", this);
            return;
        }

        StartCoroutine(CheckBossDestroyedRoutine());
    }

    private IEnumerator CheckBossDestroyedRoutine()
    {
        while (!pillarSpawned)
        {
            // In Unity, a destroyed GameObject compares as null.
            if (boss == null)
            {
                if (logDebug)
                    Debug.Log("[BossDeathSpawnWinPillar] Boss destroyed. Spawning win pillar.", this);

                if (spawnDelay > 0f)
                    yield return new WaitForSeconds(spawnDelay);

                SpawnWinPillar();
                yield break;
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void SpawnWinPillar()
    {
        if (pillarSpawned)
            return;

        if (winPillarPrefab == null || pillarSpawnPoint == null)
            return;

        Quaternion rotation = useSpawnPointRotation ? pillarSpawnPoint.rotation : Quaternion.identity;
        spawnedPillar = Instantiate(winPillarPrefab, pillarSpawnPoint.position, rotation);
        spawnedPillar.name = winPillarPrefab.name + "_AfterBossDeath";

        SetupPillarTrigger(spawnedPillar);

        pillarSpawned = true;
    }

    private void SetupPillarTrigger(GameObject pillar)
    {
        if (pillar == null)
            return;

        if (autoAddTriggerCollider)
        {
            Collider triggerCollider = pillar.GetComponent<Collider>();

            if (triggerCollider == null)
            {
                BoxCollider box = pillar.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = autoTriggerSize;
                box.center = autoTriggerCenter;
            }
            else
            {
                triggerCollider.isTrigger = true;
            }
        }

        WinPillarTrigger trigger = pillar.GetComponent<WinPillarTrigger>();

        if (trigger == null && autoAddWinPillarTrigger)
            trigger = pillar.AddComponent<WinPillarTrigger>();

        if (trigger != null)
        {
            trigger.Setup(
                winPanel,
                playerTag,
                pauseGameOnWin,
                unlockCursorOnWin
            );
        }
    }

    // Optional: You can call this manually from UnityEvent/Button for testing.
    public void ForceSpawnWinPillar()
    {
        SpawnWinPillar();
    }
}
