using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unmachine.UIv5
{
    /// <summary>
    /// 기존 UI 를 "화면에서만" 숨긴다. 오브젝트 활성 상태, 스크립트 로직, 씬 파일은 그대로.
    /// 런타임에 CanvasGroup.alpha 만 0 으로 누르고, v5 를 끄면 원래대로 돌려놓는다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class V5LegacyMask : MonoBehaviour
    {
        CanvasGroup _group;
        bool _createdGroup;
        float _prevAlpha = 1f;
        bool _masking;

        static readonly List<V5LegacyMask> s_All = new List<V5LegacyMask>();

        public static void Apply(GameObject go)
        {
            if (go == null) return;
            var m = go.GetComponent<V5LegacyMask>();
            if (m == null) m = go.AddComponent<V5LegacyMask>();
            m.Engage();
        }

        public static void ReleaseAll()
        {
            for (int i = s_All.Count - 1; i >= 0; i--)
                if (s_All[i] != null) s_All[i].Release();
            s_All.Clear();
        }

        void Engage()
        {
            if (_masking) return;
            _group = GetComponent<CanvasGroup>();
            _createdGroup = _group == null;
            if (_createdGroup) _group = gameObject.AddComponent<CanvasGroup>();
            _prevAlpha = _group.alpha;
            _masking = true;
            _group.alpha = 0f;
            if (!s_All.Contains(this)) s_All.Add(this);
        }

        void Release()
        {
            if (!_masking) return;
            _masking = false;
            if (_group != null)
            {
                if (_createdGroup) Destroy(_group);
                else _group.alpha = _prevAlpha;
            }
            Destroy(this);
        }

        void LateUpdate()
        {
            if (_masking && _group != null && _group.alpha != 0f) _group.alpha = 0f;
        }

        void OnDestroy() { s_All.Remove(this); }
    }

    public static class V5Reflect
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>팀원 스크립트의 private 필드를 "읽기만" 한다. 이름이 바뀌면 null.</summary>
        public static T Get<T>(object target, string field) where T : class
        {
            if (target == null) return null;
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(field, F);
                if (fi != null) return fi.GetValue(target) as T;
            }
            return null;
        }

        public static bool TryGetBool(object target, string field, out bool value)
        {
            value = false;
            if (target == null) return false;
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(field, F);
                if (fi != null && fi.FieldType == typeof(bool)) { value = (bool)fi.GetValue(target); return true; }
            }
            return false;
        }

        public static bool TryGetFloat(object target, string field, out float value)
        {
            value = 0f;
            if (target == null) return false;
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(field, F);
                if (fi != null && fi.FieldType == typeof(float)) { value = (float)fi.GetValue(target); return true; }
            }
            return false;
        }
    }

    public static class V5LegacyFinder
    {
        /// <summary>
        /// anchor 와 refs 를 모두 품는 가장 가까운 공통 부모를 숨긴다.
        /// 그 부모가 대화창·시스템메시지 같은 남의 UI 까지 품고 있으면 한 단계씩 내려가 ref 가 든 가지만 숨긴다.
        /// </summary>
        public static void HideGroup(Component anchor, IList<Component> refs, System.Func<Transform, bool> isForeign)
        {
            var all = new List<Transform>();
            // 브리지가 UI 가 아닌 오브젝트(예: 플레이어)에 붙어 있으면 앵커에서 제외
            if (anchor != null && anchor.transform is RectTransform) all.Add(anchor.transform);
            if (refs != null) foreach (var r in refs) if (r != null && r.transform is RectTransform) all.Add(r.transform);
            if (all.Count == 0) return;

            Transform lca = all[0];
            for (int i = 1; i < all.Count && lca != null; i++) lca = CommonParent(lca, all[i]);
            if (lca == null) return;

            if (!isForeign(lca))
            {
                // 테두리·배경 같은 형제까지 같이 가리도록, 캔버스 바로 아래 묶음(예: Canvas/Boss Health Bar)까지 올라간다
                while (lca.parent != null && lca.GetComponent<Canvas>() == null
                       && lca.parent.GetComponent<Canvas>() == null && !isForeign(lca.parent))
                    lca = lca.parent;
                V5LegacyMask.Apply(lca.gameObject);
                return;
            }

            // 남의 UI 와 섞여 있음 → lca 바로 아래 가지 중 ref 가 든 것만
            var branches = new HashSet<Transform>();
            foreach (var t in all)
            {
                var b = t;
                while (b != null && b.parent != lca) b = b.parent;
                if (b != null) branches.Add(b);
            }
            foreach (var b in branches)
                if (!isForeign(b)) V5LegacyMask.Apply(b.gameObject);
                else foreach (var t in all) if (t.IsChildOf(b) && t != b) V5LegacyMask.Apply(t.gameObject);
        }

        static Transform CommonParent(Transform a, Transform b)
        {
            for (var t = a; t != null; t = t.parent)
                if (b == t || b.IsChildOf(t)) return t;
            return null;
        }
    }
}
