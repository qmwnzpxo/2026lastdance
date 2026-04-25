using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 패턴 1. 중앙 회전 레이저 (Swoo Style) [Sequential]
    //
    // 흐름:
    //   1. 보스가 화면 중앙으로 이동
    //   2. 차지 VFX 재생 (_chargeWaitTime 대기)
    //   3. 터렛 연결부(TurretJoint) 2개가 보스에 부착 생성 → 공격 가능
    //   4. 4방향 굵은 레이저 발사 + 시계/반시계 회전 루프
    //   5. 양쪽 Joint 모두 파괴 시 레이저 정지 → 파훼 완료
    //
    // 의존 컴포넌트:
    //   · _turretJointPrefab : PanelHealth 컴포넌트가 붙어 있어야 함
    //   · _laserLinePrefab   : LineRenderer 프리팹
    //   · LaserUtility       : 기존 유틸 재사용
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase1_RotatingLaser",
                     menuName = "BossSystem/Patterns/Phase1_RotatingLaser")]
    public class Phase1RotatingLaserPattern : BossPatternBase
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("보스가 이동할 중앙 월드 좌표")]
        [SerializeField] private Vector2 _centerPosition = Vector2.zero;
        [SerializeField] private float   _moveToCenterSpeed = 5f;

        [Header("Laser")]
        [Tooltip("레이저 LineRenderer 프리팹")]
        [SerializeField] private LineRenderer _laserLinePrefab;
        [SerializeField] private Color        _laserColor   = Color.cyan;
        [SerializeField] private float        _laserLength  = 25f;
        [SerializeField] private float        _laserWidth   = 0.4f;

        [Header("Rotation")]
        [Tooltip("초당 회전 각도. 양수 = 반시계, 음수 = 시계")]
        [SerializeField] private float _rotationSpeed  = 45f;
        [Tooltip("처음 몇 초간 가속할지 (스핀업)")]
        [SerializeField] private float _spinUpDuration = 1.5f;

        [Header("Damage")]
        [SerializeField] private float     _laserDPS       = 30f;
        [SerializeField] private float     _laserThickness = 0.3f;
        [SerializeField] private float     _castDistance   = 30f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("Turret Joints")]
        [Tooltip("파괴 대상 터렛 연결부 프리팹 (PanelHealth 필요)")]
        [SerializeField] private GameObject _turretJointPrefab;
        [Tooltip("보스 기준 Joint 생성 오프셋 (2개 권장)")]
        [SerializeField] private Vector2[]  _jointOffsets = {
            new Vector2(-1.2f, 0.5f),
            new Vector2( 1.2f, 0.5f)
        };

        [Header("VFX / Timing")]
        [SerializeField] private ParticleSystem _chargeVfxPrefab;
        [Tooltip("차지 연출 대기 시간 (초)")]
        [SerializeField] private float _chargeWaitTime       = 1.5f;
        [Tooltip("파훼 완료 후 다음 패턴으로 넘어가기 전 대기 (초)")]
        [SerializeField] private float _breakCelebrationTime = 1f;

        // ─────────────────────────────────────────────────────────────────
        // OnExecuteAsync
        // ─────────────────────────────────────────────────────────────────

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            // ── 1. 중앙 이동 ──────────────────────────────────────────────
            Log("Moving to center...");
            await LaserUtility.MoveTowardsAsync(
                ctx.BossTransform, _centerPosition, _moveToCenterSpeed, ct);

            ct.ThrowIfCancellationRequested();

            // ── 2. 차지 연출 ──────────────────────────────────────────────
            ParticleSystem chargeVfx = null;
            if (_chargeVfxPrefab != null)
            {
                chargeVfx = Object.Instantiate(
                    _chargeVfxPrefab,
                    ctx.BossTransform.position,
                    Quaternion.identity,
                    ctx.BossTransform);
                chargeVfx.Play();
            }

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_chargeWaitTime), cancellationToken: ct);

            if (chargeVfx != null) Object.Destroy(chargeVfx.gameObject);
            ct.ThrowIfCancellationRequested();

            // ── 3. 터렛 연결부 소환 ───────────────────────────────────────
            int jointCount    = _jointOffsets?.Length ?? 0;
            var joints        = new GameObject[jointCount];
            int destroyedCount = 0;

            for (int i = 0; i < jointCount; i++)
            {
                if (_turretJointPrefab == null) break;

                Vector3 spawnPos = ctx.BossTransform.position + (Vector3)_jointOffsets[i];
                joints[i] = Object.Instantiate(
                    _turretJointPrefab, spawnPos, Quaternion.identity,
                    ctx.BossTransform);

                var ph = joints[i].GetComponent<PanelHealth>();
                if (ph != null)
                {
                    ph.ResetHP();
                    int captured = i;
                    ph.OnPanelDestroyed += _ =>
                    {
                        destroyedCount++;
                        Log($"Joint {captured} destroyed! ({destroyedCount}/{jointCount})");
                    };
                }
            }

            // ── 4. 레이저 4개 생성 ────────────────────────────────────────
            const int LASER_COUNT = 4;
            var lasers = new LineRenderer[LASER_COUNT];

            for (int i = 0; i < LASER_COUNT; i++)
            {
                if (_laserLinePrefab == null) break;

                var lr         = Object.Instantiate(_laserLinePrefab);
                lr.startWidth  = _laserWidth;
                lr.endWidth    = _laserWidth;
                lr.startColor  = _laserColor;
                lr.endColor    = _laserColor;
                lr.enabled     = true;
                lasers[i]      = lr;
            }

            // ── 5. 회전 루프 ──────────────────────────────────────────────
            Log("Rotating laser active. Destroy both joints to break pattern.");
            float currentAngle = 0f;
            float elapsed      = 0f;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // 파훼 조건: 모든 Joint 파괴
                    if (jointCount > 0 && destroyedCount >= jointCount)
                    {
                        Log("All joints destroyed — pattern broken!");
                        break;
                    }

                    // 스핀업 가속
                    elapsed     += Time.deltaTime;
                    float tRatio = elapsed < _spinUpDuration
                        ? (elapsed / _spinUpDuration)
                        : 1f;

                    currentAngle += _rotationSpeed * tRatio * Time.deltaTime;

                    // 레이저 방향/데미지 갱신
                    for (int i = 0; i < LASER_COUNT; i++)
                    {
                        if (lasers[i] == null) continue;

                        float   rad   = (currentAngle + i * 90f) * Mathf.Deg2Rad;
                        Vector3 dir   = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                        Vector3 from  = ctx.BossTransform.position;
                        Vector3 to    = from + dir * _laserLength;

                        lasers[i].SetPosition(0, from);
                        lasers[i].SetPosition(1, to);

                        LaserUtility.BoxCastAndDamage(
                            from, dir, _laserThickness, _castDistance,
                            _playerLayer, _laserDPS * Time.deltaTime);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                foreach (var lr in lasers)
                    if (lr != null) Object.Destroy(lr.gameObject);

                foreach (var j in joints)
                    if (j != null) Object.Destroy(j);
            }

            // ── 6. 파훼 연출 대기 ─────────────────────────────────────────
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_breakCelebrationTime), cancellationToken: ct);

            Log("RotatingLaser pattern complete.");
        }
    }
}
