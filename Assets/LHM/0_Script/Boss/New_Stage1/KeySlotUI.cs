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
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
            Debug.Log("[QTE] G키 눌림 감지");
    }
    public void Setup(string key, Color bgColor)
    {
        if (_keyText != null) _keyText.text = key;
        if (_background != null) _background.color = bgColor;
    }

    public void SetColor(Color color)
    {
        if (_background != null) _background.color = color;
    }
}