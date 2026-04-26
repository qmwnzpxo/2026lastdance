using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    // ───────────────────────────────────────────────────────────────────────
    // Phase 1-1 — 방어막 (Persistent)
    //
    // [변경] 5초 딜타임 대기를 ShieldPattern이 아닌 각 Sequential 패턴이 담당.
    //        OnBarrierBroken 수신 시 방어막(VFX + 무적)만 즉시 해제하고 루프 재시작.
    //        Sequential 패턴에서 BreakBarrier() + await delay + return 하면
    //        자연스럽게 "파훼 → 딜타임 5초 → 다음 패턴"이 된다.
    // ───────────────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "Pattern_Phase1_Shield",
                     menuName = "BossSystem/Patterns/Phase1_Shield")]
    public class Phase1ShieldPattern : BossPatternBase
    {
        [Header("Shield")]
        [SerializeField] private GameObject _shieldVfxPrefab;
        [SerializeField, Range(0.05f, 0.5f)] private float _pollInterval = 0.1f;

        // ★ 제거: _barrierDownDuration (딜타임 대기는 각 패턴이 직접 처리)

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
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

                ctx.Blackboard.OnBarrierBroken    += onBarrierBroken;
                ctx.DamageHandler.OnBarrierBroken += onBarrierBroken;

                await UniTask.WhenAny(
                    breakTcs.Task.AttachExternalCancellation(ct),
                    WaitForCoresClearedAsync(ctx, ct)
                );

                ctx.Blackboard.OnBarrierBroken    -= onBarrierBroken;
                ctx.DamageHandler.OnBarrierBroken -= onBarrierBroken;

                if (vfx != null) Object.Destroy(vfx);
                // invHandle.Dispose() → ReleaseInvincibility 자동 호출

                if (ct.IsCancellationRequested) return;

                // ★ 변경: 방어막만 즉시 해제하고 루프 재시작.
                //         딜타임 5초는 각 Sequential 패턴(BreakBarrier 호출 측)이
                //         await UniTask.Delay(_dealTime, ct) 후 return.
                Log("Shield BROKEN. Waiting for sequential pattern to finish deal-time...");

                // 방어막 재활성화는 다음 루프 진입 시 수행.
                // 다음 Sequential 패턴이 시작될 때까지 Shield는 무적 없이 대기.
                await WaitBarrierRestoredAsync(ctx, ct);

                Log("Shield reactivating...");
            }
        }

        /// <summary>
        /// DamageHandler.OnBarrierRestored 이벤트가 올 때까지 대기.
        /// Sequential 패턴이 딜타임(_dealTimeDuration)을 소진하면
        /// DamageHandler.DealTimeCoroutine이 끝나며 OnBarrierRestored를 발생시킨다.
        /// </summary>
        private async UniTask WaitBarrierRestoredAsync(BossContext ctx, CancellationToken ct)
        {
            var restoredTcs = new UniTaskCompletionSource();
            System.Action onRestored = () => restoredTcs.TrySetResult();

            ctx.DamageHandler.OnBarrierRestored += onRestored;
            try
            {
                await restoredTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                ctx.DamageHandler.OnBarrierRestored -= onRestored;
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
