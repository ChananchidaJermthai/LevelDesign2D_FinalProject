using UnityEngine;

/// <summary>
/// Projectile สำหรับ BossEnemyController
/// - เคลื่อนที่เอง ไม่จำเป็นต้องใช้ Rigidbody
/// - ใช้ SphereCast กันกระสุนทะลุ Player ตอนความเร็วสูง
/// - ถ้ามี Collider / Rigidbody อยู่แล้วก็ใช้ร่วมได้
/// </summary>
public class BossProjectile : MonoBehaviour
{
    public float hitRadius = 0.12f;

    private Vector3 direction;
    private float speed;
    private float damage;
    private float lifeTimer;
    private LayerMask playerLayer;
    private GameObject owner;
    private Vector3 previousPosition;
    private bool initialized;

    public void Init(Vector3 direction, float speed, float damage, float lifetime, LayerMask playerLayer, GameObject owner)
    {
        this.direction = direction.normalized;
        this.speed = speed;
        this.damage = damage;
        this.lifeTimer = lifetime;
        this.playerLayer = playerLayer;
        this.owner = owner;
        previousPosition = transform.position;
        initialized = true;

        EnsurePhysicsSetup();
        Destroy(gameObject, Mathf.Max(0.1f, lifetime));
    }

    private void Awake()
    {
        previousPosition = transform.position;
    }

    private void Update()
    {
        if (!initialized) return;

        float distance = speed * Time.deltaTime;
        Vector3 start = transform.position;
        Vector3 end = start + direction * distance;

        if (Physics.SphereCast(start, hitRadius, direction, out RaycastHit hit, distance, playerLayer, QueryTriggerInteraction.Ignore))
        {
            TryDamage(hit.collider);
            Destroy(gameObject);
            return;
        }

        transform.position = end;
        previousPosition = transform.position;

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized) return;
        if (IsOwner(other)) return;

        if (TryDamage(other))
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!initialized) return;
        if (collision.collider != null && IsOwner(collision.collider)) return;

        if (collision.collider != null && TryDamage(collision.collider))
            Destroy(gameObject);
    }

    private bool TryDamage(Collider hit)
    {
        if (hit == null) return false;
        if (IsOwner(hit)) return false;

        PlayerHealth hp = hit.GetComponentInParent<PlayerHealth>();
        if (hp == null) return false;

        hp.TakeDamage(damage);
        return true;
    }

    private bool IsOwner(Collider other)
    {
        if (owner == null || other == null) return false;
        return other.transform == owner.transform || other.transform.IsChildOf(owner.transform);
    }

    private void EnsurePhysicsSetup()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = Mathf.Max(0.05f, hitRadius);
            sphere.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
