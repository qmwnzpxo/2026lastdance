using UnityEngine;
using System;

[RequireComponent(typeof(EnemyArmAim))]
public class RangerEnemyCombat : EnemyCombat
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
        base.Shoot();
        armAim.TriggerRecoil();
        OnShootEvent?.Invoke();
    }





}