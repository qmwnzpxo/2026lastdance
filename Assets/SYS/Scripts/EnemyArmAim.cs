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
    protected bool isRecoil = false;

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

    protected virtual void Awake()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        _armBaseLocalPos = armPivot.localPosition;
        _fireBaseLocalPos = firePivot.localPosition;
    }

    protected virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
            TriggerRecoil();

        if (_enemyBB.enemyState.currentHp <= 0) return;
        UpdateRecoil();
    }

    protected virtual void LateUpdate()
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
        if (dist <= detectionRange && _enemyBB != null && _enemyBB.hasLOS)
        {
            Vector2 dir = _player.position - armPivot.position;
            float rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            TryFlip(rawAngle); // 시야가 있을 때만 플레이어 쪽으로 몸을 돌림
            targetWorldAngle = GetTargetWorldAngle(rawAngle);
        }
        else
        {
            // 플레이어를 안 볼 때 (순찰 중이거나, 벽 뒤에 있을 때)
            // 기괴한 -99도 대신, 현재 몸이 바라보는 방향(정면)을 바라보게 합니다.
            targetWorldAngle = _isFacingRight ? -99f : 99f;
        }

        // 4. 각도 반동 적용 및 최종 회전
        float recoilSign = _isFacingRight ? 1f : -1f;
        float finalAngle = targetWorldAngle + _recoilAngleOffset * recoilSign;

        armPivot.rotation = Quaternion.Lerp(
            armPivot.rotation,
            Quaternion.Euler(0f, 0f, finalAngle),
            Time.deltaTime * rotationSpeed);

        if (Mathf.Abs(NormalizeAngle(finalAngle)) -
            Mathf.Abs(NormalizeAngle(armPivot.rotation.z)) <= 0.05f)
        {
            isRecoil = false;
        }

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
            Time.deltaTime * rotationSpeed);

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
        isRecoil = true;
    }

    protected virtual void UpdateRecoil()
    {
        // 각도
        if (_recoilAngleRising)
        {
            _recoilAngleOffset = Mathf.MoveTowards(_recoilAngleOffset, _recoilAngleTarget, recoilRiseSpeed * Time.deltaTime);
            if (Mathf.Approximately(_recoilAngleOffset, _recoilAngleTarget))
            {
                _recoilAngleRising = false;
                _recoilAngleTarget = 0f;
            }
        }
        else
        {
            _recoilAngleOffset = Mathf.Lerp(_recoilAngleOffset, 0f, recoilReturnSpeed * Time.deltaTime);
            if (Mathf.Abs(_recoilAngleOffset) < 0.01f) _recoilAngleOffset = 0f;
        }

        // 위치
        if (_recoilPosRising)
        {
            _recoilPosOffset = Mathf.MoveTowards(_recoilPosOffset, _recoilPosTarget, recoilPushSpeed * Time.deltaTime);
            if (Mathf.Approximately(_recoilPosOffset, _recoilPosTarget))
            {
                _recoilPosRising = false;
                _recoilPosTarget = 0f;
            }
        }
        else
        {
            _recoilPosOffset = Mathf.Lerp(_recoilPosOffset, 0f, recoilPullSpeed * Time.deltaTime);
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