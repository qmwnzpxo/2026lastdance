using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnMachine.Core;
using UnMachine.Core.Boss;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace UnMachine.Boss
{
    /// <summary>
    /// BossFlowMachine(Core) 어댑터. BossController 의 useFlowV2 가 On 이면 전투 시작 후 Idle~Resting 루프를 소유한다.
    /// Idle → 다음 패턴 Begin, Attacking → IBossPattern.RunAsync, 끝나면 NotifyAttackEnded.
    /// 상태가 바뀔 때마다 BossController.ApplyFlowState 로 알린다. (BACKLOG T31, Q19 C)
    /// </summary>
    /// <remarks>
    /// 보스 오브젝트에 BossController 와 함께 둔다. 시간은 GameClock World 채널 (씬에 GameClockDriver 필요).
    /// 상태를 바꾸는 호출(Begin·인터럽트)은 이 클래스 한 곳에서만 한다 (BossFlowMachine remarks).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BossFlowDriver : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private BossFlowConfig _config;
        [SerializeField] private BossController _boss;
        [SerializeField] private BossAttackPatterns _attackPatterns;

        [Header("레거시 패턴 고정 순환 (T31 임시 — T38 선택기로 교체, T68 에서 제거)")]
        [Tooltip("위에서부터 순서대로 반복 실행. 반격 틈 = boss.md 6장 대응 패턴 값 (T38 임시 매핑 기준)")]
        [SerializeField] private LegacyPatternEntry[] _legacyCycle =
        {
            new LegacyPatternEntry(AttackPattern.Stomp, 0.4f, 0.9f),           // P3 짓밟기: 틈 0.9
            new LegacyPatternEntry(AttackPattern.MachinegunBurst, 0.4f, 0.6f), // P5 직사 연사: 틈 0.6
            new LegacyPatternEntry(AttackPattern.Howitzer, 0.4f, 0f),          // P4 곡사: 틈 없음 (DECISIONS ⑥)
            new LegacyPatternEntry(AttackPattern.Charge, 0.4f, 0.6f),          // P2 돌진: 명중 시 0.6
        };

        [Tooltip("레거시 패턴이 이 시간(초, Time.deltaTime 기준) 안에 안 끝나면 강제 종료. 기존 BT 노드 타임아웃과 같은 값")]
        [SerializeField, Min(0.1f)] private float _legacyTimeoutSeconds = 10f;

        [Header("디버그 (에디터 전용)")]
        [Tooltip("머리 위 상태 라벨 위치 (보스 기준 월드 오프셋)")]
        [SerializeField] private Vector3 _labelOffset = new Vector3(0f, 3f, 0f);

        [Header("읽기 전용 표시 (매 프레임 덮어씀)")]
        [SerializeField] private BossFlowState _debugState;
        [SerializeField] private float _debugStateRemaining;
        [SerializeField] private int _debugPhase;
        [SerializeField] private float _debugDamageMultiplier;
        [SerializeField] private bool _debugPhaseTransitionPending;
        [SerializeField] private string _debugPattern;
        [SerializeField] private bool _debugWalkActive;
        [SerializeField] private float _debugWalkVelocityX;
        [SerializeField] private float _debugPlayerDistanceX;

        private BossFlowSettings _settings;
        private IBossPattern[] _patterns;
        private BossFlowMachine _machine;
        private BossLocomotion _locomotion;
        private IBossPattern _currentPattern;
        private CancellationTokenSource _patternCts;
        private int _cycleIndex;
        private bool _isRunning;

        /// <summary>참조·설정이 정상이고 컴포넌트가 켜져 있어 루프를 맡을 수 있는지. BossController.Start 에서 확인한다.</summary>
        public bool IsReady => isActiveAndEnabled && _patterns != null;

        /// <summary>현재 받는 피해 배율 (boss.md 4장). 루프 시작 전에는 1.</summary>
        public float DamageMultiplier => _machine != null ? _machine.DamageMultiplier : 1f;

        private void Awake()
        {
            if (_config == null || _boss == null || _attackPatterns == null)
            {
                GameLog.Error(this, "[BossFlowDriver] Config·BossController·BossAttackPatterns 참조 누락 → 비활성화");
                enabled = false;
                return;
            }

            if (_legacyCycle == null || _legacyCycle.Length == 0)
            {
                GameLog.Error(this, "[BossFlowDriver] 레거시 패턴 순환 목록이 비어 있음 → 비활성화");
                enabled = false;
                return;
            }

            if (!_boss.TryGetComponent(out Rigidbody2D body))
            {
                GameLog.Error(this, "[BossFlowDriver] 보스 오브젝트에 Rigidbody2D 없음 → 비활성화");
                enabled = false;
                return;
            }

            _settings = _config.ToSettings();
            _locomotion = new BossLocomotion(body, _boss, _config.RestWalkSpeed, _config.RestWalkStopDistance);
            _patterns = BuildLegacyPatterns();
        }

        private void OnEnable()
        {
            if (_boss != null)
            {
                _boss.Died += OnBossDied;
            }
        }

        private void OnDisable()
        {
            if (_boss != null)
            {
                _boss.Died -= OnBossDied;
            }

            CancelPattern();
            StopWalking();
        }

        private void OnDestroy()
        {
            ReleaseMachine();
        }

        /// <summary>
        /// 루프를 (다시) 시작한다: 실행 중 패턴 취소 → 새 상태 머신(지정 페이즈) → Idle → 다음 패턴 Begin.
        /// 전투 시작과 페이즈 전환 직후(T33 전까지 임시) BossController 가 호출한다.
        /// </summary>
        /// <param name="phase">1 또는 2.</param>
        public void StartLoop(int phase)
        {
            if (!IsReady)
            {
                GameLog.Error(this, "[BossFlowDriver] 준비되지 않은 상태에서 StartLoop 호출 → 무시");
                return;
            }

            CancelPattern();
            StopWalking();
            ReleaseMachine();

            _machine = new BossFlowMachine(_settings);
            _machine.StateChanged += OnStateChanged;
            _machine.SetPhase(phase);
            _isRunning = true;

            GameLog.Info($"[BossFlowDriver] 루프 시작 (페이즈 {phase})", this);
            _boss.ApplyFlowState(BossState.Idle);
            BeginNextPattern();
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            _machine.Tick(GameClock.World.DeltaTime);
            _locomotion.Tick(GetWorldRate());

#if UNITY_EDITOR
            HandleDebugInput();
            UpdateDebugView();
#endif
        }

        private void FixedUpdate()
        {
            // spec: boss.md 5장 Resting 걷기. 물리 속도는 FixedUpdate 에서만 반영 (AI_RULES 2장).
            if (_isRunning)
            {
                _locomotion.ApplyFixed();
            }
        }

        /// <summary>
        /// World 채널 진행 비율: 히트스톱 중 0, 평소 World 배율(적 배율 × 일시정지).
        /// 걷기 속도에 곱해 World dt 와 같은 비율로 움직이게 한다.
        /// </summary>
        private static float GetWorldRate()
        {
            return GameClock.IsHitStopped ? 0f : GameClock.World.Scale;
        }

        private IBossPattern[] BuildLegacyPatterns()
        {
            var patterns = new IBossPattern[_legacyCycle.Length];
            for (int i = 0; i < _legacyCycle.Length; i++)
            {
                LegacyPatternEntry entry = _legacyCycle[i];
                var timing = new PatternTiming(entry.TelegraphSeconds, entry.PunishableSeconds);
                patterns[i] = new LegacyPatternBridge(_attackPatterns, entry.Pattern, timing, _legacyTimeoutSeconds);
            }

            return patterns;
        }

        private void OnStateChanged(BossFlowState previous, BossFlowState current)
        {
            // Attacking 을 벗어나면 (정상 종료든 인터럽트든) 패턴 CTS 정리.
            // 인터럽트로 끊긴 패턴은 취소되어 NotifyAttackEnded 를 보내지 않는다 (T30 리스크 대응).
            if (previous == BossFlowState.Attacking)
            {
                CancelPattern();
            }

            // Resting 을 벗어나면 (정상 종료든 Stunned·Groggy·Dead 인터럽트든) 걷기를 멈춘다.
            if (previous == BossFlowState.Resting)
            {
                StopWalking();
            }

            // Dead 는 BossController 가 이미 처리했다 (Died 이벤트로 여기 들어옴).
            if (current == BossFlowState.Dead)
            {
                _isRunning = false;
                return;
            }

            // 먼저 반영 → 아래에서 중첩으로 바뀌는 다음 상태가 마지막에 덮어쓴다.
            _boss.ApplyFlowState(ToBossState(current));

            switch (current)
            {
                case BossFlowState.Idle:
                    BeginNextPattern();
                    break;
                case BossFlowState.Attacking:
                    StartCurrentPattern();
                    break;
                case BossFlowState.Resting:
                    _locomotion.Begin();
                    break;
            }
        }

        private void BeginNextPattern()
        {
            _currentPattern = _patterns[_cycleIndex];
            _cycleIndex = (_cycleIndex + 1) % _patterns.Length;
            _machine.Begin(_currentPattern.Timing);
        }

        private void StartCurrentPattern()
        {
            CancelPattern();
            _patternCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunPatternAsync(_currentPattern, _patternCts.Token).Forget();
        }

        private async UniTaskVoid RunPatternAsync(IBossPattern pattern, CancellationToken ct)
        {
            (bool isCanceled, PatternResult result) =
                await pattern.RunAsync(_boss, ct).SuppressCancellationThrow();

            // 취소됐거나, 끝나는 순간 인터럽트가 겹쳤으면 알리지 않는다.
            if (isCanceled || ct.IsCancellationRequested)
            {
                return;
            }

            _machine.NotifyAttackEnded(result);
        }

        private void OnBossDied()
        {
            if (_machine == null)
            {
                return;
            }

            // spec: boss.md 5장 Dead 최우선 — 루프 즉시 중단. 패턴 CTS 는 StateChanged(Attacking → Dead)에서도 취소.
            _machine.EnterDead();
            CancelPattern();
            _isRunning = false;
        }

        private void CancelPattern()
        {
            if (_patternCts == null)
            {
                return;
            }

            // Cancel 중 콜백(LegacyPatternBridge 정리)이 다시 이 메서드에 들어와도 안전하게 먼저 비운다.
            CancellationTokenSource cts = _patternCts;
            _patternCts = null;
            cts.Cancel();
            cts.Dispose();
        }

        private void StopWalking()
        {
            if (_locomotion != null)
            {
                _locomotion.Stop();
            }
        }

        private void ReleaseMachine()
        {
            if (_machine == null)
            {
                return;
            }

            _machine.StateChanged -= OnStateChanged;
            _machine = null;
            _isRunning = false;
        }

        private static BossState ToBossState(BossFlowState state)
        {
            switch (state)
            {
                case BossFlowState.Idle: return BossState.Idle;
                case BossFlowState.Telegraph: return BossState.Telegraph;
                case BossFlowState.Attacking: return BossState.Attacking;
                case BossFlowState.Punishable: return BossState.Punishable;
                case BossFlowState.Resting: return BossState.Resting;
                case BossFlowState.Stunned: return BossState.Stunned;
                case BossFlowState.Groggy: return BossState.Groggy;
                case BossFlowState.PhaseTransition: return BossState.PhaseTransition;
                case BossFlowState.Dead: return BossState.Dead;
                default: throw new ArgumentOutOfRangeException(nameof(state), state, "매핑 없는 상태");
            }
        }

#if UNITY_EDITOR
        private const float LabelWidth = 240f;
        private const float LabelHeight = 90f;

        private GUIStyle _labelStyle;

        private void HandleDebugInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                GameLog.Info($"[Debug] F3: EnterStunned → {_machine.EnterStunned()} (상태 {_machine.State})", this);
            }

            if (keyboard.f4Key.wasPressedThisFrame)
            {
                GameLog.Info($"[Debug] F4: EnterGroggy → {_machine.EnterGroggy()} (상태 {_machine.State})", this);
            }
        }

        private void UpdateDebugView()
        {
            _debugState = _machine.State;
            _debugStateRemaining = _machine.StateRemaining;
            _debugPhase = _machine.Phase;
            _debugDamageMultiplier = _machine.DamageMultiplier;
            _debugPhaseTransitionPending = _machine.IsPhaseTransitionPending;
            _debugPattern = _currentPattern != null ? _currentPattern.DebugName : string.Empty;
            _debugWalkActive = _locomotion.IsActive;
            _debugWalkVelocityX = _locomotion.TargetVelocityX;
            _debugPlayerDistanceX = _locomotion.DistanceX;
        }

        private void OnGUI()
        {
            if (_machine == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(transform.position + _labelOffset);
            if (screen.z < 0f)
            {
                return;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerCenter,
                    fontStyle = FontStyle.Bold,
                };
            }

            // GUI 좌표는 y 가 위에서 아래로 → 화면 높이에서 뺀다. 라벨 아래 끝이 오프셋 위치에 오게.
            var rect = new Rect(
                screen.x - LabelWidth * 0.5f,
                Screen.height - screen.y - LabelHeight,
                LabelWidth,
                LabelHeight);
            string walk = _debugWalkActive // guard-allow: 에디터 전용 디버그 라벨
                ? $"\nWalk vx {_debugWalkVelocityX:F1} dx {_debugPlayerDistanceX:F1}"
                : string.Empty;
            string text = $"{_machine.State} {_machine.StateRemaining:F2}s\n" + // guard-allow: 에디터 전용 디버그 라벨
                          $"P{_machine.Phase} ×{_machine.DamageMultiplier:F1}\n{_debugPattern}{walk}";
            GUI.Label(rect, text, _labelStyle);
        }
#endif

        /// <summary>인스펙터에서 편집하는 레거시 패턴 순환 항목.</summary>
        [Serializable]
        private struct LegacyPatternEntry
        {
            [SerializeField] private AttackPattern _pattern;

            [Tooltip("예고(초). 최소 예고(Config)보다 짧으면 최소값 적용")]
            [SerializeField, Min(0f)] private float _telegraphSeconds;

            [Tooltip("반격 틈(초, 1페이즈 기준). 0 = Punishable 건너뜀")]
            [SerializeField, Min(0f)] private float _punishableSeconds;

            public LegacyPatternEntry(AttackPattern pattern, float telegraphSeconds, float punishableSeconds)
            {
                _pattern = pattern;
                _telegraphSeconds = telegraphSeconds;
                _punishableSeconds = punishableSeconds;
            }

            public AttackPattern Pattern => _pattern;

            public float TelegraphSeconds => _telegraphSeconds;

            public float PunishableSeconds => _punishableSeconds;
        }
    }
}
