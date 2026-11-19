using System;
using UnityEngine;

/// <summary>
/// 玩家血量（最小可用版本）。
///
/// 這個腳本不是完整的玩家控制器，只負責「被打 → 扣血 → 通知 UI → 死亡」。
/// 之後你寫真正的 PlayerController 時，把血量邏輯合併進去、或直接保留這個腳本都可以。
/// 任何武器只要呼叫 TakeDamage(float) 就能造成傷害，不需要依賴 Tag。
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Debug")]
    [Tooltip("受傷時在 Console 印出剩餘血量。")]
    public bool logDamage = true;

    /// <summary>血量變化時通知 UI：(目前血量, 最大血量)。</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>玩家死亡時觸發，可用來開 Game Over 畫面。</summary>
    public event Action OnDied;

    public bool IsDead { get; private set; }
    public float HealthRatio => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (logDamage)
        {
            Debug.Log($"[PlayerHealth] 受到 {amount} 點傷害，剩餘 {currentHealth:F0}/{maxHealth:F0}", this);
        }

        if (currentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>復活／重來時把血量補滿。</summary>
    public void ResetHealth()
    {
        IsDead = false;
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Debug.Log("[PlayerHealth] 玩家被擊落", this);
        OnDied?.Invoke();

        // 這裡之後可以接：墜機動畫、Game Over UI、重新載入場景
    }
}
