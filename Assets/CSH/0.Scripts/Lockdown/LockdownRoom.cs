using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 플레이어가 트리거에 진입하면 게이트를 닫고, 방 안의 적이 전멸하면 게이트를 연다.
/// 락다운 시작·종료 시 CameraFollow에 카메라 전환을 위임한다.
/// </summary>
public class LockdownRoom : MonoBehaviour
{
    [Header("게이트 (입출구 콜라이더)")]
    [SerializeField] private Collider2D[] _gates;

    [Header("카메라 기준점")]
    [SerializeField] private Transform _roomCenter;

    [Header("카메라 줌 설정")]
    [SerializeField] private float lockdownMargin = 1.2f;
    [SerializeField] private float aspectRatio = 16f / 9f;
    [SerializeField] private float zoomDuration = 1f;
    public float LockdownOrthoSize => _lockdownOrthoSize;

    [Header("적 오브젝트")]
    [Tooltip("락다운 구역에 속한 적 오브젝트 목록. 씬에 미리 배치해 둔다.")]
    [SerializeField] private List<GameObject> _enemyObjects = new();

    // ── 내부 상태 ──────────────────────────
    private float _lockdownOrthoSize;
    private bool _isLocked = false;

    private readonly List<EnemyState> _activeEnemies = new();
    private int _aliveCount;

    private CameraFollow _cameraFollow;
    private CancellationTokenSource _cts;

    // ────────────────────────────────────────
    #region Unity 생명주기

    private void Awake()
    {
        _cameraFollow = Camera.main.transform.parent.GetComponent<CameraFollow>();
        PreDeactivateEnemies();
        SetGatesActive(false);
    }

    // 트리거 작동시 락다운 시작
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isLocked || !other.CompareTag("Player")) return;
        StartLockdown();
    }

    // 종료/씬 이동시 원활한 흐름을 위한 작업
    private void OnDestroy()
    {
        UnsubscribeAll();
        _cts?.Cancel();
        _cts?.Dispose();
    }

    #endregion

    // ────────────────────────────────────────
    #region 락다운 시작 / 종료
    // 락다운을 시작한다
    private void StartLockdown()
    {
        _isLocked = true;
        SetGatesActive(true);

        GetComponent<Collider2D>().enabled = false; // 재입장 방지

        _lockdownOrthoSize = CalculateOrtho();
        if (_lockdownOrthoSize == 0)
        {
            ActivateEnemies();
            return;
        }
        _cameraFollow.StartLockdown(_roomCenter,_lockdownOrthoSize, zoomDuration);

        ActivateEnemies();
    }

    // 락다운을 종료한다 (정상 흐름)
    private void EndLockdown()
    {
        _isLocked = false;
        SetGatesActive(false);
        _cameraFollow.EndLockdown(zoomDuration);

        // 재 입장 방지
        gameObject.GetComponent<Collider2D>().enabled = false;
        gameObject.SetActive(false);
    }

    // 락다운을 강제 종료한다 (스킵, 디버그 등 비정상 종료 경로용)
    public void ForceEndLockdown()
    {
        UnsubscribeAll();
        EndLockdown();
        Debug.Log($"[LockdownRoom] {name}: 락다운 강제 종료.");
    }

    #endregion

    // ────────────────────────────────────────
    #region 레드대쉬 되감기 (스냅샷)

    /// <summary>레드대쉬 녹화 시작 시점의 락다운 상태</summary>
    public readonly struct Snapshot
    {
        public readonly bool isLocked;
        public readonly bool isActive;
        public readonly Vector3[] enemyPositions; // _enemyObjects 인덱스 대응
        public Snapshot(bool isLocked, bool isActive, Vector3[] enemyPositions)
        {
            this.isLocked = isLocked;
            this.isActive = isActive;
            this.enemyPositions = enemyPositions;
        }
    }

    public Snapshot CaptureSnapshot()
    {
        var positions = new Vector3[_enemyObjects.Count];
        for (int i = 0; i < _enemyObjects.Count; i++)
            if (_enemyObjects[i] != null) positions[i] = _enemyObjects[i].transform.position;
        return new Snapshot(_isLocked, gameObject.activeSelf, positions);
    }

    /// <summary>
    /// 녹화 구간에서 락다운이 걸렸다면 녹화 이전(미발동) 상태로 되돌린다.
    /// 재생 중 기록 경로를 따라 트리거에 다시 들어오면 락다운이 정상 재발동된다.
    /// </summary>
    public void RestoreSnapshot(Snapshot snap, float cameraDuration)
    {
        if (snap.isLocked == _isLocked && snap.isActive == gameObject.activeSelf) return;

        if (snap.isLocked)
        {
            // 녹화 중에는 플레이어 공격이 데미지를 주지 않으므로 락다운 해제가 일어나지 않는다.
            Debug.LogWarning($"[LockdownRoom] {name}: 녹화 중 락다운 해제됨 — 되감기 미지원 상태.");
            return;
        }

        RevertToIdle(snap, cameraDuration);
    }

    // 락다운 발동 전 상태로 복귀
    private void RevertToIdle(Snapshot snap, float cameraDuration)
    {
        UnsubscribeAll();
        _isLocked = false;
        SetGatesActive(false);

        for (int i = 0; i < _enemyObjects.Count; i++)
        {
            var obj = _enemyObjects[i];
            if (obj == null) continue;
            if (snap.enemyPositions != null && i < snap.enemyPositions.Length)
                obj.transform.position = snap.enemyPositions[i];
            var enemy = obj.GetComponent<EnemyState>();
            if (enemy == null) enemy = obj.GetComponentInChildren<EnemyState>(true);
            if (enemy != null) enemy.Init();
            obj.SetActive(false);
        }

        _cameraFollow.EndLockdown(cameraDuration);

        GetComponent<Collider2D>().enabled = true; // 재생 중 재진입 허용
        gameObject.SetActive(snap.isActive);
        Debug.Log($"[LockdownRoom] {name}: 레드대쉬 되감기로 락다운 취소.");
    }

    #endregion

    // ────────────────────────────────────────
    #region 적 관리
    // 적들을 전부 비활성화 시킨다
    private void PreDeactivateEnemies()
    {
        foreach (var obj in _enemyObjects)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }

    // 트리거 작동시 적들을 활성화 시킨다
    private void ActivateEnemies()
    {
        _activeEnemies.Clear();
        _aliveCount = 0;

        foreach (var obj in _enemyObjects)
        {
            if (obj == null) continue;

            obj.SetActive(true);

            var state = obj.GetComponent<EnemyState>();
            if (state == null)
            {
                state = obj.GetComponentInChildren<EnemyState>();
                if (state == null)
                {
                    Debug.LogWarning($"[LockdownRoom] {obj.name}에 EnemyState 컴포넌트가 없습니다.");
                    continue;
                }
            }

            state.OnDeath += OnEnemyDeath;
            _activeEnemies.Add(state);
            _aliveCount++;
        }

        if (_aliveCount == 0)
        {
            Debug.LogWarning($"[LockdownRoom] {name}: 유효한 적이 없어 즉시 클리어됩니다.");
            EndLockdown();
            return;
        }

        Debug.Log($"[LockdownRoom] {name}: 락다운 시작. 적 수={_aliveCount}");
    }

    // 적 사망 이벤트시 살아있는 적 카운트 감소
    private void OnEnemyDeath(EnemyState dead)
    {
        dead.OnDeath -= OnEnemyDeath;
        _aliveCount--;
        Debug.Log($"[LockdownRoom] {name}: 적 사망 ({dead.name}). 남은 수={_aliveCount}");

        if (_aliveCount <= 0)
            EndLockdown();
    }

    // 이벤트 구독 해지
    private void UnsubscribeAll()
    {
        foreach (var state in _activeEnemies)
        {
            if (state != null)
                state.OnDeath -= OnEnemyDeath;
        }
        _activeEnemies.Clear();
    }

    #endregion

    // ────────────────────────────────────────
    #region 게이트 / OrthoSize
    // 게이트 (비)활성화
    private void SetGatesActive(bool active)
    {
        foreach (var gate in _gates)
        {
            if (gate != null)
            {
                gate.enabled = active;
                gate.gameObject.SetActive(active);
            }
        }
    }

    // 게이트의 위치값중 가장 큰 것들로 카메라가 비출 렌즈 사이즈를 구함
    private float CalculateOrtho()
    {
        float maxY = float.MinValue;
        float maxX = float.MinValue;
        if (_gates == null)
            return 0;
        foreach (var gate in _gates)
        {
            if (gate == null) continue;
            float distY = Mathf.Abs(gate.transform.position.y - _roomCenter.position.y);
            float distX = Mathf.Abs(gate.transform.position.x - _roomCenter.position.x);
            maxY = Mathf.Max(maxY, distY);
            maxX = Mathf.Max(maxX, distX);
        }

        float requiredByY = maxY;
        float requiredByX = maxX / aspectRatio;

        return Mathf.Max(requiredByY, requiredByX) * lockdownMargin;
    }

    #endregion

    // ────────────────────────────────────────
    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (_roomCenter == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(_roomCenter.position, _lockdownOrthoSize * 0.5f);
    }

    #endregion

    // ────────────────────────────────────────
    #region 에디터

#if UNITY_EDITOR
    [ContextMenu("Debug: Force Clear Lockdown")]
    private void Debug_ForceClear()
    {
        if (!Application.isPlaying) return;
        ForceEndLockdown();
    }
#endif

    #endregion
}

/* 사용안함
    // ────────────────────────────────────────
    #region 
    public void fadeoutDialogue()
    {
        var dialogueSystem = FindFirstObjectByType<SystemMessageUI>();
        if (dialogueSystem != null)
        {
            var canvasGroup = dialogueSystem.GetComponent<CanvasGroup>();

        }
    }

    public async UniTaskVoid fadePlayer(float from, float to, float duration, CanvasGroup group, CancellationToken ct)
    {
        if(group == null) return;

        while (!ct.IsCancellationRequested)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {

            }
            await UniTask.WaitForFixedUpdate(cancellationToken: this.destroyCancellationToken);
        }
    }
    #endregion
*/