using Boss.Phase1;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{

    // ───────────────────────────────────────────────────────────────────────
    // Phase 1-2 — 360도 레이저 (Persistent)
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

        [Header("Reconnect Fade")]
        [SerializeField, Range(0.05f, 0.5f)] private float _fadeReconnectDuration = 0.2f;

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
                Debug.LogError("[Phase1LaserPattern] Blackboard is null! SetBlackboard() 또는 ctx.Blackboard를 확인하세요.");
                return;
            }

            var bossPos = ctx.BossTransform.position;
            var cores = new LaserCore[4];
            var beams = new LineRenderer[4];

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
                    ctx.DestroyedCoreCount++;
                    Log($"Core {capturedIdx} destroyed. Total destroyed={ctx.DestroyedCoreCount}");
                    FadeReconnectAsync(beams, capturedIdx, ct).Forget();
                };

                bb.RegisterCore(cores[i]);
                Debug.Log($"[Phase1LaserPattern] Core {i} registered to Blackboard.");
            }

            // ── 빔 스폰 ───────────────────────────────────────────────────
            for (int i = 0; i < 4; i++)
            {
                beams[i] = (_laserBeamPrefab != null && ObjectPool.HasPool(_laserBeamPrefab.gameObject))
                    ? ObjectPool.GetLineRenderer(_laserBeamPrefab.gameObject, Vector3.zero, Quaternion.identity)
                    : (_laserBeamPrefab != null ? Object.Instantiate(_laserBeamPrefab) : null);

                if (beams[i] != null) beams[i].enabled = false;
            }

            // ── 메인 루프 ─────────────────────────────────────────────────
            float angle = 0f;
            float soloDestructDelay = _soloCoreSelfDestructDelay; 
            bool soloTriggered = false;

            while (!ct.IsCancellationRequested)
            {
                angle += _rotationSpeed * Time.deltaTime;
                UpdateCorePositions(ctx.BossTransform.position, cores, angle);
                UpdateBeamsAndDamage(cores, beams, _laserThickness, _playerLayer, _damagePerSecond);

                if (soloDestructDelay > 0f && !soloTriggered)
                    soloTriggered = CheckSoloCoreDestruct(cores, soloDestructDelay, ct);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Cleanup(cores, beams);
        }

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

                var hit = Physics2D.BoxCast(
                    from, castSize,
                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                    dir.normalized, dist, playerLayer);

                //데미지 타격은 잠시 비활성화
                //if (hit.collider != null)
                 //   hit.collider.GetComponent<Boss.Phase1.LaserBeamManager.IPlayerHealth>()
                   //    ?.TakeDamage(damage);
            }
        }

        private static bool CheckSoloCoreDestruct(
            LaserCore[] cores, float delay, CancellationToken ct)
        {
            int aliveCount = 0;
            LaserCore lastCore = null;
            foreach (var c in cores)
            {
                if (c != null && c.gameObject.activeInHierarchy)
                {
                    aliveCount++;
                    lastCore = c;
                }
            }

            if (aliveCount == 1 && lastCore != null)
            {
                SoloCoreSelfDestructAsync(lastCore, delay, ct).Forget();
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

        private async UniTaskVoid FadeReconnectAsync(
            LineRenderer[] beams, int destroyedIdx, CancellationToken ct)
        {
            if (destroyedIdx < 0 || destroyedIdx >= beams.Length || beams[destroyedIdx] == null)
                return;

            var lr = beams[destroyedIdx];
            float elapsed = 0f;
            var startColor = lr.startColor;

            while (elapsed < _fadeReconnectDuration && !ct.IsCancellationRequested)
            {
                float t = elapsed / _fadeReconnectDuration;
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                lr.startColor = lr.endColor = c;
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (lr != null) lr.enabled = false;
        }

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