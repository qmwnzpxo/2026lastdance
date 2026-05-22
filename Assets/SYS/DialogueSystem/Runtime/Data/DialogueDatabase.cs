using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 대화 데이터를 저장하는 ScriptableObject.
    /// 우클릭 > Create > Dialogue System > Database 로 생성.
    /// CSV 임포터나 DialogueManager.LoadCSV() 로 채울 수 있음.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogueDatabase",
                     menuName  = "Dialogue System/Database")]
    public class DialogueDatabase : ScriptableObject
    {
        [SerializeField]
        private List<DialogueEntry> entries = new();

        private Dictionary<string, DialogueEntry> _lookup;
        private bool _isDirty = true;

        // ── 외부 접근 ──────────────────────────────────────────

        /// <summary>엔트리 목록 교체 (CSV 임포트 시 사용)</summary>
        public void SetEntries(List<DialogueEntry> list)
        {
            entries = list;
            _isDirty = true;
        }

        /// <summary>ID로 대화 엔트리 조회</summary>
        public DialogueEntry GetEntry(string id)
        {
            if (_isDirty) Rebuild();
            return _lookup.TryGetValue(id, out var e) ? e : null;
        }

        /// <summary>전체 엔트리 읽기 전용 접근</summary>
        public IReadOnlyList<DialogueEntry> GetAll()
        {
            if (_isDirty) Rebuild();
            return entries;
        }

        /// <summary>인스펙터에서 값 변경 시 수동 재빌드</summary>
        public void RebuildLookup() => Rebuild();

        // ── 내부 ───────────────────────────────────────────────

        private void Rebuild()
        {
            _lookup = new Dictionary<string, DialogueEntry>(entries.Count);
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.id))
                    _lookup[e.id] = e;
            _isDirty = false;
        }

        private void OnValidate() => _isDirty = true;
    }
}
