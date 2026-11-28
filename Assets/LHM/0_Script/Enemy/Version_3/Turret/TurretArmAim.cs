using UnityEngine;

public class TurretArmAim : EnemyArmAim
{
    [SerializeField] protected SpriteRenderer frontRenderer;

    protected override void Awake()
    {
        base.Awake();
        // 포탑 스프라이트 원본은 왼쪽을 향함. 시작 상태(_isFacingRight = true)에 맞게
        // 스프라이트를 맞춰두지 않으면 첫 Flip 전까지 포신이 반대(왼쪽)로 보임
        ApplySpriteFlip();
    }

    // 원본 스프라이트가 왼쪽을 향하므로 오른쪽을 볼 때 flipX = true
    void ApplySpriteFlip()
    {
        if (bodyRenderer != null) bodyRenderer.flipX = _isFacingRight;
        if (armRenderer != null) armRenderer.flipX = _isFacingRight;
        if (frontRenderer != null) frontRenderer.flipX = _isFacingRight;
    }

    public override void LateTick()
    {
        if (_player == null || armPivot == null) return;
        if (_enemyBB.enemyState.currentHp <= 0) return;

        // ── 위치 반동 적용 ──
        // 포신이 실제로 가리키는 방향의 반대(뒤)로 밀기
        // 왼쪽을 볼 땐 스프라이트가 flipX 되어 있어 포신 방향 = 회전각 + 180
        float aimWorldAngle = armPivot.rotation.eulerAngles.z;
        float pushRad = (aimWorldAngle + (_isFacingRight ? 180f : 0f)) * Mathf.Deg2Rad;
        Vector3 pushDir = new Vector3(Mathf.Cos(pushRad), Mathf.Sin(pushRad), 0f);
        armPivot.localPosition = _armBaseLocalPos + pushDir * _recoilPosOffset;

        // ── 각도 반동 + aim ──
        float dist = Vector2.Distance(bodyTransform.position, _player.position);

        float targetWorldAngle = 0f; // 기본 각도. 이 밑으로 0707추가코드

        // [핵심 수정] 거리 안이면서 동시에 시야(LOS)가 확보되었을 때만! 플레이어를 조준합니다.
        // _enemyBB null 검사는 위에서 이미 _enemyBB.enemyState를 쓴 뒤라 의미 없음
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
            // 정면을 바라보게 한다. 조준 각도는 좌우 모두 "정면 기준 상대각"이라
            // 왼쪽일 때도 0도가 정면 (180을 주면 포신이 뒤로 뒤집힘)
            targetWorldAngle = 0f;
        }

        // 4. 각도 반동 적용 및 최종 회전
        float recoilSign = _isFacingRight ? 1f : -1f;
        float finalAngle = targetWorldAngle + _recoilAngleOffset * recoilSign;

        armPivot.rotation = Quaternion.Lerp(
            armPivot.rotation,
            Quaternion.Euler(0f, 0f, finalAngle),
            AimDeltaTime * rotationSpeed);

        // isRecoil 미사용 (EnemyArmAim 참고)
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

    // 아래 TriggerRecoil·UpdateRecoil·GetTargetWorldAngle·TryFlip은 EnemyArmAim과 내용이 같아 불필요
    /*public override void TriggerRecoil()
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
    }*/

    protected override void Flip()
    {
        _isFacingRight = !_isFacingRight;

        float currentZ = armPivot.rotation.eulerAngles.z;
        float correctedZ = currentZ + (_isFacingRight ? -180f : 180f);
        armPivot.rotation = Quaternion.Euler(0f, 0f, correctedZ);

        ApplySpriteFlip();

        // 베이스 위치도 flip
        // 현재 localPosition엔 반동 밀림이 섞여 있으므로 저장된 기준값을 뒤집는다
        // (반동 중 Flip 될 때마다 기준점이 밀려 포신이 본체를 벗어나던 문제)
        _armBaseLocalPos.x = -_armBaseLocalPos.x;
        armPivot.localPosition = _armBaseLocalPos;

        // 발사 위치도 flip
        _fireBaseLocalPos.x = -_fireBaseLocalPos.x;
        firePivot.localPosition = _fireBaseLocalPos;
    }

    /// <summary>
    /// 포신이 실제로 플레이어를 겨눌 수 있을 때만 true.
    /// 조준 사거리 밖, 조준 각도 제한 밖, 발사구~플레이어 사이에 지형이 있으면 쏘지 않는다.
    /// </summary>
    public bool CanShootAtPlayer()
    {
        if (_player == null || armPivot == null || firePivot == null || _enemyBB == null)
            return false;
        if (!_enemyBB.hasLOS) return false;

        // 조준 사거리 (이 밖에서는 포신이 정면으로 돌아가 있으므로 발사 금지)
        if (Vector2.Distance(bodyTransform.position, _player.position) > detectionRange)
            return false;

        // 조준 각도 제한 (각도 밖이면 포신은 clamp 되어 있는데 탄은 플레이어로 날아감 → 바닥 쪽 발사)
        Vector2 dir = _player.position - armPivot.position;
        float rawAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float rel = _isFacingRight ? NormalizeAngle(rawAngle) : NormalizeAngle(rawAngle - 180f);
        if (rel < minAngle || rel > maxAngle) return false;

        // 발사구에서 플레이어까지 지형에 막히는지
        Vector2 firePos = firePivot.position;
        Vector2 toPlayer = (Vector2)_player.position - firePos;
        if (Physics2D.Raycast(firePos, toPlayer.normalized, toPlayer.magnitude, _enemyBB.groundLayer).collider != null)
            return false;

        return true;
    }

    // NormalizeAngle·SetFacing·OnDrawGizmosSelected도 EnemyArmAim과 내용이 같아 불필요
    /*protected override float NormalizeAngle(float angle)
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
#endif*/
}