//using Cysharp.Threading.Tasks;
//using System.Threading;
//using UnityEngine;

//namespace BossSystem.Patterns
//{

//    [CreateAssetMenu(fileName = "Pattern_Phase1_Shield",
//                     menuName = "BossSystem/Patterns/Phase1_Shield")]
//    public class Phase1ShieldPattern : BossPatternBase
//    {
//        [Header("Shield")]
//        [SerializeField] private GameObject _shieldVfxPrefab;
//        [SerializeField, Range(0.05f, 0.5f)] private float _pollInterval = 0.1f;

//        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
//        {
//            while (!ct.IsCancellationRequested)
//            {
//                // ── 방어막 ON ─────────────────────────────────────────────
//                using var invHandle = ctx.DamageHandler.CreateInvincibilityHandle();

//                GameObject vfx = null;
//                if (_shieldVfxPrefab != null)
//                    vfx = Object.Instantiate(_shieldVfxPrefab,
//                              ctx.BossTransform.position, Quaternion.identity,
//                              ctx.BossTransform);

//                Log("Shield ON. Waiting for break condition...");

//                var breakTcs = new UniTaskCompletionSource();
//                System.Action onBarrierBroken = () => breakTcs.TrySetResult();

//                ctx.Blackboard.OnBarrierBroken    += onBarrierBroken;
//                ctx.DamageHandler.OnBarrierBroken += onBarrierBroken;

//                await UniTask.WhenAny(
//                    breakTcs.Task.AttachExternalCancellation(ct),
//                    WaitForCoresClearedAsync(ctx, ct)
//                );

//                ctx.Blackboard.OnBarrierBroken    -= onBarrierBroken;
//                ctx.DamageHandler.OnBarrierBroken -= onBarrierBroken;

//                if (vfx != null) Object.Destroy(vfx);
//                // invHandle.Dispose() → ReleaseInvincibility 자동 호출

//                if (ct.IsCancellationRequested) return;


//                Log("Shield BROKEN. Waiting for sequential pattern to finish deal-time...");


//                await WaitBarrierRestoredAsync(ctx, ct);

//                Log("Shield reactivating...");
//            }
//        }

//        /// <summary>
//        /// DamageHandler.OnBarrierRestored 이벤트가 올 때까지 대기.
//        /// Sequential 패턴이 딜타임(_dealTimeDuration)을 소진하면
//        /// DamageHandler.DealTimeCoroutine이 끝나며 OnBarrierRestored를 발생시킨다.
//        /// </summary>
//        private async UniTask WaitBarrierRestoredAsync(BossContext ctx, CancellationToken ct)
//        {
//            var restoredTcs = new UniTaskCompletionSource();
//            System.Action onRestored = () => restoredTcs.TrySetResult();

//            ctx.DamageHandler.OnBarrierRestored += onRestored;
//            try
//            {
//                await restoredTcs.Task.AttachExternalCancellation(ct);
//            }
//            finally
//            {
//                ctx.DamageHandler.OnBarrierRestored -= onRestored;
//            }
//        }

//        private async UniTask WaitForCoresClearedAsync(BossContext ctx, CancellationToken ct)
//        {
//            while (!ct.IsCancellationRequested)
//            {
//                if (ctx.DestroyedCoreCount >= ctx.TotalCoreCount)
//                {
//                    Log("All cores destroyed (polling). Shield deactivated.");
//                    return;
//                }
//                await UniTask.Delay(
//                    System.TimeSpan.FromSeconds(_pollInterval), cancellationToken: ct);
//            }
//        }
//    }
//}
