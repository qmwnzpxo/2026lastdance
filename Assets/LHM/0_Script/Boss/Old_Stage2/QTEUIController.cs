using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BossSystem
{
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
        [SerializeField] private Color _failColor = Color.red;
        [SerializeField] private Color _resetColor = Color.yellow;

        [Header("Slot Colors")]
        [SerializeField] private Color _idleColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _activeColor = Color.white;
        [SerializeField] private Color _correctColor = Color.green;
        [SerializeField] private Color _wrongColor = Color.red;

        // ── 런타임 ───────────────────────────────────────────────────────
        private readonly List<KeySlotUI> _slots = new(8);
        private float _maxTime;
        private float _elapsed;
        private bool _isRunning;

        // 현재 맞춘 step 수 기억 (ShowWrong 시 초록 유지용)
        private int _currentStep = 0;

        // ─────────────────────────────────────────────────────────────────
        // 공개 API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>QTE 팝업을 열고 키 배열을 표시.</summary>
        public void Show(string[] keys, float timeLimit)
        {
            _maxTime = timeLimit;
            _elapsed = 0f;
            _isRunning = true;
            _currentStep = 0;

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
                _timerBar.value = 1f;
            }

            if (_titleText != null)
                _titleText.text = _titleString;

            if (_resultText != null)
                _resultText.text = "";

            if (_qtePanel != null)
                _qtePanel.SetActive(true);
        }

        /// <summary>
        /// 현재 진행 인덱스 하이라이트.
        /// step 이전 = 초록(정답), step = 흰색(현재 입력 대기), 이후 = 회색
        /// </summary>
        public void UpdateProgress(int step)
        {
            _currentStep = step;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < step)
                    _slots[i].SetColor(_correctColor);  // 이미 맞춤 → 초록
                else if (i == step)
                    _slots[i].SetColor(_activeColor);   // 현재 입력 대상 → 흰색
                else
                    _slots[i].SetColor(_idleColor);     // 아직 미입력 → 회색
            }

            if (_resultText != null)
                _resultText.text = "";
        }

        /// <summary>
        /// 틀렸을 때:
        ///   - 이미 맞춘 칸들은 초록 유지
        ///   - 틀린 칸만 빨간색
        ///   - "reset!" 텍스트 표시
        ///   - 1초 후 전체 리셋 연출은 NarrowingQTETrigger에서 UpdateProgress(0) 호출
        /// </summary>
        public void ShowWrong(int wrongStep)
        {
            // 이미 맞춘 칸 초록 유지
            for (int i = 0; i < wrongStep; i++)
                _slots[i].SetColor(_correctColor);

            // 틀린 칸 빨간색
            if (wrongStep >= 0 && wrongStep < _slots.Count)
                _slots[wrongStep].SetColor(_wrongColor);

            // 나머지는 회색
            for (int i = wrongStep + 1; i < _slots.Count; i++)
                _slots[i].SetColor(_idleColor);

            if (_resultText != null)
            {
                _resultText.color = _resetColor;
                _resultText.text = "reset!";
            }
        }

        /// <summary>성공 연출 — 전부 초록.</summary>
        public void ShowSuccess()
        {
            _isRunning = false;
            foreach (var slot in _slots)
                slot.SetColor(_correctColor);

            if (_resultText != null)
            {
                _resultText.color = _successColor;
                _resultText.text = "clear!";
            }
        }

        /// <summary>실패 연출 — 전부 빨간색.</summary>
        public void ShowFail()
        {
            _isRunning = false;
            foreach (var slot in _slots)
                slot.SetColor(_wrongColor);

            if (_resultText != null)
            {
                _resultText.color = _failColor;
                _resultText.text = "fail!";
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
}
