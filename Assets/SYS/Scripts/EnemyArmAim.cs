using UnityEngine;

public class EnemyArmAim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] protected Transform bodyTransform;
    [SerializeField] protected Transform armPivot;
    [SerializeField] protected SpriteRenderer bodyRenderer;
    [field: SerializeField] public SpriteRenderer armRenderer { get; private set; } // 죽을때 팔 숨기기용 프로퍼티
    [SerializeField] protected Transform firePivot;

    [Header("Range")]
    [SerializeField] protected float detectionRange = 8f;
    [SerializeField] protected float rotationSpeed = 10f;

    [Header("Angle Clamp")]
    [SerializeField] protected float minAngle = -60f;
    [SerializeField] protected float maxAngle = 60f;

    [Header("Recoil - Angle")]
    [SerializeField] protected float recoilAngle = 45f;
    [SerializeField] protected float recoilRiseSpeed = 800f;
    [SerializeField] protected float recoilReturnSpeed = 12f;

    [Header("Recoil - Position")]
    [SerializeField] protected float recoilPushDist = 0.3f;   // 뒤로 밀리는 거리
    [SerializeField] protected float recoilPushSpeed = 800f;  // 밀리는 속도
    [SerializeField] protected float recoilPullSpeed = 10f;   // 복귀 속도
    //protected bool isRecoil = false; // 쓰기만 하고 읽는 곳이 없어 미사용

    protected Transform _player;
    protected EnemyBlackboard _enemyBB;
    protected bool _isFacingRight = true;

    // 각도 반동
    protected float _recoilAngleOffset = 0f;
    protected float _recoilAngleTarget = 0f;
    protected bool _recoilAngleRising = false;

    // 위치 반동
    protected Vector3 _armBaseLocalPos;         // 원래 로컬 위치 (flip 고려)
    protected float _recoilPosOffset = 0f;      // 뒤로 밀린 거리 (스칼라)
    protected float _recoilPosTarget = 0f;
    protected bool _recoilPosRising = false;
    protected Vector3 _fireBaseLocalPos;

    // 외부 API
    public float DetactionRange { get => detectionRange; set => detectionRange = value; }
    public EnemyBlackboard EnemyBlackBoard { get => _enemyBB; set => _enemyBB = value; }

    /// <summary>
    /// 조준 회전·반동용 프레임 시간 = Time.deltaTime × 적 배율.
    /// 빨강 칩·파랑 QTE 슬로우와 히트스톱에 맞춰 느려지고, 일시정지(Time.timeScale = 0)에도 멈춘다.
    /// (CustomTimeManager.EnemyDeltaTime은 unscaled 기준이라 일시정지 중에도 흘러서 쓰지 않음)
    /// </summary>
    protected static float AimDeltaTime
    {
        get
        {
            var tmg = CustomTimeManager.Instance;
            return Time.deltaTime * (tmg != null ? tmg.EnemyTimeScale : 1f);
        }
    }

    protected virtual void Awake()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        _armBaseLocalPos = armPivot.localPosition;
        _fireBaseLocalPos = firePivot.localPosition;
    }

    public virtual void Tick() // EnemyManager → EnemyBTController가 호출
    {
#if UNITY_EDITOR
        // 디버그: N 키로 모든 적 반동 테스트 (에디터 전용, 빌드에서 제외)
        if (Input.GetKeyDown(KeyCode.N))
            TriggerRecoil();
#endif

        if (_enemyBB.enemyState.currentHp <= 0) return;
        UpdateRecoil();
    }

    public virtual void LateTick() // EnemyManager → EnemyBTController가 호출
    {
        if (_player == null || armPivot == null) return;

        if (_enemyBB.enemyState.currentHp <= 0) return;

        // ── 위치 반동 적용 ──
        // 현재 aim 방향 반대(뒤)로 밀기
        float aimWorldAngle = armPivot.rotation.eulerAngles.z;
        float pushRad = (aimWorldAngle + 180f) * Mathf.Deg2Rad;
        Vector3 pushDir = new Vector3(Mathf.Cos(pushRad), Mathf.Sin(pushRad), 0f);
        armPivot.localPosition = _armBaseLocalPos + pushDir * _recoilPosOffset;

        // ── 각도 반동 + aim ──
        float dist = Vector2.Distance(bodyTransform.position, _player.position);

        float targetWorldAngle = -99f; // 기본 각도. 이 밑으로 0707추가코드

        // [핵심 수정] 거리 안이면서 동시에 시야(LOS)가 확보되었을 때만! 플레이어를 조준합니다.
        // _enemyBB null 검사는 위에서 이미 _enemyBB.enemyState를 쓴 뒤라 의미 없음
        // (EnemyBTController.ReInit이 Tick 전에 항상 넣어 줌)
        if (dist <= detectionRange /*&& _enemyBB != null*/ && _enemyBB.hasLOS)
        {
            Vector2 dir = _player.position - armPivot.position;
            float rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            TryFlip(rawAngle); // 시야가 있을 때만 플레이어 쪽으로 몸을 돌림
            targetWorldAngle = GetTargetWorldAngle(rawAngle);
        }
        else
        {
            // 플레이어를 안 볼 때 (순찰 중이거나, 벽 뒤에 있을 때)
            // 이동 방향으로 몸을 돌린다 (벽 뒤 추격 시 반대를 보고 걷는 문워크 방지)
            if (_enemyBB.rigid2D != null)
            {
                float vx = _enemyBB.rigid2D.linearVelocity.x;
                if (Mathf.Abs(vx) > 0.1f) SetFacing(vx > 0f);
            }

            // 기괴한 -99도 대신, 현재 몸이 바라보는 방향(정면)을 바라보게 합니다.
            targetWorldAngle = _isFacingRight ? -99f : 99f;
        }

        // 4. 각도 반동 적용 및 최종 회전
        float recoilSign = _isFacingRight ? 1f : -1f;
        float finalAngle = targetWorldAngle + _recoilAngleOffset * recoilSign;

        armPivot.rotation = Quaternion.Lerp(
            armPivot.rotation,
            Quaternion.Euler(0f, 0f, finalAngle),
            AimDeltaTime * rotationSpeed);

        // isRecoil은 어디서도 읽지 않아 불필요 (rotation.z는 쿼터니언 성분이라 각도 비교로도 맞지 않음)
        /*if (Mathf.Abs(NormalizeAngle(finalAngle)) -
            Mathf.Abs(NormalizeAngle(armPivot.rotation.z)) <= 0.05f)
        {
            isRecoil = false;
        }*/

        /*Vector2 dir;
        if (_enemyBB.hasLOS)
            dir = _player.position - armPivot.position;
        else
            dir = armPivot.right;

        float rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float targetWorldAngle;
        if (dist <= detectionRange)
        {
            TryFlip(rawAngle);
            targetWorldAngle = GetTargetWorldAngle(rawAngle);
        }
        else
        {
            targetWorldAngle = -99f;
        }

        float recoilSign = _isFacingRight ? 1f : -1f;
        float finalAngle = targetWorldAngle + _recoilAngleOffset * recoilSign;
        armPivot.rotation = Quaternion.Lerp(
            armPivot.rotation,
            Quaternion.Euler(0f, 0f, finalAngle),
            AimDeltaTime * rotationSpeed);

        if (Mathf.Abs(NormalizeAngle(finalAngle)) - 
            Mathf.Abs(NormalizeAngle(armPivot.rotation.z)) <= 0.05f)
        {
            isRecoil = false;
        }*/

    }

    public virtual void TriggerRecoil()
    {
        // 각도
        _recoilAngleOffset = 0f;
        _recoilAngleTarget = recoilAngle;
        _recoilAngleRising = true;

        // 위치
        _recoilPosOffset = 0f;
        _recoilPosTarget = recoilPushDist;
        _recoilPosRising = true;
        //isRecoil = true; // 미사용
    }

    protected virtual void UpdateRecoil()
    {
        // 각도
        if (_recoilAngleRising)
        {
            _recoilAngleOffset = Mathf.MoveTowards(_recoilAngleOffset, _recoilAngleTarget, recoilRiseSpeed * AimDeltaTime);
            if (Mathf.Approximately(_recoilAngleOffset, _recoilAngleTarget))
            {
                _recoilAngleRising = false;
                _recoilAngleTarget = 0f;
            }
        }
        else
        {
            _recoilAngleOffset = Mathf.Lerp(_recoilAngleOffset, 0f, recoilReturnSpeed * AimDeltaTime);
            if (Mathf.Abs(_recoilAngleOffset) < 0.01f) _recoilAngleOffset = 0f;
        }

        // 위치
        if (_recoilPosRising)
        {
            _recoilPosOffset = Mathf.MoveTowards(_recoilPosOffset, _recoilPosTarget, recoilPushSpeed * AimDeltaTime);
            if (Mathf.Approximately(_recoilPosOffset, _recoilPosTarget))
            {
                _recoilPosRising = false;
                _recoilPosTarget = 0f;
            }
        }
        else
        {
            _recoilPosOffset = Mathf.Lerp(_recoilPosOffset, 0f, recoilPullSpeed * AimDeltaTime);
            if (_recoilPosOffset < 0.001f) _recoilPosOffset = 0f;
        }
    }

    protected virtual float GetTargetWorldAngle(float rawAngle)
    {
        if (_isFacingRight)
            return Mathf.Clamp(rawAngle, minAngle, maxAngle);
        else
        {
            float rel = NormalizeAngle(rawAngle - 180f);
            return Mathf.Clamp(rel, minAngle, maxAngle);
        }
    }

    protected virtual void TryFlip(float rawAngle)
    {
        bool shouldFaceRight = rawAngle > -90f && rawAngle < 90f;
        if (shouldFaceRight != _isFacingRight) Flip();
    }

    protected virtual void Flip()
    {
        _isFacingRight = !_isFacingRight;

        float currentZ = armPivot.rotation.eulerAngles.z;
        float correctedZ = currentZ + (_isFacingRight ? 180f : -180f);
        armPivot.rotation = Quaternion.Euler(0f, 0f, correctedZ);

        if (bodyRenderer != null) bodyRenderer.flipX = !_isFacingRight;
        if (armRenderer != null) armRenderer.flipX = !_isFacingRight;

        // 베이스 위치도 flip
        _armBaseLocalPos = armPivot.localPosition;
        _armBaseLocalPos.x = -_armBaseLocalPos.x;
        armPivot.localPosition = _armBaseLocalPos;

        // 발사 위치도 flip
        _fireBaseLocalPos = firePivot.localPosition;
        _fireBaseLocalPos.x = -_fireBaseLocalPos.x;
        firePivot.localPosition = _fireBaseLocalPos;
    }

    protected virtual float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public virtual void SetFacing(bool facingRight)
    {
        if (_isFacingRight != facingRight) Flip();
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (bodyTransform == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(bodyTransform.position, detectionRange);
    }
#endif
}