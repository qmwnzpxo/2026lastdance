using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 트리거 콜라이더를 무시하는 물리 쿼리 모음.
/// 프로젝트 설정의 Queries Hit Triggers 가 켜져 있어서, ground 레이어에 놓인 트리거
/// (락다운 트리거 등)가 벽·바닥·천장으로 오인되는 것을 막는다.
/// 시그니처는 Physics2D 기본 함수와 같게 맞춰서 호출부를 그대로 바꿔 쓸 수 있다.
/// </summary>
public static class SolidPhysics
{
    private static readonly List<RaycastHit2D> _hits = new List<RaycastHit2D>(8);
    private static readonly List<Collider2D> _cols = new List<Collider2D>(8);

    private static ContactFilter2D Filter(int layerMask)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(layerMask);
        return filter;
    }

    // 결과 중 가장 가까운 히트 반환 (없으면 default → collider == null)
    private static RaycastHit2D Closest()
    {
        RaycastHit2D best = default;
        float bestDist = float.MaxValue;
        foreach (var h in _hits)
        {
            if (h.collider == null || h.distance >= bestDist) continue;
            best = h;
            bestDist = h.distance;
        }
        return best;
    }

    public static RaycastHit2D Raycast(Vector2 origin, Vector2 direction, float distance, int layerMask)
    {
        _hits.Clear();
        Physics2D.Raycast(origin, direction, Filter(layerMask), _hits, distance);
        return Closest();
    }

    public static RaycastHit2D CircleCast(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask)
    {
        _hits.Clear();
        Physics2D.CircleCast(origin, radius, direction, Filter(layerMask), _hits, distance);
        return Closest();
    }

    /// <summary>
    /// CircleCast 결과 중 바닥으로 볼 수 있는 면(법선 y >= minNormalY)만 골라 가장 가까운 히트 반환.
    /// 벽에 붙은 채 아래로 캐스트하면 벽이 거리 0으로 먼저 잡혀 바닥으로 오인되는 것을 막는다.
    /// minNormalY 0.5 = 경사 60°까지 바닥으로 인정.
    /// </summary>
    public static RaycastHit2D CircleCastFloor(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask, float minNormalY = 0.5f)
    {
        _hits.Clear();
        Physics2D.CircleCast(origin, radius, direction, Filter(layerMask), _hits, distance);

        RaycastHit2D best = default;
        float bestDist = float.MaxValue;
        foreach (var h in _hits)
        {
            if (h.collider == null || h.normal.y < minNormalY || h.distance >= bestDist) continue;
            best = h;
            bestDist = h.distance;
        }
        return best;
    }

    public static Collider2D OverlapBox(Vector2 point, Vector2 size, float angle, int layerMask)
    {
        _cols.Clear();
        Physics2D.OverlapBox(point, size, angle, Filter(layerMask), _cols);
        return _cols.Count > 0 ? _cols[0] : null;
    }
}
