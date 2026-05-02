using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player Attack แบบ Melee Raycast / Overlap 3D
/// - กด Mouse Left เพื่อโจมตี
/// - ใช้ SphereCast จากกล้องไปด้านหน้า
/// - โดน EnemyHealth แล้วลด HP
/// </summary>
public class PlayerMeleeAttack3D : MonoBehaviour
{
    [Header("=== Input ===")]
    public KeyCode attackKey = KeyCode.Mouse0;

    [Header("=== Attack Settings ===")]
    public float attackDamage = 20f;
    public float attackRange = 2.3f;
    public float attackRadius = 0.45f;
    public float attackCooldown = 0.45f;
    public bool canHitMultipleEnemies = false;

    [Header("=== Detection ===")]
    public Transform attackOrigin;
    public LayerMask enemyLayer = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("=== Debug ===")]
    public bool drawDebugRay = true;
    public float debugRayTime = 0.1f;

    private PlayerHealth playerHealth;
    private float nextAttackTime;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (attackOrigin == null && Camera.main != null)
            attackOrigin = Camera.main.transform;
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;

        if (Input.GetKeyDown(attackKey) && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            Attack();
        }
    }

    private void Attack()
    {
        if (attackOrigin == null)
        {
            Debug.LogWarning("[PlayerMeleeAttack3D] Missing attackOrigin. Assign Camera or AttackPoint.");
            return;
        }

        Vector3 origin = attackOrigin.position;
        Vector3 direction = attackOrigin.forward;

        if (drawDebugRay)
            Debug.DrawRay(origin, direction * attackRange, Color.red, debugRayTime);

        RaycastHit[] hits = Physics.SphereCastAll(origin, attackRadius, direction, attackRange, enemyLayer, triggerInteraction);
        if (hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

        foreach (RaycastHit hit in hits)
        {
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy == null || damagedEnemies.Contains(enemy)) continue;

            enemy.TakeDamage(attackDamage);
            damagedEnemies.Add(enemy);

            if (!canHitMultipleEnemies)
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = attackOrigin;
        if (origin == null && Camera.main != null) origin = Camera.main.transform;
        if (origin == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin.position + origin.forward * attackRange, attackRadius);
    }
}
