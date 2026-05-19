using UnityEngine;

public class EnemyArmAim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Transform armPivot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer armRenderer;

    [Header("Range")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Angle Clamp")]
    [SerializeField] private float minAngle = -60f;
    [SerializeField] private float maxAngle = 60f;

    [Header("Recoil - Angle")]
    [SerializeField] private float recoilAngle = 45f;
    [SerializeField] private float recoilRiseSpeed = 800f;
    [SerializeField] private float recoilReturnSpeed = 12f;

    [Header("Recoil - Position")]
    [SerializeField] private float recoilPushDist = 0.3f;   // 뒤로 밀리는 거리
    [SerializeField] private float recoilPushSpeed = 800f;  // 밀리는 속도
    [SerializeField] private float recoilPullSpeed = 10f;   // 복귀 속도

    private Transform _player;
    private bool _isFacingRight = true;

    // 각도 반동
    private float _recoilAngleOffset = 0f;
    private float _recoilAngleTarget = 0f;
    private bool _recoilAngleRising = false;

    // 위치 반동
    private Vector3 _armBaseLocalPos;         // 원래 로컬 위치 (flip 고려)
    private float _recoilPosOffset = 0f;      // 뒤로 밀린 거리 (스칼라)
    private float _recoilPosTarget = 0f;
    private bool _recoilPosRising = false;

    private void Awake()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        _armBaseLocalPos = armPivot.localPosition;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
            TriggerRecoil();

        UpdateRecoil();
    }

    private void LateUpdate()
    {
        if (_player == null || armPivot == null) return;

        // ── 위치 반동 적용 ──
        // 현재 aim 방향 반대(뒤)로 밀기
        float aimWorldAngle = armPivot.rotation.eulerAngles.z;
        float pushRad = (aimWorldAngle + 180f) * Mathf.Deg2Rad;
        Vector3 pushDir = new Vector3(Mathf.Cos(pushRad), Mathf.Sin(pushRad), 0f);
        armPivot.localPosition = _armBaseLocalPos + pushDir * _recoilPosOffset;

        // ── 각도 반동 + aim ──
        float dist = Vector2.Distance(bodyTransform.position, _player.position);
        Vector2 dir = _player.position - armPivot.position;
        float rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float targetWorldAngle;
        if (dist <= detectionRange)
        {
            TryFlip(rawAngle);
            targetWorldAngle = GetTargetWorldAngle(rawAngle);
        }
        else
        {
            targetWorldAngle = 0f;
        }

        float recoilSign = _isFacingRight ? 1f : -1f;
        float finalAngle = targetWorldAngle + _recoilAngleOffset * recoilSign;

        armPivot.rotation = Quaternion.Lerp(
            armPivot.rotation,
            Quaternion.Euler(0f, 0f, finalAngle),
            Time.deltaTime * rotationSpeed);
    }

    public void TriggerRecoil()
    {
        // 각도
        _recoilAngleOffset = 0f;
        _recoilAngleTarget = recoilAngle;
        _recoilAngleRising = true;

        // 위치
        _recoilPosOffset = 0f;
        _recoilPosTarget = recoilPushDist;
        _recoilPosRising = true;
    }

    private void UpdateRecoil()
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

    private float GetTargetWorldAngle(float rawAngle)
    {
        if (_isFacingRight)
            return Mathf.Clamp(rawAngle, minAngle, maxAngle);
        else
        {
            float rel = NormalizeAngle(rawAngle - 180f);
            return Mathf.Clamp(rel, minAngle, maxAngle);
        }
    }

    private void TryFlip(float rawAngle)
    {
        bool shouldFaceRight = rawAngle > -90f && rawAngle < 90f;
        if (shouldFaceRight != _isFacingRight) Flip();
    }

    private void Flip()
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
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void SetFacing(bool facingRight)
    {
        if (_isFacingRight != facingRight) Flip();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (bodyTransform == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(bodyTransform.position, detectionRange);
    }
#endif
}