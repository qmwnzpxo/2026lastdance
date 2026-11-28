using System.Collections.Generic;
using ItemSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 챕터 씬의 구역 오브젝트 단위 저장. 씬에 따로 배치할 필요 없이 런타임에 자동으로 동작한다.
///   - 챕터 씬    = RespawnPoint가 하나 이상 배치된 씬
///   - 구역 오브젝트 = 각 RespawnPoint(SpawnPoint)의 부모 오브젝트 (예: Grid_levels/1-3-L). 이름이 저장 키
///   - 구역 순서  = 구역 SpawnPoint의 x 오름차순
///   - 저장      : 플레이어가 구역 SpawnPoint를 오른쪽으로 지나가면 그 구역 + 플레이어 상태(HP·게이지·섬광탄·칩)를
///                 save.json에 저장 (앞으로만 갱신). 새 챕터 씬에 들어오면 (그 씬, 시작 위치, 시작 상태)로 저장
///   - 불러오기  : 챕터 씬이 열릴 때 같은 씬의 저장이 있으면
///                 1) 저장 구역 SpawnPoint로 이동 + 플레이어 상태 복원
///                 2) 저장 구역보다 앞의 구역 전부: 적·락다운 제거
///                 3) 그중 바로 앞 구역을 뺀 나머지: ItemBox도 제거
///                 저장 구역 자체는 씬 초기 상태 그대로
///                 메인 메뉴 이어하기 버튼은 Continue() 호출 (SectionSaveButton 참고)
/// ※ 저장된 씬에 다시 들어가면 항상 저장 구역으로 이동한다. 처음부터 하려면 Clear() 호출.
/// </summary>
public static class SectionSave
{
    /// <summary>구역 오브젝트 + 그 구역의 SpawnPoint</summary>
    internal readonly struct Section
    {
        public readonly Transform root;
        public readonly RespawnPoint point;
        public string Name => root.name;
        public Section(Transform root, RespawnPoint point) { this.root = root; this.point = point; }
    }

    // ────────────────────────────────────────
    #region 외부 API

    /// <summary>이어할 저장이 있는지 (저장된 씬이 Build Settings에 있어야 true)</summary>
    public static bool HasSave
    {
        get
        {
            var data = SaveManager.GetOrCreate().Load();
            return !string.IsNullOrEmpty(data.sectionScene)
                && Application.CanStreamedLevelBeLoaded(data.sectionScene);
        }
    }

    public static string SavedScene => SaveManager.GetOrCreate().Load().sectionScene;
    /// <summary>저장된 구역 오브젝트 이름. 비어 있으면 씬 시작 위치</summary>
    public static string SavedSection => SaveManager.GetOrCreate().Load().sectionName;

    /// <summary>저장된 챕터 씬을 Loading 씬을 거쳐 연다. 위치·상태·구역 정리는 씬이 열릴 때 자동 처리. 저장이 없으면 false.</summary>
    public static bool Continue()
    {
        if (!HasSave)
        {
            Debug.LogWarning("[SectionSave] 이어할 저장이 없습니다.");
            return false;
        }
        SceneLoader.LoadScene(SavedScene);
        return true;
    }

    /// <summary>구역 저장만 지운다 (스탯·카르마 등 다른 저장 데이터는 유지).</summary>
    public static void Clear()
    {
        var mgr = SaveManager.GetOrCreate();
        var data = mgr.Load();
        data.sectionScene = "";
        data.sectionName = "";
        data.sectionPlayer = new SectionPlayerState();
        mgr.Save(data);
        Debug.Log("[SectionSave] 구역 저장 초기화");
    }

    #endregion
    // ────────────────────────────────────────
    #region 씬 로드 처리

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // sceneLoaded는 씬 오브젝트의 Awake/OnEnable 이후, Start 이전에 호출됨
    // → RespawnPoint 등록, PlayerState.InitState, LockdownRoom의 적 사전 비활성화가 끝난 상태
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        var sections = CollectSections(scene);
        if (sections.Count == 0) return; // 챕터 씬 아님 (메인·로딩·컷씬 등)

        var player = Object.FindFirstObjectByType<PlayerState>();
        if (player == null) return;

        var mgr = SaveManager.GetOrCreate();
        var data = mgr.Load();
        int startIndex = -1;

        if (data.sectionScene == scene.name)
        {
            startIndex = FindSection(sections, data.sectionName);
            if (startIndex >= 0)
            {
                MovePlayer(player, sections[startIndex].point.Position);
                ClearPreviousSections(sections, startIndex);
                Debug.Log($"[SectionSave] {scene.name} 구역 '{data.sectionName}'에서 시작");
            }
            else if (!string.IsNullOrEmpty(data.sectionName))
            {
                Debug.LogWarning($"[SectionSave] 저장된 구역 '{data.sectionName}'을 {scene.name}에서 찾지 못해 시작 위치에서 시작합니다.");
            }
            // 저장 시점의 HP·게이지·칩 복원 (PlayerState.Awake의 InitState 이후라 덮어쓰기 안전)
            player.RestoreSectionState(data.sectionPlayer);
        }
        else
        {
            // 새 챕터 씬 진입 → 이 씬 시작 위치 + 시작 상태로 저장
            data.sectionScene = scene.name;
            data.sectionName = "";
            data.sectionPlayer = player.CaptureSectionState();
            mgr.Save(data);
        }

        var go = new GameObject("[SectionSaveTracker]");
        SceneManager.MoveGameObjectToScene(go, scene); // 씬이 바뀌면 함께 사라지도록
        go.AddComponent<SectionSaveTracker>().Init(scene.name, sections, player, startIndex);
    }

    private static List<Section> CollectSections(Scene scene)
    {
        var list = new List<Section>();
        var names = new HashSet<string>();
        foreach (var p in RespawnPoint.All)
        {
            if (p == null || p.gameObject.scene != scene) continue;

            // 부모가 없으면 SpawnPoint 자신을 구역으로 취급 (정리할 대상 없음)
            Transform root = p.transform.parent != null ? p.transform.parent : p.transform;
            if (!names.Add(root.name))
                Debug.LogWarning($"[SectionSave] {scene.name}: 구역 이름 '{root.name}'이 중복됩니다. 저장 키가 겹치니 이름을 다르게 지어 주세요.", root);
            list.Add(new Section(root, p));
        }
        list.Sort((a, b) => a.point.Position.x.CompareTo(b.point.Position.x));
        return list;
    }

    private static int FindSection(List<Section> sections, string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        for (int i = 0; i < sections.Count; i++)
            if (sections[i].Name == name) return i;
        return -1;
    }

    /// <summary>
    /// 저장 구역(savedIndex)보다 앞의 구역 정리.
    /// 전부: 적·락다운 제거 / 바로 앞 구역(savedIndex - 1)을 뺀 나머지: ItemBox도 제거
    /// </summary>
    private static void ClearPreviousSections(List<Section> sections, int savedIndex)
    {
        for (int i = 0; i < savedIndex; i++)
        {
            Transform root = sections[i].root;
            if (root == sections[i].point.transform) continue; // 부모 없는 SpawnPoint

            // 락다운: 정상 종료(EndLockdown)와 같이 트리거 오브젝트를 비활성화 → 재발동 없음
            foreach (var room in root.GetComponentsInChildren<LockdownRoom>(true))
            {
                if (room.transform == root)
                {
                    // 구역 오브젝트에 직접 붙은 경우: 구역 전체를 끄지 않도록 트리거·컴포넌트만 끔
                    var col = room.GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                    room.enabled = false;
                }
                else
                {
                    room.gameObject.SetActive(false);
                }
            }

            // 적
            foreach (var enemy in root.GetComponentsInChildren<EnemyState>(true))
                enemy.gameObject.SetActive(false);

            // 상자 (바로 앞 구역은 남김)
            if (i < savedIndex - 1)
            {
                foreach (var box in root.GetComponentsInChildren<ItemDropOnBreak>(true))
                    box.gameObject.SetActive(false);
            }
        }
    }

    private static void MovePlayer(PlayerState player, Vector2 pos)
    {
        player.transform.position = pos;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
        }
        CameraFollow.SnapMainToTarget(); // 순간이동 후 카메라가 끌려오지 않도록
    }

    /// <summary>트래커가 구역 통과를 감지했을 때 저장</summary>
    internal static void SaveSection(string sceneName, string sectionName, PlayerState player)
    {
        var mgr = SaveManager.GetOrCreate();
        var data = mgr.Load();
        data.sectionScene = sceneName;
        data.sectionName = sectionName;
        data.sectionPlayer = player.CaptureSectionState();
        mgr.Save(data);
        Debug.Log($"[SectionSave] 구역 저장: {sceneName} / {sectionName}");
    }

    #endregion
}

/// <summary>챕터 씬에서 플레이어가 다음 구역 SpawnPoint를 지나갔는지 감시한다. SectionSave가 자동 생성.</summary>
public class SectionSaveTracker : MonoBehaviour
{
    private string _sceneName;
    private List<SectionSave.Section> _sections;
    private PlayerState _player;
    private int _current; // 마지막으로 저장된 구역 순서 (-1 = 시작 위치)

    internal void Init(string sceneName, List<SectionSave.Section> sections, PlayerState player, int startIndex)
    {
        _sceneName = sceneName;
        _sections = sections;
        _player = player;
        _current = startIndex;
    }

    private void Update()
    {
        if (_player == null || _player.isDead) return;

        // 지나간(플레이어보다 왼쪽에 있는) 다음 구역이 있으면 앞으로 갱신. 한 프레임에 여러 개 넘어도 마지막 것만 저장
        // (사망 리스폰 기준 RespawnPoint.FindNearestLeft와 같은 '왼쪽' 판정)
        float x = _player.transform.position.x;
        int reached = _current;
        while (reached + 1 < _sections.Count
               && _sections[reached + 1].point != null
               && _sections[reached + 1].point.Position.x < x)
        {
            reached++;
        }

        if (reached > _current)
        {
            _current = reached;
            SectionSave.SaveSection(_sceneName, _sections[_current].Name, _player);
        }
    }
}
