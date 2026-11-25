using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반 적(EnemyBTController)들의 Update/FixedUpdate/LateUpdate를 한 곳에서 돌리는 매니저.
/// - 적은 OnEnable/OnDisable 시 자동 등록/해제된다.
/// - 씬에 배치하지 않아도 첫 등록 시 자동 생성된다 (씬 단위, DontDestroyOnLoad 아님).
/// - SetAllActive(false)로 전체 적 행동 정지, EnemyBTController.BehaviourEnabled로 개별 정지.
/// </summary>
[DefaultExecutionOrder(-50)]
public class EnemyManager : MonoBehaviour
{
    private static EnemyManager _instance;
    private static bool _quitting;

    private readonly List<EnemyBTController> _enemies = new();
    private readonly List<EnemyBTController> _pendingAdd = new();
    private readonly List<EnemyBTController> _pendingRemove = new();
    private bool _iterating;

    [SerializeField] private bool _allActive = true;

    // ─── 외부 API ───────────────────────────────
    public static EnemyManager Instance
    {
        get
        {
            if (_instance == null && !_quitting)
                _instance = new GameObject("[EnemyManager]").AddComponent<EnemyManager>();
            return _instance;
        }
    }

    /// <summary>전체 on/off 상태. 매니저가 없으면 true.</summary>
    public static bool IsGloballyActive => _instance == null || _instance._allActive;

    /// <summary>현재 등록된(활성) 적 목록. 순회 중 수정 금지.</summary>
    public IReadOnlyList<EnemyBTController> Enemies => _enemies;

    /// <summary>모든 적의 행동을 켜거나 끈다. 끄면 진행 중인 액션이 취소된다.</summary>
    public void SetAllActive(bool active)
    {
        if (_allActive == active) return;
        _allActive = active;
        foreach (var e in _enemies)
            if (e != null) e.RefreshPause();
    }

    // ─── 등록 ───────────────────────────────────
    public static void Register(EnemyBTController enemy)
    {
        var mgr = Instance;
        if (mgr == null || enemy == null) return;

        if (mgr._iterating)
        {
            mgr._pendingRemove.Remove(enemy);
            if (!mgr._pendingAdd.Contains(enemy)) mgr._pendingAdd.Add(enemy);
        }
        else if (!mgr._enemies.Contains(enemy))
        {
            mgr._enemies.Add(enemy);
        }
    }

    public static void Unregister(EnemyBTController enemy)
    {
        // 해제는 매니저를 새로 만들지 않는다 (씬 언로드/종료 중 생성 방지)
        var mgr = _instance;
        if (mgr == null || enemy == null) return;

        if (mgr._iterating)
        {
            mgr._pendingAdd.Remove(enemy);
            if (!mgr._pendingRemove.Contains(enemy)) mgr._pendingRemove.Add(enemy);
        }
        else
        {
            mgr._enemies.Remove(enemy);
        }
    }

    // ─── Unity 생명주기 ─────────────────────────
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnApplicationQuit() => _quitting = true;

    // Enter Play Mode 옵션(도메인 리로드 끔) 대응
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _quitting = false;
    }

    private void FixedUpdate()
    {
        BeginIterate();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e != null && e.isActiveAndEnabled) e.FixedTick();
        }
        EndIterate();
    }

    private void Update()
    {
        BeginIterate();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e != null && e.isActiveAndEnabled) e.Tick();
        }
        EndIterate();
    }

    private void LateUpdate()
    {
        BeginIterate();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e != null && e.isActiveAndEnabled) e.LateTick();
        }
        EndIterate();
    }

    private void BeginIterate() => _iterating = true;

    private void EndIterate()
    {
        _iterating = false;

        if (_pendingRemove.Count > 0)
        {
            foreach (var e in _pendingRemove) _enemies.Remove(e);
            _pendingRemove.Clear();
        }
        if (_pendingAdd.Count > 0)
        {
            foreach (var e in _pendingAdd)
                if (e != null && !_enemies.Contains(e)) _enemies.Add(e);
            _pendingAdd.Clear();
        }
        _enemies.RemoveAll(e => e == null);
    }
}
