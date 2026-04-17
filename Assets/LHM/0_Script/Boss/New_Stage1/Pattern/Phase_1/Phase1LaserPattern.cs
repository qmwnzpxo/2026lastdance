using Boss.Phase1;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{

    // ───────────────────────────────────────────────────────────────────────
    // Phase 1 — 360도 레이저 (Persistent)
    // ───────────────────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "Pattern_Phase1_360Laser",
                     menuName = "BossSystem/Patterns/Phase1_360Laser")]
    public class Phase1LaserPattern : BossPatternBase
    {
        [Header("Laser Cores")]
        [SerializeField] private GameObject _laserCorePrefab;
        [SerializeField] private GameObject _laserBeamPrefab;
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

        [Header("Shield")]
        [SerializeField] private GameObject _shieldVfxPrefab;
        [SerializeField, Range(0.05f, 0.5f)] private float _pollInterval = 0.1f;

        [Header("Recovery")]
        [Tooltip("방어막 파괴 후 재생성까지의 딜 타임 (초)")]
        [SerializeField] private float _recoveryTime = 5f;
        private static readonly int[] _aliveBuffer = new int[4];

        private static readonly Vector3[] _spawnDirs =
        {
            Vector3.up, Vector3.right, Vector3.down, Vector3.left
        };

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            for (int i = 0; i < 4; i++)
            {
                // 1. 꺼내고 위치 잡기
                var coreGo = ObjectPool.Get(_laserCorePrefab, ctx.BossTransform.position, Quaternion.identity);
                var core = coreGo.GetComponent<LaserCore>();

                // 2. 초기화 (HP 등)
                core.Reset();

                // 3. 블랙보드에 등록 (이게 있어야 쉴드 패턴이 잠에서 깨어납니다!)
                ctx.Blackboard.RegisterCore(core);
            }

            // 2. 패턴 유지 루프
            while (!ct.IsCancellationRequested)
            {
                // 여기서 코어 회전이나 레이저 발사 로직을 구현하세요.
                // 모든 코어가 파괴되면 루프 탈출
                if (ctx.DestroyedCoreCount >= 4) break;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
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

            var castSize = new UnityEngine.Vector2(0.1f, thickness);
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
            }
        }

        // 반환값: 트리거 발생 여부 (한 번만 실행하기 위해)
        private static bool CheckSoloCoreDestruct(LaserCore[] cores, float delay, CancellationToken ct)
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