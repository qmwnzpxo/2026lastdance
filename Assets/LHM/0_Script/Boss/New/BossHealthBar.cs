using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 체력 바 UI - 보스 체력 바 업데이트 전담
/// 화면 상단 중앙에 위치
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Text healthText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image backgroundImage;

    [Header("색상 설정")]
    [SerializeField] private Color highHealthColor = new Color(0f, 1f, 0f);  // 초록
    [SerializeField] private Color mediumHealthColor = new Color(1f, 1f, 0f); // 노랑
    [SerializeField] private Color lowHealthColor = new Color(1f, 0f, 0f);   // 빨강

    [Header("페이즈 표시")]
    [SerializeField] private Text phaseText;
    [SerializeField] private GameObject phaseIndicator;

    private float maxHP;
    private float currentHP;
    private int currentPhase = 1;

    private void Start()
    {
        // UI 초기화
        if (healthSlider != null)
        {
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;
        }

        if (phaseText != null)
        {
            phaseText.text = "PHASE 1";
        }

        // 기본적으로 비활성화 (보스 등장 시 활성화)
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 체력 바 초기화
    /// </summary>
    public void Initialize(float maxHealth)
    {
        maxHP = maxHealth;
        currentHP = maxHealth;

        // UI 활성화
        gameObject.SetActive(true);

        UpdateHealth(currentHP);
        UpdatePhase(1);
    }

    /// <summary>
    /// 체력 업데이트
    /// </summary>
    public void UpdateHealth(float health)
    {
        currentHP = health;

        if (healthSlider != null)
        {
            float healthPercentage = currentHP / maxHP;
            healthSlider.value = healthPercentage;
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHP:F0} / {maxHP:F0}";
        }

        // 체력 비율에 따른 색상 변경
        UpdateHealthColor();

        // 페이즈 재계산
        int newPhase = (currentHP / maxHP > 0.5f) ? 1 : 2;
        if (newPhase != currentPhase)
        {
            UpdatePhase(newPhase);
        }
    }

    /// <summary>
    /// 체력 색상 업데이트
    /// </summary>
    private void UpdateHealthColor()
    {
        if (fillImage == null)
            return;

        float healthPercentage = currentHP / maxHP;

        if (healthPercentage > 0.5f)
        {
            fillImage.color = highHealthColor;
        }
        else if (healthPercentage > 0.25f)
        {
            fillImage.color = mediumHealthColor;
        }
        else
        {
            fillImage.color = lowHealthColor;
        }
    }

    /// <summary>
    /// 페이즈 업데이트
    /// </summary>
    private void UpdatePhase(int phase)
    {
        currentPhase = phase;

        if (phaseText != null)
        {
            phaseText.text = $"PHASE {phase}";
        }

        // 페이즈 전환 연출
        if (phaseIndicator != null)
        {
            StartCoroutine(PhaseTransitionAnimation());
        }
    }

    /// <summary>
    /// 페이즈 전환 애니메이션
    /// </summary>
    private System.Collections.IEnumerator PhaseTransitionAnimation()
    {
        // 깜빡임 효과
        Color originalColor = phaseIndicator.GetComponent<Image>()?.color ?? Color.white;

        for (int i = 0; i < 3; i++)
        {
            if (phaseIndicator.GetComponent<Image>() != null)
            {
                phaseIndicator.GetComponent<Image>().color = Color.yellow;
            }
            yield return new WaitForSeconds(0.1f);

            if (phaseIndicator.GetComponent<Image>() != null)
            {
                phaseIndicator.GetComponent<Image>().color = originalColor;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// 보스 사망 시 UI 비활성화
    /// </summary>
    public void OnBossDeath()
    {
        gameObject.SetActive(false);
    }
}
