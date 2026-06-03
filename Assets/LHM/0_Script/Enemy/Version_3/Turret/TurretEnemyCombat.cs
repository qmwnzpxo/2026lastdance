using UnityEngine;

public class TurretEnemyCombat : EnemyCombat
{

    [SerializeField] EnemyArmAim armAim;


    public override void Initialize(Transform playerTransform)
    {
        base.Initialize(playerTransform);
        if (armAim == null)
            armAim = GetComponent<EnemyArmAim>();
    }

    protected override void Shoot()
    {
        base.Shoot();
        armAim.TriggerRecoil();
    }


}