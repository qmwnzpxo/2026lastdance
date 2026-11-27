using UnityEngine;
using UnMachine.Core;
using UnMachine.Core.Boss;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace UnMachine.Boss
{
    /// <summary>
    /// 그로기 게이지 어댑터 (T61 ✂️): 받은 피해 × 계수, 돌진 벽 충돌 +25 → 100 이면 BossFlowDriver.EnterGroggy (4초, ×1.5).
    /// Groggy 가 끝나면 0. Groggy·PhaseTransition·Dead 중에는 오르지 않는다. spec: boss.md 7장
    /// </summary>
    /// <remarks>
    /// 1주 축소: 출처 구분(일섬 +10·일반 +1)·낙섬 QTE 없음, CSH 수정 없음. 보스 오브젝트에 BossFlowDriver 와 함께 둔다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BossGroggyDriver : MonoBehaviour
    {
        [SerializeField] private GroggyConfig _config;
        [SerializeField] private BossController _boss;
        [SerializeField] private BossFlowDriver _flowDriver;
        [SerializeField] private BossAttackPatterns _attackPatterns;

        [Header("읽기 전용 표시")]
        [SerializeField] private float _debugValue;
        [SerializeField] private string _debugLastSource;

        private GroggyGauge _gauge;
        private bool _wasGroggy;

        /// <summary>게이지 0~1 (UI 용). 준비 전에는 0.</summary>
        public float Normalized => _gauge != null ? _gauge.Normalized : 0f;

        private void Awake()
        {
            if (_config == null || _boss == null || _flowDriver == null || _attackPatterns == null)
            {
                GameLog.Error(this,
                    "[BossGroggyDriver] Config·BossController·BossFlowDriver·BossAttackPatterns 참조 누락 → 비활성화");
                enabled = false;
                return;
            }

            _gauge = _config.CreateGauge();
        }

        private void OnEnable()
        {
            if (_gauge == null)
            {
                return;
            }

            _boss.Damaged += OnDamaged;
            _boss.BattleStarted += OnBattleStarted;
            _attackPatterns.ChargeHitWall += OnChargeHitWall;
            _flowDriver.FlowStateChanged += OnFlowStateChanged;
        }

        private void OnDisable()
        {
            if (_gauge == null)
            {
                return;
            }

            _boss.Damaged -= OnDamaged;
            _boss.BattleStarted -= OnBattleStarted;
            _attackPatterns.ChargeHitWall -= OnChargeHitWall;
            _flowDriver.FlowStateChanged -= OnFlowStateChanged;
        }

        private void Update()
        {
            if (!_flowDriver.IsRunning)
            {
                return;
            }

            if (_flowDriver.State != BossFlowState.Groggy)
            {
                _gauge.Tick(GameClock.World.DeltaTime);
            }

            // 가득 찼는데 그때 진입 못 했으면 (전환 중 등) 가능해지는 즉시 진입.
            if (_gauge.IsFull && CanGain())
            {
                _flowDriver.EnterGroggy();
            }

#if UNITY_EDITOR
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                Gain(_config.WallHitAmount, "F5");
            }

            _debugValue = _gauge.Value;
#endif
        }

#if UNITY_EDITOR
        /// <summary>(에디터 전용) 테스트 패널에서 게이지를 올린다. 일반 증가와 같은 조건 (Groggy·전환 중 무시).</summary>
        public void DebugGain(float amount)
        {
            Gain(amount, "테스트 패널");
        }

        /// <summary>(에디터 전용) 벽 충돌 1회분.</summary>
        public float DebugWallHitAmount => _config != null ? _config.WallHitAmount : 0f;
#endif

        private void OnBattleStarted()
        {
            _gauge.Reset();
            _wasGroggy = false;
        }

        private void OnDamaged(float rawDamage)
        {
            Gain(rawDamage * _config.DamageToGauge, "피해");
        }

        private void OnChargeHitWall()
        {
            Gain(_config.WallHitAmount, "벽 충돌");
        }

        private void Gain(float amount, string source)
        {
            if (!_flowDriver.IsRunning || !CanGain() || amount <= 0f || float.IsNaN(amount))
            {
                return;
            }

            _gauge.Add(amount);
#if UNITY_EDITOR
            _debugLastSource = source;
#endif
            if (_gauge.IsFull)
            {
                GameLog.Info($"[BossGroggyDriver] 게이지 가득 ({source}) → Groggy", this);
                _flowDriver.EnterGroggy();
            }
        }

        private bool CanGain()
        {
            BossFlowState state = _flowDriver.State;
            return state != BossFlowState.Groggy
                && state != BossFlowState.PhaseTransition
                && state != BossFlowState.Dead;
        }

        private void OnFlowStateChanged(BossFlowState state)
        {
            bool isGroggy = state == BossFlowState.Groggy;
            if (_wasGroggy && !isGroggy)
            {
                _gauge.Reset(); // spec: boss.md 7장 — Groggy 끝나면 0
            }

            _wasGroggy = isGroggy;
        }
    }
}
