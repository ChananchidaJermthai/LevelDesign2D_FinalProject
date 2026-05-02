using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ใส่ไว้บน Enemy ทุกตัวที่อยากให้ Player โจมตีได้
/// ใช้คู่กับ PlayerMeleeAttack3D
/// มีระบบหลอดเลือด Enemy ผ่าน Slider/Image UI
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("=== Health ===")]
    public float maxHealth = 50f;
    public float currentHealth { get; private set; }

    [Header("=== Health Bar UI ===")]
    [Tooltip("ลาก Slider หลอดเลือดของ Enemy มาใส่ ถ้าไม่ใส่ก็ยังใช้ระบบเลือดได้ตามปกติ")]
    public Slider healthBarSlider;

    [Tooltip("ลาก Image Fill ของ Slider มาใส่ ถ้าอยากเปลี่ยนสีตามเลือด")]
    public Image healthBarFill;

    [Tooltip("ใช้ Gradient เปลี่ยนสีหลอดเลือด เช่น เขียว -> เหลือง -> แดง")]
    public Gradient healthBarColor = new Gradient
    {
        colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.red, 0f),
            new GradientColorKey(Color.yellow, 0.5f),
            new GradientColorKey(Color.green, 1f)
        },
        alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        }
    };

    [Tooltip("ซ่อนหลอดเลือดตอนเลือดเต็ม")]
    public bool hideHealthBarWhenFull = true;

    [Tooltip("ถ้ามี CanvasGroup ให้ลากมาใส่ จะใช้ซ่อน/แสดงแบบไม่ต้อง SetActive")]
    public CanvasGroup healthBarCanvasGroup;

    [Tooltip("Root ของหลอดเลือด เช่น World Space Canvas ที่อยู่เหนือหัว Enemy")]
    public Transform healthBarRoot;

    [Tooltip("ให้หลอดเลือดหันเข้าหากล้องตลอด เหมาะกับ World Space Canvas")]
    public bool faceCamera = true;

    [Tooltip("ถ้าเปิด จะให้ healthBarRoot ลอยตามตำแหน่ง Enemy + Offset")]
    public bool followEnemy = true;

    [Tooltip("ตำแหน่งหลอดเลือดเหนือหัว Enemy")]
    public Vector3 healthBarOffset = new Vector3(0f, 2.2f, 0f);

    [Header("=== Hit Feedback ===")]
    public bool useHitFlash = true;
    public Color hitColor = Color.red;
    public float flashDuration = 0.08f;

    [Header("=== Death ===")]
    public GameObject deathEffect;
    public bool destroyOnDeath = true;
    public float destroyDelay = 0f;

    public System.Action<float, float> OnHealthChanged;
    public System.Action OnDeath;

    private bool isDead;
    private Renderer[] renderers;
    private Color[][] originalColors;

    public bool IsDead => isDead;
    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        CacheRendererColors();
    }

    private void Start()
    {
        SetupHealthBar();
        UpdateHealthBar();
    }

    private void LateUpdate()
    {
        UpdateWorldHealthBarTransform();
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthBar();

        Debug.Log($"[EnemyHealth] {name} took {damage} damage. HP = {currentHealth}/{maxHealth}");

        if (useHitFlash)
            StartCoroutine(HitFlashRoutine());

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthBar();
    }

    public void SetMaxHealth(float newMaxHealth, bool fillHealth = true)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);

        if (fillHealth)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthBar();
    }

    private void SetupHealthBar()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.minValue = 0f;
            healthBarSlider.maxValue = 1f;
            healthBarSlider.wholeNumbers = false;
        }

        if (healthBarRoot == null && healthBarSlider != null)
            healthBarRoot = healthBarSlider.transform;
    }

    private void UpdateHealthBar()
    {
        float percent = HealthPercent;

        if (healthBarSlider != null)
            healthBarSlider.value = percent;

        if (healthBarFill != null)
            healthBarFill.color = healthBarColor.Evaluate(percent);

        bool shouldShow = !hideHealthBarWhenFull || percent < 0.999f;
        SetHealthBarVisible(shouldShow && !isDead);
    }

    private void SetHealthBarVisible(bool visible)
    {
        if (healthBarCanvasGroup != null)
        {
            healthBarCanvasGroup.alpha = visible ? 1f : 0f;
            healthBarCanvasGroup.interactable = false;
            healthBarCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (healthBarSlider != null)
            healthBarSlider.gameObject.SetActive(visible);
    }

    private void UpdateWorldHealthBarTransform()
    {
        if (healthBarRoot == null) return;

        if (followEnemy)
            healthBarRoot.position = transform.position + healthBarOffset;

        if (!faceCamera) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 direction = healthBarRoot.position - cam.transform.position;
        if (direction.sqrMagnitude > 0.001f)
            healthBarRoot.rotation = Quaternion.LookRotation(direction);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[EnemyHealth] {name} died.");

        SetHealthBarVisible(false);

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        OnDeath?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    private void CacheRendererColors()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            originalColors[i] = new Color[mats.Length];

            for (int j = 0; j < mats.Length; j++)
                originalColors[i][j] = mats[j].color;
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (renderers == null || renderers.Length == 0)
            yield break;

        for (int i = 0; i < renderers.Length; i++)
        {
            foreach (Material mat in renderers[i].materials)
                mat.color = hitColor;
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int j = 0; j < mats.Length && j < originalColors[i].Length; j++)
                mats[j].color = originalColors[i][j];
        }
    }
}
