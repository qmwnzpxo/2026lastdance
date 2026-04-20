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

        // ── GC-safe 정적 버퍼 ────────────────────────────────────────────
        // _aliveBuffer: 살아있는 코어의 원본 인덱스를 담는 버퍼
        // 최대 4개 코어이므로 고정 크기 배열로 GC 없이 재사용
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
            // 빔별 색상 페이드 진행률 (0 = 완전 투명, 1 = 완전 불투명)
            // UpdateBeamsAndDamage 안에서만 읽고 씀 → FadeReconnectAsync 불필요
            var beamAlpha = new float[4];

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
                    Log($"Core {capturedIdx} destroyed. Total={ctx.DestroyedCoreCount}");
                    // null 표시만 — 빔 제어는 UpdateBeamsAndDamage 한 곳에서만 함
                    cores[capturedIdx] = null;
                };

                bb.RegisterCore(cores[i]);
                Debug.Log($"[Phase1LaserPattern] Core {i} registered.");
            }

            // ── 빔 스폰 (4개 고정) ────────────────────────────────────────
            for (int i = 0; i < 4; i++)
            {
                beams[i] = (_laserBeamPrefab != null && ObjectPool.HasPool(_laserBeamPrefab.gameObject))
                    ? ObjectPool.GetLineRenderer(_laserBeamPrefab.gameObject, Vector3.zero, Quaternion.identity)
                    : (_laserBeamPrefab != null ? Object.Instantiate(_laserBeamPrefab) : null);

                if (beams[i] != null) beams[i].enabled = false;
                beamAlpha[i] = 1f;
            }

            // ── 메인 루프 ─────────────────────────────────────────────────
            float angle = 0f;
            float soloDestructDelay = _soloCoreSelfDestructDelay;
            bool soloTriggered = false;

            while (!ct.IsCancellationRequested)
            {
                angle += _rotationSpeed * Time.deltaTime;
                UpdateCorePositions(ctx.BossTransform.position, cores, angle);
                UpdateBeamsAndDamage(cores, beams, beamAlpha,
                                     _laserThickness, _playerLayer, _damagePerSecond,
                                     _fadeReconnectDuration);

                if (soloDestructDelay > 0f && !soloTriggered)
                    soloTriggered = CheckSoloCoreDestruct(cores, soloDestructDelay, ct);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Cleanup(cores, beams);
        }

        // ─────────────────────────────────────────────────────────────────
        // 코어 위치 갱신
        // ─────────────────────────────────────────────────────────────────

        private void UpdateCorePositions(Vector3 center, LaserCore[] cores, float angle)
        {
            for (int i = 0; i < cores.Length; i++)
            {
                // null이면 이미 파괴된 코어 → 건너뜀
                if (cores[i] == null || !cores[i].gameObject.activeInHierarchy) continue;

                float rad = (angle + i * 90f) * Mathf.Deg2Rad;
                cores[i].transform.position = new Vector3(
                    center.x + Mathf.Cos(rad) * _radius,
                    center.y + Mathf.Sin(rad) * _radius,
                    center.z);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 빔 업데이트 & 데미지
        //
        // beamAlpha[i] : 빔 슬롯 i의 현재 투명도 (0~1).
        //   - 해당 슬롯이 활성 연결선으로 사용 중 → 매 프레임 1로 스냅
        //   - 더 이상 연결에 필요 없는 슬롯     → fadeDuration 동안 0으로 감소
        //   - 0에 도달하면 enabled = false
        //
        // 이렇게 하면 FadeReconnectAsync 같은 별도 Task 없이 단일 함수에서
        // 빔의 활성/비활성/페이드를 모두 처리할 수 있어 경쟁 조건이 사라짐.
        // ─────────────────────────────────────────────────────────────────
        private static void UpdateBeamsAndDamage(
            LaserCore[] cores,
            LineRenderer[] beams,
            float[] beamAlpha,
            float thickness,
            LayerMask playerLayer,
            float dps,
            float fadeDuration)
        {
            // ── 살아있는 코어 인덱스 수집 ────────────────────────────────
            int aliveCount = 0;
            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i] != null && cores[i].gameObject.activeInHierarchy)
                    _aliveBuffer[aliveCount++] = i;
            }

            // ── 어떤 빔 슬롯이 이번 프레임에 '활성 연결선'으로 쓰이는지 표시
            // stackalloc 대신 고정 배열 재사용 (GC 없음)
            // activeSlot[i] = true  → beams[i]가 이번 프레임 연결선으로 사용됨
            // activeSlot[i] = false → 페이드아웃 대상
            bool slot0 = false, slot1 = false, slot2 = false, slot3 = false;

            float damage = dps * Time.deltaTime;
            float fadeStep = fadeDuration > 0f ? Time.deltaTime / fadeDuration : 1f;
            var castSize = new Vector2(0.1f, thickness);

            // ── 살아있는 코어끼리 순환 연결 ──────────────────────────────
            if (aliveCount >= 2)
            {
                for (int i = 0; i < aliveCount; i++)
                {
                    int nextI = (i + 1) % aliveCount;
                    int coreIdxA = _aliveBuffer[i];
                    int coreIdxB = _aliveBuffer[nextI];

                    // 빔 슬롯은 alive 순서 i번을 사용
                    // (슬롯 번호가 코어 원본 인덱스와 달라도 무관)
                    switch (i)
                    {
                        case 0: slot0 = true; break;
                        case 1: slot1 = true; break;
                        case 2: slot2 = true; break;
                        case 3: slot3 = true; break;
                    }

                    Vector3 from = cores[coreIdxA].transform.position;
                    Vector3 to = cores[coreIdxB].transform.position;
                    Vector3 dir = to - from;
                    float dist = dir.magnitude;
                    if (dist < 0.01f) continue;

                    if (beams[i] != null)
                    {
                        // 활성 슬롯: alpha를 즉시 1로 복원
                        beamAlpha[i] = 1f;
                        beams[i].enabled = true;

                        Color col = beams[i].startColor;
                        col.a = 1f;
                        beams[i].startColor = beams[i].endColor = col;

                        beams[i].SetPosition(0, from);
                        beams[i].SetPosition(1, to);
                    }

                    // 데미지
                    var hit = Physics2D.BoxCast(
                        from, castSize,
                        Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                        dir.normalized, dist, playerLayer);
                    // if (hit.collider != null)
                    //     hit.collider.GetComponent<IPlayerHealth>()?.TakeDamage(damage);
                }
            }

            // ── 비활성 슬롯: 페이드아웃 처리 ────────────────────────────
            // 4개 슬롯을 명시적으로 처리 (루프 + switch보다 브랜치 예측 친화적)
            FadeSlot(beams, beamAlpha, 0, slot0, fadeStep);
            FadeSlot(beams, beamAlpha, 1, slot1, fadeStep);
            FadeSlot(beams, beamAlpha, 2, slot2, fadeStep);
            FadeSlot(beams, beamAlpha, 3, slot3, fadeStep);
        }

        /// <summary>
        /// 빔 슬롯 하나의 페이드아웃 처리.
        /// isActive = true 이면 아무것도 안 함 (UpdateBeamsAndDamage에서 이미 처리).
        /// isActive = false이면 alpha를 줄이고, 0이 되면 비활성화.
        /// </summary>
        private static void FadeSlot(
            LineRenderer[] beams, float[] beamAlpha, int idx, bool isActive, float fadeStep)
        {
            if (isActive) return;                              // 활성 슬롯은 건드리지 않음
            if (beams[idx] == null) return;
            if (!beams[idx].enabled) return;                  // 이미 꺼진 슬롯은 스킵

            beamAlpha[idx] = Mathf.Max(0f, beamAlpha[idx] - fadeStep);

            Color c = beams[idx].startColor;
            c.a = beamAlpha[idx];
            beams[idx].startColor = beams[idx].endColor = c;

            if (beamAlpha[idx] <= 0f)
                beams[idx].enabled = false;
        }

        // ─────────────────────────────────────────────────────────────────
        // 마지막 남은 코어 자폭
        // ─────────────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────
        // 정리
        // ─────────────────────────────────────────────────────────────────

        private static void Cleanup(LaserCore[] cores, LineRenderer[] beams)
        {
            foreach (var c in cores)
            {
                if (c == null) continue;
                if (ObjectPool.HasPool(c.gameObject)) ObjectPool.Return(c.gameObject);
                else Object.Destroy(c.gameObject);
            }

            foreach (var b in beams)
            {
                if (b == null) continue;
                if (ObjectPool.HasPool(b.gameObject)) ObjectPool.Return(b.gameObject);
                else Object.Destroy(b.gameObject);
            }
        }
    }
}
