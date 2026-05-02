using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 1:
/// - เดินสุ่มไปมาแถวจุดเกิด
/// - เห็น Player แล้วไล่
/// - ถ้า Player ออกนอกระยะ หรือ Enemy ถูกล่อไกลเกิน Leash Range จะกลับจุดเดินเดิม
/// - ก่อนโจมตีจะหยุดและแสดงวงแดงขยาย แล้วค่อยทำดาเมจ
/// - เพิ่ม Movement Zone เพื่อกัน Enemy เดิน/ไล่ออกนอกพื้นที่จนตกแมพ
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyMeleePatrol : MonoBehaviour
{
    private enum State
    {
        Wander,
        Chase,
        ReturnToSpawn,
        Attack
    }

    [Header("=== Target ===")]
    public Transform player;
    public LayerMask playerLayer = ~0;
    public string playerTag = "Player";

    [Header("=== Movement ===")]
    public float walkSpeed = 2f;
    public float chaseSpeed = 4f;
    public float rotationSpeed = 12f;
    public float gravity = 20f;

    [Header("=== Wander ===")]
    public float wanderRadius = 6f;
    public float waitAtPointTime = 1.2f;
    public float arriveDistance = 0.35f;

    [Header("=== Detection / Leash ===")]
    public float detectRange = 9f;
    public float losePlayerRange = 12f;
    [Tooltip("ถ้า Enemy ถูกล่อออกจากจุดเกิดเกินค่านี้ จะเลิกตาม Player และเดินกลับ")]
    public float leashRange = 14f;

    [Header("=== Movement Zone / Anti Fall ===")]
    [Tooltip("ลาก BoxCollider/Collider ของพื้นที่ที่ Enemy อนุญาตให้เดินได้มาใส่ ถ้าไม่ใส่จะใช้ Logic เดิมทั้งหมด")]
    public Collider movementZone;
    [Tooltip("ถ้าเปิดไว้ ถึง Enemy จะเห็น Player แต่ถ้า Player ยังไม่เข้า Zone จะไม่ไล่/ไม่โจมตี")]
    public bool requirePlayerInsideZoneToChase = true;
    [Tooltip("ถ้า Player ออกจาก Zone ระหว่างสู้ Enemy จะหยุดไล่และกลับไปเดินปกติ")]
    public bool stopChaseWhenPlayerLeavesZone = true;
    [Tooltip("บังคับไม่ให้ Enemy เดินออกนอก Zone")]
    public bool keepEnemyInsideZone = true;
    [Tooltip("สุ่มจุดเดินเล่นให้อยู่ใน Zone ด้วย")]
    public bool clampWanderTargetInsideZone = true;
    [Tooltip("เว้นระยะจากขอบ Zone เพื่อกัน CharacterController คร่อมขอบแล้วตกแมพ")]
    public float zoneEdgePadding = 0.4f;
    [Tooltip("เช็ค Zone เฉพาะแกน X/Z เพื่อใช้เป็นพื้นที่บนพื้น ไม่สนความสูง Y")]
    public bool ignoreZoneY = true;

    [Header("=== Attack ===")]
    public float attackRange = 2.2f;
    public float attackRadius = 2.5f;
    public float attackDamage = 15f;
    [Tooltip("เวลาก่อนทำดาเมจ วงแดงจะขยายในช่วงนี้")]
    public float attackWarningTime = 0.8f;
    public float attackCooldown = 1.4f;
    public float attackRecoverTime = 0.25f;
    public float attackHeightOffset = 0.2f;

    [Header("=== Warning Circle ===")]
    public GroundWarningCircle warningCircle;
    public LayerMask groundLayer = ~0;
    public float groundRayHeight = 3f;
    public float groundRayDistance = 10f;

    [Header("=== Debug ===")]
    public bool drawGizmos = true;

    private CharacterController controller;
    private EnemyHealth enemyHealth;
    private State state = State.Wander;

    private Vector3 spawnPosition;
    private Vector3 wanderTarget;
    private float verticalVelocity;
    private float waitTimer;
    private float lastAttackTime = -999f;
    private bool isAttacking;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        enemyHealth = GetComponent<EnemyHealth>();
        spawnPosition = transform.position;

        if (movementZone != null && keepEnemyInsideZone)
        {
            spawnPosition = ClampPointToMovementZone(spawnPosition);
            transform.position = spawnPosition;
        }

        if (warningCircle == null)
        {
            GameObject circleObj = new GameObject("Attack_Warning_Circle");
            warningCircle = circleObj.AddComponent<GroundWarningCircle>();
        }

        PickNewWanderTarget();
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;

        TryFindPlayer();
        ApplyGravity();

        if (isAttacking)
            return;

        bool playerBlockedByZone = ShouldIgnorePlayerBecauseOutsideZone();
        float distanceToSpawn = Vector3.Distance(transform.position, spawnPosition);

        if (distanceToSpawn > leashRange)
        {
            state = State.ReturnToSpawn;
        }
        else if (!playerBlockedByZone && CanSeePlayer())
        {
            float playerDistance = Vector3.Distance(transform.position, player.position);
            state = playerDistance <= attackRange ? State.Attack : State.Chase;
        }
        else
        {
            if (stopChaseWhenPlayerLeavesZone && playerBlockedByZone && state == State.Chase)
            {
                PickNewWanderTarget();
            }

            state = State.Wander;
        }

        switch (state)
        {
            case State.Wander:
                Wander();
                break;
            case State.Chase:
                ChasePlayer();
                break;
            case State.ReturnToSpawn:
                ReturnToSpawn();
                break;
            case State.Attack:
                TryAttack();
                break;
        }
    }

    private void TryFindPlayer()
    {
        if (player != null) return;

        GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (foundPlayer != null)
            player = foundPlayer.transform;
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.position);
        return distance <= detectRange || distance <= losePlayerRange && state == State.Chase;
    }

    private bool ShouldIgnorePlayerBecauseOutsideZone()
    {
        if (movementZone == null) return false;
        if (player == null) return false;
        if (!requirePlayerInsideZoneToChase && !stopChaseWhenPlayerLeavesZone) return false;

        bool playerInsideZone = IsPointInsideMovementZone(player.position);
        return !playerInsideZone;
    }

    private void Wander()
    {
        float distance = Vector3.Distance(transform.position, wanderTarget);

        if (distance <= arriveDistance)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtPointTime)
            {
                waitTimer = 0f;
                PickNewWanderTarget();
            }
            Move(Vector3.zero, walkSpeed);
            return;
        }

        MoveTowards(wanderTarget, walkSpeed);
    }

    private void ChasePlayer()
    {
        if (player == null) return;
        if (ShouldIgnorePlayerBecauseOutsideZone())
        {
            state = State.Wander;
            return;
        }

        MoveTowards(player.position, chaseSpeed);
    }

    private void ReturnToSpawn()
    {
        float distance = Vector3.Distance(transform.position, spawnPosition);
        if (distance <= arriveDistance)
        {
            PickNewWanderTarget();
            state = State.Wander;
            return;
        }

        MoveTowards(spawnPosition, walkSpeed);
    }

    private void TryAttack()
    {
        if (ShouldIgnorePlayerBecauseOutsideZone()) return;
        if (Time.time < lastAttackTime + attackCooldown) return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        state = State.Attack;

        Vector3 attackCenter = GetGroundPoint(transform.position) + Vector3.up * attackHeightOffset;

        float timer = 0f;
        while (timer < attackWarningTime)
        {
            if (ShouldIgnorePlayerBecauseOutsideZone())
            {
                warningCircle.Hide();
                isAttacking = false;
                yield break;
            }

            if (player != null)
                FaceDirection(player.position - transform.position);

            float t = attackWarningTime <= 0f ? 1f : timer / attackWarningTime;
            float radius = Mathf.Lerp(0.05f, attackRadius, t);
            warningCircle.Draw(attackCenter, radius);

            timer += Time.deltaTime;
            yield return null;
        }

        warningCircle.Draw(attackCenter, attackRadius);
        DealAreaDamage(attackCenter);

        yield return new WaitForSeconds(0.08f);
        warningCircle.Hide();

        lastAttackTime = Time.time;
        yield return new WaitForSeconds(attackRecoverTime);

        isAttacking = false;
    }

    private void DealAreaDamage(Vector3 center)
    {
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, playerLayer, QueryTriggerInteraction.Ignore);
        HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();

        foreach (Collider hit in hits)
        {
            PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
            if (hp == null || damagedPlayers.Contains(hp)) continue;

            if (movementZone != null && requirePlayerInsideZoneToChase && !IsPointInsideMovementZone(hp.transform.position))
                continue;

            hp.TakeDamage(attackDamage);
            damagedPlayers.Add(hp);
        }
    }

    private void PickNewWanderTarget()
    {
        Vector2 random = Random.insideUnitCircle * wanderRadius;
        wanderTarget = spawnPosition + new Vector3(random.x, 0f, random.y);

        if (movementZone != null && clampWanderTargetInsideZone)
            wanderTarget = ClampPointToMovementZone(wanderTarget);

        wanderTarget = GetGroundPoint(wanderTarget);
    }

    private void MoveTowards(Vector3 target, float speed)
    {
        if (movementZone != null && keepEnemyInsideZone)
            target = ClampPointToMovementZone(target);

        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            Move(Vector3.zero, speed);
            return;
        }

        Move(direction.normalized, speed);
        FaceDirection(direction);
    }

    private void Move(Vector3 direction, float speed)
    {
        Vector3 horizontalMove = direction * speed;

        if (movementZone != null && keepEnemyInsideZone)
        {
            Vector3 proposedPosition = transform.position + horizontalMove * Time.deltaTime;
            Vector3 clampedPosition = ClampPointToMovementZone(proposedPosition);
            Vector3 allowedMove = (clampedPosition - transform.position) / Mathf.Max(Time.deltaTime, 0.0001f);
            allowedMove.y = 0f;
            horizontalMove = allowedMove;
        }

        Vector3 motion = horizontalMove + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        if (movementZone != null && keepEnemyInsideZone && !IsPointInsideMovementZone(transform.position))
        {
            Vector3 clamped = ClampPointToMovementZone(transform.position);
            clamped.y = transform.position.y;

            controller.enabled = false;
            transform.position = clamped;
            controller.enabled = true;
        }
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity -= gravity * Time.deltaTime;
    }

    private Vector3 GetGroundPoint(Vector3 point)
    {
        Vector3 rayStart = point + Vector3.up * groundRayHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayDistance, groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point;

        return point;
    }

    private bool IsPointInsideMovementZone(Vector3 worldPoint)
    {
        if (movementZone == null) return true;

        BoxCollider box = movementZone as BoxCollider;
        if (box != null)
        {
            Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint);
            Vector3 half = box.size * 0.5f;
            Vector3 min = box.center - half;
            Vector3 max = box.center + half;

            float padding = Mathf.Max(0f, zoneEdgePadding);

            bool insideX = localPoint.x >= min.x + padding && localPoint.x <= max.x - padding;
            bool insideZ = localPoint.z >= min.z + padding && localPoint.z <= max.z - padding;
            bool insideY = ignoreZoneY || (localPoint.y >= min.y && localPoint.y <= max.y);

            return insideX && insideZ && insideY;
        }

        Bounds bounds = movementZone.bounds;
        float p = Mathf.Max(0f, zoneEdgePadding);

        bool boundsInsideX = worldPoint.x >= bounds.min.x + p && worldPoint.x <= bounds.max.x - p;
        bool boundsInsideZ = worldPoint.z >= bounds.min.z + p && worldPoint.z <= bounds.max.z - p;
        bool boundsInsideY = ignoreZoneY || (worldPoint.y >= bounds.min.y && worldPoint.y <= bounds.max.y);

        return boundsInsideX && boundsInsideZ && boundsInsideY;
    }

    private Vector3 ClampPointToMovementZone(Vector3 worldPoint)
    {
        if (movementZone == null) return worldPoint;

        BoxCollider box = movementZone as BoxCollider;
        if (box != null)
        {
            Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint);
            Vector3 half = box.size * 0.5f;
            Vector3 min = box.center - half;
            Vector3 max = box.center + half;

            float padding = Mathf.Max(0f, zoneEdgePadding);
            float minX = min.x + padding;
            float maxX = max.x - padding;
            float minZ = min.z + padding;
            float maxZ = max.z - padding;

            if (minX > maxX)
            {
                float middleX = (min.x + max.x) * 0.5f;
                minX = middleX;
                maxX = middleX;
            }

            if (minZ > maxZ)
            {
                float middleZ = (min.z + max.z) * 0.5f;
                minZ = middleZ;
                maxZ = middleZ;
            }

            localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
            localPoint.z = Mathf.Clamp(localPoint.z, minZ, maxZ);

            return box.transform.TransformPoint(localPoint);
        }

        Bounds bounds = movementZone.bounds;
        float p = Mathf.Max(0f, zoneEdgePadding);

        worldPoint.x = Mathf.Clamp(worldPoint.x, bounds.min.x + p, bounds.max.x - p);
        worldPoint.z = Mathf.Clamp(worldPoint.z, bounds.min.z + p, bounds.max.z - p);
        return worldPoint;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 origin = Application.isPlaying ? spawnPosition : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, wanderRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origin, leashRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        if (movementZone != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);

            BoxCollider box = movementZone as BoxCollider;
            if (box != null)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else
            {
                Gizmos.DrawWireCube(movementZone.bounds.center, movementZone.bounds.size);
            }
        }
    }
}
