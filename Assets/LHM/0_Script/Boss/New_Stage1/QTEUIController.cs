using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // QTEUIController.cs — 화면 중앙 QTE 팝업 UI
    //
    // 프리팹 구조 (Canvas 하위):
    //   QTEPanel (GameObject)
    //     ├── Background (Image) — 반투명 패널
    //     ├── TitleText (TMP)    — "!! 패턴 파훼 !!" 등 타이틀
    //     ├── TimerBar (Slider)  — 남은 시간 게이지
    //     ├── KeyContainer (HorizontalLayoutGroup)
    //     │     └── KeySlot x8  — 각 키를 표시하는 슬롯
    //     └── ResultText (TMP)  — 성공 / 실패 메시지
    //
    // KeySlot 구조:
    //   KeySlot (GameObject)
    //     ├── Background (Image) — 슬롯 배경
    //     └── KeyText (TMP)      — "W" / "A" / "S" / "D"
    // ═══════════════════════════════════════════════════════════════════════
    public class QTEUIController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _qtePanel;

        [Header("Title")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private string _titleString = "!! 패턴 파훼 !!";

        [Header("Timer")]
        [SerializeField] private Slider _timerBar;

        [Header("Key Slots")]
        [Tooltip("KeySlot 프리팹. KeySlotUI 컴포넌트 필요.")]
        [SerializeField] private KeySlotUI _keySlotPrefab;
        [SerializeField] private Transform _keyContainer;

        [Header("Result")]
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private Color _successColor = Color.green;
        [SerializeField] private Color _failColor    = Color.red;
        [SerializeField] private Color _resetColor   = Color.yellow;

        // ── 런타임 ───────────────────────────────────────────────────────
        private readonly List<KeySlotUI> _slots = new(8);
        private float _maxTime;
        private float _elapsed;
        private bool  _isRunning;

        // ── 슬롯 상태 색상 ────────────────────────────────────────────────
        [Header("Slot Colors")]
        [SerializeField] private Color _idleColor    = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _activeColor  = Color.white;
        [SerializeField] private Color _correctColor = Color.green;
        [SerializeField] private Color _wrongColor   = Color.red;

        // ─────────────────────────────────────────────────────────────────
        // 공개 API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>QTE 팝업을 열고 키 배열을 표시.</summary>
        public void Show(string[] keys, float timeLimit)
        {
            _maxTime = timeLimit;
            _elapsed = 0f;
            _isRunning = true;

            // 슬롯 생성
            ClearSlots();
            foreach (var key in keys)
            {
                var slot = Instantiate(_keySlotPrefab, _keyContainer);
                slot.Setup(key, _idleColor);
                _slots.Add(slot);
            }

            if (_timerBar != null)
            {
                _timerBar.minValue = 0f;
                _timerBar.maxValue = 1f;
                _timerBar.value    = 1f;
            }

            if (_titleText != null)
                _titleText.text = _titleString;

            if (_resultText != null)
                _resultText.text = "";

            if (_qtePanel != null)
                _qtePanel.SetActive(true);
        }

        /// <summary>현재 진행 인덱스 하이라이트. step = 다음에 눌러야 할 슬롯 인덱스.</summary>
        public void UpdateProgress(int step)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < step)
                    _slots[i].SetColor(_correctColor);   // 이미 맞춤
                else if (i == step)
                    _slots[i].SetColor(_activeColor);    // 현재 입력 대상
                else
                    _slots[i].SetColor(_idleColor);      // 아직 미입력
            }
        }

        /// <summary>틀렸을 때 짧게 빨간색 플래시 후 초기화 표시.</summary>
        public void ShowWrong(int wrongStep)
        {
            if (wrongStep >= 0 && wrongStep < _slots.Count)
                _slots[wrongStep].SetColor(_wrongColor);

            if (_resultText != null)
            {
                _resultText.color = _resetColor;
                _resultText.text  = "처음부터!";
            }
        }

        /// <summary>성공 연출.</summary>
        public void ShowSuccess()
        {
            _isRunning = false;
            foreach (var slot in _slots)
                slot.SetColor(_correctColor);

            if (_resultText != null)
            {
                _resultText.color = _successColor;
                _resultText.text  = "파훼 성공!";
            }
        }

        /// <summary>실패 연출 (시간 초과 or 벽 압사).</summary>
        public void ShowFail()
        {
            _isRunning = false;
            foreach (var slot in _slots)
                slot.SetColor(_wrongColor);

            if (_resultText != null)
            {
                _resultText.color = _failColor;
                _resultText.text  = "실패!";
            }
        }

        /// <summary>팝업 닫기.</summary>
        public void Hide()
        {
            _isRunning = false;
            if (_qtePanel != null)
                _qtePanel.SetActive(false);
            ClearSlots();
        }

        // ─────────────────────────────────────────────────────────────────
        // 타이머 갱신
        // ─────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_isRunning || _maxTime <= 0f) return;

            _elapsed += Time.deltaTime;
            if (_timerBar != null)
                _timerBar.value = Mathf.Clamp01(1f - _elapsed / _maxTime);
        }

        // ─────────────────────────────────────────────────────────────────
        // 내부
        // ─────────────────────────────────────────────────────────────────

        private void ClearSlots()
        {
            foreach (var slot in _slots)
                if (slot != null) Destroy(slot.gameObject);
            _slots.Clear();
        }

        private void Awake()
        {
            if (_qtePanel != null)
                _qtePanel.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KeySlotUI — 키 한 칸 슬롯
    // ═══════════════════════════════════════════════════════════════════════
    public class KeySlotUI : MonoBehaviour
    {
        [SerializeField] private Image    _background;
        [SerializeField] private TMP_Text _keyText;

        public void Setup(string key, Color bgColor)
        {
            if (_keyText != null)    _keyText.text  = key;
            if (_background != null) _background.color = bgColor;
        }

        public void SetColor(Color color)
        {
            if (_background != null) _background.color = color;
        }
    }
}
