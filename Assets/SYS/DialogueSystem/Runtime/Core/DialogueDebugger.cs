using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace DialogueSystem
{
    /// <summary>
    /// 씬 아무 오브젝트에나 붙이면 콘솔에 문제 원인 출력.
    /// 문제 해결 후 제거해도 됨.
    /// </summary>
    public class DialogueDebugger : MonoBehaviour
    {
        [Header("테스트할 대화 ID")]
        public string testDialogueId = "CUTSCENE_001";

        [Header("연결 확인용 참조")]
        public DialogueManager manager;
        public DialogueUI      panelUI;
        public TMP_Text        dialogueText;
        public GameObject      dialoguePanel;

        private void Start()
        {
            Debug.Log("=== [DialogueDebugger] 진단 시작 ===");

            // 1. DialogueManager 체크
            if (manager == null)
                manager = DialogueManager.Instance;

            if (manager == null)
            {
                Debug.LogError("[진단] ❌ DialogueManager가 씬에 없습니다.");
                return;
            }
            Debug.Log("[진단] ✅ DialogueManager 존재");

            // 2. DB & 엔트리 체크
            var db = manager.GetType()
                .GetField("database",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(manager) as DialogueDatabase;

            if (db == null)
            {
                Debug.LogError("[진단] ❌ DialogueDatabase가 Manager에 연결 안 됨.");
                return;
            }

            db.RebuildLookup();
            var all = db.GetAll();
            Debug.Log($"[진단] ✅ Database 엔트리 수: {all.Count}개");

            if (all.Count == 0)
            {
                Debug.LogError("[진단] ❌ Database가 비어 있습니다. CSV 임포트를 먼저 하세요.");
                return;
            }

            var entry = db.GetEntry(testDialogueId);
            if (entry == null)
            {
                Debug.LogError($"[진단] ❌ ID '{testDialogueId}' 를 Database에서 찾을 수 없습니다.");
                Debug.Log($"[진단]   등록된 ID 목록: {string.Join(", ", ListIds(all))}");
                return;
            }
            Debug.Log($"[진단] ✅ 엔트리 발견: [{entry.id}] speaker='{entry.speaker}' text='{Truncate(entry.rawText)}'");

            if (!string.IsNullOrEmpty(entry.conditionFlag))
                Debug.LogWarning($"[진단] ⚠ conditionFlag='{entry.conditionFlag}' — 이 플래그가 false면 대화가 즉시 스킵됩니다.");

            if (entry.autoAdvanceTime > 0f)
                Debug.Log($"[진단] ℹ autoAdvanceTime={entry.autoAdvanceTime}초 — 자동 진행됩니다.");

            // 3. PanelUI 체크
            if (panelUI == null)
            {
                var found = FindFirstObjectByType<DialogueUI>();
                if (found == null)
                    Debug.LogError("[진단] ❌ DialogueUI 컴포넌트를 씬에서 찾을 수 없습니다.");
                else
                {
                    panelUI = found;
                    Debug.Log($"[진단] ✅ DialogueUI 발견: {found.gameObject.name}");
                }
            }

            // 4. TMP Text 체크
            if (dialogueText == null && panelUI != null)
            {
                dialogueText = panelUI.GetComponentInChildren<TMP_Text>();
            }

            if (dialogueText == null)
                Debug.LogError("[진단] ❌ Dialogue Text (TMP_Text) 가 DialogueUI에 연결 안 됨. Inspector > Dialogue Text 확인.");
            else
                Debug.Log($"[진단] ✅ TMP_Text 연결 확인: {dialogueText.gameObject.name}");

            // 5. Panel GameObject 체크
            if (dialoguePanel == null && panelUI != null)
            {
                var panel = panelUI.transform.GetChild(0)?.gameObject;
                dialoguePanel = panel;
            }

            // 6. Manager의 PanelUI 연결 체크
            var managerPanelUI = manager.GetType()
                .GetField("panelUI",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(manager) as DialogueUI;

            if (managerPanelUI == null)
                Debug.LogError("[진단] ❌ DialogueManager > Panel UI 가 Inspector에서 연결 안 됨.");
            else
                Debug.Log("[진단] ✅ Manager.panelUI 연결 확인");

            Debug.Log("=== [DialogueDebugger] 진단 완료. 위 로그 확인 후 ❌ 항목 수정 ===");
        }

        private void Update()
        {
            // T 키로 언제든 테스트 대화 시작
            if (Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log($"[DialogueDebugger] T키 → StartDialogue('{testDialogueId}')");
                DialogueManager.Instance?.StartDialogue(testDialogueId);
            }
        }

        private static string Truncate(string s) =>
            s == null ? "(null)" : s.Length > 30 ? s[..30] + "…" : s;

        private static string ListIds(System.Collections.Generic.IReadOnlyList<DialogueEntry> all)
        {
            var ids = new System.Text.StringBuilder();
            for (int i = 0; i < Mathf.Min(all.Count, 10); i++)
            {
                if (i > 0) ids.Append(", ");
                ids.Append(all[i].id);
            }
            if (all.Count > 10) ids.Append($" ... 외 {all.Count - 10}개");
            return ids.ToString();
        }
    }
}
