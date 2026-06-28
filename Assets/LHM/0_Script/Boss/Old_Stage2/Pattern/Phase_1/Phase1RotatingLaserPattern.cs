//using Cysharp.Threading.Tasks;
//using System.Threading;
//using UnityEngine;

//namespace BossSystem.Patterns
//{
//    [CreateAssetMenu(fileName = "Pattern_Phase1_RotatingLaser",
//                     menuName = "BossSystem/Patterns/Phase1_RotatingLaser")]
//    public class Phase1RotatingLaserPattern : BossPatternBase
//    {
//        [Header("Movement")]
//        [SerializeField] private Vector2 _centerPosition = Vector2.zero;
//        [SerializeField] private float   _moveToCenterSpeed = 5f;

//        [Header("Laser")]
//        [SerializeField] private GameObject _laserLinePrefab;
//        [SerializeField] private Color      _laserColor  = Color.cyan;
//        [SerializeField] private float      _laserLength = 25f;
//        [SerializeField] private float      _laserWidth  = 0.4f;

//        [Header("Rotation")]
//        [SerializeField] private float _rotationSpeed  = 45f;
//        [SerializeField] private float _spinUpDuration = 1.5f;

//        [Header("Damage")]
//        [SerializeField] private float     _laserDPS       = 30f;
//        [SerializeField] private float     _laserThickness = 0.3f;
//        [SerializeField] private float     _castDistance   = 30f;
//        [SerializeField] private LayerMask _playerLayer;

//        [Header("Turret Joints")]
//        [SerializeField] private GameObject _turretJointPrefab;
//        [SerializeField] private Vector2[]  _jointOffsets = {
//            new Vector2(-1.2f, 0.5f),
//            new Vector2( 1.2f, 0.5f)
//        };

//        [Header("VFX / Timing")]
//        [SerializeField] private ParticleSystem _chargeVfxPrefab;
//        [SerializeField] private float _chargeWaitTime = 1.5f;

//        [Tooltip("파훼 후 딜타임 지속 시간(초). DamageHandler의 Deal Time Duration과 동일하게 설정.")]
//        [SerializeField] private float _dealTimeDuration = 5f;

//        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
//        {
//            // ── 1. 중앙 이동 ──────────────────────────────────────────────
//            Log("Moving to center...");
//            await LaserUtility.MoveTowardsAsync(
//                ctx.BossTransform, _centerPosition, _moveToCenterSpeed, ct);

//            ct.ThrowIfCancellationRequested();

//            // ── 2. 차지 연출 ──────────────────────────────────────────────
//            ParticleSystem chargeVfx = null;
//            if (_chargeVfxPrefab != null)
//            {
//                chargeVfx = Object.Instantiate(
//                    _chargeVfxPrefab,
//                    ctx.BossTransform.position,
//                    Quaternion.identity,
//                    ctx.BossTransform);
//                chargeVfx.Play();
//            }

//            await UniTask.Delay(
//                System.TimeSpan.FromSeconds(_chargeWaitTime), cancellationToken: ct);

//            if (chargeVfx != null) Object.Destroy(chargeVfx.gameObject);
//            ct.ThrowIfCancellationRequested();

//            // ── 3. 터렛 연결부 소환 ───────────────────────────────────────
//            int jointCount     = _jointOffsets?.Length ?? 0;
//            var joints         = new GameObject[jointCount];
//            int destroyedCount = 0;

//            for (int i = 0; i < jointCount; i++)
//            {
//                if (_turretJointPrefab == null) break;

//                Vector3 spawnPos = ctx.BossTransform.position + (Vector3)_jointOffsets[i];
//                joints[i] = Object.Instantiate(
//                    _turretJointPrefab, spawnPos, Quaternion.identity,
//                    ctx.BossTransform);

//                var ph = joints[i].GetComponent<PanelHealth>();
//                if (ph != null)
//                {
//                    ph.ResetHP();
//                    int captured = i;
//                    ph.OnPanelDestroyed += _ =>
//                    {
//                        destroyedCount++;
//                        Log($"Joint {captured} destroyed! ({destroyedCount}/{jointCount})");
//                    };
//                }
//            }

//            // ── 4. 레이저 4개 생성 ────────────────────────────────────────
//            const int LASER_COUNT = 4;
//            var laserGos = new GameObject[LASER_COUNT];
//            var lasers   = new LineRenderer[LASER_COUNT];

//            for (int i = 0; i < LASER_COUNT; i++)
//            {
//                if (_laserLinePrefab == null) break;

//                var go      = Object.Instantiate(_laserLinePrefab);
//                var lr      = go.GetComponent<LineRenderer>();
//                laserGos[i] = go;

//                if (lr != null)
//                {
//                    lr.startWidth = _laserWidth;
//                    lr.endWidth   = _laserWidth;
//                    lr.startColor = _laserColor;
//                    lr.endColor   = _laserColor;
//                    lr.enabled    = true;
//                }
//                lasers[i] = lr;
//            }

//            // ── 5. 회전 루프 — 터렛 전부 파괴될 때까지 ───────────────────
//            Log("Rotating laser active. Destroy both joints to break pattern.");
//            float currentAngle   = 0f;
//            float elapsed        = 0f;
//            bool  jointsDestroyed = false;

//            try
//            {
//                while (!ct.IsCancellationRequested)
//                {
//                    if (jointCount > 0 && destroyedCount >= jointCount)
//                    {
//                        jointsDestroyed = true;
//                        Log("All joints destroyed — pattern broken!");
//                        break;
//                    }

//                    elapsed     += Time.deltaTime;
//                    float tRatio = elapsed < _spinUpDuration
//                        ? (elapsed / _spinUpDuration)
//                        : 1f;

//                    currentAngle += _rotationSpeed * tRatio * Time.deltaTime;

//                    for (int i = 0; i < LASER_COUNT; i++)
//                    {
//                        if (lasers[i] == null) continue;

//                        float   rad  = (currentAngle + i * 90f) * Mathf.Deg2Rad;
//                        Vector3 dir  = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
//                        Vector3 from = ctx.BossTransform.position;
//                        Vector3 to   = from + dir * _laserLength;

//                        lasers[i].SetPosition(0, from);
//                        lasers[i].SetPosition(1, to);

//                        LaserUtility.BoxCastAndDamage(
//                            from, dir, _laserThickness, _castDistance,
//                            _playerLayer, _laserDPS * Time.deltaTime);
//                    }

//                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
//                }
//            }
//            finally
//            {
//                foreach (var go in laserGos)
//                    if (go != null) Object.Destroy(go);

//                foreach (var j in joints)
//                    if (j != null) Object.Destroy(j);
//            }

//            // ── 6. 터렛 파괴로 루프 종료된 경우에만 파훼 처리 ───────────
//            // ct 취소(페이즈 전환 등)로 루프가 끊긴 경우는 파훼 처리하지 않음.
//            if (!jointsDestroyed) return;

//            // ── 7. 방어막 해제 → 5초 딜타임 ─────────────────────────────
//            ctx.DamageHandler.BreakBarrier();
//            Log($"Barrier broken! Deal time {_dealTimeDuration}s.");

//            await UniTask.Delay(
//                System.TimeSpan.FromSeconds(_dealTimeDuration), cancellationToken: ct);

//            Log("RotatingLaser pattern complete. Handing off to next pattern.");
//        }
//    }
//}
