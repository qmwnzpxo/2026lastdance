using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1-1 — 방어막 (Persistent)
    // ───────────────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "Pattern_Phase1_Shield",
                     menuName = "BossSystem/Patterns/Phase1_Shield")]
    public class Phase1ShieldPattern : BossPatternBase
    {
        [Header("Shield")]
        [SerializeField] private GameObject _shieldVfxPrefab;
        [SerializeField, Range(0.05f, 0.5f)] private float _pollInterval = 0.1f;
        [Tooltip("파훼 후 방어막이 꺼져있는 시간 (초). 기획: 5초")]
        [SerializeField] private float _barrierDownDuration = 5f;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            // Persistent 패턴이므로 페이즈 내내 반복
            while (!ct.IsCancellationRequested)
            {
                // ── 방어막 ON ─────────────────────────────────────────────
                using var invHandle = ctx.DamageHandler.CreateInvincibilityHandle();

                GameObject vfx = null;
                if (_shieldVfxPrefab != null)
                    vfx = Object.Instantiate(_shieldVfxPrefab,
                              ctx.BossTransform.position, Quaternion.identity,
                              ctx.BossTransform);

                Log("Shield ON. Waiting for break condition...");
                var breakTcs = new UniTaskCompletionSource();
                System.Action onBarrierBroken = () => breakTcs.TrySetResult();
                ctx.Blackboard.OnBarrierBroken += onBarrierBroken;

                // WhenAny: 이벤트 or 폴링 조건 중 먼저 충족되는 것
                await UniTask.WhenAny(
                    breakTcs.Task.AttachExternalCancellation(ct),
                    WaitForCoresClearedAsync(ctx, ct)
                );

                ctx.Blackboard.OnBarrierBroken -= onBarrierBroken;

                if (vfx != null) Object.Destroy(vfx);
                // invHandle.Dispose() → ReleaseInvincibility 자동 호출

                if (ct.IsCancellationRequested) return;

                // ── 방어막 OFF: 5초간 무적 해제 ──────────────────────────
                Log($"Shield BROKEN. Down for {_barrierDownDuration}s.");
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_barrierDownDuration),
                    cancellationToken: ct);

                Log("Shield reactivating...");
                // DestroyedCoreCount 리셋은 BossController.EnterPhaseAsync에서 처리
                // 여기서는 단순히 루프를 돌아 방어막 재활성화
            }
        }

        private async UniTask WaitForCoresClearedAsync(BossContext ctx, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (ctx.DestroyedCoreCount >= ctx.TotalCoreCount)
                {
                    Log("All cores destroyed (polling). Shield deactivated.");
                    return;
                }
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_pollInterval), cancellationToken: ct);
            }
        }
    }
}
