using UnityEngine;
using System;

public class TurretEnemyCombat : EnemyCombat
{

    [SerializeField] EnemyArmAim armAim;
    public event Action OnShootEvent;

    public override void Initialize(Transform playerTransform)
    {
        base.Initialize(playerTransform);
        if (armAim == null)
            armAim = GetComponent<EnemyArmAim>();
    }

    protected override void Shoot()
    {
        // 포신이 플레이어를 겨눌 수 없는 상황(사거리·각도 밖, 발사구 앞 지형)이면 발사하지 않음
        if (armAim is TurretArmAim turretAim && !turretAim.CanShootAtPlayer())
            return;

        base.Shoot();
        armAim.TriggerRecoil();
        OnShootEvent?.Invoke();
    }


}