// ═══════════════════════════════════════════════════════════════════════
// NarrowingQTETrigger
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

    // ── 입력 버퍼 ─────────────────────────────────────────────────────
    private string _inputBuffer = null;
    private bool _ignoreInput = false;

    private static readonly string[] _wasdKeys = { "W", "A", "S", "D" };
    private const int KEY_COUNT = 8;

    // ─────────────────────────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────────────────────────

    public void Initialize(Transform player, float timeLimit, QTEUIController qteUI)
    {
        _playerTransform = player;
        _timeLimit = timeLimit;
        _qteUI = qteUI;

        if (_qteUI == null)
            _qteUI = Object.FindObjectOfType<QTEUIController>();

        if (_qteUI == null)
            Debug.LogWarning("[NarrowingQTE] QTEUIController를 찾을 수 없습니다.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Update — G키 감지 / WASD 버퍼 저장
    // ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_qteTriggered)
        {
            if (_playerTransform != null
                && Input.GetKeyDown(KeyCode.G)
                && Vector2.Distance(transform.position, _playerTransform.position) <= _interactRadius)
            {
                Debug.Log("[NarrowingQTE] G키 → QTE 시작");
                _qteTriggered = true;
                RunQTEAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
            return;
        }

        if (_ignoreInput || _inputBuffer != null) return;

        if (Input.GetKeyDown(KeyCode.W)) _inputBuffer = "W";
        else if (Input.GetKeyDown(KeyCode.A)) _inputBuffer = "A";
        else if (Input.GetKeyDown(KeyCode.S)) _inputBuffer = "S";
        else if (Input.GetKeyDown(KeyCode.D)) _inputBuffer = "D";
    }

    // ─────────────────────────────────────────────────────────────────
    // QTE 메인 루프
    // ─────────────────────────────────────────────────────────────────

    private async UniTaskVoid RunQTEAsync(CancellationToken ct)
    {
        string[] sequence = GenerateRandomSequence(KEY_COUNT);
        Debug.Log($"[NarrowingQTE] 시퀀스: {string.Join(" - ", sequence)}");

        _inputBuffer = null;
        _ignoreInput = false;

        _qteUI?.Show(sequence, _timeLimit);
        _qteUI?.UpdateProgress(0);

        float elapsed = 0f;
        int step = 0;
        bool success = false;

        while (!ct.IsCancellationRequested && elapsed < _timeLimit)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            elapsed += Time.deltaTime;

            if (_inputBuffer == null) continue;

            string pressed = _inputBuffer;
            _inputBuffer = null;

            Debug.Log($"[NarrowingQTE] 입력={pressed} / 예상={sequence[step]} / step={step}");

            if (pressed == sequence[step])
            {
                step++;
                _qteUI?.UpdateProgress(step);
                Debug.Log($"[NarrowingQTE] 정답! {step}/{KEY_COUNT}");

                if (step >= KEY_COUNT)
                {
                    success = true;
                    break;
                }
            }
            else
            {
                Debug.Log($"[NarrowingQTE] 오답! '{pressed}' (예상 '{sequence[step]}') → 1초 후 리셋");
                _qteUI?.ShowWrong(step);

                _ignoreInput = true;
                _inputBuffer = null;

                await UniTask.Delay(
                    System.TimeSpan.FromMilliseconds(1000), cancellationToken: ct);

                _inputBuffer = null;
                _ignoreInput = false;

                step = 0;
                _qteUI?.UpdateProgress(0);
            }
        }

        // ── 결과 처리 — UI 닫기를 finally로 보장 ─────────────────────
        _ignoreInput = true;

        try
        {
            if (success)
            {
                Debug.Log("[NarrowingQTE] 성공!");
                _qteUI?.ShowSuccess();

                await UniTask.Delay(
                    System.TimeSpan.FromMilliseconds(800),
                    cancellationToken: ct).SuppressCancellationThrow();
            }
            else
            {
                Debug.Log("[NarrowingQTE] 실패 (시간 초과)");
                _qteUI?.ShowFail();

                await UniTask.Delay(
                    System.TimeSpan.FromMilliseconds(800),
                    cancellationToken: ct).SuppressCancellationThrow();
            }
        }
        finally
        {
            // ct 취소(씬 전환, 오브젝트 파괴 등) 상황에서도 반드시 UI 닫힘
            _qteUI?.Hide();
            Debug.Log("[NarrowingQTE] UI 닫힘");
        }

        if (success)
            OnQTESuccess?.Invoke();
        else
            OnQTEFail?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────
    // 오브젝트 파괴 시에도 UI 닫기
    // ─────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        _qteUI?.Hide();
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    private static string[] GenerateRandomSequence(int count)
    {
        var seq = new string[count];
        for (int i = 0; i < count; i++)
            seq[i] = _wasdKeys[Random.Range(0, _wasdKeys.Length)];
        return seq;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactRadius);
    }
#endif
}
