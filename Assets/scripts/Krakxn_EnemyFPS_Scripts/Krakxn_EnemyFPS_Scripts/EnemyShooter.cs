using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy 2:
/// - อยู่เฉย
/// - Player เข้าระยะแล้วเล็งด้วยเส้นสีแดงก่อนยิง
/// - ยิงเป็นชุด เช่น ยิง 5 นัด แล้ว Cooldown
/// - ปรับความเร็วกระสุน / ดาเมจ / ความเร็วในการยิง / Cooldown ได้
/// </summary>
public class EnemyShooter : MonoBehaviour
{
    [Header("=== Target ===")]
    public Transform player;
    public LayerMask playerLayer = ~0;
    public string playerTag = "Player";

    [Header("=== Range ===")]
    public float shootRange = 14f;
    public bool rotateToPlayer = true;
    public float rotationSpeed = 10f;

    [Header("=== Fire Settings ===")]
    public Transform firePoint;
    public GameObject projectilePrefab;
    public float projectileSpeed = 15f;
    public float projectileDamage = 10f;
    public float projectileLifetime = 5f;

    [Tooltip("เวลาหน่วงก่อนยิงแต่ละนัด ใช้สำหรับแสดงเส้นแดงให้ Player หลบ")]
    public float aimWarningTime = 0.45f;

    [Tooltip("ระยะเวลาระหว่างนัดใน Burst เดียวกัน")]
    public float shotInterval = 0.25f;

    [Tooltip("ยิงครบกี่นัดแล้วจะเข้า Cooldown")]
    public int shotsBeforeCooldown = 5;

    [Tooltip("Cooldown หลังยิงครบชุด")]
    public float burstCooldown = 2f;

    [Header("=== Warning Line ===")]
    public LineRenderer warningLine;
    public Color warningColor = Color.red;
    public float warningLineWidth = 0.06f;
    public float warningLineMaxLength = 30f;

    [Header("=== Debug ===")]
    public bool drawGizmos = true;

    private EnemyHealth enemyHealth;
    private bool isAiming;
    private float nextShotTime;
    private float cooldownTimer;
    private int shotsInCurrentBurst;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();

        if (firePoint == null)
            firePoint = transform;

        SetupWarningLine();
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;

        TryFindPlayer();

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (!IsPlayerInRange())
        {
            HideWarningLine();
            return;
        }

        if (rotateToPlayer && player != null && !isAiming)
            FacePlayer();

        if (!isAiming && cooldownTimer <= 0f && Time.time >= nextShotTime)
            StartCoroutine(AimAndShootRoutine());
    }

    private void TryFindPlayer()
    {
        if (player != null) return;

        GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (foundPlayer != null)
            player = foundPlayer.transform;
    }

    private bool IsPlayerInRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= shootRange;
    }

    private IEnumerator AimAndShootRoutine()
    {
        isAiming = true;

        float timer = 0f;
        Vector3 shootDirection = transform.forward;

        while (timer < aimWarningTime)
        {
            if (!IsPlayerInRange())
            {
                HideWarningLine();
                isAiming = false;
                yield break;
            }

            Vector3 targetPoint = GetPlayerAimPoint();
            shootDirection = (targetPoint - firePoint.position).normalized;

            if (rotateToPlayer)
                FaceDirection(shootDirection);

            ShowWarningLine(shootDirection);

            timer += Time.deltaTime;
            yield return null;
        }

        HideWarningLine();
        Shoot(shootDirection);

        shotsInCurrentBurst++;
        if (shotsInCurrentBurst >= Mathf.Max(1, shotsBeforeCooldown))
        {
            shotsInCurrentBurst = 0;
            cooldownTimer = burstCooldown;
        }
        else
        {
            nextShotTime = Time.time + shotInterval;
        }

        isAiming = false;
    }

    private Vector3 GetPlayerAimPoint()
    {
        if (player == null) return transform.position + transform.forward;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
            return player.position + Vector3.up * (cc.height * 0.5f);

        return player.position + Vector3.up * 1f;
    }

    private void Shoot(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.01f) return;

        GameObject bullet;

        if (projectilePrefab != null)
        {
            bullet = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        }
        else
        {
            bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bullet.name = "Auto_Enemy_Projectile";
            bullet.transform.position = firePoint.position;
            bullet.transform.rotation = Quaternion.LookRotation(direction);
            bullet.transform.localScale = Vector3.one * 0.18f;
        }

        EnemyProjectile projectile = bullet.GetComponent<EnemyProjectile>();
        if (projectile == null)
            projectile = bullet.AddComponent<EnemyProjectile>();

        projectile.Init(direction, projectileSpeed, projectileDamage, projectileLifetime, playerLayer, gameObject);
    }

    private void SetupWarningLine()
    {
        if (warningLine == null)
        {
            GameObject lineObj = new GameObject("Shooter_Warning_Line");
            lineObj.transform.SetParent(transform);
            warningLine = lineObj.AddComponent<LineRenderer>();
        }

        warningLine.useWorldSpace = true;
        warningLine.positionCount = 2;
        warningLine.startWidth = warningLineWidth;
        warningLine.endWidth = warningLineWidth;
        warningLine.startColor = warningColor;
        warningLine.endColor = warningColor;

        if (warningLine.material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            warningLine.material = new Material(shader);
        }

        HideWarningLine();
    }

    private void ShowWarningLine(Vector3 direction)
    {
        if (warningLine == null) return;

        warningLine.enabled = true;
        warningLine.SetPosition(0, firePoint.position);
        warningLine.SetPosition(1, firePoint.position + direction.normalized * warningLineMaxLength);
    }

    private void HideWarningLine()
    {
        if (warningLine != null)
            warningLine.enabled = false;
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 direction = player.position - transform.position;
        FaceDirection(direction);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootRange);
    }
}
