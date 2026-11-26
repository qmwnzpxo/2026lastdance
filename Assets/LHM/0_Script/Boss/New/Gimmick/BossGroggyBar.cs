using UnityEngine;
using UnityEngine.UI;

namespace UnMachine.Boss
{
    /// <summary>
    /// 그로기 게이지 바 (T63 ✂️, 체력바 아래 얇은 바). 전투 시작 때 보이고 사망 때 숨긴다. spec: boss.md 15장
    /// </summary>
    /// <remarks>"낙섬!" 아이콘은 1주 축소로 삭제. Fill 이미지는 Image Type = Filled (Horizontal) 로 둔다.</remarks>
    [DisallowMultipleComponent]
    public sealed class BossGroggyBar : MonoBehaviour
    {
        [SerializeField] private BossGroggyDriver _groggy;
        [SerializeField] private BossController _boss;

        [Tooltip("켜고 끌 바 루트 (이 컴포넌트가 붙은 오브젝트와 달라도 됨)")]
        [SerializeField] private GameObject _root;

        [Tooltip("Image Type = Filled 인 채움 이미지")]
        [SerializeField] private Image _fill;

        private void Awake()
        {
            if (_groggy == null || _boss == null || _root == null || _fill == null)
            {
                GameLog.Error(this, "[BossGroggyBar] 참조 누락 → 비활성화");
                enabled = false;
                return;
            }

            if (_root == gameObject)
            {
                GameLog.Warn("[BossGroggyBar] Root 가 자기 자신이면 숨긴 뒤 다시 켤 수 없음 → 자식 오브젝트를 지정", this);
            }

            _root.SetActive(false);
        }

        private void OnEnable()
        {
            if (_boss != null)
            {
                _boss.BattleStarted += Show;
                _boss.Died += Hide;
            }
        }

        private void OnDisable()
        {
            if (_boss != null)
            {
                _boss.BattleStarted -= Show;
                _boss.Died -= Hide;
            }
        }

        private void LateUpdate()
        {
            if (_root.activeSelf)
            {
                _fill.fillAmount = _groggy.Normalized;
            }
        }

        private void Show()
        {
            _root.SetActive(true);
        }

        private void Hide()
        {
            _root.SetActive(false);
        }
    }
}
