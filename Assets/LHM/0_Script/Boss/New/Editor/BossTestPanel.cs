using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnMachine.Boss;
using UnMachine.Core.Boss;
using Object = UnityEngine.Object;

namespace UnMachine.EditorTools
{
    /// <summary>
    /// 보스 v2 씬 테스트 패널 (Play 중 사용). 조작 버튼 + 실시간 상태 + Console 로그·상태를 보고 자동 판정하는 체크리스트
    /// + 자동 순회(P1 → 2P → C1~C4 → 사망) + 보고서 복사. HANDOFF "확인할 것" 항목을 한 창에서 확인하기 위한 도구.
    /// </summary>
    /// <remarks>
    /// 씬·프리팹·에셋은 수정하지 않는다 (Play 중 런타임 객체만 조작). 판정은 GameLog 문구에 기대므로 문구를 바꾸면 여기도 맞출 것.
    /// 플레이어 조작(회피·점프)은 사람이 한다 — 자동 순회 중 플레이어가 죽으면 그 단계는 타임아웃된다.
    /// </remarks>
    public sealed class BossTestPanel : EditorWindow
    {
        private const string MenuPath = "UnMachine/Boss Test Panel";
        private const float FindInterval = 1f;
        private const float CountInterval = 0.25f;
        private const float RepaintInterval = 0.2f;
        private const float TransitionCheckDelay = 0.6f; // 전환 진입 후 회수가 끝났는지 볼 시점
        private const float DeathCheckDelay = 1.5f;
        private const float StepTimeout = 30f;
        private const float Phase2HpRatio = 0.49f;

        private enum CheckId
        {
            P1Landing,
            P1Shockwave,
            P1Offscreen,
            Groggy,
            Transition,
            TransitionClear,
            C1,
            C2,
            C3,
            C4,
            ComboSignal,
            DeathClear,
            NoErrors,
        }

        private enum CheckState
        {
            Pending,
            Pass,
            Fail,
        }

        private enum StepKind
        {
            ForceP1,
            Groggy,
            EnsurePhase2,
            ForceCombo,
            Kill,
        }

        private static readonly string[] CheckLabels =
        {
            "T46 P1 점프: 도약→추적→고정→낙하→착지",
            "T46 충격파 적중 기록 (서 있으면 4)",
            "T46 화면 밖(추적·고정) 동안 피해 0",
            "T61 그로기 진입",
            "T33·T34 2P 전환 + 붉은 틴트 On",
            "T23 전환 직후 남은 탄 0 (드론 제외)",
            "T56·T57 C1 돌진→짓밟기 끝",
            "T56 C2 착지→직사 끝",
            "T56 C3 3연속 점프 끝",
            "T57 C4 곡사(발사만)→돌진 끝",
            "T58 콤보 신호가 단계 1보다 먼저",
            "T23 사망 후 남은 탄·드론·충격파 0",
            "Console 에러·예외 0",
        };

        private static readonly (StepKind Kind, SelectionUnit Unit)[] Sequence =
        {
            (StepKind.ForceP1, SelectionUnit.P1),
            (StepKind.Groggy, SelectionUnit.P1),
            (StepKind.EnsurePhase2, SelectionUnit.P1),
            (StepKind.ForceCombo, SelectionUnit.C1),
            (StepKind.ForceCombo, SelectionUnit.C2),
            (StepKind.ForceCombo, SelectionUnit.C3),
            (StepKind.ForceCombo, SelectionUnit.C4),
            (StepKind.Kill, SelectionUnit.P1),
        };

        private static readonly SelectionUnit[] AllUnits =
        {
            SelectionUnit.P1, SelectionUnit.P2, SelectionUnit.P3, SelectionUnit.P4, SelectionUnit.P5,
            SelectionUnit.P6, SelectionUnit.C1, SelectionUnit.C2, SelectionUnit.C3, SelectionUnit.C4,
        };

        // 참조 (Play 중 자동 탐색)
        private BossController _boss;
        private BossFlowDriver _driver;
        private BossGroggyDriver _groggy;
        private double _nextFindTime;

        // 체크 결과
        private readonly CheckState[] _states = new CheckState[CheckLabels.Length];
        private readonly string[] _details = new string[CheckLabels.Length];

        // 로그 집계
        private int _landings;
        private int _shockwaveHits;
        private int _offscreenDamage;
        private int _groggyEntries;
        private bool _isTintOn;
        private bool _hasTransitionEntered;
        private readonly int[] _comboEnds = new int[4];
        private readonly string[] _comboReasons = new string[4];
        private int _signalOk;
        private int _signalMissing;
        private bool _hasLastSignal;
        private SelectionUnit _lastSignalUnit;
        private int _errors;
        private string _firstError;

        // 시간 지연 판정
        private double _transitionCheckAt = -1;
        private double _deathCheckAt = -1;
        private bool _hasDeathBeenSeen;
        private bool _isDeathCheckDone;

        // 탄 집계 (주기적으로)
        private double _nextCountTime;
        private string _hazardText = "-";

        // 자동 순회
        private int _stepIndex = -1;
        private double _stepStart;
        private int _stepBaseline;
        private double _nextGroggyTry;
        private readonly StringBuilder _sequenceLog = new StringBuilder();

        private double _nextRepaint;
        private Vector2 _scroll;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<BossTestPanel>("Boss Test");
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ResetResults();
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                ResetResults();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                StopSequence("Play 종료");
                _boss = null;
                _driver = null;
                _groggy = null;
            }
        }

        private void ResetResults()
        {
            for (int i = 0; i < _states.Length; i++)
            {
                _states[i] = CheckState.Pending;
                _details[i] = string.Empty;
            }

            _landings = _shockwaveHits = _offscreenDamage = _groggyEntries = 0;
            _isTintOn = _hasTransitionEntered = false;
            Array.Clear(_comboEnds, 0, _comboEnds.Length);
            for (int i = 0; i < _comboReasons.Length; i++)
            {
                _comboReasons[i] = string.Empty;
            }

            _signalOk = _signalMissing = 0;
            _hasLastSignal = false;
            _errors = 0;
            _firstError = null;
            _transitionCheckAt = _deathCheckAt = -1;
            _hasDeathBeenSeen = _isDeathCheckDone = false;
            _hazardText = "-";
            _stepIndex = -1;
            _sequenceLog.Clear();
            Set(CheckId.NoErrors, CheckState.Pass, "0");
        }

        // ───────────────────────── 로그 판정 ─────────────────────────

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                if (_firstError == null)
                {
                    _firstError = FirstLine(message);
                }

                Set(CheckId.NoErrors, CheckState.Fail, $"{_errors}개 — 첫 번째: {_firstError}");
            }

            if (!EditorApplication.isPlaying || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (message.StartsWith("[JumpSlam] 착지", StringComparison.Ordinal))
            {
                _landings++;
                Set(CheckId.P1Landing, CheckState.Pass, $"{_landings}회 ({FirstLine(message)})");
                UpdateOffscreenCheck();
            }
            else if (message.StartsWith("[BossShockwave] 플레이어 적중", StringComparison.Ordinal))
            {
                _shockwaveHits++;
                Set(CheckId.P1Shockwave, CheckState.Pass, $"{_shockwaveHits}회");
            }
            else if (message.StartsWith("[Boss] 피해", StringComparison.Ordinal))
            {
                // 피해가 실제로 들어간 순간(로그 = 같은 프레임) 점프 단계가 화면 밖이면 실패
                if (_driver != null && IsOffscreen(_driver.DebugJumpSlamPhase))
                {
                    _offscreenDamage++;
                    UpdateOffscreenCheck();
                }
            }
            else if (message.Contains("EnterGroggy → True"))
            {
                _groggyEntries++;
                Set(CheckId.Groggy, CheckState.Pass, $"{_groggyEntries}회");
            }
            else if (message.Contains("2P 붉은 틴트 On"))
            {
                _isTintOn = true;
                UpdateTransitionCheck();
            }
            else if (message.StartsWith("[Boss] PhaseTransition 진입", StringComparison.Ordinal))
            {
                _hasTransitionEntered = true;
                _transitionCheckAt = EditorApplication.timeSinceStartup + TransitionCheckDelay;
                UpdateTransitionCheck();
            }
            else if (message.StartsWith("[ComboSignal] ", StringComparison.Ordinal))
            {
                _hasLastSignal = TryParseUnit(message, "[ComboSignal] ".Length, out _lastSignalUnit);
            }
            else if (message.StartsWith("[Combo ", StringComparison.Ordinal))
            {
                OnComboLog(message);
            }
        }

        private void OnComboLog(string message)
        {
            if (!TryParseUnit(message, "[Combo ".Length, out SelectionUnit unit))
            {
                return;
            }

            int comboIndex = unit - SelectionUnit.C1;
            if (comboIndex < 0 || comboIndex >= _comboEnds.Length)
            {
                return;
            }

            if (message.Contains("단계 1/"))
            {
                if (_hasLastSignal && _lastSignalUnit == unit)
                {
                    _signalOk++;
                }
                else
                {
                    _signalMissing++;
                }

                _hasLastSignal = false;
                CheckState state = _signalMissing > 0 ? CheckState.Fail : CheckState.Pass;
                Set(CheckId.ComboSignal, state, $"먼저 나옴 {_signalOk} / 없음 {_signalMissing}");
                return;
            }

            int reasonStart = message.IndexOf("끝 (", StringComparison.Ordinal);
            if (reasonStart < 0)
            {
                return;
            }

            reasonStart += "끝 (".Length;
            int reasonEnd = message.IndexOf(')', reasonStart);
            string reason = reasonEnd > reasonStart ? message.Substring(reasonStart, reasonEnd - reasonStart) : "?";

            _comboEnds[comboIndex]++;
            if (!_comboReasons[comboIndex].Contains(reason))
            {
                _comboReasons[comboIndex] += (_comboReasons[comboIndex].Length > 0 ? ", " : string.Empty) + reason;
            }

            Set(CheckId.C1 + comboIndex, CheckState.Pass, $"{_comboEnds[comboIndex]}회 ({_comboReasons[comboIndex]})");
        }

        private void UpdateOffscreenCheck()
        {
            if (_offscreenDamage > 0)
            {
                Set(CheckId.P1Offscreen, CheckState.Fail, $"화면 밖 피해 {_offscreenDamage}회");
            }
            else if (_landings > 0)
            {
                Set(CheckId.P1Offscreen, CheckState.Pass, $"점프 {_landings}회 동안 0");
            }
        }

        private void UpdateTransitionCheck()
        {
            if (_hasTransitionEntered && _isTintOn)
            {
                Set(CheckId.Transition, CheckState.Pass, "전환 진입 + 틴트 On");
            }
            else if (_hasTransitionEntered)
            {
                Set(CheckId.Transition, CheckState.Pending, "전환 진입, 틴트 로그 대기");
            }
        }

        // ───────────────────────── 매 틱 ─────────────────────────

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (_driver == null && now >= _nextFindTime)
            {
                _nextFindTime = now + FindInterval;
                FindReferences();
            }

            if (_boss != null)
            {
                if (!_hasDeathBeenSeen && _boss.GetCurrentState() == BossState.Dead)
                {
                    _hasDeathBeenSeen = true;
                    _deathCheckAt = now + DeathCheckDelay;
                }

                RunTimedChecks(now);
                if (now >= _nextCountTime)
                {
                    _nextCountTime = now + CountInterval;
                    _hazardText = CountHazards(includeDrones: true, out _);
                }
            }

            UpdateSequence(now);

            if (now >= _nextRepaint)
            {
                _nextRepaint = now + RepaintInterval;
                Repaint();
            }
        }

        private void FindReferences()
        {
            _driver = Object.FindAnyObjectByType<BossFlowDriver>(); // guard-allow: 에디터 테스트 도구
            if (_driver == null)
            {
                return;
            }

            if (!_driver.TryGetComponent(out _boss))
            {
                _boss = Object.FindAnyObjectByType<BossController>(); // guard-allow: 에디터 테스트 도구
            }

            _groggy = Object.FindAnyObjectByType<BossGroggyDriver>(); // guard-allow: 에디터 테스트 도구
        }

        private void RunTimedChecks(double now)
        {
            if (_transitionCheckAt > 0 && now >= _transitionCheckAt)
            {
                _transitionCheckAt = -1;
                string text = CountHazards(includeDrones: false, out int total);
                Set(CheckId.TransitionClear, total == 0 ? CheckState.Pass : CheckState.Fail, text);
            }

            if (_deathCheckAt > 0 && now >= _deathCheckAt)
            {
                _deathCheckAt = -1;
                _isDeathCheckDone = true;
                string text = CountHazards(includeDrones: true, out int total);
                Set(CheckId.DeathClear, total == 0 ? CheckState.Pass : CheckState.Fail, text);
            }
        }

        /// <summary>씬에 켜져 있는 보스 탄·드론 수. 에디터 도구라 Find 를 쓴다 (0.25초마다).</summary>
        private string CountHazards(bool includeDrones, out int total)
        {
            int cannonballs = CountActive<HowitzerCannonball>();
            int projectiles = CountActive<BossProjectile>();
            int energy = CountActive<EnergyBullet>();
            int markers = _boss != null ? _boss.DebugCountActiveWarningMarkers() : 0;
            int shockwaves = _driver != null ? _driver.DebugActiveShockwaveCount : 0;
            int drones = includeDrones
                ? CountActive<BossDrone>() + CountActive<BodyDrone>() + CountActive<ConductorDrone>()
                : 0;

            total = cannonballs + projectiles + energy + markers + shockwaves + drones;
            return $"합계 {total} = 포탄 {cannonballs} · 총알/파편 {projectiles} · 에너지탄 {energy} · " +
                   $"마커 {markers} · 충격파 {shockwaves}" + (includeDrones ? $" · 드론 {drones}" : string.Empty);
        }

        private static int CountActive<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None); // guard-allow: 에디터 테스트 도구
            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        // ───────────────────────── 자동 순회 ─────────────────────────

        private void StartSequence()
        {
            if (_driver == null || _boss == null)
            {
                GameLog.Warn("[TestPanel] 보스(BossFlowDriver)를 찾지 못해 자동 순회를 시작할 수 없음");
                return;
            }

            _sequenceLog.Clear();
            _stepIndex = -1;
            AdvanceStep(EditorApplication.timeSinceStartup);
        }

        private void StopSequence(string reason)
        {
            if (_stepIndex < 0)
            {
                return;
            }

            _sequenceLog.AppendLine($"중단: {reason}");
            _stepIndex = -1;
            if (_driver != null)
            {
                _driver.DebugForceUnit(false, _driver.DebugForcedUnit);
            }
        }

        private void AdvanceStep(double now)
        {
            _stepIndex++;
            if (_stepIndex >= Sequence.Length)
            {
                _sequenceLog.AppendLine("자동 순회 끝");
                GameLog.Info("[TestPanel] 자동 순회 끝 → '보고서 복사' 로 결과를 붙여 주세요");
                _stepIndex = -1;
                return;
            }

            _stepStart = now;
            (StepKind kind, SelectionUnit unit) = Sequence[_stepIndex];
            switch (kind)
            {
                case StepKind.ForceP1:
                    _stepBaseline = _landings;
                    _driver.DebugForceUnit(true, SelectionUnit.P1);
                    break;
                case StepKind.Groggy:
                    // 게이지를 채워 Groggy 진입 확인. 막히는 상태(전환 등)면 UpdateSequence 가 다시 시도
                    _driver.DebugForceUnit(false, _driver.DebugForcedUnit);
                    _stepBaseline = _groggyEntries;
                    _nextGroggyTry = now;
                    break;
                case StepKind.EnsurePhase2:
                    _driver.DebugForceUnit(false, _driver.DebugForcedUnit);
                    if (_driver.DebugPhase < 2)
                    {
                        _boss.DebugSetHpRatio(Phase2HpRatio);
                    }

                    break;
                case StepKind.ForceCombo:
                    _stepBaseline = _comboEnds[unit - SelectionUnit.C1];
                    _driver.DebugForceUnit(true, unit);
                    break;
                case StepKind.Kill:
                    _driver.DebugForceUnit(false, _driver.DebugForcedUnit);
                    _boss.DebugKill();
                    break;
            }

            string label = kind == StepKind.ForceCombo || kind == StepKind.ForceP1 ? unit.ToString() : kind.ToString();
            _sequenceLog.AppendLine($"단계 {_stepIndex + 1}/{Sequence.Length}: {label}");
            GameLog.Info($"[TestPanel] 자동 순회 단계 {_stepIndex + 1}/{Sequence.Length}: {label}");
        }

        private void UpdateSequence(double now)
        {
            if (_stepIndex < 0)
            {
                return;
            }

            if (_driver == null || _boss == null)
            {
                StopSequence("보스 참조 없음");
                return;
            }

            (StepKind kind, SelectionUnit unit) = Sequence[_stepIndex];
            bool isDone;
            switch (kind)
            {
                case StepKind.ForceP1:
                    isDone = _landings > _stepBaseline;
                    break;
                case StepKind.Groggy:
                    // Groggy 가 끝나 다음 패턴이 돌기 시작할 때까지 기다린다 (전환 요청이 Groggy 뒤로 밀리지 않게)
                    isDone = _groggyEntries > _stepBaseline && _driver.State != BossFlowState.Groggy;
                    if (_groggyEntries <= _stepBaseline && _groggy != null && now >= _nextGroggyTry)
                    {
                        _nextGroggyTry = now + 1.0;
                        for (int i = 0; i < 4; i++)
                        {
                            _groggy.DebugGain(_groggy.DebugWallHitAmount);
                        }
                    }

                    break;
                case StepKind.EnsurePhase2:
                    isDone = _driver.DebugPhase >= 2 && _driver.State != BossFlowState.PhaseTransition;
                    break;
                case StepKind.ForceCombo:
                    isDone = _comboEnds[unit - SelectionUnit.C1] > _stepBaseline;
                    break;
                default:
                    isDone = _isDeathCheckDone;
                    break;
            }

            if (isDone)
            {
                AdvanceStep(now);
                return;
            }

            if (now - _stepStart > StepTimeout)
            {
                _sequenceLog.AppendLine($"  ✗ 타임아웃 ({StepTimeout:F0}초)");
                GameLog.Warn($"[TestPanel] 자동 순회 단계 {_stepIndex + 1} 타임아웃 → 다음 단계");
                AdvanceStep(now);
            }
        }

        // ───────────────────────── GUI ─────────────────────────

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play 를 누르면 보스를 자동으로 찾는다. 테스트 씬: UnMachine/Test Scene/Boss Arena.\n" +
                    "체크리스트는 Play 마다 초기화된다 (마지막 결과는 Play 종료 후에도 보임).",
                    MessageType.Info);
            }
            else if (_driver == null)
            {
                EditorGUILayout.HelpBox("BossFlowDriver 를 찾는 중… (씬에 v2 보스가 있어야 함)", MessageType.Warning);
            }
            else if (_boss != null && !_boss.DebugIsFlowV2Active)
            {
                EditorGUILayout.HelpBox("BossController 의 Use Flow V2 가 꺼져 있거나 Driver 준비 실패 → v2 테스트 불가", MessageType.Error);
            }

            DrawStatus();
            DrawControls();
            DrawChecklist();
            DrawSequence();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("상태", EditorStyles.boldLabel);
            if (_driver == null || _boss == null)
            {
                EditorGUILayout.LabelField("-");
                return;
            }

            EditorGUILayout.LabelField(
                "흐름",
                $"{_driver.State} {_driver.StateRemaining:F2}s · P{_driver.DebugPhase} · ×{_driver.DamageMultiplier:F1}");
            EditorGUILayout.LabelField("패턴", $"{_driver.CurrentUnit} {_driver.DebugPatternName}");
            EditorGUILayout.LabelField(
                "P1 점프",
                $"{_driver.DebugJumpSlamPhase} · 피해 거부 {(_boss.DebugIsUntargetable ? "ON" : "off")}");
            EditorGUILayout.LabelField(
                "HP / 그로기",
                $"{_boss.DebugHpRatio:P0} / {(_groggy != null ? (_groggy.Normalized * 100f).ToString("F0") : "-")}");
            EditorGUILayout.LabelField("남은 탄", _hazardText);
            EditorGUILayout.Space();
        }

        private void DrawControls()
        {
            EditorGUILayout.LabelField("조작", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_driver == null || _boss == null || !EditorApplication.isPlaying))
            {
                EditorGUILayout.LabelField(
                    "강제 단위",
                    _driver != null && _driver.DebugIsForcing ? _driver.DebugForcedUnit.ToString() : "(선택기)");
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < AllUnits.Length; i++)
                {
                    if (i == 6)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }

                    SelectionUnit unit = AllUnits[i];
                    bool isCurrent = _driver != null && _driver.DebugIsForcing && _driver.DebugForcedUnit == unit;
                    Color previous = GUI.backgroundColor;
                    if (isCurrent)
                    {
                        GUI.backgroundColor = Color.green;
                    }

                    if (GUILayout.Button(unit.ToString()))
                    {
                        _driver.DebugForceUnit(true, unit);
                    }

                    GUI.backgroundColor = previous;
                }

                if (GUILayout.Button("해제"))
                {
                    _driver.DebugForceUnit(false, _driver.DebugForcedUnit);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("2P 전환 (HP 49%)"))
                {
                    _boss.DebugSetHpRatio(Phase2HpRatio);
                }

                if (GUILayout.Button("HP 50% 경계"))
                {
                    _boss.DebugSetHpRatio(0.5f);
                }

                if (GUILayout.Button("사망"))
                {
                    _boss.DebugKill();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(_groggy == null))
                {
                    if (GUILayout.Button("그로기 +25"))
                    {
                        _groggy.DebugGain(_groggy.DebugWallHitAmount);
                    }
                }

                if (GUILayout.Button("그로기 진입"))
                {
                    _driver.EnterGroggy();
                }

                if (GUILayout.Button("기절"))
                {
                    _driver.DebugEnterStunned();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
        }

        private void DrawChecklist()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("체크리스트 (자동 판정)", EditorStyles.boldLabel);
            if (GUILayout.Button("초기화", GUILayout.Width(60f)))
            {
                ResetResults();
            }

            if (GUILayout.Button("보고서 복사", GUILayout.Width(90f)))
            {
                string report = BuildReport();
                EditorGUIUtility.systemCopyBuffer = report;
                GameLog.Info("[TestPanel] 보고서를 클립보드에 복사함\n" + report);
            }

            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < CheckLabels.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                Color previous = GUI.contentColor;
                GUI.contentColor = _states[i] == CheckState.Pass ? Color.green
                    : _states[i] == CheckState.Fail ? new Color(1f, 0.4f, 0.4f)
                    : Color.gray;
                EditorGUILayout.LabelField(Mark(_states[i]), GUILayout.Width(20f));
                GUI.contentColor = previous;
                EditorGUILayout.LabelField(new GUIContent(CheckLabels[i], _details[i]));
                EditorGUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(_details[i]))
                {
                    EditorGUILayout.LabelField("      " + _details[i], EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space();
        }

        private void DrawSequence()
        {
            EditorGUILayout.LabelField("자동 순회", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "P1 → 그로기 → 2P 전환 → C1 → C2 → C3 → C4 → 사망 순서로 강제 실행. 각 단계는 완료 로그가 나오면 다음으로 (최대 30초).\n" +
                "플레이어는 피하기만 하면 됨. C1 예외(명중·벽)는 일부러 맞거나 벽에 붙어서 따로 확인.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _driver == null))
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(_stepIndex >= 0))
                {
                    if (GUILayout.Button("자동 순회 시작"))
                    {
                        StartSequence();
                    }
                }

                using (new EditorGUI.DisabledScope(_stepIndex < 0))
                {
                    if (GUILayout.Button("중지"))
                    {
                        StopSequence("사용자 중지");
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (_sequenceLog.Length > 0)
            {
                EditorGUILayout.LabelField(_sequenceLog.ToString(), EditorStyles.helpBox);
            }
        }

        // ───────────────────────── 보조 ─────────────────────────

        private string BuildReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[보스 테스트 보고서] {DateTime.Now:yyyy-MM-dd HH:mm}");
            for (int i = 0; i < CheckLabels.Length; i++)
            {
                builder.Append(Mark(_states[i])).Append(' ').Append(CheckLabels[i]);
                if (!string.IsNullOrEmpty(_details[i]))
                {
                    builder.Append(" — ").Append(_details[i]);
                }

                builder.AppendLine();
            }

            builder.AppendLine($"남은 탄(지금): {_hazardText}");
            if (_sequenceLog.Length > 0)
            {
                builder.AppendLine("자동 순회:");
                builder.Append(_sequenceLog);
            }

            return builder.ToString();
        }

        private void Set(CheckId id, CheckState state, string detail)
        {
            _states[(int)id] = state;
            _details[(int)id] = detail;
        }

        private static string Mark(CheckState state)
        {
            switch (state)
            {
                case CheckState.Pass: return "✓";
                case CheckState.Fail: return "✗";
                default: return "·";
            }
        }

        private static bool IsOffscreen(JumpSlamPhase phase)
        {
            return phase == JumpSlamPhase.Tracking || phase == JumpSlamPhase.Locked;
        }

        private static bool TryParseUnit(string message, int start, out SelectionUnit unit)
        {
            unit = SelectionUnit.P1;
            if (message.Length < start + 2)
            {
                return false;
            }

            return Enum.TryParse(message.Substring(start, 2), out unit);
        }

        private static string FirstLine(string message)
        {
            int newline = message.IndexOf('\n');
            return newline >= 0 ? message.Substring(0, newline) : message;
        }
    }
}
