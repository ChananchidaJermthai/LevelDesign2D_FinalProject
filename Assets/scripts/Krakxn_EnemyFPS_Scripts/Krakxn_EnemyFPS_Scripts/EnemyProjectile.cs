using UnityEngine;

/// <summary>
/// กระสุนของ Enemy 2
/// ต้องมี Collider และ Rigidbody
/// ถ้าไม่มี Script จะสร้าง Rigidbody ให้เอง
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    private float damage = 10f;
    private float lifeTime = 5f;
    private LayerMask playerLayer = ~0;
    private GameObject owner;
    private Rigidbody rb;
    private bool initialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public void Init(Vector3 direction, float speed, float newDamage, float newLifeTime, LayerMask newPlayerLayer, GameObject newOwner)
    {
        initialized = true;
        damage = newDamage;
        lifeTime = newLifeTime;
        playerLayer = newPlayerLayer;
        owner = newOwner;

        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.linearVelocity = direction.normalized * speed;

        Destroy(gameObject, lifeTime);
    }

    private void Start()
    {
        if (!initialized)
            Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        if (owner != null && other.transform.IsChildOf(owner.transform)) return;

        bool isPlayerLayer = (playerLayer.value & (1 << other.gameObject.layer)) != 0;
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (isPlayerLayer && playerHealth != null)
            playerHealth.TakeDamage(damage);

        Destroy(gameObject);
    }
}
