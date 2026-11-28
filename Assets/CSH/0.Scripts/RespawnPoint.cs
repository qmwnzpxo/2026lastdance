using System.Collections.Generic;
using UnityEngine;

public class RespawnPoint : MonoBehaviour
{
    private static readonly List<RespawnPoint> _points = new();

    public Vector2 Position => transform.position;

    /// <summary>현재 활성화된 모든 리스폰 지점 (구역 저장 SectionSave에서 사용)</summary>
    public static IReadOnlyList<RespawnPoint> All => _points;

    private void OnEnable() { _points.Add(this); }
    private void OnDisable() { _points.Remove(this); }

    public static RespawnPoint FindNearestLeft(float playerX)
    {
        RespawnPoint best = null;
        float bestX = float.NegativeInfinity;

        for (int i = 0; i < _points.Count; i++)
        {
            var p = _points[i];
            if (p == null) continue;

            float x = p.transform.position.x;
            if (x >= playerX) continue; // 왼쪽 조건

            if (x > bestX)
            {
                bestX = x;
                best = p;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
#endif
}