using System.Collections.Generic;
using UnityEngine;

namespace BossSystem
{
    // ═══════════════════════════════════════════════════════════════════════
    // ObjectPool.cs 
    //═══════════════════════════════════════════════════════════════════════
    public class ObjectPool 
    {
        // 프리팹 InstanceID → 풀 스택
        private static readonly Dictionary<int, Stack<GameObject>> _pools   = new(16);
        // 인스턴스 InstanceID → 원본 프리팹 InstanceID (Return 시 풀 역조회)
        private static readonly Dictionary<int, int>               _origins = new(64);

       
        public static void Preload(GameObject prefab, Transform parent, int count)
        {
            if (prefab == null) return;

            int key = prefab.GetInstanceID();
            if (!_pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<GameObject>(count);
                _pools[key] = stack;
            }

            for (int i = 0; i < count; i++)
            {
                var go = Object.Instantiate(prefab, parent);
                go.SetActive(false);
                _origins[go.GetInstanceID()] = key;
                stack.Push(go);
            }

            Debug.Log($"[ObjectPool] Preloaded {count}x '{prefab.name}'");
        }

        /// <summary>
        /// 풀에서 꺼내 활성화. 풀이 비어있으면 새로 Instantiate.
        /// IPoolable이면 Reset() 자동 호출.
        /// </summary>
        public static GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot,
                                     Transform parent = null)
        {
            if (prefab == null) return null;

            int key = prefab.GetInstanceID();

            GameObject go;
            if (_pools.TryGetValue(key, out var stack) && stack.Count > 0)
            {
                go = stack.Pop();
                go.transform.SetPositionAndRotation(pos, rot);
                if (parent != null) go.transform.SetParent(parent);
            }
            else
            {
                // 풀 미등록 또는 고갈 → 폴백 Instantiate
                go = Object.Instantiate(prefab, pos, rot, parent);
                _origins[go.GetInstanceID()] = key;
                if (!_pools.ContainsKey(key))
                    _pools[key] = new Stack<GameObject>(4);
            }

            // IPoolable 리셋 (HP, 플래그 등 초기화)
            //foreach (var poolable in go.GetComponentsInChildren<IPoolable>()) 
                //poolable.Reset();

            go.SetActive(true);
            return go;
        }

        /// <summary>
        /// 풀에 반환. SetActive(false) 후 스택에 Push.
        /// 등록되지 않은 오브젝트는 일반 Destroy로 폴백.
        /// </summary>
        public static void Return(GameObject go)
        {
            if (go == null) return;

            int instanceId = go.GetInstanceID();
            if (_origins.TryGetValue(instanceId, out int prefabKey)
                && _pools.TryGetValue(prefabKey, out var stack))
            {
                go.SetActive(false);
                go.transform.SetParent(null); // 씬 루트로 이동 (부모 파괴 방지)
                stack.Push(go);
            }
            else
            {
                // 풀 미등록 → 일반 파괴 (하위 호환)
                Object.Destroy(go);
            }
        }

        /// <summary>
        /// 오브젝트가 풀에 등록된 프리팹 인스턴스인지 확인.
        /// LaserCore.Die()에서 Return vs Destroy 분기에 사용.
        /// </summary>
        public static bool HasPool(GameObject go)
        {
            if (go == null) return false;
            return _origins.ContainsKey(go.GetInstanceID());
        }

        /// <summary>씬 전환 / 테스트 시 전체 풀 초기화.</summary>
        public static void ClearAll()
        {
            foreach (var stack in _pools.Values)
                while (stack.Count > 0)
                {
                    var go = stack.Pop();
                    if (go != null) Object.Destroy(go);
                }

            _pools.Clear();
            _origins.Clear();
            Debug.Log("[ObjectPool] All pools cleared.");
        }

        // ─────────────────────────────────────────────────────────────────
        // LineRenderer 전용 풀 헬퍼
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// LineRenderer 컴포넌트를 가진 오브젝트를 풀에서 꺼내 반환.
        /// 경고선(warningLine)처럼 빈번하게 Get/Return하는 경우에 사용.
        /// </summary>
        public static LineRenderer GetLineRenderer(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var go = Get(prefab, pos, rot);
            return go != null ? go.GetComponent<LineRenderer>() : null;
        }
    }
}
