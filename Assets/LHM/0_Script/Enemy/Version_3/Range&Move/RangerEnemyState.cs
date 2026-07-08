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

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage); // 부모 클래스에서 데미지 계산 및 체력 차감

        // 체력이 남아있을 때만(죽지 않았을 때만) 피격 모션을 재생합니다.
        if (currentHp > 0 && _animController != null)
        {
            _animController.TriggerHit();
        }
    }

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