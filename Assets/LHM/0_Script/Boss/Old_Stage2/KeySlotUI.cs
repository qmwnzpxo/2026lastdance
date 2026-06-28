// ═══════════════════════════════════════════════════════════════════════
// KeySlotUI — 키 한 칸 슬롯
// ═══════════════════════════════════════════════════════════════════════
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeySlotUI : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _keyText;

    public void Setup(string key, Color textColor)
    {
        if (_keyText != null) _keyText.text = key;
        if (_keyText != null) _keyText.color = textColor;
    }

    /// <summary>단어(텍스트) 색상 변경. 정답=초록, 오답=빨간, 대기=흰색/회색.</summary>
    public void SetColor(Color color)
    {
        if (_keyText != null) _keyText.color = color;
    }
}
