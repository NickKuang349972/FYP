using UnityEngine;

/// <summary>
/// 追蹤飛彈：鎖定玩家、持續轉向、命中後造成傷害並播放爆炸特效。
///
/// 這次修正的重點：
/// 1. 引信延遲（armingTime）：飛彈剛離開機身時會和 Boss 自己的碰撞體重疊，
///    沒有這段延遲就會一發射就原地自爆。
/// 2. 逾時改用 Explode()，不再直接 Destroy —— 原本超過 lifeTime 會「憑空消失」，
///    沒有爆炸特效也沒有傷害。
/// 3. 移動改用 Rigidbody.MovePosition/MoveRotation，並開 ContinuousSpeculative，
///    避免高速時直接穿過玩家。
/// 4. 傷害改用 IDamageable 介面，不再依賴可能沒定義的 Player tag。
/// 5. 轉向用 Clamp01 限制 Slerp 的 t，高速轉向時不會抖動。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HomingMissile : MonoBehaviour
{
    [Header("Effects")]
    [Tooltip("請將爆炸特效的 Prefab 拖曳到這裡")]
    public GameObject explosionPrefab;

    [Header("Safety")]
    [Tooltip("發射後多久才開始偵測碰撞（秒）。太短會剛離開機身就自爆。")]
    public float armingTime = 0.25f;

    private Transform target;
    private Transform owner;

    private float speed;
    private float rotateSpeed;
    private float lifeTime;
    private float damage;

    private bool isInitialized;
    private bool hasExploded;
    private float elapsedTime;
    private Rigidbody body;

    public bool HasExploded => hasExploded;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Initialize(float missileSpeed, float missileRotateSpeed, float missileLifeTime, float missileDamage)
    {
        speed = missileSpeed;
        rotateSpeed = missileRotateSpeed;
        lifeTime = missileLifeTime;
        damage = missileDamage;
        isInitialized = true;
    }

    public void SetTarget(Transform playerTarget)
    {
        target = playerTarget;
    }

    /// <param name="playerTarget">要追的目標（玩家）。</param>
    /// <param name="ownerRoot">發射者，用來忽略自己人的碰撞體（可為 null）。</param>
    public void Launch(Transform playerTarget, Transform ownerRoot = null)
    {
        target = playerTarget;
        owner = ownerRoot;
        elapsedTime = 0f;
        hasExploded = false;

        if (body == null) body = GetComponent<Rigidbody>();

        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    void Update()
    {
        if (!isInitialized || hasExploded) return;

        elapsedTime += Time.deltaTime;

        // 逾時：也要爆炸，不能讓飛彈憑空消失
        if (elapsedTime >= lifeTime)
        {
            Explode();
            return;
        }

        Steer();
        Move();
    }

    void Steer()
    {
        if (target == null) return;   // 目標被銷毀就直線飛

        Vector3 direction = target.position - transform.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
        float t = Mathf.Clamp01(rotateSpeed * Time.deltaTime);
        Quaternion newRotation = Quaternion.Slerp(transform.rotation, lookRotation, t);

        if (body != null) body.MoveRotation(newRotation);
        else transform.rotation = newRotation;
    }

    void Move()
    {
        Vector3 nextPosition = transform.position + transform.forward * (speed * Time.deltaTime);

        if (body != null) body.MovePosition(nextPosition);
        else transform.position = nextPosition;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasExploded || !isInitialized) return;
        if (elapsedTime < armingTime) return;          // 引信還沒開，忽略一切碰撞
        if (other == null) return;

        // 忽略發射者自己（Boss 機身、旋翼……）
        if (owner != null && (other.transform == owner || other.transform.IsChildOf(owner))) return;

        ApplyDamage(other);
        Explode();
    }

    void ApplyDamage(Collider other)
    {
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            return;
        }

        // 相容舊寫法：對方只實作了同名方法時仍然收得到傷害
        other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // 先關掉碰撞體，避免同一幀又觸發第二次
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }
}
