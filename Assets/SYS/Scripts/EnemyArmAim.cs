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

    private Transform _player;
    private bool _isFacingRight = true;

    private void Awake()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;
    }

    private void LateUpdate()
    {
        if (_player == null || armPivot == null) return;

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
            targetWorldAngle = 0f; // 기본 자세 (flipX가 방향 처리)
        }

        // ★ localEulerAngles → rotation (월드 기준)
        // 부모 localScale.x 음수여도 Y 반전 없음
        armPivot.rotation = Quaternion.Lerp(
            armPivot.rotation,
            Quaternion.Euler(0f, 0f, targetWorldAngle),
            Time.deltaTime * rotationSpeed);
    }

    private float GetTargetWorldAngle(float rawAngle)
    {
        if (_isFacingRight)
        {
            // 오른쪽: rawAngle 그대로 클램프
            return Mathf.Clamp(rawAngle, minAngle, maxAngle);
        }
        else
        {
            // 왼쪽: 180° 기준 상대각도 → 클램프 → flipX 보정(-180°) 상쇄
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

        if (bodyRenderer != null) bodyRenderer.flipX = !_isFacingRight;
        if (armRenderer != null) armRenderer.flipX = !_isFacingRight;

        Vector3 pos = armPivot.localPosition;
        pos.x = -pos.x;
        armPivot.localPosition = pos;
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