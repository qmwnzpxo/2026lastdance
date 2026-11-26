using UnityEngine;

namespace UnMachine.Boss
{
    /// <summary>
    /// P1 착지 예정 지점 그림자. 추적 중 색 / 고정(마지막 회피 기회) 색을 바꾼다. spec: boss.md 6장 P1
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossShadowMarker : MonoBehaviour
    {
        [Tooltip("비우면 자신·자식에서 찾는다")]
        [SerializeField] private SpriteRenderer _renderer;

        [SerializeField] private Color _trackingColor = new Color(0f, 0f, 0f, 0.5f);
        [SerializeField] private Color _lockedColor = new Color(0.6f, 0f, 0f, 0.7f);

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        /// <summary>보이게 하고 위치·색을 맞춘다. 매 프레임 호출해도 된다.</summary>
        public void Show(float x, float groundY, bool isLocked)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.position = new Vector3(x, groundY, transform.position.z);
            if (_renderer != null)
            {
                _renderer.color = isLocked ? _lockedColor : _trackingColor;
            }
        }

        /// <summary>숨긴다.</summary>
        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
