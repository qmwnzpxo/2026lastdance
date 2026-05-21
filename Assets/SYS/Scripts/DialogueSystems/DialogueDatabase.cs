using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// CSV 파일 하나를 감싸는 ScriptableObject.
    /// Project 창 우클릭 → Create → DialogueSystem → Database 로 생성.
    /// Inspector에서 CSV TextAsset을 연결하세요.
    /// </summary>
    [CreateAssetMenu(menuName = "DialogueSystem/Database", fileName = "NewDialogueDatabase")]
    public class DialogueDatabase : ScriptableObject
    {
        [SerializeField] private TextAsset csvFile;

        private Dictionary<string, DialogueEntry> _entries;
        private string _firstId; // Dictionary는 순서 미보장이므로 따로 저장

        public void Load()
        {
            if (csvFile == null)
            {
                Debug.LogError($"[DialogueDatabase] {name} — CSV 파일이 연결되지 않았습니다.");
                return;
            }
            _firstId = null;
            _entries = DialogueParser.ParseCSV(csvFile.text, out _firstId);
            Debug.Log($"[DialogueDatabase] {name} — {_entries.Count}개 항목 로드 완료");
        }

        public DialogueEntry Get(string id)
        {
            if (_entries == null) Load();
            if (_entries == null) return null;

            if (_entries.TryGetValue(id, out var entry)) return entry;

            Debug.LogWarning($"[DialogueDatabase] '{id}' 항목을 찾을 수 없습니다.");
            return null;
        }

        public string FirstId()
        {
            if (_entries == null) Load();
            return _firstId;
        }

#if UNITY_EDITOR
        private void OnValidate() => _entries = null;
#endif
    }
}
