using UnityEngine;

[RequireComponent(typeof(EnemyArmAim))]
public class RangerEnemyCombat : EnemyCombat
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