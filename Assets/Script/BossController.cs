using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 直升機 Boss 的行為控制：懸停、追蹤玩家、雙管機槍由下往上掃射、追蹤飛彈、半血狂暴、死亡處理。
///
/// 這次修正的重點（對照原本的版本）：
/// 1. 協程改用 try/finally，就算中途出錯也不會讓 isAttacking 卡在 true（Boss 不會再「打一下就變木頭」）。
/// 2. 雷射改用 RaycastNonAlloc 過濾掉自己身上的碰撞體，不會再打在機身上把光柱截斷。
/// 3. 光柱改用計時器控制開關，不再每發子彈開一個協程；切換狀態時一定會關閉，不會「永久亮著」。
/// 4. 傷害改用 IDamageable 介面（找不到才退回 SendMessage），不再依賴 Player tag。
/// 5. 飛彈會 SetParent(null, true) 保留世界縮放、SetActive(true)、強制 isTrigger、並加上引信延遲。
/// 6. 飛彈打完後可用 missilePrefab 自動補充。
/// 7. 新增 Defeated 的完整處理：關碰撞、冒煙下墜、爆炸、銷毀，並提供 OnDefeated 事件。
/// </summary>
public class BossController : MonoBehaviour, IDamageable
{
    public enum BossState
    {
        Idle,
        NormalAttack,
        BerserkMode,
        Defeated
    }

    [Header("Boss State and Attributes")]
    public BossState currentState = BossState.Idle;
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("血量低於這個比例時進入狂暴階段。")]
    [Range(0f, 1f)]
    public float berserkHealthRatio = 0.5f;

    [Header("Player Target")]
    public Transform playerTransform;
    public float rotationSpeed = 5f;

    [Header("Hovering Movement")]
    public float floatSpeed = 2f;
    public float floatHeight = 0.5f;
    public bool enableHover = true;

    [Header("Machine Gun Attack Settings")]
    public float normalFireRate = 0.08f;
    public float normalDuration = 2.0f;
    public float berserkFireRate = 0.03f;
    public float berserkDuration = 4.0f;
    public float laserDamage = 5f;

    [Header("Machine Gun Strafing (機槍掃射)")]
    [Tooltip("開火前的預警時間（秒）。這段時間只顯示警示區，不會造成傷害。")]
    public float telegraphTime = 0.7f;
    [Tooltip("掃射起點距離玩家多遠（沿地面算，單位）。數字越小，掃射越貼近目標。")]
    public float sweepStartDistance = 6f;
    [Tooltip("掃到玩家頭上多高才停（單位）。")]
    public float sweepOvershoot = 3f;

    [Header("Warning Zone (警示區)")]
    [Tooltip("貼在地面上的半透明紅色長方形寬度（單位）。")]
    public float warningWidth = 5f;
    [Tooltip("警示區顏色，第四個值是透明度。")]
    public Color warningColor = new Color(1f, 0.15f, 0.1f, 0.35f);
    [Tooltip("警示區離地面多高，太貼近地板會和地板互相閃爍，留一點間隙。")]
    public float warningGroundOffset = 0.08f;
    [Tooltip("可以指定自己的半透明材質；留空會自動產生一個。")]
    public Material warningMaterial;

    [Header("Tracer (曳光彈)")]
    [Tooltip("曳光彈的飛行速度（單位/秒）。越大越像機槍掃過去。")]
    public float tracerSpeed = 300f;
    [Tooltip("每一發曳光彈看得見的長度（單位）。3~8 像子彈，太長就變回雷射了。")]
    public float tracerLength = 6f;

    [Header("Double Gun Setup")]
    public Transform[] firePoints;
    public LineRenderer[] laserLineRenderers;
    public float laserMaxDistance = 50f;
    [Tooltip("雷射可以命中的物理層。預設全部；想讓雷射穿過某些層就在這裡取消勾選。")]
    public LayerMask laserHitMask = ~0;

    [Header("Missile Tracking Settings")]
    [Tooltip("Base speed of the missile.")]
    [Range(1f, 15f)]
    public float missileSpeed = 4f;

    [Tooltip("How fast the missile turns towards target.")]
    [Range(1f, 40f)]
    public float missileRotateSpeed = 15f;

    [Tooltip("Maximum lifetime of the missile in seconds.")]
    public float missileLifeTime = 5f;

    [Tooltip("Damage dealt to player upon collision.")]
    public float missileDamage = 20f;

    [Header("Missile Objects on Model")]
    [Tooltip("Drag the child Missiles GameObjects from the hierarchy here.")]
    public GameObject[] missileObjects;
    [Tooltip("掛在模型上的飛彈用完之後，用這個 Prefab 繼續補充（可留空＝打完就沒有飛彈了）。")]
    public GameObject missilePrefab;
    private int currentMissileIndex = 0;
    private Vector3[] missileRestPositions;
    private Quaternion[] missileRestRotations;

    [Header("Prefabs")]
    public GameObject energyPayloadPrefab;

    [Header("Death")]
    [Tooltip("死亡後多久銷毀本體（秒）。")]
    public float deathDelay = 3f;
    [Tooltip("死亡時的下墜速度。")]
    public float deathFallSpeed = 4f;
    [Tooltip("死亡爆炸特效（可留空）。")]
    public GameObject deathExplosionPrefab;

    /// <summary>血量變化時通知 UI：(目前血量, 最大血量)。</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>Boss 被擊敗時觸發，可用來開勝利結算。</summary>
    public event Action OnDefeated;

    private bool isAttacking = false;
    private bool isDefeatedHandled = false;
    private Vector3 startPosition;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    // 光柱原本的寬度/顏色，預警時會暫時改細改色，開火時要還原
    private float[] beamStartWidths;
    private float[] beamEndWidths;
    private Color[] beamStartColors;
    private Color[] beamEndColors;

    private bool isLockedOn = false;   // 掃射期間鎖定方向，不再跟著玩家轉

    // 這一輪掃射的起點（地面）與終點（玩家頭上）
    private Vector3 sweepAimStart;
    private Vector3 sweepAimEnd;
    private Vector3 sweepRight;        // 掃射線的橫向軸（機槍散布用）

    // 每一門砲各自一顆正在飛的曳光彈
    private bool[] tracerActive;
    private Vector3[] tracerOrigin;
    private Vector3[] tracerDirection;
    private float[] tracerDistance;
    private float[] tracerStartTime;

    // 貼在地上的半透明警示區（執行時自動產生一個 Mesh）
    private GameObject warningObject;
    private Mesh warningMesh;
    private Material autoWarningMaterial;
    private readonly Vector3[] warningCorners = new Vector3[4];
    private readonly Color[] warningColors = new Color[4];
    private static readonly int[] warningTriangles = { 0, 2, 1, 0, 3, 2 };

    public bool IsDefeated => currentState == BossState.Defeated;
    public float HealthRatio => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    // ─────────────────────────────────────────────
    // Unity 生命週期
    // ─────────────────────────────────────────────

    void Awake()
    {
        startPosition = transform.position;

        int beamCount = laserLineRenderers != null ? laserLineRenderers.Length : 0;
        int pointCount = firePoints != null ? firePoints.Length : 0;
        int gunCount = Mathf.Max(beamCount, pointCount);

        tracerActive = new bool[gunCount];
        tracerOrigin = new Vector3[gunCount];
        tracerDirection = new Vector3[gunCount];
        tracerDistance = new float[gunCount];
        tracerStartTime = new float[gunCount];

        // 記住光柱原本的粗細與顏色，預警時要暫時改掉、開火時還原
        beamStartWidths = new float[beamCount];
        beamEndWidths = new float[beamCount];
        beamStartColors = new Color[beamCount];
        beamEndColors = new Color[beamCount];

        for (int i = 0; i < beamCount; i++)
        {
            LineRenderer line = laserLineRenderers[i];
            if (line == null) continue;

            beamStartWidths[i] = line.startWidth;
            beamEndWidths[i] = line.endWidth;
            beamStartColors[i] = line.startColor;
            beamEndColors[i] = line.endColor;
        }

        // 記住掛在模型上的飛彈「原始位置」，之後要用來生成補充的飛彈
        if (missileObjects != null && missileObjects.Length > 0)
        {
            missileRestPositions = new Vector3[missileObjects.Length];
            missileRestRotations = new Quaternion[missileObjects.Length];

            for (int i = 0; i < missileObjects.Length; i++)
            {
                if (missileObjects[i] == null) continue;
                missileRestPositions[i] = missileObjects[i].transform.position;
                missileRestRotations[i] = missileObjects[i].transform.rotation;
            }
        }

        CreateWarningZone();
    }

    void Start()
    {
        currentHealth = maxHealth;

        // 舊場景升級：新加入的欄位在已存在的物件上可能是 0
        if (berserkHealthRatio <= 0f) berserkHealthRatio = 0.5f;
        if (laserHitMask.value == 0) laserHitMask = ~0;

        SetLasersEnabled(false);

        if (playerTransform == null)
        {
            playerTransform = FindPlayerTransform();
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("[BossController] 找不到玩家：playerTransform 是空的，" +
                             "場景裡也沒有 Tag = Player 的物件。Boss 不會攻擊。", this);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        ChangeState(BossState.NormalAttack);
    }

    void Update()
    {
        // 曳光彈每幀往前飛，Boss 死了也要收乾淨
        UpdateTracers();

        if (currentState == BossState.Defeated) return;

        HandleTargetingAndMovement();

        switch (currentState)
        {
            case BossState.Idle:
                break;

            case BossState.NormalAttack:
                // 血量過半就直接切狂暴，不用等這一輪打完
                if (HealthRatio <= berserkHealthRatio)
                {
                    ChangeState(BossState.BerserkMode);
                    break;
                }
                if (!isAttacking) StartCoroutine(NormalAttackRoutine());
                break;

            case BossState.BerserkMode:
                if (!isAttacking) StartCoroutine(BerserkAttackRoutine());
                break;
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        isAttacking = false;
        isLockedOn = false;
        SetLasersEnabled(false);
        HideWarningZone();
    }

    void OnDestroy()
    {
        if (warningObject != null)
        {
            Destroy(warningObject);
        }

        if (autoWarningMaterial != null)
        {
            Destroy(autoWarningMaterial);
        }
    }

    // ─────────────────────────────────────────────
    // 移動與瞄準
    // ─────────────────────────────────────────────

    void HandleTargetingAndMovement()
    {
        // 掃射期間鎖定方向、不再轉向玩家 —— 這是「可以躲掉」的關鍵：
        // 如果每一發都重新瞄準玩家，雷射是瞬時命中，玩家永遠躲不掉。
        if (!isLockedOn && playerTransform != null)
        {
            Vector3 lookDirection = playerTransform.position - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }
        }

        if (!enableHover) return;

        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    Transform FindPlayerTransform()
    {
        GameObject playerObj = null;

        // 工程裡如果沒有定義 Player tag，FindGameObjectWithTag 會丟 UnityException
        try
        {
            playerObj = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            // 沒有定義 tag，往下用元件搜尋
        }

        if (playerObj != null) return playerObj.transform;

        PlayerHealth health = FindObjectOfType<PlayerHealth>();
        return health != null ? health.transform : null;
    }

    // ─────────────────────────────────────────────
    // 狀態切換
    // ─────────────────────────────────────────────

    public void ChangeState(BossState newState)
    {
        // 死透了就不再切回攻擊狀態
        if (currentState == BossState.Defeated && newState != BossState.Defeated) return;

        // 順序很重要：先停協程，再關光柱，否則會殘留亮著的光束
        StopAllCoroutines();
        isAttacking = false;
        isLockedOn = false;
        SetLasersEnabled(false);
        HideWarningZone();

        if (newState == BossState.Defeated)
        {
            EnterDefeated();
            return;
        }

        currentState = newState;
    }

    void EnterDefeated()
    {
        if (isDefeatedHandled) return;
        isDefeatedHandled = true;

        currentState = BossState.Defeated;
        isAttacking = false;
        SetLasersEnabled(false);

        currentHealth = 0f;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDefeated?.Invoke();

        // 關掉所有碰撞體，避免屍體還擋子彈
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) body.isKinematic = true;

        Debug.Log("[BossController] Boss 已被擊敗", this);

        if (deathExplosionPrefab != null)
        {
            Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
        }

        StartCoroutine(DefeatSequence());
    }

    IEnumerator DefeatSequence()
    {
        float timer = 0f;
        while (timer < deathDelay)
        {
            timer += Time.deltaTime;
            transform.position += Vector3.down * (deathFallSpeed * Time.deltaTime);
            transform.Rotate(Vector3.forward, 120f * Time.deltaTime, Space.Self);
            yield return null;
        }

        if (deathExplosionPrefab != null)
        {
            Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    // 攻擊流程
    // ─────────────────────────────────────────────

    IEnumerator NormalAttackRoutine()
    {
        isAttacking = true;
        try
        {
            yield return StartCoroutine(LaserSweep(normalDuration, normalFireRate));
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(LaserSweep(normalDuration, normalFireRate));
            yield return new WaitForSeconds(1f);

            ShootMissile();
            yield return new WaitForSeconds(1.5f);

            DropEnergyPayload();
            yield return new WaitForSeconds(2f);
        }
        finally
        {
            // 不論正常結束、被 StopAllCoroutines 還是中途拋例外，都會走到這裡
            isAttacking = false;
        }
    }

    IEnumerator BerserkAttackRoutine()
    {
        isAttacking = true;
        try
        {
            yield return StartCoroutine(LaserSweep(berserkDuration, berserkFireRate));
            yield return new WaitForSeconds(0.5f);

            for (int i = 0; i < 3; i++)
            {
                ShootMissile();
                yield return new WaitForSeconds(0.4f);
            }

            yield return new WaitForSeconds(1f);

            DropEnergyPayload();
            yield return new WaitForSeconds(1f);
        }
        finally
        {
            isAttacking = false;
        }
    }

    /// <summary>
    /// 一次完整的機槍掃射，分成三段：
    ///   1. 鎖定：以「開火瞬間玩家的位置」決定一條從地面掃到玩家頭上的射擊線
    ///   2. 預警：把這條射擊線畫成一個長方形（約 telegraphTime 秒），不造成傷害
    ///   3. 掃射：槍口沿著這條線由下往上掃，每一發射出一顆曳光彈
    /// 方向鎖定之後，玩家只要離開這條線就不會被打到，所以「會動就能躲」。
    /// </summary>
    IEnumerator LaserSweep(float sweepDuration, float fireRate)
    {
        if (!CaptureSweepAxis()) yield break;

        isLockedOn = true;
        CancelTracers();

        try
        {
            // ── 1. 預警：地面亮起半透明紅色長方形 ──
            ShowWarningZone();

            float telegraphTimer = 0f;
            while (telegraphTimer < telegraphTime)
            {
                telegraphTimer += Time.deltaTime;
                yield return null;
            }

            HideWarningZone();

            // ── 2. 由下往上掃射 ──────────────────────
            float interval = Mathf.Max(0.01f, fireRate);
            float duration = Mathf.Max(0.01f, sweepDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                Vector3 aimPoint = Vector3.Lerp(sweepAimStart, sweepAimEnd, progress);

                FireBurst(aimPoint);

                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
        }
        finally
        {
            isLockedOn = false;
            CancelTracers();
            HideWarningZone();
        }
    }

    /// <summary>
    /// 鎖定這一輪的掃射線：從「Boss 與玩家之間的地面」一路掃到「玩家頭上」。
    /// 決定之後整輪都不再跟著玩家修正，玩家離開這條線就打不到。
    /// </summary>
    bool CaptureSweepAxis()
    {
        if (firePoints == null || firePoints.Length == 0) return false;
        if (playerTransform == null) return false;

        Vector3 muzzleCenter = GetMuzzlesCenter();
        Vector3 playerPos = playerTransform.position;

        float groundY = FindGroundY(playerPos);

        Vector3 bossGround = new Vector3(muzzleCenter.x, groundY, muzzleCenter.z);
        Vector3 playerGround = new Vector3(playerPos.x, groundY, playerPos.z);

        Vector3 toPlayer = playerGround - bossGround;
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;
        if (distance < 0.01f) return false;

        Vector3 dirToPlayer = toPlayer / distance;

        // 起點：玩家前方 sweepStartDistance 的地面，往上掃到玩家身上。
        // 用「距離玩家多遠」而不是「幾成距離」，才不會因為 Boss 站得遠而掃到很前面去。
        float startDistance = Mathf.Clamp(sweepStartDistance, 1f, Mathf.Max(1f, distance - 1f));

        sweepAimStart = playerGround - dirToPlayer * startDistance;
        sweepAimEnd = playerPos + Vector3.up * sweepOvershoot;

        sweepRight = Vector3.Cross(Vector3.up, dirToPlayer);
        if (sweepRight.sqrMagnitude < 0.0001f) sweepRight = Vector3.right;
        sweepRight.Normalize();

        return true;
    }

    /// <summary>找出某個位置下方最低的地面高度（跳過玩家與 Boss 自己的碰撞體）。</summary>
    float FindGroundY(Vector3 worldPos)
    {
        Vector3 origin = worldPos + Vector3.up * 100f;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, Vector3.down, 300f, laserHitMask, QueryTriggerInteraction.Ignore);

        float groundY = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;

            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            if (playerTransform != null &&
                (col.transform == playerTransform || col.transform.IsChildOf(playerTransform))) continue;

            if (hits[i].point.y < groundY) groundY = hits[i].point.y;
        }

        return float.IsInfinity(groundY) ? worldPos.y - 10f : groundY;
    }

    /// <summary>兩門砲位置的平均值，用來當掃射範圍的中心。</summary>
    Vector3 GetMuzzlesCenter()
    {
        if (firePoints == null) return transform.position;

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < firePoints.Length; i++)
        {
            if (firePoints[i] == null) continue;
            sum += firePoints[i].position;
            count++;
        }

        return count > 0 ? sum / count : transform.position;
    }

    /// <summary>兩門砲同時朝同一個瞄準點打一發，彈道各自從自己的炮口射出。</summary>
    void FireBurst(Vector3 aimPoint)
    {
        if (firePoints == null) return;

        // 機槍的散布：在預警長方形的寬度內隨機偏移，
        // 這樣「畫出來的長方形」才等於「真的會被掃到的範圍」。
        Vector3 aim = aimPoint + sweepRight * (UnityEngine.Random.Range(-0.5f, 0.5f) * warningWidth);

        for (int i = 0; i < firePoints.Length; i++)
        {
            Transform muzzle = firePoints[i];
            if (muzzle == null) continue;

            Vector3 origin = muzzle.position;
            Vector3 dir = aim - origin;
            if (dir.sqrMagnitude < 0.0001f) continue;
            dir.Normalize();

            float distance = laserMaxDistance;

            if (TryGetAimHit(origin, dir, out RaycastHit hit))
            {
                distance = hit.distance;
                DamageCollider(hit.collider, laserDamage);
            }

            SpawnTracer(i, origin, dir, distance);
        }
    }

    /// <summary>生出一顆曳光彈：從炮口沿著彈道往前飛，飛到命中點就消失。</summary>
    void SpawnTracer(int index, Vector3 origin, Vector3 direction, float distance)
    {
        if (tracerActive == null || index < 0 || index >= tracerActive.Length) return;

        tracerActive[index] = true;
        tracerOrigin[index] = origin;
        tracerDirection[index] = direction;
        tracerDistance[index] = Mathf.Max(distance, tracerLength);
        tracerStartTime[index] = Time.time;
    }

    /// <summary>
    /// 每幀推進每一顆曳光彈。
    /// 畫出來的是一小段線（tracerLength 長），所以看起來是一顆顆子彈而不是一道光柱，
    /// 而且兩發之間會有空檔 —— 這就是「斷斷續續」的來源。
    /// </summary>
    void UpdateTracers()
    {
        if (tracerActive == null || laserLineRenderers == null) return;

        int count = Mathf.Min(tracerActive.Length, laserLineRenderers.Length);

        for (int i = 0; i < count; i++)
        {
            if (!tracerActive[i]) continue;

            LineRenderer line = laserLineRenderers[i];
            if (line == null)
            {
                tracerActive[i] = false;
                continue;
            }

            float traveled = (Time.time - tracerStartTime[i]) * tracerSpeed;

            // 飛到命中點（或射程盡頭）就收起來
            if (traveled >= tracerDistance[i])
            {
                tracerActive[i] = false;
                line.enabled = false;
                continue;
            }

            float head = Mathf.Min(traveled + tracerLength, tracerDistance[i]);
            Vector3 tailPos = tracerOrigin[i] + tracerDirection[i] * traveled;
            Vector3 headPos = tracerOrigin[i] + tracerDirection[i] * head;

            if (line.positionCount != 2) line.positionCount = 2;

            RestoreBeamStyle(i);
            line.enabled = true;
            line.SetPosition(0, tailPos);
            line.SetPosition(1, headPos);
        }
    }

    /// <summary>把所有還在飛的曳光彈收掉（切狀態、死亡、換攻擊時用）。</summary>
    void CancelTracers()
    {
        if (tracerActive == null) return;

        for (int i = 0; i < tracerActive.Length; i++)
        {
            tracerActive[i] = false;

            if (laserLineRenderers != null && i < laserLineRenderers.Length && laserLineRenderers[i] != null)
            {
                laserLineRenderers[i].enabled = false;
            }
        }
    }

    // ─────────────────────────────────────────────
    // 地面警示區（半透明紅色長方形）
    // ─────────────────────────────────────────────

    /// <summary>執行時建立一個只有 4 個頂點的 Mesh，之後直接把頂點擺到地面上。</summary>
    void CreateWarningZone()
    {
        warningObject = new GameObject("BossWarningZone");
        warningObject.transform.SetParent(null, true);
        warningObject.transform.position = Vector3.zero;
        warningObject.transform.rotation = Quaternion.identity;
        warningObject.transform.localScale = Vector3.one;

        MeshFilter filter = warningObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = warningObject.AddComponent<MeshRenderer>();

        warningMesh = new Mesh();
        warningMesh.name = "BossWarningZoneMesh";
        filter.sharedMesh = warningMesh;

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (warningMaterial != null)
        {
            renderer.sharedMaterial = warningMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

            if (shader != null)
            {
                autoWarningMaterial = new Material(shader);
                autoWarningMaterial.color = warningColor;
                autoWarningMaterial.renderQueue = 3000;
                renderer.sharedMaterial = autoWarningMaterial;
            }
            else
            {
                Debug.LogWarning("[BossController] 找不到半透明 Shader，警示區不會顯示。" +
                                 "請自己建一個半透明紅色材質並拖到 warningMaterial。", this);
            }
        }

        warningObject.SetActive(false);
    }

    /// <summary>在地上畫出這一輪會被掃過的長方形並顯示（只有 4 個頂點，一勞永逸）。</summary>
    void ShowWarningZone()
    {
        if (warningObject == null || warningMesh == null) return;

        Vector3 flat = sweepAimEnd - sweepAimStart;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;

        Vector3 dir = flat.normalized;

        Vector3 right = sweepRight;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.Cross(Vector3.up, dir).normalized;

        float halfWidth = warningWidth * 0.5f;
        float y = sweepAimStart.y + warningGroundOffset;   // 貼在地面上方一點，避免和地板打架

        Vector3 startPoint = new Vector3(sweepAimStart.x, y, sweepAimStart.z);
        Vector3 endPoint = new Vector3(sweepAimEnd.x, y, sweepAimEnd.z) + dir * (sweepOvershoot * 0.5f);

        warningCorners[0] = startPoint - right * halfWidth;
        warningCorners[1] = startPoint + right * halfWidth;
        warningCorners[2] = endPoint + right * halfWidth;
        warningCorners[3] = endPoint - right * halfWidth;

        warningMesh.Clear();
        warningMesh.vertices = warningCorners;
        warningMesh.triangles = warningTriangles;

        // 同時給頂點色，就算材質本身沒有 tint 也還是紅的
        for (int i = 0; i < warningColors.Length; i++) warningColors[i] = warningColor;
        warningMesh.colors = warningColors;

        warningMesh.RecalculateNormals();
        warningMesh.RecalculateBounds();

        if (autoWarningMaterial != null) autoWarningMaterial.color = warningColor;

        warningObject.SetActive(true);
    }

    void HideWarningZone()
    {
        if (warningObject != null && warningObject.activeSelf)
        {
            warningObject.SetActive(false);
        }
    }

    /// <summary>
    /// 從槍口往前打一條射線，並「忽略自己身上的碰撞體」——
    /// 原本的 Physics.Raycast 會打到直昇機自己的 MeshCollider，光柱就被截在機頭。
    /// </summary>
    bool TryGetAimHit(Vector3 origin, Vector3 direction, out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.RaycastNonAlloc(
            origin, direction, hitBuffer, laserMaxDistance, laserHitMask, QueryTriggerInteraction.Collide);

        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (col == null) continue;

            // 自己機身/旋翼/飛彈掛點都跳過
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            if (hitBuffer[i].distance < bestDistance)
            {
                bestDistance = hitBuffer[i].distance;
                bestHit = hitBuffer[i];
                found = true;
            }
        }

        return found;
    }

    void DamageCollider(Collider col, float amount)
    {
        if (col == null) return;

        IDamageable damageable = col.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(amount);
            return;
        }

        // 相容舊寫法：對方如果只實作了同名方法，還是讓它收到傷害
        col.SendMessage("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>把光柱的粗細與顏色還原成 Inspector 裡設定的樣子。</summary>
    void RestoreBeamStyle(int index)
    {
        if (beamStartWidths == null || index >= beamStartWidths.Length) return;

        LineRenderer line = laserLineRenderers[index];
        if (line == null) return;

        line.startWidth = beamStartWidths[index];
        line.endWidth = beamEndWidths[index];
        line.startColor = beamStartColors[index];
        line.endColor = beamEndColors[index];
    }

    void SetLasersEnabled(bool on)
    {
        if (!on) CancelTracers();
    }

    // ─────────────────────────────────────────────
    // 飛彈
    // ─────────────────────────────────────────────

    void ShootMissile()
    {
        if (missileObjects == null || missileObjects.Length == 0)
        {
            SpawnMissileFromPrefab();
            return;
        }

        int index = FindUsableMissileIndex();

        if (index < 0)
        {
            // 模型上的飛彈都射光了，改用 Prefab 補充
            SpawnMissileFromPrefab();
            return;
        }

        GameObject missileObj = missileObjects[index];
        missileObjects[index] = null;   // 每個掛點只能用一次
        currentMissileIndex = (index + 1) % missileObjects.Length;

        if (missileObj == null) return;

        PrepareAndLaunchMissile(missileObj);
    }

    int FindUsableMissileIndex()
    {
        int count = missileObjects.Length;
        for (int offset = 0; offset < count; offset++)
        {
            int i = (currentMissileIndex + offset) % count;
            if (missileObjects[i] != null) return i;
        }
        return -1;
    }

    void SpawnMissileFromPrefab()
    {
        if (missilePrefab == null) return;

        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;

        if (missileRestPositions != null && missileRestPositions.Length > 0)
        {
            int i = Mathf.Clamp(currentMissileIndex, 0, missileRestPositions.Length - 1);
            spawnPosition = transform.position + (missileRestPositions[i] - startPosition);
            spawnRotation = missileRestRotations[i];
        }

        GameObject missileObj = Instantiate(missilePrefab, spawnPosition, spawnRotation);
        PrepareAndLaunchMissile(missileObj);
    }

    void PrepareAndLaunchMissile(GameObject missileObj)
    {
        if (missileObj == null) return;

        // 注意：一定要用 SetParent(null, true)，直接寫 parent = null 會讓飛彈的尺寸改變
        missileObj.transform.SetParent(null, true);
        missileObj.SetActive(true);

        // 強制開啟 Trigger，否則 OnTriggerEnter 永遠不會被呼叫
        Collider col = missileObj.GetComponent<Collider>();
        if (col == null) col = missileObj.AddComponent<BoxCollider>();
        col.isTrigger = true;

        Rigidbody rb = missileObj.GetComponent<Rigidbody>();
        if (rb == null) rb = missileObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        // Kinematic 剛體請用 ContinuousSpeculative，用 ContinuousDynamic 會跳警告
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        HomingMissile homing = missileObj.GetComponent<HomingMissile>();
        if (homing == null) homing = missileObj.AddComponent<HomingMissile>();

        homing.Initialize(missileSpeed, missileRotateSpeed, missileLifeTime, missileDamage);
        homing.Launch(playerTransform, transform);
    }

    void DropEnergyPayload()
    {
        if (energyPayloadPrefab == null) return;
        if (firePoints == null || firePoints.Length == 0 || firePoints[0] == null) return;

        Instantiate(energyPayloadPrefab, firePoints[0].position, Quaternion.identity);
    }

    // ─────────────────────────────────────────────
    // 受傷
    // ─────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (currentState == BossState.Defeated) return;
        if (damage <= 0f) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            ChangeState(BossState.Defeated);
            return;
        }

        if (currentState != BossState.BerserkMode && HealthRatio <= berserkHealthRatio)
        {
            ChangeState(BossState.BerserkMode);
        }
    }
}
