using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  공격모드 진입 시 GhostBorderReveal 셰이더를 구동하는 컨트롤러
//  - UI RawImage 버전 (머티리얼을 런타임에 인스턴스화해서 구동)
//  - Animation Event 또는 상태머신 OnStateEnter에서 PlayReveal() 호출
//
//  사전 준비:
//   1) GhostBorderReveal 셰이더로 머티리얼 에셋 하나 만들고
//      NoiseTex, EdgeColor 등 값 세팅
//   2) RawImage 컴포넌트의 Material 슬롯에 그 머티리얼 할당
//   3) RawImage의 Texture 슬롯에 보더 PNG 할당
//      (Texture는 RawImage가 알아서 _MainTex로 흘려보내줌)
// ============================================================
[RequireComponent(typeof(RawImage))]
public class GhostBorderRevealController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RawImage targetImage;

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
    private Material instancedMat;
    private Coroutine routine;

    private void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<RawImage>();

        if (targetImage.material == null)
        {
            Debug.LogError($"[{nameof(GhostBorderRevealController)}] RawImage의 Material 슬롯이 비어있습니다. " +
                            "GhostBorderReveal 머티리얼을 할당하세요.", this);
        }

        // 원본 머티리얼 에셋을 직접 건드리지 않도록 런타임 인스턴스 생성
        // (RawImage의 Material 슬롯에 GhostBorderReveal 머티리얼이 미리 할당돼 있어야 함)
        instancedMat = Instantiate(targetImage.material);
        targetImage.material = instancedMat;

        if (!instancedMat.HasProperty(ProgressID))
        {
            Debug.LogError($"[{nameof(GhostBorderRevealController)}] 현재 머티리얼 '{instancedMat.shader.name}'에 " +
                            "_Progress 프로퍼티가 없습니다. GhostBorderReveal 셰이더가 맞는지 확인하세요.", this);
        }

        Debug.Log($"[{nameof(GhostBorderRevealController)}] Awake 완료. shader={instancedMat.shader.name}", this);

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
            Debug.Log($"[{nameof(GhostBorderRevealController)}] K key detected (new Input System)", this);
            PlayReveal();
        }
#else
        // 레거시 Input Manager
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log($"[{nameof(GhostBorderRevealController)}] K key detected (legacy Input)", this);
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
        return instancedMat.GetFloat(ProgressID);
    }

    private void SetProgress(float value)
    {
        instancedMat.SetFloat(ProgressID, value);
    }

    private void OnDestroy()
    {
        if (instancedMat != null)
        {
            Destroy(instancedMat);
        }
    }
}
