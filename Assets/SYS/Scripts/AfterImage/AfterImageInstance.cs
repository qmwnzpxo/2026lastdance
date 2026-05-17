// AfterImageInstance.cs
// 잔상 하나의 생명주기를 담당합니다.
// 머티리얼 인스턴스를 오브젝트 생성 시 딱 한 번만 만들어 GC를 최소화합니다.
// 직접 씬에 붙이지 말고 AfterImagePool이 생성/관리합니다.

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AfterImageInstance : MonoBehaviour
{
    SpriteRenderer _sr;
    Material       _mat;     // 이 인스턴스 전용 머티리얼 (Awake에서 한 번만 생성)
    float          _elapsed;
    float          _lifetime;
    bool           _running;

    // Shader.PropertyToID 캐싱 — 매 프레임 string 해시 방지
    static readonly int ID_LifeRatio  = Shader.PropertyToID("_LifeRatio");
    static readonly int ID_ColorStart = Shader.PropertyToID("_ColorStart");
    static readonly int ID_ColorEnd   = Shader.PropertyToID("_ColorEnd");

    // ── 초기화 ──────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        // 셰이더를 인스펙터에서 연결한 머티리얼로 교체하려면
        // AfterImagePool에 public Material baseMaterial 을 두고 넘겨도 됩니다.
        _mat = new Material(Shader.Find("Custom/AfterImage"));
        _sr.material = _mat;
    }

    // ── 외부 인터페이스 ──────────────────────────────────────────────────────

    /// <summary>
    /// AfterImageSpawner가 Rent() 직후 호출해서 이 인스턴스를 세팅합니다.
    /// </summary>
    public void Initialize(
        Sprite     sprite,
        bool       flipX,
        Vector3    position,
        Quaternion rotation,
        Vector3    scale,
        int        sortingLayerID,
        int        sortingOrder,
        Color      colorStart,
        Color      colorEnd,
        float      lifetime)
    {
        // 스프라이트 / 렌더러 설정
        _sr.sprite         = sprite;
        _sr.flipX          = flipX;
        _sr.sortingLayerID = sortingLayerID;
        _sr.sortingOrder   = sortingOrder - 1;   // 원본 스프라이트 뒤

        // 트랜스폼 설정
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = scale;

        // 머티리얼 파라미터
        _mat.SetColor(ID_ColorStart, colorStart);
        _mat.SetColor(ID_ColorEnd,   colorEnd);
        _mat.SetFloat(ID_LifeRatio,  1f);

        // 타이머 초기화
        _elapsed  = 0f;
        _lifetime = lifetime;
        _running  = true;
    }

    // ── 업데이트 ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _lifetime);

        // LifeRatio: 1(생성 직후) → 0(소멸 직전)
        _mat.SetFloat(ID_LifeRatio, 1f - t);

        if (t >= 1f)
        {
            _running = false;
            AfterImagePool.Instance.Return(this);
        }
    }
}
