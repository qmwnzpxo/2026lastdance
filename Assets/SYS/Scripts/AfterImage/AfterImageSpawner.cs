// AfterImageSpawner.cs
// 캐릭터 오브젝트에 붙이는 컴포넌트입니다.
//
// ★ 특정 상황에만 잔상 활성화하는 방법
//   spawner.enabled = true;   // 잔상 시작
//   spawner.enabled = false;  // 잔상 중단 (Update가 아예 안 돌아 비용 없음)
//
// ★ 런타임 색상 변경
//   spawner.ColorStart = new Color(...);
//   spawner.ColorEnd   = new Color(...);

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AfterImageSpawner : MonoBehaviour
{
    // ── 인스펙터 설정 ────────────────────────────────────────────────────────

    [Header("Timing")]
    [Tooltip("잔상 생성 간격 (초). 낮을수록 촘촘하게 생성됩니다. 권장: 0.03~0.06")]
    [SerializeField] float spawnInterval = 0.04f;

    [Tooltip("잔상 하나가 완전히 사라지는 데 걸리는 시간 (초). 권장: 0.2~0.5")]
    [SerializeField] float lifetime = 0.35f;

    [Header("Gradient Colors")]
    [Tooltip("잔상이 막 생성됐을 때 색상 (알파 포함)")]
    [SerializeField] Color colorStart = new Color(0f, 0.9f, 1f, 0.75f);   // 시안

    [Tooltip("잔상이 사라지기 직전 색상 (알파 0 권장)")]
    [SerializeField] Color colorEnd   = new Color(1f, 0.9f, 0f, 0f);      // 옐로우 → 투명

    // ── 런타임 색상 프로퍼티 (코드에서 상황별로 변경 가능) ──────────────────

    /// <summary>생성 직후 색상. 런타임에서 자유롭게 변경 가능합니다.</summary>
    public Color ColorStart
    {
        get => colorStart;
        set => colorStart = value;
    }

    /// <summary>소멸 직전 색상. 런타임에서 자유롭게 변경 가능합니다.</summary>
    public Color ColorEnd
    {
        get => colorEnd;
        set => colorEnd = value;
    }

    // ── 내부 상태 ────────────────────────────────────────────────────────────

    SpriteRenderer _sr;
    float          _timer;

    // ── Unity 이벤트 ─────────────────────────────────────────────────────────

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        // 활성화 즉시 첫 잔상을 내보내기 위해 타이머를 만료 상태로 설정
        _timer = spawnInterval;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < spawnInterval) return;

        _timer -= spawnInterval;   // 누산 오차 제거
        Spawn();
    }

    // ── 잔상 생성 ────────────────────────────────────────────────────────────

    void Spawn()
    {
        if (AfterImagePool.Instance == null)
        {
            Debug.LogError("[AfterImageSpawner] AfterImagePool이 씬에 없습니다. " +
                           "빈 GameObject에 AfterImagePool 컴포넌트를 추가해주세요.");
            return;
        }

        AfterImageInstance inst = AfterImagePool.Instance.Rent();
        inst.Initialize(
            _sr.sprite,
            _sr.flipX,
            transform.position,
            transform.rotation,
            transform.lossyScale,
            _sr.sortingLayerID,
            _sr.sortingOrder,
            colorStart,
            colorEnd,
            lifetime
        );
    }
}

// ── 사용 예시 (PlayerController 등 외부 스크립트) ────────────────────────────
//
// AfterImageSpawner _afterImage;
//
// void Awake()
// {
//     _afterImage = GetComponent<AfterImageSpawner>();
//     _afterImage.enabled = false; // 기본은 꺼둠
// }
//
// // 대시 시작
// void OnDashStart()
// {
//     _afterImage.ColorStart = new Color(0f, 0.9f, 1f, 0.75f); // 시안
//     _afterImage.ColorEnd   = new Color(0f, 0.4f, 1f, 0f);
//     _afterImage.enabled = true;
// }
//
// // 대시 종료
// void OnDashEnd()
// {
//     _afterImage.enabled = false;
// }
//
// // 피격 시 (색상만 교체)
// void OnHit()
// {
//     _afterImage.ColorStart = new Color(1f, 0.2f, 0.2f, 0.8f); // 빨강
//     _afterImage.ColorEnd   = new Color(1f, 0f,   0f,   0f);
//     _afterImage.enabled = true;
//     Invoke(nameof(StopAfterImage), 0.3f);
// }
//
// void StopAfterImage() => _afterImage.enabled = false;
