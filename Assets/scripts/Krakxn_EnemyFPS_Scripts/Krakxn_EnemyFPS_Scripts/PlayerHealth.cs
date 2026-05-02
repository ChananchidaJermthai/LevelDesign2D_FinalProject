using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player Health System
/// EnemyMeleePatrol และ EnemyProjectile จะเรียก TakeDamage()
/// FPSPlayerController จะเรียก SetInvincible() ตอน Dash ถ้าเปิด dashInvincible
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("=== Health ===")]
    public float maxHealth = 100f;
    public float currentHealth { get; private set; }

    [Header("=== Invincibility ===")]
    public float invincibilityDuration = 0.5f;

    [Header("=== UI Optional ===")]
    public Slider healthBarSlider;
    public Image healthBarFill;

    [Header("=== Hit Feedback Optional ===")]
    public bool useHitFlash = true;
    public Color hitColor = Color.red;
    public float flashDuration = 0.1f;

    [Header("=== Death ===")]
    public GameObject deathEffect;
    public bool disablePlayerOnDeath = true;

    public System.Action<float, float> OnHealthChanged;
    public System.Action OnDeath;

    private bool isInvincible;
    private bool isInvincibleForced;
    private bool isDead;
    private Renderer[] renderers;
    private Color[][] originalColors;

    public bool IsDead => isDead;
    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        CacheRendererColors();
        UpdateHealthBar();
    }

    public void TakeDamage(float amount)
    {
        if (isDead || isInvincible || isInvincibleForced || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthBar();

        Debug.Log($"[PlayerHealth] Took {amount} damage. HP = {currentHealth}/{maxHealth}");

        if (useHitFlash)
            StartCoroutine(HitFlashRoutine());

        StartCoroutine(InvincibilityRoutine());

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

    public void SetInvincible(bool state)
    {
        isInvincibleForced = state;
    }

    public void Revive(bool fullHealth = true)
    {
        isDead = false;
        if (fullHealth) currentHealth = maxHealth;
        UpdateHealthBar();
        gameObject.SetActive(true);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("[PlayerHealth] Player died.");

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        OnDeath?.Invoke();

        if (disablePlayerOnDeath)
            gameObject.SetActive(false);
    }

    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    private void UpdateHealthBar()
    {
        if (healthBarSlider != null)
            healthBarSlider.value = HealthPercent;

        if (healthBarFill != null)
        {
            float pct = HealthPercent;
            healthBarFill.color = pct > 0.5f
                ? Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f)
                : Color.Lerp(Color.red, Color.yellow, pct * 2f);
        }
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
