using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 1:
/// - เดินสุ่มไปมาแถวจุดเกิด
/// - เห็น Player แล้วไล่
/// - ถ้า Player ออกนอกระยะ หรือ Enemy ถูกล่อไกลเกิน Leash Range จะกลับจุดเดินเดิม
/// - ก่อนโจมตีจะหยุดและแสดงวงแดงขยาย แล้วค่อยทำดาเมจ
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

        float distanceToSpawn = Vector3.Distance(transform.position, spawnPosition);

        if (distanceToSpawn > leashRange)
        {
            state = State.ReturnToSpawn;
        }
        else if (CanSeePlayer())
        {
            float playerDistance = Vector3.Distance(transform.position, player.position);
            state = playerDistance <= attackRange ? State.Attack : State.Chase;
        }
        else
        {
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

            hp.TakeDamage(attackDamage);
            damagedPlayers.Add(hp);
        }
    }

    private void PickNewWanderTarget()
    {
        Vector2 random = Random.insideUnitCircle * wanderRadius;
        wanderTarget = spawnPosition + new Vector3(random.x, 0f, random.y);
        wanderTarget = GetGroundPoint(wanderTarget);
    }

    private void MoveTowards(Vector3 target, float speed)
    {
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
        Vector3 motion = horizontalMove + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);
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
    }
}
