/// <summary>
/// 任何「可以被傷害」的東西都實作這個介面（玩家、Boss、可破壞障礙物……）。
/// 攻擊方寫 GetComponentInParent&lt;IDamageable&gt;()，比 SendMessage("TakeDamage") 安全：
/// 名字打錯會在編譯期就報錯，而且不需要依賴 Tag 或 Layer。
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}
