using UnityEngine;

public class RangerEnemyState : EnemyState
{
    private RangerEnemyAnimation _animController;
    private EnemyArmAim _armAim;

    protected override void Awake()
    {
        base.Awake();
        // 애니메이션 컨트롤러 연결
        _animController = GetComponentInChildren<RangerEnemyAnimation>();
        _armAim = GetComponent<EnemyArmAim>();
    }

    // (예시) 부모 클래스에 피격 함수가 있다면 오버라이드하여 연결
    /*
    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        if (_animController != null)
        {
            _animController.TriggerHit();
        }
    }
    */

    public override void Die()
    {
        if (_armAim != null)
        {
            if (_armAim.armRenderer != null)
            {
                _armAim.armRenderer.enabled = false;
            }
            _armAim.enabled = false; // 죽은 상태에서 팔이 플레이어를 추적하지 않도록 끔
        }
        // 1. 사망 애니메이션 실행
        if (_animController != null)
        {
            _animController.SetDead(true);
        }

        // 2. 부모 클래스의 기본 사망 처리 (콜라이더 끄기, 아이템 드랍 등)
        base.Die();

        // 주의: base.Die() 안에 Destroy(gameObject)가 바로 있다면 애니메이션이 재생되기도 전에 사라집니다.
        // 사망 애니메이션을 볼 수 있도록 Destroy 시간을 늦추거나 코루틴을 사용해야 합니다.
    }
}