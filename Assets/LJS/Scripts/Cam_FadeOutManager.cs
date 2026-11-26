using UnityEngine;
using UnityEngine.UI; 
using System.Collections; 

public class Cam_FadeOutManager : MonoBehaviour
{
    [Header("화면을 가릴 검은색 UI 이미지")]
    public Image fadeImage;

    [Header("검은 화면 유지 시간 (초)")]
    public float holdDuration = 1.0f; // 새로 추가된 유지 시간 변수

    [Header("페이드 아웃에 걸리는 시간 (초)")]
    public float fadeDuration = 2.0f;

    private Coroutine _fadeCoroutine;

    public void PlayFadeEffect()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[Cam_FadeOutManager] fadeImage가 연결되지 않았습니다.");
            return;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }

        _fadeCoroutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        if (fadeImage == null) yield break;

        // 1. 알파값을 즉시 1(완전한 불투명 검은색)로 만듭니다.
        Color color = fadeImage.color;
        color.a = 1f;
        fadeImage.color = color;
        fadeImage.raycastTarget = true;

        // 2. 지정된 시간 동안 검은 화면을 유지하며 대기합니다.
        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        // 3. 유지 시간이 끝나면 서서히 알파값을 0으로 뺍니다.
        if (fadeDuration > 0f)
        {
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                if (fadeImage == null) yield break;
                elapsedTime += Time.deltaTime;
                color.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsedTime / fadeDuration));
                fadeImage.color = color;

                yield return null;
            }
        }

        // 4. 완벽한 투명 상태로 고정
        if (fadeImage != null)
        {
            color.a = 0f;
            fadeImage.color = color;
            fadeImage.raycastTarget = false;
        }

        _fadeCoroutine = null;
    }
}
