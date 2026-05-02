using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss Enemy Controller
/// Flow:
/// 1) Melee Attack แบบ EnemyMeleePatrol จำนวน 3 ครั้ง
/// 2) Charge / พุ่งใส่ Player จำนวน 2 ครั้ง พร้อมกรอบสี่เหลี่ยมเตือนบนพื้น
/// 3) Shoot / ยิงระยะไกล จำนวน 5 นัด พร้อมเส้นแดงเตือนก่อนยิงทุกนัด
/// 4) Cooldown แล้ววนกลับไป Melee ใหม่
///
/// Version นี้เพิ่ม Boss Zone / Arena:
/// - ถ้า Player ยังไม่เข้า Zone, Boss จะไม่เริ่มไล่/โจมตี แม้จะอยู่ใน activationRange
/// - ถ้า Player ออกจาก Zone, Boss จะหยุด Phase ปัจจุบันและกลับเป็น Idle
/// - ตอน Boss เดินหรือพุ่ง จะถูกกันไม่ให้ออกนอก Zone
///
/// ต้องมี CharacterController บน Boss
/// ถ้ามี EnemyHealth อยู่ด้วย Script จะหยุดทำงานเมื่อ EnemyHealth.IsDead = true
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BossEnemyController : MonoBehaviour
{
    private enum BossState
    {
        Idle,
        MeleePhase,
        ChargePhase,
        ShootPhase,
        Cooldown
    }

    [Header("=== Target ===")]
    public Transform player;
    public string playerTag = "Player";
    public LayerMask playerLayer = ~0;
    public float activationRange = 35f;

    [Header("=== Boss Zone / Arena ===")]
    [Tooltip("ลาก BoxCollider/Collider ของพื้นที่ Boss มาใส่ช่องนี้ แนะนำให้ใช้ BoxCollider แบบ Is Trigger")]
    public Collider bossZone;
    [Tooltip("ถ้าเปิด Boss จะเริ่มทำงานเฉพาะตอน Player อยู่ใน Boss Zone เท่านั้น")]
    public bool requirePlayerInsideBossZone = true;
    [Tooltip("ถ้าเปิด เมื่อ Player ออกจาก Boss Zone ระหว่างโจมตี Boss จะยกเลิกและกลับเป็น Idle")]
    public bool stopWhenPlayerLeavesZone = true;
    [Tooltip("ถ้าเปิด Boss จะถูกกันไม่ให้เดิน/พุ่งออกนอก Boss Zone")]
    public bool keepBossInsideBossZone = true;
    [Tooltip("กัน Boss ไม่ให้ชิดขอบ Zone เกินไป")]
    public float bossZoneEdgePadding = 0.35f;

    [Header("=== Movement ===")]
    public float chaseSpeed = 4f;
    public float rotationSpeed = 12f;
    [Tooltip("ใช้เป็นค่าบวก เช่น 20 หรือ 40")]
    public float gravity = 25f;

    [Header("=== Melee Phase ===")]
    public int meleeAttacksPerCycle = 3;
    public float meleeAttackRange = 2.4f;
    public float meleeAttackRadius = 2.7f;
    public float meleeDamage = 15f;
    [Tooltip("เวลาขึ้นวงแดงก่อนทำดาเมจ")]
    public float meleeWarningTime = 0.8f;
    public float meleeRecoverTime = 0.35f;
    public float meleeAttackHeightOffset = 0.2f;

    [Header("=== Charge Phase ===")]
    public int chargesPerCycle = 2;
    public float chargeDistance = 13f;
    public float chargeSpeed = 18f;
    public float chargeWidth = 2.8f;
    public float chargeDamage = 25f;
    public float chargeHitRadius = 1.4f;
    [Tooltip("เวลาแสดงกรอบสี่เหลี่ยมบนพื้นก่อนพุ่ง")]
    public float chargeWarningTime = 0.9f;
    public float chargeRecoverTime = 0.45f;

    [Header("=== Shoot Phase ===")]
    public int shotsPerCycle = 5;
    public Transform firePoint;
    public GameObject projectilePrefab;
    public float projectileSpeed = 16f;
    public float projectileDamage = 10f;
    public float projectileLifetime = 5f;
    [Tooltip("เวลาขึ้นเส้นแดงก่อนยิงแต่ละนัด")]
    public float shootWarningTime = 0.45f;
    public float shotInterval = 0.25f;
    public float warningLineMaxLength = 35f;

    [Header("=== Cycle Cooldown ===")]
    [Tooltip("หลังยิงครบชุด Boss จะยืนนิ่ง Cooldown ก่อนวนกลับไป Melee")]
    public float cycleCooldown = 2f;

    [Header("=== Warning Visuals ===")]
    public Color warningColor = Color.red;
    public float warningLineWidth = 0.08f;
    public float groundOffset = 0.05f;
    public LayerMask groundLayer = ~0;
    public float groundRayHeight = 5f;
    public float groundRayDistance = 20f;

    [Header("=== Debug ===")]
    public bool drawGizmos = true;
    public bool drawBossZoneGizmo = true;

    private CharacterController controller;
    private EnemyHealth enemyHealth;
    private BossState state = BossState.Idle;

    private LineRenderer meleeCircleLine;
    private LineRenderer chargeRectLine;
    private LineRenderer shootWarningLine;

    private float verticalVelocity;
    private bool isRunningAction;
    private bool cycleStarted;

    private const int CircleSegments = 72;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (firePoint == null)
            firePoint = transform;

        SetupWarningRenderers();
    }

    private void OnEnable()
    {
        if (!cycleStarted)
        {
            cycleStarted = true;
            StartCoroutine(BossLoopRoutine());
        }
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead)
        {
            HideAllWarnings();
            return;
        }

        TryFindPlayer();
        ApplyGravityOnly();
    }

    private IEnumerator BossLoopRoutine()
    {
        while (true)
        {
            if (enemyHealth != null && enemyHealth.IsDead)
                yield break;

            TryFindPlayer();

            if (!IsPlayerActive())
            {
                state = BossState.Idle;
                HideAllWarnings();
                yield return null;
                continue;
            }

            state = BossState.MeleePhase;
            yield return StartCoroutine(MeleePhaseRoutine());

            if (!IsPlayerActive()) continue;

            state = BossState.ChargePhase;
            yield return StartCoroutine(ChargePhaseRoutine());

            if (!IsPlayerActive()) continue;

            state = BossState.ShootPhase;
            yield return StartCoroutine(ShootPhaseRoutine());

            if (!IsPlayerActive()) continue;

            state = BossState.Cooldown;
            HideAllWarnings();
            yield return new WaitForSeconds(cycleCooldown);
        }
    }

    private IEnumerator MeleePhaseRoutine()
    {
        int count = Mathf.Max(1, meleeAttacksPerCycle);

        for (int i = 0; i < count; i++)
        {
            if (!IsPlayerActive()) yield break;

            yield return StartCoroutine(ChaseUntilMeleeRange());

            if (!IsPlayerActive()) yield break;

            yield return StartCoroutine(MeleeAttackRoutine());
        }
    }

    private IEnumerator ChaseUntilMeleeRange()
    {
        while (IsPlayerActive() && DistanceToPlayer() > meleeAttackRange)
        {
            if (enemyHealth != null && enemyHealth.IsDead) yield break;

            Vector3 direction = player.position - transform.position;
            direction.y = 0f;

            MoveHorizontal(direction.normalized, chaseSpeed);
            FaceDirection(direction);

            yield return null;
        }
    }

    private IEnumerator MeleeAttackRoutine()
    {
        isRunningAction = true;

        Vector3 attackCenter = GetGroundPoint(transform.position) + Vector3.up * meleeAttackHeightOffset;
        float timer = 0f;

        while (timer < meleeWarningTime)
        {
            if (ShouldCancelCurrentAction())
            {
                HideMeleeCircle();
                isRunningAction = false;
                yield break;
            }

            if (player != null)
                FaceDirection(player.position - transform.position);

            float t = meleeWarningTime <= 0f ? 1f : Mathf.Clamp01(timer / meleeWarningTime);
            float radius = Mathf.Lerp(0.05f, meleeAttackRadius, t);
            DrawMeleeCircle(attackCenter, radius);

            timer += Time.deltaTime;
            yield return null;
        }

        if (!ShouldCancelCurrentAction())
        {
            DrawMeleeCircle(attackCenter, meleeAttackRadius);
            DealMeleeDamage(attackCenter);
        }

        yield return new WaitForSeconds(0.08f);
        HideMeleeCircle();
        yield return new WaitForSeconds(meleeRecoverTime);

        isRunningAction = false;
    }

    private void DealMeleeDamage(Vector3 center)
    {
        if (!IsPlayerActive()) return;

        Collider[] hits = Physics.OverlapSphere(center, meleeAttackRadius, playerLayer, QueryTriggerInteraction.Ignore);
        HashSet<PlayerHealth> damaged = new HashSet<PlayerHealth>();

        foreach (Collider hit in hits)
        {
            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp == null || damaged.Contains(hp)) continue;

            hp.TakeDamage(meleeDamage);
            damaged.Add(hp);
        }
    }

    private IEnumerator ChargePhaseRoutine()
    {
        int count = Mathf.Max(1, chargesPerCycle);

        for (int i = 0; i < count; i++)
        {
            if (!IsPlayerActive()) yield break;
            yield return StartCoroutine(ChargeAttackRoutine());
        }
    }

    private IEnumerator ChargeAttackRoutine()
    {
        isRunningAction = true;

        Vector3 startPoint = GetGroundPoint(transform.position);
        Vector3 chargeDirection = GetDirectionToPlayerFlat();

        if (chargeDirection.sqrMagnitude <= 0.01f)
            chargeDirection = transform.forward;

        chargeDirection.y = 0f;
        chargeDirection.Normalize();

        FaceDirectionInstant(chargeDirection);

        float allowedChargeDistance = GetAllowedChargeDistance(transform.position, chargeDirection, chargeDistance);
        if (allowedChargeDistance <= 0.15f)
        {
            HideChargeRectangle();
            yield return new WaitForSeconds(chargeRecoverTime);
            isRunningAction = false;
            yield break;
        }

        float timer = 0f;
        while (timer < chargeWarningTime)
        {
            if (ShouldCancelCurrentAction())
            {
                HideChargeRectangle();
                isRunningAction = false;
                yield break;
            }

            // Lock ทิศทางตั้งแต่ช่วงเตือน เพื่อให้ Player อ่านทางแล้วหลบได้
            DrawChargeRectangle(startPoint, chargeDirection, allowedChargeDistance, chargeWidth);
            FaceDirection(chargeDirection);

            timer += Time.deltaTime;
            yield return null;
        }

        HideChargeRectangle();

        float traveled = 0f;
        HashSet<PlayerHealth> damaged = new HashSet<PlayerHealth>();

        while (traveled < allowedChargeDistance)
        {
            if (enemyHealth != null && enemyHealth.IsDead) yield break;

            // ถ้า Player ออกจาก Zone ระหว่างพุ่ง จะยังหยุดได้ เพื่อกัน Boss พุ่งออกขอบ/ตกแมพ
            if (stopWhenPlayerLeavesZone && !IsPlayerActive())
                break;

            float step = Mathf.Min(chargeSpeed * Time.deltaTime, allowedChargeDistance - traveled);
            Vector3 motion = chargeDirection * step;
            MoveBoss(motion + Vector3.up * verticalVelocity * Time.deltaTime);
            traveled += step;

            DealChargeDamage(damaged);
            yield return null;
        }

        yield return new WaitForSeconds(chargeRecoverTime);
        isRunningAction = false;
    }

    private void DealChargeDamage(HashSet<PlayerHealth> damaged)
    {
        if (!IsPlayerActive()) return;

        Vector3 hitCenter = transform.position + Vector3.up * Mathf.Max(0.5f, controller.height * 0.5f);
        Collider[] hits = Physics.OverlapSphere(hitCenter, chargeHitRadius, playerLayer, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp == null || damaged.Contains(hp)) continue;

            hp.TakeDamage(chargeDamage);
            damaged.Add(hp);
        }
    }

    private IEnumerator ShootPhaseRoutine()
    {
        int count = Mathf.Max(1, shotsPerCycle);

        for (int i = 0; i < count; i++)
        {
            if (!IsPlayerActive()) yield break;
            yield return StartCoroutine(ShootOneShotRoutine());
            yield return new WaitForSeconds(shotInterval);
        }
    }

    private IEnumerator ShootOneShotRoutine()
    {
        isRunningAction = true;

        float timer = 0f;
        Vector3 shootDirection = transform.forward;

        while (timer < shootWarningTime)
        {
            if (ShouldCancelCurrentAction())
            {
                HideShootWarningLine();
                isRunningAction = false;
                yield break;
            }

            Vector3 targetPoint = GetPlayerAimPoint();
            shootDirection = (targetPoint - firePoint.position).normalized;

            FaceDirection(shootDirection);
            DrawShootWarningLine(shootDirection);

            timer += Time.deltaTime;
            yield return null;
        }

        HideShootWarningLine();

        if (!ShouldCancelCurrentAction())
            ShootProjectile(shootDirection);

        isRunningAction = false;
    }

    private void ShootProjectile(Vector3 direction)
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
            bullet.name = "Boss_Auto_Projectile";
            bullet.transform.position = firePoint.position;
            bullet.transform.rotation = Quaternion.LookRotation(direction);
            bullet.transform.localScale = Vector3.one * 0.22f;
        }

        BossProjectile projectile = bullet.GetComponent<BossProjectile>();
        if (projectile == null)
            projectile = bullet.AddComponent<BossProjectile>();

        projectile.Init(direction, projectileSpeed, projectileDamage, projectileLifetime, playerLayer, gameObject);
    }

    private bool ShouldCancelCurrentAction()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return true;
        if (stopWhenPlayerLeavesZone && !IsPlayerActive()) return true;
        return false;
    }

    private void MoveHorizontal(Vector3 direction, float speed)
    {
        direction.y = 0f;
        Vector3 horizontalMove = direction.normalized * speed;
        Vector3 motion = horizontalMove + Vector3.up * verticalVelocity;
        MoveBoss(motion * Time.deltaTime);
    }

    private void MoveBoss(Vector3 motion)
    {
        controller.Move(motion);

        if (!keepBossInsideBossZone || bossZone == null)
            return;

        Vector3 clampedPosition = ClampPointToBossZone(transform.position, bossZoneEdgePadding);
        Vector3 correction = clampedPosition - transform.position;
        correction.y = 0f;

        if (correction.sqrMagnitude > 0.000001f)
            controller.Move(correction);
    }

    private void ApplyGravityOnly()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity -= gravity * Time.deltaTime;

        // ถ้าไม่ได้ขยับแนวนอนในเฟรมนี้ ก็ยังต้อง Move แนวตั้งให้ตกลงพื้น
        if (!isRunningAction)
            MoveBoss(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private void TryFindPlayer()
    {
        if (player != null) return;

        GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (foundPlayer != null)
            player = foundPlayer.transform;
    }

    private bool IsPlayerActive()
    {
        if (player == null) return false;

        PlayerHealth hp = player.GetComponentInParent<PlayerHealth>();
        if (hp != null && hp.IsDead) return false;

        if (Vector3.Distance(transform.position, player.position) > activationRange)
            return false;

        if (requirePlayerInsideBossZone && !IsPlayerInsideBossZone())
            return false;

        return true;
    }

    private bool IsPlayerInsideBossZone()
    {
        if (bossZone == null) return true;
        if (player == null) return false;

        return IsPointInsideBossZone(player.position, 0f);
    }

    private bool IsPointInsideBossZone(Vector3 worldPoint, float padding)
    {
        if (bossZone == null) return true;

        BoxCollider box = bossZone as BoxCollider;
        if (box != null)
        {
            Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;

            float localPadX = padding / Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.x));
            float localPadZ = padding / Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.z));

            float maxX = Mathf.Max(0f, half.x - localPadX);
            float maxZ = Mathf.Max(0f, half.z - localPadZ);

            return Mathf.Abs(local.x) <= maxX && Mathf.Abs(local.z) <= maxZ;
        }

        Bounds b = bossZone.bounds;
        return worldPoint.x >= b.min.x + padding && worldPoint.x <= b.max.x - padding &&
               worldPoint.z >= b.min.z + padding && worldPoint.z <= b.max.z - padding;
    }

    private Vector3 ClampPointToBossZone(Vector3 worldPoint, float padding)
    {
        if (bossZone == null) return worldPoint;

        BoxCollider box = bossZone as BoxCollider;
        if (box != null)
        {
            Vector3 local = box.transform.InverseTransformPoint(worldPoint);
            Vector3 offsetFromCenter = local - box.center;
            Vector3 half = box.size * 0.5f;

            float localPadX = padding / Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.x));
            float localPadZ = padding / Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.z));

            float maxX = Mathf.Max(0f, half.x - localPadX);
            float maxZ = Mathf.Max(0f, half.z - localPadZ);

            offsetFromCenter.x = Mathf.Clamp(offsetFromCenter.x, -maxX, maxX);
            offsetFromCenter.z = Mathf.Clamp(offsetFromCenter.z, -maxZ, maxZ);

            Vector3 clampedLocal = box.center + offsetFromCenter;
            Vector3 clampedWorld = box.transform.TransformPoint(clampedLocal);
            clampedWorld.y = worldPoint.y;
            return clampedWorld;
        }

        Bounds b = bossZone.bounds;
        float minX = b.min.x + padding;
        float maxXBounds = b.max.x - padding;
        float minZ = b.min.z + padding;
        float maxZBounds = b.max.z - padding;

        if (minX > maxXBounds)
        {
            minX = b.center.x;
            maxXBounds = b.center.x;
        }

        if (minZ > maxZBounds)
        {
            minZ = b.center.z;
            maxZBounds = b.center.z;
        }

        return new Vector3(
            Mathf.Clamp(worldPoint.x, minX, maxXBounds),
            worldPoint.y,
            Mathf.Clamp(worldPoint.z, minZ, maxZBounds)
        );
    }

    private float GetAllowedChargeDistance(Vector3 startPosition, Vector3 direction, float requestedDistance)
    {
        if (!keepBossInsideBossZone || bossZone == null)
            return requestedDistance;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
            return 0f;

        direction.Normalize();

        if (!IsPointInsideBossZone(startPosition, bossZoneEdgePadding))
            return 0f;

        Vector3 endPoint = startPosition + direction * requestedDistance;
        if (IsPointInsideBossZone(endPoint, bossZoneEdgePadding))
            return requestedDistance;

        float low = 0f;
        float high = requestedDistance;

        // Binary search หาระยะพุ่งสูงสุดที่ยังไม่ออกนอก Boss Zone
        for (int i = 0; i < 14; i++)
        {
            float mid = (low + high) * 0.5f;
            Vector3 testPoint = startPosition + direction * mid;

            if (IsPointInsideBossZone(testPoint, bossZoneEdgePadding))
                low = mid;
            else
                high = mid;
        }

        return Mathf.Max(0f, low);
    }

    private float DistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector3.Distance(transform.position, player.position);
    }

    private Vector3 GetDirectionToPlayerFlat()
    {
        if (player == null) return transform.forward;
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        return direction.normalized;
    }

    private Vector3 GetPlayerAimPoint()
    {
        if (player == null) return transform.position + transform.forward;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
            return player.position + Vector3.up * (cc.height * 0.5f);

        return player.position + Vector3.up * 1f;
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void FaceDirectionInstant(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f) return;
        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private Vector3 GetGroundPoint(Vector3 point)
    {
        Vector3 rayStart = point + Vector3.up * groundRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, groundRayDistance, groundLayer, QueryTriggerInteraction.Ignore);

        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;

                return hit.point + Vector3.up * groundOffset;
            }
        }

        return point + Vector3.up * groundOffset;
    }

    private Vector3 ProjectPointToGround(Vector3 point)
    {
        return GetGroundPoint(point);
    }

    private void SetupWarningRenderers()
    {
        meleeCircleLine = CreateLineRenderer("Boss_Melee_Warning_Circle", true);
        chargeRectLine = CreateLineRenderer("Boss_Charge_Warning_Rectangle", true);
        shootWarningLine = CreateLineRenderer("Boss_Shoot_Warning_Line", true);

        HideAllWarnings();
    }

    private LineRenderer CreateLineRenderer(string objectName, bool worldSpace)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform);

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = worldSpace;
        lr.startWidth = warningLineWidth;
        lr.endWidth = warningLineWidth;
        lr.startColor = warningColor;
        lr.endColor = warningColor;
        lr.loop = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            lr.material = new Material(shader);

        return lr;
    }

    private void DrawMeleeCircle(Vector3 center, float radius)
    {
        if (meleeCircleLine == null) return;

        meleeCircleLine.enabled = true;
        meleeCircleLine.positionCount = CircleSegments + 1;

        for (int i = 0; i <= CircleSegments; i++)
        {
            float angle = (Mathf.PI * 2f / CircleSegments) * i;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            pos = ProjectPointToGround(pos);
            meleeCircleLine.SetPosition(i, pos);
        }
    }

    private void HideMeleeCircle()
    {
        if (meleeCircleLine != null) meleeCircleLine.enabled = false;
    }

    private void DrawChargeRectangle(Vector3 startPoint, Vector3 direction, float length, float width)
    {
        if (chargeRectLine == null) return;

        direction.y = 0f;
        direction.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        float halfWidth = width * 0.5f;

        Vector3 p1 = startPoint - right * halfWidth;
        Vector3 p2 = startPoint + right * halfWidth;
        Vector3 endCenter = startPoint + direction * length;
        Vector3 p3 = endCenter + right * halfWidth;
        Vector3 p4 = endCenter - right * halfWidth;

        chargeRectLine.enabled = true;
        chargeRectLine.positionCount = 5;
        chargeRectLine.SetPosition(0, ProjectPointToGround(p1));
        chargeRectLine.SetPosition(1, ProjectPointToGround(p2));
        chargeRectLine.SetPosition(2, ProjectPointToGround(p3));
        chargeRectLine.SetPosition(3, ProjectPointToGround(p4));
        chargeRectLine.SetPosition(4, ProjectPointToGround(p1));
    }

    private void HideChargeRectangle()
    {
        if (chargeRectLine != null) chargeRectLine.enabled = false;
    }

    private void DrawShootWarningLine(Vector3 direction)
    {
        if (shootWarningLine == null || firePoint == null) return;

        shootWarningLine.enabled = true;
        shootWarningLine.positionCount = 2;
        shootWarningLine.SetPosition(0, firePoint.position);
        shootWarningLine.SetPosition(1, firePoint.position + direction.normalized * warningLineMaxLength);
    }

    private void HideShootWarningLine()
    {
        if (shootWarningLine != null) shootWarningLine.enabled = false;
    }

    private void HideAllWarnings()
    {
        HideMeleeCircle();
        HideChargeRectangle();
        HideShootWarningLine();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, chargeHitRadius);

        if (drawBossZoneGizmo && bossZone != null)
        {
            Gizmos.color = Color.green;

            BoxCollider box = bossZone as BoxCollider;
            if (box != null)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else
            {
                Gizmos.DrawWireCube(bossZone.bounds.center, bossZone.bounds.size);
            }
        }
    }
}
