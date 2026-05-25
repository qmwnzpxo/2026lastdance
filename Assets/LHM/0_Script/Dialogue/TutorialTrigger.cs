// ──────────────────────────────────────────────────────────────────────────
// TutorialTrigger.cs
// Collider2D(isTrigger=true)가 붙은 오브젝트에 부착.
// 플레이어가 진입하면 TutorialManager에 진입 이벤트를 전달.
// ──────────────────────────────────────────────────────────────────────────
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TutorialTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager _tutorialManager;

    [Header("Settings")]
    [Tooltip("트리거 감지에 사용할 레이어 태그")]
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("true면 최초 1회만 발동 후 비활성화")]
    [SerializeField] private bool _triggerOnce = true;

    private bool _triggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered) return;
        if (!other.CompareTag(_playerTag)) return;

        if (_triggerOnce)
            _triggered = true;

        _tutorialManager.OnPlayerEnterTrigger();
    }
}
