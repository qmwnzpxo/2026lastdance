#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// Tools > Dialogue System > CSV Importer 로 열기.
    ///
    /// ─ CSV 파일 선택 → DialogueDatabase ScriptableObject에 임포트
    /// ─ 미리보기 테이블로 데이터 확인
    /// ─ 빠른 유효성 검사 (ID 중복, 끊긴 nextId 체크)
    /// </summary>
    public class DialogueEditorWindow : EditorWindow
    {
        // ── 탭 ─────────────────────────────────────────────────────────────────
        private enum Tab { Import, Validate, Help }
        private Tab _tab = Tab.Import;

        // ── 임포트 탭 상태 ─────────────────────────────────────────────────────
        private DialogueDatabase _database;
        private string           _csvPath  = "";
        private string           _status   = "";
        private bool             _isError;

        // ── 검증 탭 상태 ───────────────────────────────────────────────────────
        private Vector2 _scroll;

        // ── 메뉴 등록 ──────────────────────────────────────────────────────────
        [MenuItem("Tools/Dialogue System/CSV Importer")]
        public static void Open() =>
            GetWindow<DialogueEditorWindow>("📖 Dialogue Importer");

        // ── GUI ────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();
            GUILayout.Space(8);

            switch (_tab)
            {
                case Tab.Import:   DrawImportTab();   break;
                case Tab.Validate: DrawValidateTab(); break;
                case Tab.Help:     DrawHelpTab();     break;
            }
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            GUILayout.Space(6);
            GUILayout.Label("Dialogue System — CSV Importer", style);
            GUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            DrawTab(Tab.Import,   "📥 임포트");
            DrawTab(Tab.Validate, "✅ 검증");
            DrawTab(Tab.Help,     "❓ 도움말");
            GUILayout.EndHorizontal();
        }

        private void DrawTab(Tab t, string label)
        {
            var style = _tab == t
                ? new GUIStyle(EditorStyles.miniButtonMid) { fontStyle = FontStyle.Bold }
                : EditorStyles.miniButtonMid;
            if (GUILayout.Button(label, style)) _tab = t;
        }

        // ── 임포트 탭 ──────────────────────────────────────────────────────────
        private void DrawImportTab()
        {
            GUILayout.Label("① 대상 Database 선택", EditorStyles.boldLabel);
            _database = (DialogueDatabase)EditorGUILayout.ObjectField(
                "DialogueDatabase", _database, typeof(DialogueDatabase), false);

            GUILayout.Space(8);
            GUILayout.Label("② CSV 파일 선택", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            _csvPath = EditorGUILayout.TextField("경로", _csvPath);
            if (GUILayout.Button("찾기", GUILayout.Width(56)))
                _csvPath = EditorUtility.OpenFilePanel("CSV 파일 선택", Application.dataPath, "csv");
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            using (new EditorGUI.DisabledGroupScope(_database == null || string.IsNullOrEmpty(_csvPath)))
            {
                if (GUILayout.Button("▶  CSV → Database 임포트", GUILayout.Height(32)))
                    DoImport();
            }

            GUILayout.Space(6);
            if (!string.IsNullOrEmpty(_status))
            {
                var msgType = _isError ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(_status, msgType);
            }

            // 빠른 Database 생성 버튼
            GUILayout.Space(12);
            GUILayout.Label("빠른 작업", EditorStyles.boldLabel);
            if (GUILayout.Button("새 DialogueDatabase 생성"))
                CreateNewDatabase();
        }

        private void DoImport()
        {
            if (!File.Exists(_csvPath))
            {
                SetStatus("CSV 파일을 찾을 수 없습니다.", true);
                return;
            }

            var entries = CSVDialogueLoader.LoadFromFile(_csvPath);
            _database.SetEntries(entries);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            SetStatus($"✅ {entries.Count}개 항목 임포트 완료!", false);
            _tab = Tab.Validate; // 임포트 후 자동으로 검증 탭으로
        }

        private void CreateNewDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "새 DialogueDatabase", "NewDialogueDatabase", "asset",
                "저장 위치를 선택하세요");
            if (string.IsNullOrEmpty(path)) return;

            var db = CreateInstance<DialogueDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            _database = db;
            EditorGUIUtility.PingObject(db);
        }

        // ── 검증 탭 ────────────────────────────────────────────────────────────
        private void DrawValidateTab()
        {
            if (_database == null)
            {
                EditorGUILayout.HelpBox("임포트 탭에서 Database를 선택하세요.", MessageType.Warning);
                return;
            }

            GUILayout.Label($"Database: {_database.name}", EditorStyles.boldLabel);
            var all    = _database.GetAll();
            var issues = new System.Collections.Generic.List<string>();

            // ID 중복 체크
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var e in all)
            {
                if (!ids.Add(e.id))
                    issues.Add($"⚠ 중복 ID: '{e.id}'");
            }

            // 끊긴 nextId 체크
            foreach (var e in all)
            {
                if (!string.IsNullOrEmpty(e.nextId) && !ids.Contains(e.nextId))
                    issues.Add($"⚠ '{e.id}' → nextId '{e.nextId}' 가 Database에 없음");

                if (e.choices != null)
                    foreach (var c in e.choices)
                        if (!string.IsNullOrEmpty(c.nextId) && !ids.Contains(c.nextId))
                            issues.Add($"⚠ '{e.id}' 선택지 '{c.text}' → nextId '{c.nextId}' 없음");
            }

            if (issues.Count == 0)
                EditorGUILayout.HelpBox($"✅ 문제 없음 — {all.Count}개 항목", MessageType.Info);
            else
                EditorGUILayout.HelpBox($"{issues.Count}개 문제 발견", MessageType.Warning);

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var issue in issues)
                EditorGUILayout.HelpBox(issue, MessageType.Warning);

            GUILayout.Space(8);
            GUILayout.Label("전체 엔트리 목록", EditorStyles.boldLabel);
            foreach (var e in all)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"[{e.id}]", GUILayout.Width(130));
                GUILayout.Label(e.speaker, GUILayout.Width(80));
                GUILayout.Label(e.rawText?.Length > 40
                    ? e.rawText[..40] + "…" : e.rawText);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        // ── 도움말 탭 ──────────────────────────────────────────────────────────
        private void DrawHelpTab()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(GetHelpText(), MessageType.None);
            GUILayout.EndScrollView();
        }

        private static string GetHelpText() =>
@"━━ CSV 컬럼 순서 ━━━━━━━━━━━━━━━━━━━━━━━━━━━
0  ID          고유 식별자   (예: NPC001_001)
1  Speaker     화자 이름
2  Portrait    포트레이트 키 (Resources/Portraits/ 하위)
3  Text        대화 텍스트 (쌍따옴표로 감싸기 권장)
4  Next        다음 ID (비워두면 종료)
5  Choices     선택지 ""표시|ID;표시2|ID2"" 형식
6  BGM         BGM 키
7  Voice       보이스 키
8  CameraFX    none / shake / zoom_in / zoom_out
9  AutoTime    자동 진행 초 (0=수동)
10 Condition   표시 조건 플래그
11 SetFlag     대화 후 세팅할 플래그

━━ 텍스트 이펙트 태그 ━━━━━━━━━━━━━━━━━━━━━━━
[shake]...[/shake]        글자 흔들기
[wave]...[/wave]          물결 움직임
[rainbow]...[/rainbow]    무지개 색상 순환
[bigpop]...[/bigpop]      커졌다 줄어드는 팝
[break]...[/break]        산산조각
[fast]...[/fast]          타이프라이터 빠르게
[slow]...[/slow]          타이프라이터 느리게
[pause=1.5]               1.5초 대기
[color=#FF6600]...[/color] 색상 지정
[size=150]...[/size]      글자 크기 (%)
[b]...[/b]                볼드
[i]...[/i]                이탤릭
[n]                       줄바꿈

━━ 주석 처리 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
행 맨 앞에 # 또는 // 입력 시 무시됩니다.

━━ Timeline 사용 ━━━━━━━━━━━━━━━━━━━━━━━━━━━
1. Timeline 창 열기
2. 트랙 추가 > Dialogue Track
3. 트랙 위 우클릭 > Add Dialogue Clip
4. 클립 선택 → Inspector에서 Start Dialogue ID 입력";

        private void SetStatus(string msg, bool isError)
        {
            _status  = msg;
            _isError = isError;
        }
    }
}
#endif
