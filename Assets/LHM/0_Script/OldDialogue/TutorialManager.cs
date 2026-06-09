// ──────────────────────────────────────────────────────────────────────────
// TutorialManager.cs
// 튜토리얼 스텝 비동기 시퀀스 컨트롤러.
//
// [흐름 - 시간 기반 없음, 오직 액션 기반]
//  트리거 진입
//   └─ Step N 시작
//       ├─ ShowAsync()       슬라이드 인 연출. 완료되면 메시지가 화면에 고정.
//       ├─ WaitUntilAction() 클리어 조건(입력 등) 감지까지 매 프레임 폴링.
//       ├─ DismissAsync()    액션 감지 → 페이드아웃 연출.
//       └─ OnStepCleared     다음 스텝으로.
// ──────────────────────────────────────────────────────────────────────────
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;   // Legacy면 하단 주석 참고

[Serializable]
public class TutorialStepData
{
    [Tooltip("시스템 대화창에 표시할 안내 메시지")]
    public string message;
}

public class TutorialManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private SystemMessageUI _systemMessageUI;

    [Header("Step Messages")]
    [SerializeField]
    private string[] _stepMessages = new[]
    {
        "일섬(우클릭)을 사용하여 통과하세요.",
        "대시(Shift)로 적의 공격을 회피하세요.",
    };

    // ── 런타임 상태 ────────────────────────────────────────────────────────
    private int _currentStep = 0;
    private bool _isRunning = false;
    private CancellationTokenSource _cts;

    // ── 이벤트 ────────────────────────────────────────────────────────────
    public event Action<int> OnStepStarted;
    public event Action<int> OnStepCleared;
    public event Action OnTutorialCompleted;

    // ── 클리어 조건 테이블 ─────────────────────────────────────────────────
    // 각 스텝의 클리어 판정 함수. Awake에서 등록.
    private Func<bool>[] _clearConditions;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _clearConditions = new Func<bool>[]
        {

            // Step 0: 왼쪽이동
            () => Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame,
            // Step 1: 오른쪽이동
            () => Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame,
            // Step 2: 점프
            () => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame,
            // Step 3: 좌 Shift(대시)
            () => Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame,
            // Step 4: 좌클릭(일반공격)
            () => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame,
            // Step 5: 우클릭(일섬)
            () => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame,
            // Step 6: 적섬멸 완료시 -- 임시로 m키 입력으로 대체. 이하 동일(추후 수정 필요)
            () => Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame,
            // Step 7: 벽 붙기 완료시
            () => Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame,
            // Step 8: 천장 붙기 완료시
            () => Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame,
             // Step 9: 적섬멸 완료시
            () => Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame,

            // ▶ Legacy Input 버전:
            // () => Input.GetMouseButtonDown(1),
            // () => Input.GetKeyDown(KeyCode.LeftShift),
        };

        if (_clearConditions.Length < _stepMessages.Length)
            Debug.LogWarning("[TutorialManager] _clearConditions 수가 _stepMessages보다 적습니다.");
    }

    // ─────────────────────────────────────────────────────────────────────
    #region 외부 API

    /// <summary>TutorialTrigger에서 호출. 이미 실행 중이면 무시.</summary>
    public void OnPlayerEnterTrigger()
    {
        if (_isRunning) return;
        RunTutorialSequence().Forget();
    }

    /// <summary>씬 전환 등 강제 중단 시 호출.</summary>
    public void AbortTutorial()
    {
        _cts?.Cancel();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region 핵심 시퀀스

    private async UniTaskVoid RunTutorialSequence()
    {
        _isRunning = true;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            for (int i = _currentStep; i < _stepMessages.Length; i++)
            {
                await RunStep(i, ct);
                if (ct.IsCancellationRequested) return;
            }

            OnTutorialCompleted?.Invoke();
            Debug.Log("[TutorialManager] 튜토리얼 완료");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[TutorialManager] 튜토리얼 중단됨");
            _systemMessageUI.Hide();
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async UniTask RunStep(int stepIndex, CancellationToken ct)
    {
        OnStepStarted?.Invoke(stepIndex);
        Debug.Log($"[TutorialManager] Step {stepIndex} 시작: {_stepMessages[stepIndex]}");

        // 1. 슬라이드 인 — 연출이 끝나면 메시지는 화면에 고정된 채 await 반환
        await _systemMessageUI.ShowAsync(_stepMessages[stepIndex], ct);

        // 2. 클리어 조건 감지까지 대기
        //    메시지가 떠 있는 상태에서 플레이어가 자유롭게 행동
        if (stepIndex < _clearConditions.Length)
            await WaitUntilActionAsync(_clearConditions[stepIndex], ct);

        // 3. 액션 감지 → 페이드아웃
        await _systemMessageUI.DismissAsync(ct);

        // 4. 클리어 처리
        _currentStep = stepIndex + 1;
        OnStepCleared?.Invoke(stepIndex);
        Debug.Log($"[TutorialManager] Step {stepIndex} 클리어");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region 헬퍼

    private static async UniTask WaitUntilActionAsync(
        Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            bool canceled = await UniTask
                .NextFrame(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
