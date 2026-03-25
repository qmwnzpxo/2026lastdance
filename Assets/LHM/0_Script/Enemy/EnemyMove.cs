using System.Collections;
using UnityEngine;

public class EnemyMove : EnemyStat
{ 
    IEnumerator EnemyNormalmove()
    {
        //이동로직

        yield return null;
    }
    IEnumerator EnemyAttackmove()
    {
        //추적이동로직

        yield return null;
    }
    IEnumerator EnemyAroundmove()
    {
        //순찰이동로직

        yield return null;
    }
}
