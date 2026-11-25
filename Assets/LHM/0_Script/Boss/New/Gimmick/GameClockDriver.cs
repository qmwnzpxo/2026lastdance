using UnityEngine;
using UnMachine.Core;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace UnMachine.Boss
{
    /// <summary>
    /// GameClock(Core) 어댑터. 매 프레임 기존 시간 체계(CustomTimeManager)의 배율을 채널에 옮기고 Tick 한다.
    /// GameClock.HitStop 은 HitstopManager 에도 위임해 플레이어도 함께 느려지게 한다. (BACKLOG T28, Q20·Q25)
    /// 씬에 하나만 둔다.
    /// </summary>
    /// <remarks>
    /// 다른 스크립트의 Update 보다 먼저 Tick 해서, 같은 프레임의 로직이 이번 프레임 dt 를 읽게 한다.
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class GameClockDriver : MonoBehaviour
    {
        private const int ExecutionOrder = -1000;

        [Tooltip("GameClock.HitStop 을 HitstopManager 에 위임할 때의 배율. " +
                 "0.05 = CSH Light 프리셋과 동일 (완전 정지 아님, BACKLOG Q25). 보스 World 채널은 GameClock 에서 0.")]
        [SerializeField, Range(0f, 1f)] private float _delegatedHitstopScale = 0.05f;

        [Header("디버그 (에디터 전용)")]
        [Tooltip("F2 를 누르면 GameClock.HitStop(이 값) 요청 (초)")]
        [SerializeField, Min(0f)] private float _debugHitStopSeconds = 0.5f;

        [Header("읽기 전용 표시 (매 프레임 덮어씀)")]
        [SerializeField] private float _worldScale;
        [SerializeField] private float _worldDt;
        [SerializeField] private float _playerScale;
        [SerializeField] private float _playerDt;
        [SerializeField] private float _uiDt;
        [SerializeField] private float _gameClockHitStopRemaining;
        [SerializeField] private float _hitstopManagerRemaining;

        private static GameClockDriver s_active;

        private bool _warnedMissingTimeManager;
        private bool _warnedMissingHitstopManager;

        private void Awake()
        {
            if (s_active != null && s_active != this)
            {
                GameLog.Error(this, $"[GameClockDriver] 씬에 이미 Driver가 있다({s_active.name}). 이 컴포넌트를 끈다.");
                enabled = false;
                return;
            }

            s_active = this;
            // 정적 상태 → 도메인 리로드를 끈 에디터에서 이전 플레이 값이 남지 않게 초기화 (GameClock remarks)
            GameClock.Reset();
        }

        private void OnEnable()
        {
            if (s_active != this)
            {
                return;
            }

            GameClock.HitStopExtended += OnHitStopExtended;
        }

        private void OnDisable()
        {
            GameClock.HitStopExtended -= OnHitStopExtended;
        }

        private void OnDestroy()
        {
            if (s_active == this)
            {
                s_active = null;
            }
        }

        private void Update()
        {
            SyncScales();
            // unscaled 기준: UI 채널은 일시정지(Time.timeScale = 0) 중에도 진행 (ARCHITECTURE 3-1).
            // 히트스톱 타이머도 실제 시간 → HitstopManager(ignoreTimeScale)와 같은 기준.
            GameClock.Tick(Time.unscaledDeltaTime);

#if UNITY_EDITOR
            HandleDebugInput();
            UpdateDebugView();
#endif
        }

        /// <summary>
        /// World = 적 배율(CustomTimeManager.EnemyTimeScale, 히트스톱 배율 포함), Player = 플레이어 배율.
        /// 일시정지(Time.timeScale)는 World·Player 에만 곱한다.
        /// </summary>
        private void SyncScales()
        {
            float pause = Time.timeScale;
            float enemyScale = 1f;
            float playerScale = 1f;

            CustomTimeManager timeManager = CustomTimeManager.Instance;
            if (timeManager != null)
            {
                enemyScale = timeManager.EnemyTimeScale;
                playerScale = timeManager.PlayerTimeScale;
            }
            else if (!_warnedMissingTimeManager)
            {
                _warnedMissingTimeManager = true;
                GameLog.Warn("[GameClockDriver] CustomTimeManager 없음 → World·Player 배율 1로 진행", this);
            }

            GameClock.World.Scale = enemyScale * pause;
            GameClock.Player.Scale = playerScale * pause;
        }

        /// <summary>GameClock 히트스톱이 연장될 때만 호출된다. HitstopManager 쪽도 긴 쪽 유지라 그대로 넘긴다.</summary>
        private void OnHitStopExtended(float seconds)
        {
            HitstopManager hitstopManager = HitstopManager.Instance;
            if (hitstopManager == null)
            {
                if (!_warnedMissingHitstopManager)
                {
                    _warnedMissingHitstopManager = true;
                    GameLog.Warn("[GameClockDriver] HitstopManager 없음 → 히트스톱 위임 생략 (보스 쪽만 정지)", this);
                }

                return;
            }

            hitstopManager.TriggerHitstop(seconds, _delegatedHitstopScale);
        }

#if UNITY_EDITOR
        private void HandleDebugInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f2Key.wasPressedThisFrame)
            {
                return;
            }

            bool extended = GameClock.HitStop(_debugHitStopSeconds);
            GameLog.Info($"[GameClockDriver] F2: HitStop({_debugHitStopSeconds:F2}) → 연장 {extended}", this);
        }

        private void UpdateDebugView()
        {
            _worldScale = GameClock.World.Scale;
            _worldDt = GameClock.World.DeltaTime;
            _playerScale = GameClock.Player.Scale;
            _playerDt = GameClock.Player.DeltaTime;
            _uiDt = GameClock.UI.DeltaTime;
            _gameClockHitStopRemaining = GameClock.HitStopRemaining;

            HitstopManager hitstopManager = HitstopManager.Instance;
            _hitstopManagerRemaining = hitstopManager != null ? hitstopManager.RemainingTime : 0f;
        }
#endif
    }
}
