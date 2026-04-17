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
            // [변경] 대기 루프 삭제! 일단 실행되면 바로 방어막부터 켭니다.
            using (var invHandle = ctx.DamageHandler.CreateInvincibilityHandle())
            {
                // 방어막 VFX 생성
                GameObject vfx = null;
                if (_shieldVfxPrefab != null)
                    vfx = Object.Instantiate(_shieldVfxPrefab, ctx.BossTransform);

                // 코어가 다 부서질 때까지 대기
                while (!ct.IsCancellationRequested)
                {
                    // 코어가 생성되었고(Total > 0), 그 코어들이 다 파괴되었다면(Destroyed >= Total)
                    if (ctx.TotalCoreCount > 0 && ctx.DestroyedCoreCount >= ctx.TotalCoreCount)
                    {
                        break; // 루프 탈출 -> using 종료 -> 무적 해제
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                if (vfx != null) Object.Destroy(vfx);
            }

            // 딜 타임 후 패턴 종료 (컨트롤러가 다시 이 패턴을 실행할 것입니다)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_recoveryTime), cancellationToken: ct);
        }
    }
}
