using Boss.Phase1;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1-2 — 360도 레이저 (Sequential)
    //
    // 종료 조건
    //   A) 파훼: 타임아웃 전에 코어 4개 전부 파괴 → 파훼 이벤트 발생
    //   B) 타임아웃: _patternDuration 초 경과 → 남은 코어 강제 파괴 (파훼 판정 없음)
    //
    // 종료 연출
    //   빔 페이드아웃(_beamFadeDuration) 후 모든 오브젝트 정리 → 다음 패턴 진행
    // ───────────────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "Pattern_Phase1_360Laser",
                     menuName = "BossSystem/Patterns/Phase1_360Laser")]
    public class Phase1LaserPattern : BossPatternBase
    {
        [Header("Laser Cores")]
        [SerializeField] private GameObject _laserCorePrefab;
        [SerializeField] private LineRenderer _laserBeamPrefab;
        [SerializeField] private float _radius = 5f;
        [SerializeField] private float _rotationSpeed = 30f;

        [Header("Damage")]
        [SerializeField] private float _damagePerSecond = 20f;
        [SerializeField] private float _laserThickness = 0.5f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("Sequential Settings")]
        [Tooltip("패턴 지속 시간 (초). 이 시간 안에 코어를 전부 파괴하면 파훼 판정.")]
        [SerializeField] private float _patternDuration = 15f;
        [Tooltip("종료 시 빔 페이드아웃 시간 (초).")]
        [SerializeField, Range(0.05f, 1f)] private float _beamFadeDuration = 0.3f;

        [Header("Last Core Behavior")]
        [SerializeField, Range(0f, 10f)] private float _soloCoreSelfDestructDelay = 3f;

        // GC-safe 정적 버퍼
        private static readonly int[] _aliveBuffer = new int[4];
        private static readonly Vector3[] _spawnDirs =
        {
            Vector3.up, Vector3.right, Vector3.down, Vector3.left
        };

        private BossBlackboard _blackboard_ref;
        public void SetBlackboard(BossBlackboard bb) => _blackboard_ref = bb;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            var bb = _blackboard_ref ?? ctx.Blackboard;
            if (bb == null)
            {
                Debug.LogError("[Phase1LaserPattern] Blackboard is null!");
                return;
            }

            var bossPos = ctx.BossTransform.position;
            var cores = new LaserCore[4];
            var beams = new LineRenderer[4];

            // 이번 패턴 실행에서 파괴된 코어 수 (ctx.DestroyedCoreCount 와 별도 추적)
            int destroyedThisPattern = 0;

            // ── 코어 스폰 ─────────────────────────────────────────────────
            for (int i = 0; i < 4; i++)
            {
                var spawnPos = bossPos + _spawnDirs[i] * _radius;
                var go = ObjectPool.HasPool(_laserCorePrefab)
                    ? ObjectPool.Get(_laserCorePrefab, spawnPos, Quaternion.identity)
                    : Object.Instantiate(_laserCorePrefab, spawnPos, Quaternion.identity);

                cores[i] = go.GetComponent<LaserCore>();
                cores[i].Reset();

                int capturedIdx = i;
                cores[capturedIdx].OnCoreDestroyed += (destroyedCore) =>
                {
                    destroyedThisPattern++;
                    ctx.DestroyedCoreCount++;
                    Log($"Core {capturedIdx} destroyed. ThisPattern={destroyedThisPattern}");
                    FadeBeamAsync(beams, capturedIdx, _beamFadeDuration, ct).Forget();
                };

                bb.RegisterCore(cores[i]);
            }

            // ── 빔 스폰 ───────────────────────────────────────────────────
            for (int i = 0; i < 4; i++)
            {
                beams[i] = (_laserBeamPrefab != null && ObjectPool.HasPool(_laserBeamPrefab.gameObject))
                    ? ObjectPool.GetLineRenderer(_laserBeamPrefab.gameObject, Vector3.zero, Quaternion.identity)
                    : (_laserBeamPrefab != null ? Object.Instantiate(_laserBeamPrefab) : null);

                if (beams[i] != null) beams[i].enabled = false;
            }

            // ── 메인 루프 (타임아웃 or 전체 파괴까지) ────────────────────
            bool broke = false;   // 파훼 판정 여부
            bool soloTriggered = false;
            float elapsed = 0f;

            while (!ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;

                // 코어 위치·빔 갱신
                UpdateCorePositions(ctx.BossTransform.position, cores, elapsed * _rotationSpeed);
                UpdateBeamsAndDamage(cores, beams, _laserThickness, _playerLayer, _damagePerSecond);

                // 마지막 코어 자동 파괴 체크
                if (_soloCoreSelfDestructDelay > 0f && !soloTriggered)
                    soloTriggered = CheckSoloCoreDestruct(cores, _soloCoreSelfDestructDelay, ct);

                // ── 파훼 판정: 전부 파괴 ──────────────────────────────────
                if (destroyedThisPattern >= 4)
                {
                    broke = true;
                    Log("All cores destroyed — BREAK!");
                    break;
                }

                // ── 타임아웃 ─────────────────────────────────────────────
                if (elapsed >= _patternDuration)
                {
                    Log($"Timeout ({_patternDuration}s). Forcing remaining cores to die (no break).");
                    await ForceDestroyRemainingAsync(cores, beams, ct);
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // ── 종료 연출: 빔 전체 페이드아웃 ───────────────────────────
            await FadeAllBeamsAsync(beams, _beamFadeDuration, ct);

            // ── 파훼 이벤트 ──────────────────────────────────────────────
            if (broke)
            {
                ctx.Blackboard?.OnBarrierBroken_Invoke(); // 아래 확장 메서드 참조
                Log("Break event fired.");
            }

            Cleanup(cores, beams);
            Log($"Pattern ended. Broke={broke}");
        }

        // ─────────────────────────────────────────────────────────────────
        // 코어 위치 갱신
        // ─────────────────────────────────────────────────────────────────

        private void UpdateCorePositions(Vector3 center, LaserCore[] cores, float angle)
        {
            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i] == null || !cores[i].gameObject.activeInHierarchy) continue;
                float rad = (angle + i * 90f) * Mathf.Deg2Rad;
                cores[i].transform.position = new Vector3(
                    center.x + Mathf.Cos(rad) * _radius,
                    center.y + Mathf.Sin(rad) * _radius,
                    center.z);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 빔 + 데미지 갱신
        // ─────────────────────────────────────────────────────────────────

        private static void UpdateBeamsAndDamage(
            LaserCore[] cores, LineRenderer[] beams,
            float thickness, LayerMask playerLayer, float dps)
        {
            int aliveCount = 0;
            for (int i = 0; i < cores.Length; i++)
                if (cores[i] != null && cores[i].gameObject.activeInHierarchy)
                    _aliveBuffer[aliveCount++] = i;

            for (int i = 0; i < beams.Length; i++)
                if (beams[i] != null) beams[i].enabled = false;

            if (aliveCount < 2) return;

            var castSize = new Vector2(0.1f, thickness);
            float damage = dps * Time.deltaTime;

            for (int i = 0; i < aliveCount; i++)
            {
                int next = (i + 1) % aliveCount;
                Vector3 from = cores[_aliveBuffer[i]].transform.position;
                Vector3 to = cores[_aliveBuffer[next]].transform.position;
                Vector3 dir = to - from;
                float dist = dir.magnitude;
                if (dist < 0.01f) continue;

                if (beams[i] != null)
                {
                    beams[i].enabled = true;
                    beams[i].SetPosition(0, from);
                    beams[i].SetPosition(1, to);
                }

                // 데미지 판정 (필요 시 주석 해제)
                // var hit = Physics2D.BoxCast(from, castSize,
                //     Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                //     dir.normalized, dist, playerLayer);
                // if (hit.collider != null)
                //     hit.collider.GetComponent<IPlayerHealth>()?.TakeDamage(damage);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 마지막 코어 자동 파괴
        // ─────────────────────────────────────────────────────────────────

        private static bool CheckSoloCoreDestruct(
            LaserCore[] cores, float delay, CancellationToken ct)
        {
            int aliveCount = 0;
            LaserCore last = null;
            foreach (var c in cores)
            {
                if (c != null && c.gameObject.activeInHierarchy)
                {
                    aliveCount++;
                    last = c;
                }
            }

            if (aliveCount == 1 && last != null)
            {
                SoloCoreSelfDestructAsync(last, delay, ct).Forget();
                return true;
            }
            return false;
        }

        private static async UniTaskVoid SoloCoreSelfDestructAsync(
            LaserCore core, float delay, CancellationToken ct)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: ct)
                         .SuppressCancellationThrow();
            if (core != null && core.gameObject.activeInHierarchy)
                core.TakeDamage(float.MaxValue);
        }

        // ─────────────────────────────────────────────────────────────────
        // 타임아웃 시 남은 코어 강제 파괴 (파훼 판정 없음)
        // ─────────────────────────────────────────────────────────────────

        private static async UniTask ForceDestroyRemainingAsync(
            LaserCore[] cores, LineRenderer[] beams, CancellationToken ct)
        {
            // 빔 먼저 끔
            foreach (var b in beams)
                if (b != null) b.enabled = false;

            // 살아있는 코어 즉시 강제 파괴 (OnCoreDestroyed 이벤트는 발생하지만
            // destroyedThisPattern 카운트가 이미 4 미만이므로 파훼 판정에 영향 없음)
            foreach (var c in cores)
                if (c != null && c.gameObject.activeInHierarchy)
                    c.TakeDamage(float.MaxValue);

            // 파괴 VFX 재생 시간을 한 프레임 대기
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // ─────────────────────────────────────────────────────────────────
        // 빔 페이드아웃
        // ─────────────────────────────────────────────────────────────────

        /// <summary>특정 인덱스 빔 하나만 페이드아웃 (코어 파괴 콜백용).</summary>
        private static async UniTaskVoid FadeBeamAsync(
            LineRenderer[] beams, int idx, float duration, CancellationToken ct)
        {
            if (idx < 0 || idx >= beams.Length || beams[idx] == null) return;

            var lr = beams[idx];
            var startColor = lr.startColor;
            float elapsed = 0f;

            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                float t = elapsed / duration;
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                lr.startColor = lr.endColor = c;
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (lr != null) lr.enabled = false;
        }

        /// <summary>패턴 종료 시 살아있는 빔 전체 페이드아웃.</summary>
        private static async UniTask FadeAllBeamsAsync(
            LineRenderer[] beams, float duration, CancellationToken ct)
        {
            // 각 빔의 시작 알파를 캡처
            var startColors = new Color[beams.Length];
            for (int i = 0; i < beams.Length; i++)
                startColors[i] = beams[i] != null ? beams[i].startColor : Color.clear;

            float elapsed = 0f;
            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                float t = elapsed / duration;
                for (int i = 0; i < beams.Length; i++)
                {
                    if (beams[i] == null || !beams[i].enabled) continue;
                    Color c = startColors[i];
                    c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                    beams[i].startColor = beams[i].endColor = c;
                }
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            foreach (var b in beams)
                if (b != null) b.enabled = false;
        }

        // ─────────────────────────────────────────────────────────────────
        // 정리
        // ─────────────────────────────────────────────────────────────────

        private static void Cleanup(LaserCore[] cores, LineRenderer[] beams)
        {
            foreach (var c in cores)
                if (c != null)
                {
                    if (ObjectPool.HasPool(c.gameObject)) ObjectPool.Return(c.gameObject);
                    else Object.Destroy(c.gameObject);
                }

            foreach (var b in beams)
                if (b != null)
                {
                    if (ObjectPool.HasPool(b.gameObject)) ObjectPool.Return(b.gameObject);
                    else Object.Destroy(b.gameObject);
                }
        }
    }
}
