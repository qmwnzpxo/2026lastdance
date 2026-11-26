using System;
using System.Collections.Generic;
using System.Text;
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
    /// Idle → PatternSelector 로 다음 단위 선택 → PatternRegistry 의 패턴 Begin, Attacking → IBossPattern.RunAsync,
    /// 끝나면 NotifyAttackEnded. 상태가 바뀔 때마다 BossController.ApplyFlowState 로 알린다. (BACKLOG T31·T38)
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
        [SerializeField] private PatternTableConfig _patternTable;

        [Tooltip("P1 점프 내려찍기 (T46). 지정하면 P1 은 아래 목록 대신 JumpSlamPattern. 비우면 목록의 임시 레거시")]
        [SerializeField] private JumpSlamConfig _jumpSlam;

        [Header("단위 → 패턴 (T38: P2~P6 레거시 영구 / P1 = Jump Slam 지정 시 새 패턴 / C1~C4 = 콤보, T56)")]
        [Tooltip("예고·반격 틈 = boss.md 6장 패턴 값. 표에 있어도 여기 없는 단위는 선택되지 않는다")]
        [SerializeField] private UnitPatternEntry[] _unitPatterns =
        {
            new UnitPatternEntry(SelectionUnit.P1, AttackPattern.Stomp, 0.5f, 1.2f),           // 임시: P1 미구현
            new UnitPatternEntry(SelectionUnit.P2, AttackPattern.Charge, 0.6f, 0.6f),
            new UnitPatternEntry(SelectionUnit.P3, AttackPattern.Stomp, 0.45f, 0.9f),
            new UnitPatternEntry(SelectionUnit.P4, AttackPattern.Howitzer, 0.4f, 0f),          // 틈 없음 (Q24)
            new UnitPatternEntry(SelectionUnit.P5, AttackPattern.MachinegunBurst, 0.4f, 0.6f),
            new UnitPatternEntry(SelectionUnit.P6, AttackPattern.DroneSpawn, 0.4f, 0.5f),
            // 콤보 (T56): 패턴 칸은 안 씀 (구성 = ComboPlan). 예고 = 첫 단계, 틈 = 마지막 단계 뒤
            new UnitPatternEntry(SelectionUnit.C1, AttackPattern.Charge, 0.6f, 0.9f),          // 끝 = P3 틈
            new UnitPatternEntry(SelectionUnit.C2, AttackPattern.Stomp, 0.5f, 0.6f),           // 끝 = P5 틈
            new UnitPatternEntry(SelectionUnit.C3, AttackPattern.Stomp, 0.5f, 1.8f),           // 마지막 착지만 긴 틈 〔임시값〕
            new UnitPatternEntry(SelectionUnit.C4, AttackPattern.Howitzer, 0.4f, 0.6f),        // 끝 = P2 틈
        };

        [Tooltip("레거시 패턴이 이 시간(초, Time.deltaTime 기준) 안에 안 끝나면 강제 종료. 기존 BT 노드 타임아웃과 같은 값")]
        [SerializeField, Min(0.1f)] private float _legacyTimeoutSeconds = 10f;

        [Tooltip("활성 드론이 이 수 이상이면 P6 사용 불가 → 선택기가 P5 로 대체. spec: boss.md 4장 드론 최대 1")]
        [SerializeField, Min(1)] private int _maxActiveDrones = 1;

        [Header("난수")]
        [Tooltip("켜면 전투 시작마다 같은 시드 → 같은 입력이면 같은 선택 순서 (재현 테스트용)")]
        [SerializeField] private bool _useFixedSeed;
        [SerializeField] private int _seed = 12345;

        [Header("디버그 (에디터 전용)")]
        [Tooltip("머리 위 상태 라벨 위치 (보스 기준 월드 오프셋)")]
        [SerializeField] private Vector3 _labelOffset = new Vector3(0f, 3f, 0f);

        [Tooltip("켜면 선택기를 건너뛰고 아래 단위만 반복 (반복 방지 무시, 패턴별 테스트용). 에디터에서만 적용")]
        [SerializeField] private bool _forceUnit;
        [SerializeField] private SelectionUnit _forcedUnit;

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
        [SerializeField] private DistanceBand _debugBand;
        [SerializeField] private float _debugDwellSeconds;
        [SerializeField] private string _debugCandidates;
        [SerializeField] private string _debugRecent;
        [SerializeField] private JumpSlamPhase _debugJumpSlamPhase;

        private BossFlowSettings _settings;
        private PatternRegistry _registry;
        private PatternTable _table;
        private DistanceBandTracker _bandTracker;
        private PatternSelector _selector;
        private Func<SelectionUnit, bool> _isUnitAvailable;
        private BossFlowMachine _machine;
        private BossLocomotion _locomotion;
        private IBossPattern _currentPattern;
        private JumpSlamPattern _jumpSlamPattern;
        private IBossPattern _fallbackPattern;
        private SelectionUnit _fallbackUnit;
        private SelectionUnit _currentUnit;
        private CancellationTokenSource _patternCts;
        private bool _isRunning;

        /// <summary>참조·설정이 정상이고 컴포넌트가 켜져 있어 루프를 맡을 수 있는지. BossController.Start 에서 확인한다.</summary>
        public bool IsReady => isActiveAndEnabled && _registry != null;

        /// <summary>흐름 상태가 바뀔 때 (BossController 반영 직후). 연출 View 가 구독한다 (T39).</summary>
        public event Action<BossFlowState> FlowStateChanged;

        /// <summary>현재 흐름 상태. 루프 전에는 Idle.</summary>
        public BossFlowState State => _machine != null ? _machine.State : BossFlowState.Idle;

        /// <summary>루프가 돌고 있는지 (전투 중, Dead 전).</summary>
        public bool IsRunning => _isRunning;

        /// <summary>그로기 게이지가 가득 찼을 때 BossGroggyDriver 가 호출 (T61). Dead·전환·Groggy 중이면 false.</summary>
        public bool EnterGroggy()
        {
            if (!_isRunning)
            {
                return false;
            }

            bool isEntered = _machine.EnterGroggy();
            GameLog.Info($"[BossFlowDriver] EnterGroggy → {isEntered} (상태 {_machine.State})", this);
            return isEntered;
        }

        /// <summary>지금(또는 마지막으로) 실행하는 선택 단위. 예고 색 결정용.</summary>
        public SelectionUnit CurrentUnit => _currentUnit;

        /// <summary>현재 단위가 콤보(C1~C4)인지. 콤보 시작 신호용 (T58).</summary>
        public bool IsCurrentCombo => ComboPlan.TryGet(_currentUnit, out _);

        /// <summary>현재 예고 앞부분의 콤보 신호 구간(초). 단독 패턴이면 0. 이 구간에는 눈 색을 켜지 않는다 (T58).</summary>
        public float CurrentSignalLead => IsCurrentCombo && _config != null ? _config.ComboSignalLeadSeconds : 0f;

        /// <summary>현재 상태 남은 시간(초, World 채널). 루프 전에는 0.</summary>
        public float StateRemaining => _machine != null ? _machine.StateRemaining : 0f;

        /// <summary>현재 받는 피해 배율 (boss.md 4장). 루프 시작 전에는 1.</summary>
        public float DamageMultiplier => _machine != null ? _machine.DamageMultiplier : 1f;

        private void Awake()
        {
            if (_config == null || _boss == null || _attackPatterns == null || _patternTable == null)
            {
                GameLog.Error(this,
                    "[BossFlowDriver] Config·BossController·BossAttackPatterns·PatternTableConfig 참조 누락 → 비활성화");
                enabled = false;
                return;
            }

            if (_unitPatterns == null || _unitPatterns.Length == 0)
            {
                GameLog.Error(this, "[BossFlowDriver] 단위 → 패턴 목록이 비어 있음 → 비활성화");
                enabled = false;
                return;
            }

            try
            {
                _table = _patternTable.ToTable();
                _bandTracker = _patternTable.CreateDistanceBandTracker();
            }
            catch (ArgumentException e)
            {
                GameLog.Error(this, $"[BossFlowDriver] PatternTableConfig 값 오류 → 비활성화: {e.Message}");
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
            _isUnitAvailable = IsUnitAvailable; // 대리자 1회 생성 (선택기는 생성 시 한 번 받음)

            PatternRegistry registry = BuildRegistry(body);
            if (registry == null)
            {
                enabled = false;
                return;
            }

            _registry = registry; // IsReady 는 여기서 true
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
            if (_jumpSlamPattern != null)
            {
                _jumpSlamPattern.Dispose();
                _jumpSlamPattern = null;
            }
        }

        /// <summary>
        /// 루프를 (다시) 시작한다: 실행 중 패턴 취소 → 새 상태 머신(지정 페이즈) → Idle → 다음 패턴 Begin.
        /// 전투 시작 때 BossController 가 호출한다. 페이즈 전환은 <see cref="RequestPhaseTransition"/> (T33).
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
            // SetPhase 뒤에 구독 → 시작 페이즈는 다시 알리지 않는다 (BossController 가 이미 알고 있음).
            _machine.PhaseChanged += OnPhaseChanged;

            // 새 전투 = 새 선택 기록. 고정 시드면 매번 같은 난수열 → 같은 입력이면 같은 선택 순서 (T37 완료 기준).
            IRandom random = _useFixedSeed ? new SystemRandom(_seed) : new SystemRandom();
            _selector = new PatternSelector(_table, random, _isUnitAvailable);
            _bandTracker.Reset();
            _isRunning = true;

            GameLog.Info($"[BossFlowDriver] 루프 시작 (페이즈 {phase})", this);
            _boss.ApplyFlowState(BossState.Idle);
            BeginNextPattern();
        }

        /// <summary>
        /// 페이즈 전환을 요청한다 (HP 50% 최초 하회, 디버그 F1). Groggy 중이면 끝난 뒤로 보류된다.
        /// spec: boss.md 5장·8장 전환 1
        /// </summary>
        /// <returns>받아들여졌으면(즉시 진입 또는 보류) true. 루프 전·이미 요청함·Dead 면 false.</returns>
        public bool RequestPhaseTransition()
        {
            if (!_isRunning)
            {
                GameLog.Warn("[BossFlowDriver] 루프가 돌지 않을 때 RequestPhaseTransition 호출 → 무시", this);
                return false;
            }

            bool isAccepted = _machine.RequestPhaseTransition();
            GameLog.Info(
                $"[BossFlowDriver] 페이즈 전환 요청 → {isAccepted} " +
                $"(상태 {_machine.State}, 보류 {_machine.IsPhaseTransitionPending})",
                this);
            return isAccepted;
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            float dt = GameClock.World.DeltaTime;
            UpdateDistanceBand(dt); // Tick 안에서 Idle → 다음 선택이 일어나므로 먼저 갱신
            _machine.Tick(dt);
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

        /// <summary>인스펙터 목록으로 단위 → 패턴 등록. 단위 중복이면 null (호출부가 비활성화).</summary>
        private PatternRegistry BuildRegistry(Rigidbody2D body)
        {
            var registry = new PatternRegistry();
            if (_jumpSlam != null)
            {
                // T46: P1 = 새 패턴. 목록의 P1(임시 레거시)은 건너뛴다.
                Animator animator = _boss.GetComponentInChildren<Animator>(true);
                _jumpSlamPattern = new JumpSlamPattern(_jumpSlam, _boss, body, animator);
                registry.Register(SelectionUnit.P1, _jumpSlamPattern);
            }

            for (int i = 0; i < _unitPatterns.Length; i++)
            {
                UnitPatternEntry entry = _unitPatterns[i];
                if (entry.Unit == SelectionUnit.P1 && _jumpSlamPattern != null)
                {
                    continue;
                }

                if (ComboPlan.TryGet(entry.Unit, out _))
                {
                    continue; // 콤보는 단독 패턴을 다 등록한 뒤 (단계 패턴이 필요)
                }

                var timing = new PatternTiming(entry.TelegraphSeconds, entry.PunishableSeconds);
                var pattern = new LegacyPatternBridge(_attackPatterns, entry.Pattern, timing, _legacyTimeoutSeconds);

                if (registry.Contains(entry.Unit))
                {
                    GameLog.Error(this, $"[BossFlowDriver] 단위 {entry.Unit} 가 목록에 두 번 있음 → 비활성화");
                    return null;
                }

                registry.Register(entry.Unit, pattern);
                if (_fallbackPattern == null || entry.Unit == SelectionUnit.P5)
                {
                    _fallbackPattern = pattern; // 선택 실패 시 대신 쓸 패턴: P5, 없으면 첫 항목
                    _fallbackUnit = entry.Unit;
                }
            }

            return RegisterCombos(registry) ? registry : null;
        }

        /// <summary>목록의 C1~C4 → ComboPattern (T56). 단계 패턴이 없으면 그 콤보만 건너뛴다. 중복이면 false.</summary>
        private bool RegisterCombos(PatternRegistry registry)
        {
            for (int i = 0; i < _unitPatterns.Length; i++)
            {
                UnitPatternEntry entry = _unitPatterns[i];
                if (!ComboPlan.TryGet(entry.Unit, out ComboPlan plan))
                {
                    continue;
                }

                if (registry.Contains(entry.Unit))
                {
                    GameLog.Error(this, $"[BossFlowDriver] 단위 {entry.Unit} 가 목록에 두 번 있음 → 비활성화");
                    return false;
                }

                IBossPattern[] steps = BuildComboSteps(plan, registry);
                if (steps == null)
                {
                    GameLog.Warn($"[BossFlowDriver] 콤보 {entry.Unit} 단계 패턴 누락 → 등록 안 함 (선택 안 됨)", this);
                    continue;
                }

                // T58: 예고 앞에 증기·경고음 구간을 붙인다 (그 동안 눈 색 없음 → 신호가 먼저)
                var timing = new PatternTiming(
                    entry.TelegraphSeconds + _config.ComboSignalLeadSeconds, entry.PunishableSeconds);
                registry.Register(entry.Unit, new ComboPattern(plan, steps, timing));
            }

            return true;
        }

        /// <summary>
        /// 콤보 단계 패턴 목록. 첫 단계가 레거시면 C1 = 돌진 Brake, C4 = 곡사 발사만 옵션을 켠 다리로 바꾼다 (T57).
        /// 단계가 하나라도 등록되지 않았으면 null.
        /// </summary>
        private static IBossPattern[] BuildComboSteps(ComboPlan plan, PatternRegistry registry)
        {
            var steps = new IBossPattern[plan.Steps.Count];
            for (int s = 0; s < steps.Length; s++)
            {
                if (!registry.TryGet(plan.Steps[s], out steps[s]))
                {
                    return null;
                }
            }

            if (steps[0] is LegacyPatternBridge first)
            {
                bool isC1 = plan.Combo == SelectionUnit.C1; // spec: boss.md 8장 C1 지나치고 0.3초 뒤 Brake
                bool isC4 = plan.Combo == SelectionUnit.C4; // spec: boss.md 8장 C4 P4 발사 직후 P2
                if (isC1 || isC4)
                {
                    steps[0] = first.WithComboOptions(chargeBrakeAfterPass: isC1, howitzerDetachFall: isC4);
                }
            }

            return steps;
        }

        /// <summary>선택기용 사용 가능 판정: 등록된 단위만, P6 은 활성 드론이 상한 미만일 때만. spec: boss.md 6장 P6</summary>
        private bool IsUnitAvailable(SelectionUnit unit)
        {
            if (!_registry.Contains(unit))
            {
                return false;
            }

            return unit != SelectionUnit.P6 || _boss.GetActiveDroneCount() < _maxActiveDrones;
        }

        /// <summary>플레이어와의 x 거리로 구간·체류 시간 갱신. 플레이어가 없으면 그대로 둔다.</summary>
        private void UpdateDistanceBand(float dt)
        {
            Transform player = _boss.GetPlayer();
            if (player == null)
            {
                return;
            }

            _bandTracker.Update(Mathf.Abs(player.position.x - _boss.transform.position.x), dt);
        }

        /// <summary>다음 단위를 고른다. 강제 선택(에디터) → 선택기. 고를 수 없으면 false.</summary>
        private bool TrySelectNextUnit(out SelectionUnit unit)
        {
#if UNITY_EDITOR
            if (_forceUnit)
            {
                unit = _forcedUnit; // 반복 방지·기록 무시
                return true;
            }
#endif
            if (!_bandTracker.HasBand)
            {
                UpdateDistanceBand(0f); // 전투 첫 선택은 Update 전에 일어남
            }

            // 플레이어를 못 찾으면 중거리·체류 0 으로 고른다.
            DistanceBand band = _bandTracker.HasBand ? _bandTracker.Band : DistanceBand.Mid;
            float dwell = _bandTracker.HasBand ? _bandTracker.DwellSeconds : 0f;
            return _selector.TrySelect(_machine.Phase, band, dwell, out unit);
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
                ClearShockwaves();
                _isRunning = false;
                FlowStateChanged?.Invoke(current);
                return;
            }

            // 먼저 반영 → 아래에서 중첩으로 바뀌는 다음 상태가 마지막에 덮어쓴다.
            _boss.ApplyFlowState(ToBossState(current));
            FlowStateChanged?.Invoke(current);

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
                case BossFlowState.PhaseTransition:
                    // Telegraph 에서 끊기면 위 Attacking 정리를 거치지 않으므로 여기서도 취소 (중복 호출 안전).
                    CancelPattern();
                    ClearShockwaves(); // spec: boss.md 8장 "화면의 탄 제거"
                    _boss.EnterFlowPhaseTransition();
                    break;
            }
        }

        /// <summary>PhaseTransition 이 끝나 페이즈가 바뀌면 BossController 에 알린다 (Idle 진입 직전에 호출됨).</summary>
        private void OnPhaseChanged(int phase)
        {
            _boss.ApplyFlowPhase(phase);
        }

        private void BeginNextPattern()
        {
            if (TrySelectNextUnit(out SelectionUnit unit) && _registry.TryGet(unit, out IBossPattern pattern))
            {
                _currentUnit = unit;
                _currentPattern = pattern;
            }
            else
            {
                // 강제 단위가 목록에 없거나, 전부 사용 불가일 때. 보스가 멈추지 않게 대체 패턴.
                GameLog.Warn($"[BossFlowDriver] 단위 선택 실패 → {_fallbackPattern.DebugName} 로 대체", this);
                _currentPattern = _fallbackPattern;
                _currentUnit = _fallbackUnit;
            }

#if UNITY_EDITOR
            RecordSelectionDebug();
#endif
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

        private void ClearShockwaves()
        {
            if (_jumpSlamPattern != null)
            {
                _jumpSlamPattern.ClearShockwaves();
            }
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
            _machine.PhaseChanged -= OnPhaseChanged;
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
        private const float LabelWidth = 360f;
        private const float LabelHeight = 110f;

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
            _debugBand = _bandTracker.Band;
            _debugDwellSeconds = _bandTracker.DwellSeconds;
            _debugJumpSlamPhase = _jumpSlamPattern != null ? _jumpSlamPattern.Phase : JumpSlamPhase.Idle;
        }

        /// <summary>선택 직후 1회: 후보 가중치(체류 보정·대체 반영)와 최근 2개를 문자열로 남긴다.</summary>
        private void RecordSelectionDebug()
        {
            _debugPattern = $"{_currentUnit} {_currentPattern.DebugName}"; // guard-allow: 에디터 전용, 패턴당 1회

            if (_forceUnit)
            {
                _debugCandidates = "(강제 선택)";
                return;
            }

            var builder = new StringBuilder();
            IReadOnlyList<PatternWeight> candidates = _selector.LastCandidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                builder.Append(candidates[i].Unit).Append(':').Append(candidates[i].Weight.ToString("0.#")).Append(' ');
            }

            if (_selector.LastDwellBonusApplied)
            {
                builder.Append("[체류×]");
            }

            if (_selector.LastWasSubstituted)
            {
                builder.Append("[대체 ").Append(_selector.LastSourceUnit).Append(']');
            }

            if (_selector.LastRepeatBanRelaxed)
            {
                builder.Append("[반복허용]");
            }

            _debugCandidates = builder.ToString();
            _debugRecent = _selector.HistoryCount >= 2
                ? $"{_selector.PreviousSelected} → {_selector.LastSelected}" // guard-allow: 에디터 전용
                : _selector.LastSelected.ToString();
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
                          $"P{_machine.Phase} ×{_machine.DamageMultiplier:F1}\n{_debugPattern}{walk}\n" +
                          $"{_debugBand} {_debugDwellSeconds:F1}s | {_debugCandidates}";
            GUI.Label(rect, text, _labelStyle);
        }
#endif

        /// <summary>인스펙터에서 편집하는 단위 → 레거시 패턴 항목.</summary>
        [Serializable]
        private struct UnitPatternEntry
        {
            [SerializeField] private SelectionUnit _unit;
            [SerializeField] private AttackPattern _pattern;

            [Tooltip("예고(초). 최소 예고(Config)보다 짧으면 최소값 적용")]
            [SerializeField, Min(0f)] private float _telegraphSeconds;

            [Tooltip("반격 틈(초, 1페이즈 기준). 0 = Punishable 건너뜀")]
            [SerializeField, Min(0f)] private float _punishableSeconds;

            public UnitPatternEntry(
                SelectionUnit unit, AttackPattern pattern, float telegraphSeconds, float punishableSeconds)
            {
                _unit = unit;
                _pattern = pattern;
                _telegraphSeconds = telegraphSeconds;
                _punishableSeconds = punishableSeconds;
            }

            public SelectionUnit Unit => _unit;

            public AttackPattern Pattern => _pattern;

            public float TelegraphSeconds => _telegraphSeconds;

            public float PunishableSeconds => _punishableSeconds;
        }
    }
}
