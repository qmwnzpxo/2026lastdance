// ScreenGlitchController.cs
// 위치: Assets/Scripts/Effects/ScreenGlitchController.cs
//
// 사용법:
//   1. Canvas > UI > RawImage 생성 (Anchor: Stretch 전체화면)
//   2. RawImage의 Material에 ScreenGlitch 머티리얼 할당
//   3. 이 스크립트를 같은 오브젝트에 추가
//   4. Timeline Animation Track으로 Intensity / Alpha 등 직접 키프레임
//      또는 코드에서 TriggerDamage() / TriggerBoss() 호출

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class ScreenGlitchController : MonoBehaviour
{
    // ── 머티리얼 프로퍼티 ID ────────────────────────────────────
    static readonly int ID_Intensity     = Shader.PropertyToID("_Intensity");
    static readonly int ID_Alpha         = Shader.PropertyToID("_Alpha");
    static readonly int ID_BlockSize     = Shader.PropertyToID("_BlockSize");
    static readonly int ID_BlockSpeed    = Shader.PropertyToID("_BlockSpeed");
    static readonly int ID_BlockStrength = Shader.PropertyToID("_BlockStrength");
    static readonly int ID_ChromaStr     = Shader.PropertyToID("_ChromaStrength");
    static readonly int ID_VJumpStr      = Shader.PropertyToID("_VJumpStrength");
    static readonly int ID_VJumpSpd      = Shader.PropertyToID("_VJumpSpeed");
    static readonly int ID_ScanDensity   = Shader.PropertyToID("_ScanDensity");
    static readonly int ID_ScanStrength  = Shader.PropertyToID("_ScanStrength");
    static readonly int ID_GlitchColor   = Shader.PropertyToID("_GlitchColor");

    // ── Inspector / Timeline 에서 조절 가능한 프로퍼티 ─────────
    [Header("── 전체 컨트롤 ──────────────────────")]
    [Range(0f, 1f)] public float intensity   = 0f;  // Timeline 키프레임 대상
    [Range(0f, 1f)] public float alpha       = 1f;

    [Header("── 블록 글리치 ────────────────────")]
    [Range(0.005f, 0.5f)]  public float blockSize     = 0.05f;
    [Range(0f,    50f)]    public float blockSpeed    = 12f;
    [Range(0f,     0.5f)]  public float blockStrength = 0.15f;

    [Header("── RGB 색수차 ─────────────────────")]
    [Range(0f,    0.08f)]  public float chromaStrength = 0.02f;

    [Header("── 수직 점프 ──────────────────────")]
    [Range(0f,    1f)]     public float vJumpStrength = 0.1f;
    [Range(0f,   20f)]     public float vJumpSpeed    = 4f;

    [Header("── 스캔 노이즈 ────────────────────")]
    [Range(10f, 600f)]     public float scanDensity   = 120f;
    [Range(0f,    1f)]     public float scanStrength  = 0.25f;

    [Header("── 색상 ────────────────────────────")]
    public Color glitchColor = new Color(0.2f, 1f, 0.9f, 1f);

    // ── 내부 ────────────────────────────────────────────────────
    Material    _mat;
    RawImage    _image;
    Coroutine   _currentRoutine;

    // ────────────────────────────────────────────────────────────
    void Awake()
    {
        _image = GetComponent<RawImage>();

        // 머티리얼 인스턴스 복사 (다른 오브젝트 영향 방지)
        _mat = new Material(_image.material);
        _image.material = _mat;
    }

    void Update()
    {
        ApplyToMaterial();
    }

    // ── 머티리얼에 현재 값 반영 ─────────────────────────────────
    void ApplyToMaterial()
    {
        _mat.SetFloat(ID_Intensity,     intensity);
        _mat.SetFloat(ID_Alpha,         alpha);
        _mat.SetFloat(ID_BlockSize,     blockSize);
        _mat.SetFloat(ID_BlockSpeed,    blockSpeed);
        _mat.SetFloat(ID_BlockStrength, blockStrength);
        _mat.SetFloat(ID_ChromaStr,     chromaStrength);
        _mat.SetFloat(ID_VJumpStr,      vJumpStrength);
        _mat.SetFloat(ID_VJumpSpd,      vJumpSpeed);
        _mat.SetFloat(ID_ScanDensity,   scanDensity);
        _mat.SetFloat(ID_ScanStrength,  scanStrength);
        _mat.SetColor(ID_GlitchColor,   glitchColor);
    }

    // ════════════════════════════════════════════════════════════
    // 코드에서 직접 호출하는 트리거 메서드
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 데미지 글리치 — 강하게 터졌다가 빠르게 복구
    /// </summary>
    public void TriggerDamage(float peakIntensity = 0.9f, float duration = 0.3f)
    {
        if (_currentRoutine != null) StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(DamageRoutine(peakIntensity, duration));
    }

    /// <summary>
    /// 보스 등장 — 서서히 강해졌다가 천천히 가라앉음
    /// </summary>
    public void TriggerBoss(float peakIntensity = 0.75f, float duration = 2.0f)
    {
        if (_currentRoutine != null) StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(BossRoutine(peakIntensity, duration));
    }

    /// <summary>
    /// 즉시 강도 설정 (Timeline에서 직접 쓸 때는 intensity 프로퍼티 키프레임)
    /// </summary>
    public void SetIntensity(float value) => intensity = Mathf.Clamp01(value);

    // ── 코루틴 ──────────────────────────────────────────────────
    IEnumerator DamageRoutine(float peak, float duration)
    {
        float originIntensity = intensity;

        // 순간 점프
        intensity = peak;
        yield return null;

        // 빠른 복구
        float t = 0f;
        float half = duration * 0.5f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;

            // 처음엔 강하게 흔들다가 후반부터 부드럽게 복구
            float shake = Mathf.Lerp(peak, originIntensity,
                          Mathf.SmoothStep(0f, 1f, progress));
            // 약간의 랜덤 노이즈 추가 (흔들림 느낌)
            shake += Random.Range(-0.1f, 0.1f) * (1f - progress) * peak;
            intensity = Mathf.Clamp01(shake);

            yield return null;
        }

        intensity = originIntensity;
        _currentRoutine = null;
    }

    IEnumerator BossRoutine(float peak, float duration)
    {
        float originIntensity = intensity;
        float rampUp   = duration * 0.3f;
        float hold      = duration * 0.2f;
        float rampDown  = duration * 0.5f;

        // 1. 서서히 올라오기
        float t = 0f;
        while (t < rampUp)
        {
            t += Time.deltaTime;
            intensity = Mathf.Lerp(originIntensity, peak,
                        Mathf.SmoothStep(0f, 1f, t / rampUp));
            yield return null;
        }

        // 2. 유지 + 가벼운 흔들림
        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            intensity = peak + Random.Range(-0.05f, 0.05f);
            yield return null;
        }

        // 3. 천천히 복구
        t = 0f;
        while (t < rampDown)
        {
            t += Time.deltaTime;
            intensity = Mathf.Lerp(peak, originIntensity,
                        Mathf.SmoothStep(0f, 1f, t / rampDown));
            yield return null;
        }

        intensity = originIntensity;
        _currentRoutine = null;
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
