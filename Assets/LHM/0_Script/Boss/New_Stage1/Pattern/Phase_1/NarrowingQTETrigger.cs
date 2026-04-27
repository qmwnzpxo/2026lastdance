// ═══════════════════════════════════════════════════════════════════════
// NarrowingQTETrigger
//
// 흐름:
//   G키 (인터랙트 범위 내) → QTE 시작
//   → WASD 8개 랜덤 배열 생성 → UI 팝업 표시
//   → 키 입력 순서대로 맞추기
//     · 정답: 다음 칸으로 진행 (UI 초록 표시)
//     · 오답: 빨간 플래시 0.3초 → step = 0 리셋
//   → 8개 전부 맞추면 OnQTESuccess
//   → 시간 초과 or 벽 압사면 OnQTEFail
// ═══════════════════════════════════════════════════════════════════════
using BossSystem;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NarrowingQTETrigger : MonoBehaviour
{
    [Header("Interact")]
    [SerializeField] private float _interactRadius = 1.5f;

    [Header("UI")]
    [SerializeField] private QTEUIController _qteUI;

    // ── 이벤트 ────────────────────────────────────────────────────────
    public event System.Action OnQTESuccess;
    public event System.Action OnQTEFail;

    // ── 내부 상태 ─────────────────────────────────────────────────────
    private Transform _playerTransform;
    private float _timeLimit;
    private bool _qteTriggered;

    // WASD 풀 & 키 개수
    private static readonly string[] _wasdKeys = { "W", "A", "S", "D" };
    private const int KEY_COUNT = 8;

    // ─────────────────────────────────────────────────────────────────
    // 초기화 (패턴에서 Instantiate 후 호출)
    // ─────────────────────────────────────────────────────────────────

    public void Initialize(Transform player, float timeLimit, QTEUIController qteUI)
    {
        Debug.Log($"[QTE] Initialize 호출됨. player={player}, qteUI={qteUI}");
        _playerTransform = player;
        _timeLimit = timeLimit;
        _qteUI = qteUI;


        if (_qteUI == null)
            _qteUI = Object.FindObjectOfType<QTEUIController>();

        if (_qteUI == null)
            Debug.LogWarning("[NarrowingQTE] QTEUIController를 찾을 수 없습니다. UI 없이 진행합니다.");
    }

    // ─────────────────────────────────────────────────────────────────
    // G키 감지
    // ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        Debug.Log($"triggered={_qteTriggered}, player={_playerTransform}, G={Input.GetKeyDown(KeyCode.G)}");

        if (_qteTriggered) return;
        if (_playerTransform == null) return;




        if (_qteTriggered) return;
        if (_playerTransform == null) return;
        if (!Input.GetKeyDown(KeyCode.G)) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist > _interactRadius) return;
        Debug.Log($"dist={dist}, radius={_interactRadius}"); // 거리 확인
        _qteTriggered = true;
        RunQTEAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // ─────────────────────────────────────────────────────────────────
    // QTE 메인 루프
    // ─────────────────────────────────────────────────────────────────

    private async UniTaskVoid RunQTEAsync(CancellationToken ct)
    {
        // 1. 랜덤 배열 생성
        string[] sequence = GenerateRandomSequence(KEY_COUNT);
        Debug.Log($"[NarrowingQTE] Sequence: {string.Join(" - ", sequence)}");

        // 2. UI 열기
        _qteUI?.Show(sequence, _timeLimit);
        _qteUI?.UpdateProgress(0);

        float elapsed = 0f;
        int step = 0;
        bool success = false;

        while (!ct.IsCancellationRequested && elapsed < _timeLimit)
        {
            elapsed += Time.deltaTime;

            string pressed = GetPressedWASD();
            if (pressed != null)
            {
                if (pressed == sequence[step])
                {
                    // ── 정답 ─────────────────────────────────────────
                    step++;
                    _qteUI?.UpdateProgress(step);
                    Debug.Log($"[NarrowingQTE] Correct! step={step}/{KEY_COUNT}");

                    if (step >= KEY_COUNT)
                    {
                        success = true;
                        break;
                    }
                }
                else
                {
                    // ── 오답 → 0.3초 플래시 후 처음부터 ──────────────
                    Debug.Log($"[NarrowingQTE] Wrong '{pressed}' (expected '{sequence[step]}'). Reset!");
                    _qteUI?.ShowWrong(step);

                    await UniTask.Delay(
                        System.TimeSpan.FromMilliseconds(300), cancellationToken: ct);

                    step = 0;
                    _qteUI?.UpdateProgress(step);
                }
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // 3. 결과 처리
        if (success)
        {
            _qteUI?.ShowSuccess();
            // 성공 연출 잠깐 보여주고 이벤트 발생
            await UniTask.Delay(
                System.TimeSpan.FromMilliseconds(600), cancellationToken: ct)
                .SuppressCancellationThrow();
            OnQTESuccess?.Invoke();
        }
        else
        {
            _qteUI?.ShowFail();
            await UniTask.Delay(
                System.TimeSpan.FromMilliseconds(600), cancellationToken: ct)
                .SuppressCancellationThrow();
            OnQTEFail?.Invoke();
        }

        _qteUI?.Hide();
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    /// <summary>WASD 중복 허용 랜덤 8개 생성.</summary>
    private static string[] GenerateRandomSequence(int count)
    {
        var seq = new string[count];
        for (int i = 0; i < count; i++)
            seq[i] = _wasdKeys[Random.Range(0, _wasdKeys.Length)];
        return seq;
    }

    /// <summary>이번 프레임 WASD 입력. 없으면 null.</summary>
    private static string GetPressedWASD()
    {
        Debug.Log(Input.GetKeyDown(KeyCode.W));
        if (Input.GetKeyDown(KeyCode.W)) return "W";
        if (Input.GetKeyDown(KeyCode.A)) return "A";
        if (Input.GetKeyDown(KeyCode.S)) return "S";
        if (Input.GetKeyDown(KeyCode.D)) return "D";
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactRadius);
    }
#endif
}
