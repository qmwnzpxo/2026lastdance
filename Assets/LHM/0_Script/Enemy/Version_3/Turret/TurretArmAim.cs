using UnityEngine;

public class TurretArmAim : EnemyArmAim
{
    [SerializeField] protected SpriteRenderer frontRenderer;
    protected override void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
            TriggerRecoil();

        UpdateRecoil();
    }

    protected override void LateUpdate()
    {
        if (_player == null || armPivot == null) return;

        // ── 위치 반동 적용 ──
        // 현재 aim 방향 반대(뒤)로 밀기
        float aimWorldAngle = armPivot.rotation.eulerAngles.z;
        float pushRad = (aimWorldAngle) * Mathf.Deg2Rad;
        Vector3 pushDir = new Vector3(Mathf.Cos(pushRad), Mathf.Sin(pushRad), 0f);
        armPivot.localPosition = _armBaseLocalPos + pushDir * _recoilPosOffset;

        // ── 각도 반동 + aim ──
        float dist = Vector2.Distance(bodyTransform.position, _player.position);

        float targetWorldAngle = 0f; // 기본 각도. 이 밑으로 0707추가코드

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
            targetWorldAngle = _isFacingRight ? 0f : 180f;
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

    public override void TriggerRecoil()
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

    protected override void UpdateRecoil()
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

    protected override float GetTargetWorldAngle(float rawAngle)
    {
        if (_isFacingRight)
            return Mathf.Clamp(rawAngle, minAngle, maxAngle);
        else
        {
            float rel = NormalizeAngle(rawAngle - 180f);
            return Mathf.Clamp(rel, minAngle, maxAngle);
        }
    }

    protected override void TryFlip(float rawAngle)
    {
        bool shouldFaceRight = rawAngle > -90f && rawAngle < 90f;
        if (shouldFaceRight != _isFacingRight) Flip();
    }

    protected override void Flip()
    {
        _isFacingRight = !_isFacingRight;

        float currentZ = armPivot.rotation.eulerAngles.z;
        float correctedZ = currentZ + (_isFacingRight ? -180f : 180f);
        armPivot.rotation = Quaternion.Euler(0f, 0f, correctedZ);

        if (bodyRenderer != null) bodyRenderer.flipX = _isFacingRight;
        if (armRenderer != null) armRenderer.flipX = _isFacingRight;
        if (frontRenderer != null) frontRenderer.flipX = _isFacingRight;

        // 베이스 위치도 flip
        _armBaseLocalPos = armPivot.localPosition;
        _armBaseLocalPos.x = -_armBaseLocalPos.x;
        armPivot.localPosition = _armBaseLocalPos;

        // 발사 위치도 flip
        _fireBaseLocalPos = firePivot.localPosition;
        _fireBaseLocalPos.x = -_fireBaseLocalPos.x;
        firePivot.localPosition = _fireBaseLocalPos;
    }

    protected override float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public override void SetFacing(bool facingRight)
    {
        if (_isFacingRight != facingRight) Flip();
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        if (bodyTransform == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(bodyTransform.position, detectionRange);
    }
#endif
}