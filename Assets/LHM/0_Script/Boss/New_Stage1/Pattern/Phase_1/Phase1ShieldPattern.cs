using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace BossSystem.Patterns
{
    [CreateAssetMenu(fileName = "Pattern_Phase1_Shield", menuName = "BossSystem/Patterns/Phase1_Shield")]
    public class Phase1ShieldPattern : BossPatternBase
    {
        [Header("Shield")]
        [SerializeField] private GameObject _shieldVfxPrefab;
        [SerializeField, Range(0.05f, 0.5f)] private float _pollInterval = 0.1f;

        [Header("Recovery")]
        [Tooltip("방어막 파괴 후 재생성까지의 딜 타임 (초)")]
        [SerializeField] private float _recoveryTime = 5f;

        protected override async UniTask OnExecuteAsync(BossContext ctx, CancellationToken ct)
        {
            // Phase1ShieldPattern.cs 의 OnExecuteAsync 내부 루프
            while (!ct.IsCancellationRequested)
            {
                // [수정된 부분] 레이저 패턴이 코어를 리스폰해서 1개 이상이 될 때까지 대기
                while (ctx.TotalCoreCount == 0 && !ct.IsCancellationRequested)
                {
                    // 가능하면 UniTask를 사용하여 대기
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                using (var invHandle = ctx.DamageHandler.CreateInvincibilityHandle())
                {
                    // 방어막 VFX 생성 등...

                    while (!ct.IsCancellationRequested)
                    {
                        if (ctx.TotalCoreCount > 0 && ctx.DestroyedCoreCount >= ctx.TotalCoreCount)
                        {
                            break; // 코어가 다 부서지면 방어막 해제
                        }

                        // 가능하면 UniTask.Delay를 사용하여 대기
                        await UniTask.Delay(System.TimeSpan.FromSeconds(_pollInterval), cancellationToken: ct);
                    }
                }

                // 딜 타임 (보스가 데미지를 입는 시간)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_recoveryTime), cancellationToken: ct);
            }
        }
    }
}
