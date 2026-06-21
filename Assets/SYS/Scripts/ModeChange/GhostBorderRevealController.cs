using System.Collections;
using UnityEngine;

// ============================================================
//  공격모드 진입 시 GhostBorderReveal 셰이더를 구동하는 컨트롤러
//  - SpriteRenderer 버전 (MaterialPropertyBlock 사용, 머티리얼 인스턴스 안 늘어남)
//  - Animation Event 또는 상태머신 OnStateEnter에서 PlayReveal() 호출
// ============================================================
[RequireComponent(typeof(SpriteRenderer))]
public class GhostBorderRevealController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Timing")]
    [SerializeField] private float duration = 0.35f;
    [Tooltip("진행도를 몇 단계로 끊을지. 클수록 부드럽고, 적을수록 디지털/기계적 느낌")]
    [SerializeField] private int steps = 7;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional: Auto Hide After")]
    [SerializeField] private bool autoHide = false;
    [SerializeField] private float visibleDuration = 1.2f;
    [SerializeField] private float hideDuration = 0.15f; // 사라질 땐 보통 더 빠르게 끊김

    [Header("Debug")]
    [Tooltip("플레이 모드에서 K키로 PlayReveal() 테스트")]
    [SerializeField] private bool enableTestKey = true;

    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private MaterialPropertyBlock mpb;
    private Coroutine routine;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        SetProgress(0f);
    }

    private void Update()
    {
        if (!enableTestKey) return;

#if ENABLE_INPUT_SYSTEM
        // 새 Input System 사용 중인 프로젝트
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.kKey.wasPressedThisFrame)
        {
            PlayReveal();
        }
#else
        // 레거시 Input Manager
        if (Input.GetKeyDown(KeyCode.K))
        {
            PlayReveal();
        }
#endif
    }

    /// <summary>공격 모션 전환 애니메이션 이벤트에서 호출</summary>
    public void PlayReveal()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RevealRoutine(0f, 1f, duration, () =>
        {
            if (autoHide)
                routine = StartCoroutine(HideAfterDelay());
        }));
    }

    /// <summary>강제로 즉시 사라지게 하고 싶을 때 (예: 콤보 캔슬)</summary>
    public void HideImmediate()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RevealRoutine(GetCurrentProgress(), 0f, hideDuration, null));
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(visibleDuration);
        routine = StartCoroutine(RevealRoutine(1f, 0f, hideDuration, null));
    }

    private IEnumerator RevealRoutine(float from, float to, float time, System.Action onComplete)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / time);
            float eased = easing.Evaluate(normalized);

            // 계단식 양자화: 매끈한 보간 대신 '뚝뚝' 끊기는 디지털 느낌을 만든다
            float stepped = Mathf.Round(eased * steps) / steps;
            float value = Mathf.Lerp(from, to, stepped);

            SetProgress(value);
            yield return null;
        }
        SetProgress(to);
        onComplete?.Invoke();
    }

    private float GetCurrentProgress()
    {
        targetRenderer.GetPropertyBlock(mpb);
        return mpb.GetFloat(ProgressID);
    }

    private void SetProgress(float value)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ProgressID, value);
        targetRenderer.SetPropertyBlock(mpb);
    }
}

// ------------------------------------------------------------
// UI Canvas(Image / RawImage)에 쓰는 경우 SpriteRenderer 대신 아래 패턴 사용:
//
//   private Material _instancedMat;
//   void Awake() {
//       _instancedMat = Instantiate(image.material); // 머티리얼 공유 깨기
//       image.material = _instancedMat;
//   }
//   void SetProgress(float v) => _instancedMat.SetFloat("_Progress", v);
//
// (UI Graphic은 MaterialPropertyBlock을 직접 못 쓰므로 머티리얼 인스턴스를 따로 둬야 함)
// ------------------------------------------------------------
