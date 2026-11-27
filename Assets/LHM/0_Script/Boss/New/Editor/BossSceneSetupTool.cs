using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnMachine.Boss;
using Object = UnityEngine.Object;

namespace UnMachine.EditorTools
{
    /// <summary>
    /// 지금 열린 씬의 보스에 v2 흐름 컴포넌트를 붙이고 참조를 연결한다 (BACKLOG T36 ✂️ 자동화).
    /// Driver·예고 View·콤보 신호 View·그로기 Driver + Config 4종 + BossController 의 Flow Driver·Use Flow V2.
    /// </summary>
    /// <remarks>
    /// AI_RULES 0장 2번 예외 (현민 허락 2026-11-27): 열린 씬의 오브젝트만 Undo 가능하게 수정하고 Dirty 로 표시한다.
    /// 씬을 열거나 저장하지 않는다 (저장은 현민). 프리팹 원본은 건드리지 않는다 (씬 인스턴스 오버라이드).
    /// 이미 값이 들어 있는 칸은 덮어쓰지 않는다.
    /// </remarks>
    public static class BossSceneSetupTool
    {
        private const string SetupMenu = "UnMachine/Boss Setup/열린 씬 보스에 v2 연결";
        private const string CheckMenu = "UnMachine/Boss Setup/연결 상태 검사";

        private const string FlowConfigPath = "Assets/LHM/3_ScriptableObject/BossFlowConfig.asset";
        private const string PatternTablePath = "Assets/LHM/3_ScriptableObject/PatternTableConfig.asset";
        private const string GroggyConfigPath = "Assets/LHM/3_ScriptableObject/GroggyConfig.asset";
        private const string JumpSlamConfigPath = "Assets/LHM/3_ScriptableObject/JumpSlamConfig.asset";

        [MenuItem(SetupMenu)]
        private static void Setup()
        {
            if (!TryFindBoss(out BossController boss, out BossAttackPatterns patterns))
            {
                return;
            }

            var flowConfig = BossTestSceneBuilder.ResolveOrCreateConfig<BossFlowConfig>(null, FlowConfigPath);
            var patternTable = BossTestSceneBuilder.ResolveOrCreateConfig<PatternTableConfig>(null, PatternTablePath);
            var groggyConfig = BossTestSceneBuilder.ResolveOrCreateConfig<GroggyConfig>(null, GroggyConfigPath);
            var jumpSlamTemplate = BossTestSceneBuilder.ResolveOrCreateConfig<JumpSlamConfig>(null, JumpSlamConfigPath);
            if (flowConfig == null || patternTable == null || groggyConfig == null || jumpSlamTemplate == null)
            {
                GameLog.Error(null, "[BossSetup] Config 에셋을 준비하지 못해 중단 (Console 위쪽 로그 확인)");
                return;
            }

            GameObject go = boss.gameObject;
            Undo.SetCurrentGroupName("보스 v2 연결");
            int undoGroup = Undo.GetCurrentGroup();
            var report = new StringBuilder($"[BossSetup] '{go.name}' ({go.scene.name}) v2 연결\n");

            BossFlowDriver driver = GetOrAdd<BossFlowDriver>(go, report);
            Assign(driver, "_config", flowConfig, report);
            Assign(driver, "_boss", boss, report);
            Assign(driver, "_attackPatterns", patterns, report);
            Assign(driver, "_patternTable", patternTable, report);

            // T54: 이 씬 전용 JumpSlamConfig (방 값 = 씬에서 측정). 공용 에셋(테스트 씬용)은 수정하지 않는다
            JumpSlamConfig jumpSlam = ResolveSceneJumpSlam(boss, jumpSlamTemplate, report);
            AssignJumpSlam(driver, jumpSlam, jumpSlamTemplate, report);

            BossTelegraphView telegraph = GetOrAdd<BossTelegraphView>(go, report);
            Assign(telegraph, "_driver", driver, report);

            ComboSignalView comboSignal = GetOrAdd<ComboSignalView>(go, report);
            Assign(comboSignal, "_driver", driver, report);

            BossGroggyDriver groggy = GetOrAdd<BossGroggyDriver>(go, report);
            Assign(groggy, "_config", groggyConfig, report);
            Assign(groggy, "_boss", boss, report);
            Assign(groggy, "_flowDriver", driver, report);
            Assign(groggy, "_attackPatterns", patterns, report);

            Assign(boss, "_flowDriver", driver, report);
            SetBool(boss, "_useFlowV2", true, report);

            // T28: v2 흐름은 GameClock.World dt 로 움직인다 → 씬에 Driver 가 없으면 dt = 0 으로 멈춘다
            EnsureGameClockDriver(go.scene, report);

            // T67: 그로기 바 UI (이미 있으면 건너뜀)
            CreateGroggyBarCore(boss, groggy, undoGroup);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;

            report.AppendLine("→ 씬 저장(Ctrl+S)은 직접. 되돌리려면 Ctrl+Z 한 번 (만든 JumpSlamConfig 에셋은 남음).");
            report.AppendLine("수동 (선택): 증기·경고음(T59), Animator 'State'·JumpSlam 트리거, 그림자·충격파 프리팹(T54).");
            report.Append(BuildCheckReport(boss));
            GameLog.Info(report.ToString(), go);
        }

        /// <summary>열린 씬에 GameClockDriver 가 없으면 루트 오브젝트 'GameClock' 으로 만든다 (테스트 씬 빌더와 같은 구성).</summary>
        private static void EnsureGameClockDriver(UnityEngine.SceneManagement.Scene scene, StringBuilder report)
        {
            GameClockDriver[] drivers =
                Object.FindObjectsByType<GameClockDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (drivers.Length > 0)
            {
                return;
            }

            var clock = new GameObject("GameClock");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clock, scene);
            clock.AddComponent<GameClockDriver>();
            Undo.RegisterCreatedObjectUndo(clock, "GameClock 추가");
            report.AppendLine("  + 씬 루트에 'GameClock' (GameClockDriver) 추가 — 없으면 v2 흐름 시간이 0 으로 멈춤");
        }

        // ───────────── 씬 전용 JumpSlamConfig (T54) ─────────────

        private const string SceneJumpSlamPathFormat = "Assets/LHM/3_ScriptableObject/JumpSlamConfig_{0}.asset";
        private const float RoomProbeDistance = 200f;
        private const float WallProbeHeight = 0.5f; // 바닥에서 이 높이로 좌우 벽을 잰다 (플레이어·충격파 높이)

        /// <summary>
        /// 'JumpSlamConfig_씬이름' 이 있으면 그것, 없으면 공용 에셋을 복사해 씬에서 잰 방 값을 넣는다.
        /// 측정에 실패하면 공용 에셋을 그대로 돌려준다 (방 값은 수동).
        /// </summary>
        private static JumpSlamConfig ResolveSceneJumpSlam(BossController boss, JumpSlamConfig template, StringBuilder report)
        {
            string sceneName = boss.gameObject.scene.name;
            if (string.IsNullOrEmpty(sceneName))
            {
                report.AppendLine("  ! 씬이 저장된 적 없음 → 공용 JumpSlamConfig 사용, 방 값 수동 (T54)");
                return template;
            }

            string path = string.Format(SceneJumpSlamPathFormat, sceneName);
            var existing = AssetDatabase.LoadAssetAtPath<JumpSlamConfig>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!TryMeasureRoom(boss, out float minX, out float maxX, out float groundY, out float ceilingY, report))
            {
                report.AppendLine("  ! 방 측정 실패 → 공용 JumpSlamConfig 사용, 방 값 수동 (T54)");
                return template;
            }

            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), path))
            {
                report.AppendLine($"  ! {path} 복사 실패 → 공용 JumpSlamConfig 사용, 방 값 수동 (T54)");
                return template;
            }

            var created = AssetDatabase.LoadAssetAtPath<JumpSlamConfig>(path);
            var so = new SerializedObject(created);
            so.FindProperty("_roomMinX").floatValue = minX;
            so.FindProperty("_roomMaxX").floatValue = maxX;
            so.FindProperty("_groundY").floatValue = groundY;
            so.FindProperty("_ceilingY").floatValue = ceilingY;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            report.AppendLine(
                $"  + {path} 생성: 방 x {minX:F2} ~ {maxX:F2}, 바닥 y {groundY:F2}, 천장 y {ceilingY:F2} " +
                "(공용 JumpSlamConfig 는 그대로)");
            return created;
        }

        /// <summary>비어 있거나 공용 에셋이면 씬 전용으로 바꾼다. 다른 값이 직접 지정돼 있으면 유지.</summary>
        private static void AssignJumpSlam(
            BossFlowDriver driver, JumpSlamConfig value, JumpSlamConfig template, StringBuilder report)
        {
            var so = new SerializedObject(driver);
            SerializedProperty prop = so.FindProperty("_jumpSlam");
            if (prop == null)
            {
                report.AppendLine("  ! BossFlowDriver._jumpSlam 필드 없음 → 수동 확인");
                return;
            }

            Object current = prop.objectReferenceValue;
            if (current == value)
            {
                return;
            }

            if (current != null && current != template)
            {
                report.AppendLine($"  = BossFlowDriver._jumpSlam 기존 값 유지 ({current.name})");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties(); // Undo 기록
            report.AppendLine($"  → BossFlowDriver._jumpSlam = {value.name}");
        }

        /// <summary>
        /// 보스 몸 가운데에서 아래(바닥)·위(천장), 바닥 + 0.5 높이에서 좌우(벽)로 광선을 쏴 방 경계를 잰다.
        /// 보스 자신·트리거·player/enemy/enemyBullet/UI 레이어는 무시.
        /// </summary>
        private static bool TryMeasureRoom(
            BossController boss, out float minX, out float maxX, out float groundY, out float ceilingY,
            StringBuilder report)
        {
            minX = maxX = groundY = ceilingY = 0f;
            Physics2D.SyncTransforms();

            Vector2 center = boss.TryGetComponent(out Collider2D body)
                ? (Vector2)body.bounds.center
                : (Vector2)boss.transform.position;

            bool ok = true;
            if (TryProbe(boss, center, Vector2.down, out Vector2 floor))
            {
                groundY = floor.y;
            }
            else
            {
                report.AppendLine("  ! 바닥 측정 실패 (보스 아래 콜라이더 없음)");
                ok = false;
            }

            if (TryProbe(boss, center, Vector2.up, out Vector2 ceiling))
            {
                ceilingY = ceiling.y;
            }
            else
            {
                report.AppendLine("  ! 천장 측정 실패 (보스 위 콜라이더 없음)");
                ok = false;
            }

            if (!ok)
            {
                return false;
            }

            var low = new Vector2(center.x, groundY + WallProbeHeight);
            if (TryProbe(boss, low, Vector2.left, out Vector2 left))
            {
                minX = left.x;
            }
            else
            {
                report.AppendLine("  ! 왼쪽 벽 측정 실패");
                ok = false;
            }

            if (TryProbe(boss, low, Vector2.right, out Vector2 right))
            {
                maxX = right.x;
            }
            else
            {
                report.AppendLine("  ! 오른쪽 벽 측정 실패");
                ok = false;
            }

            return ok;
        }

        private static bool TryProbe(BossController boss, Vector2 origin, Vector2 direction, out Vector2 point)
        {
            point = default;
            int mask = Physics2D.DefaultRaycastLayers & ~LayerMask.GetMask("player", "enemy", "enemyBullet", "UI");
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, RoomProbeDistance, mask);
            for (int i = 0; i < hits.Length; i++) // 가까운 순
            {
                Collider2D hit = hits[i].collider;
                if (hit == null || hit.isTrigger || hit.transform.IsChildOf(boss.transform))
                {
                    continue;
                }

                point = hits[i].point;
                return true;
            }

            return false;
        }

        [MenuItem(SetupMenu, true)]
        private static bool ValidateSetup()
        {
            return !EditorApplication.isPlaying;
        }

        // ───────────── 그로기 바 UI (T67 ✂️) ─────────────

        private const string GroggyBarMenu = "UnMachine/Boss Setup/그로기 바 UI 만들기";
        private const string BuiltinUiSpritePath = "UI/Skin/UISprite.psd";
        private const float BarHeight = 10f;          // 체력바 아래 얇은 바 (spec: boss.md 15장)
        private const float BarGap = 4f;              // 체력바와의 간격
        private const float FallbackBarWidth = 400f;  // 체력바가 없을 때
        private const float FallbackTopOffset = 80f;
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color BarFillColor = new Color(1f, 0.75f, 0.2f);

        [MenuItem(GroggyBarMenu)]
        private static void CreateGroggyBar()
        {
            if (!TryFindBoss(out BossController boss, out _))
            {
                return;
            }

            if (!boss.TryGetComponent(out BossGroggyDriver groggy))
            {
                GameLog.Error(boss, "[BossSetup] 보스에 BossGroggyDriver 없음 → 먼저 '열린 씬 보스에 v2 연결' 실행");
                return;
            }

            Undo.SetCurrentGroupName("그로기 바 UI 만들기");
            CreateGroggyBarCore(boss, groggy, Undo.GetCurrentGroup());
        }

        /// <summary>그로기 바를 만든다 (이미 있으면 건너뜀). v2 연결 메뉴에서도 같은 Undo 묶음으로 호출.</summary>
        private static void CreateGroggyBarCore(BossController boss, BossGroggyDriver groggy, int undoGroup)
        {
            BossGroggyBar[] existing =
                Object.FindObjectsByType<BossGroggyBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                GameLog.Info($"[BossSetup] 그로기 바가 이미 있음 ('{existing[0].name}') → 만들지 않음", existing[0]);
                return;
            }

            // 1순위: SYS 체력바(Chapter_1_Boss 의 Boss HP)를 복제해 같은 모양으로
            if (TryFindSysHealthBar(boss, out UnifiedHealthBarVisual hpVisual))
            {
                CreateSysGroggyBar(boss, groggy, hpVisual, undoGroup);
                return;
            }

            RectTransform parent = ResolveBarParent(boss, out bool isUnderHealthBar);
            if (parent == null)
            {
                return;
            }

            // GroggyBar (컴포넌트) → Root (배경, 켜고 끔) → Fill (채움)
            RectTransform bar = CreateRect("GroggyBar", parent);
            if (isUnderHealthBar)
            {
                // 체력바 아래에 같은 폭으로
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0f);
                bar.pivot = new Vector2(0.5f, 1f);
                bar.anchoredPosition = new Vector2(0f, -BarGap);
                bar.sizeDelta = new Vector2(0f, BarHeight);
            }
            else
            {
                bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
                bar.pivot = new Vector2(0.5f, 1f);
                bar.anchoredPosition = new Vector2(0f, -FallbackTopOffset);
                bar.sizeDelta = new Vector2(FallbackBarWidth, BarHeight);
            }

            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinUiSpritePath);
            RectTransform root = CreateRect("Root", bar);
            Stretch(root);
            var back = root.gameObject.AddComponent<UnityEngine.UI.Image>();
            back.sprite = sprite;
            back.type = UnityEngine.UI.Image.Type.Sliced;
            back.color = BarBackColor;
            back.raycastTarget = false;

            RectTransform fillRect = CreateRect("Fill", root);
            Stretch(fillRect);
            var fill = fillRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            fill.sprite = sprite;
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.color = BarFillColor;
            fill.raycastTarget = false;

            var groggyBar = bar.gameObject.AddComponent<BossGroggyBar>();
            var so = new SerializedObject(groggyBar);
            so.FindProperty("_groggy").objectReferenceValue = groggy;
            so.FindProperty("_boss").objectReferenceValue = boss;
            so.FindProperty("_root").objectReferenceValue = root.gameObject;
            so.FindProperty("_fill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo(); // 새로 만든 오브젝트라 생성 Undo 에 포함

            Undo.RegisterCreatedObjectUndo(bar.gameObject, "그로기 바 UI 만들기");
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
            Selection.activeGameObject = bar.gameObject;

            GameLog.Info(
                $"[BossSetup] 그로기 바 생성: {GetPath(bar)} ({(isUnderHealthBar ? "체력바 아래" : "Canvas 위쪽 가운데")}). " +
                "위치·크기·색은 인스펙터에서 조정 → Ctrl+S. 되돌리려면 Ctrl+Z.",
                bar.gameObject);
        }

        // ── SYS 체력바 복제 (Chapter_1_Boss) ──

        private const string GroggyMaterialPath = "Assets/LHM/1_Material/Boss Groggy.mat";
        private const string FillColorProperty = "_ColorFill";
        private const float SysBarHeightRatio = 0.35f; // 체력바 높이 대비
        private const float SysBarMinHeight = 8f;
        private const float SysBarGap = 8f;

        /// <summary>보스 DamageHandler 에 연결된 BossHealthBarBridge 의 비주얼. 없으면 씬에 하나뿐인 브릿지.</summary>
        private static bool TryFindSysHealthBar(BossController boss, out UnifiedHealthBarVisual visual)
        {
            visual = null;
            BossHealthBarBridge[] bridges =
                Object.FindObjectsByType<BossHealthBarBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bridges.Length == 0)
            {
                return false;
            }

            boss.TryGetComponent(out BossSystem.DamageHandler damageHandler);
            BossHealthBarBridge chosen = null;
            for (int i = 0; i < bridges.Length; i++)
            {
                Object source = new SerializedObject(bridges[i]).FindProperty("damageHandler")?.objectReferenceValue;
                if (damageHandler != null && source == damageHandler)
                {
                    chosen = bridges[i];
                    break;
                }
            }

            if (chosen == null && bridges.Length == 1)
            {
                chosen = bridges[0];
            }

            if (chosen == null)
            {
                return false;
            }

            Object assigned = new SerializedObject(chosen).FindProperty("healthBar")?.objectReferenceValue;
            visual = assigned as UnifiedHealthBarVisual;
            if (visual == null)
            {
                visual = chosen.GetComponent<UnifiedHealthBarVisual>();
            }

            return visual != null && visual.transform is RectTransform;
        }

        private static void CreateSysGroggyBar(
            BossController boss, BossGroggyDriver groggy, UnifiedHealthBarVisual hpVisual, int undoGroup)
        {
            var hpRect = (RectTransform)hpVisual.transform;
            float hpHeight = hpRect.rect.height;
            float width = hpRect.rect.width;
            float height = Mathf.Max(SysBarMinHeight, Mathf.Round(hpHeight * SysBarHeightRatio));

            // 체력바 테두리 (Chapter_1_Boss 의 'Bored'). 테두리 두께(체력바 밖으로 나온 만큼)는 그대로 둔다.
            RectTransform frameRect = FindHealthBarFrame(hpRect);
            Vector2 framePadding = Vector2.zero;
            Vector2 frameOffset = Vector2.zero;
            if (frameRect != null)
            {
                framePadding = new Vector2(
                    Mathf.Max(0f, (frameRect.rect.width - width) * 0.5f),
                    Mathf.Max(0f, (frameRect.rect.height - hpHeight) * 0.5f));
                frameOffset = RectCenter(frameRect) - RectCenter(hpRect);
            }

            // 컨테이너(BossGroggyBar) = 체력바와 같은 앵커·피벗, 바로 아래로 (테두리끼리 SysBarGap 띄움)
            RectTransform bar = CreateRect("Groggy Bar", hpRect.parent);
            bar.SetSiblingIndex(hpRect.GetSiblingIndex() + 1);
            bar.anchorMin = hpRect.anchorMin;
            bar.anchorMax = hpRect.anchorMax;
            bar.pivot = hpRect.pivot;
            bar.sizeDelta = new Vector2(hpRect.sizeDelta.x, height);
            float hpBottom = hpRect.anchoredPosition.y - hpHeight * hpRect.pivot.y;
            float barY = hpBottom - SysBarGap - framePadding.y * 2f - height * (1f - hpRect.pivot.y);
            bar.anchoredPosition = new Vector2(hpRect.anchoredPosition.x, barY);

            // Root (켜고 끔) → Bored(테두리 복제) + Fill(비주얼 복제)
            RectTransform root = CreateRect("Root", bar);
            Stretch(root);

            if (frameRect != null)
            {
                GameObject frameClone = Object.Instantiate(frameRect.gameObject, root);
                frameClone.name = frameRect.name;
                var frameCloneRect = (RectTransform)frameClone.transform;
                frameCloneRect.anchorMin = frameCloneRect.anchorMax = new Vector2(0.5f, 0.5f);
                frameCloneRect.pivot = new Vector2(0.5f, 0.5f);
                frameCloneRect.sizeDelta = new Vector2(width, height) + framePadding * 2f;
                frameCloneRect.anchoredPosition = frameOffset;
            }

            // 브릿지는 제거: HP 가 아니라 그로기 게이지로 채움
            GameObject clone = Object.Instantiate(hpVisual.gameObject, root);
            clone.name = "Fill";
            BossHealthBarBridge cloneBridge = clone.GetComponent<BossHealthBarBridge>();
            if (cloneBridge != null)
            {
                Object.DestroyImmediate(cloneBridge);
            }

            var cloneRect = (RectTransform)clone.transform;
            Stretch(cloneRect);
            var cloneVisual = clone.GetComponent<UnifiedHealthBarVisual>();

            // 색: 체력바 머티리얼을 복사한 그로기 전용 머티리얼 (원본은 그대로)
            var hpSo = new SerializedObject(hpVisual);
            Material hpMaterial = hpSo.FindProperty("sourceMaterial")?.objectReferenceValue as Material;
            Material groggyMaterial = EnsureGroggyMaterial(hpMaterial);

            var visualSo = new SerializedObject(cloneVisual);
            SerializedProperty sizeProp = visualSo.FindProperty("barSize");
            if (sizeProp != null)
            {
                sizeProp.vector2Value = new Vector2(width, height);
            }

            SerializedProperty materialProp = visualSo.FindProperty("sourceMaterial");
            if (materialProp != null && groggyMaterial != null)
            {
                materialProp.objectReferenceValue = groggyMaterial;
            }

            visualSo.ApplyModifiedPropertiesWithoutUndo();
            if (groggyMaterial != null && clone.TryGetComponent(out UnityEngine.UI.Image cloneImage))
            {
                cloneImage.material = groggyMaterial;
            }

            var groggyBar = bar.gameObject.AddComponent<BossGroggyBar>();
            var so = new SerializedObject(groggyBar);
            so.FindProperty("_groggy").objectReferenceValue = groggy;
            so.FindProperty("_boss").objectReferenceValue = boss;
            so.FindProperty("_root").objectReferenceValue = root.gameObject;
            so.FindProperty("_visual").objectReferenceValue = cloneVisual;
            so.ApplyModifiedPropertiesWithoutUndo(); // 새 오브젝트라 생성 Undo 에 포함

            Undo.RegisterCreatedObjectUndo(bar.gameObject, "그로기 바 UI 만들기");
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
            Selection.activeGameObject = bar.gameObject;

            GameLog.Info(
                $"[BossSetup] 그로기 바 생성 (SYS 체력바 '{hpVisual.name}' 복제, " +
                $"테두리 {(frameRect != null ? $"'{frameRect.name}' 복제" : "없음")}): {GetPath(bar)}, " +
                $"{width:F0}×{height:F0}, 머티리얼 {(groggyMaterial != null ? groggyMaterial.name : "원본 그대로")}. " +
                "위치·높이는 'Groggy Bar' RectTransform, 색은 머티리얼 _ColorFill 에서 조정 → Ctrl+S. 되돌리려면 Ctrl+Z.",
                bar.gameObject);
        }

        private const string HealthBarFrameName = "Bored"; // Chapter_1_Boss 의 체력바 테두리 이름

        /// <summary>
        /// 체력바 테두리: 같은 부모의 'Bored' → 없으면 체력바를 감싸는 형제 Image (UnifiedHealthBarVisual 아닌 것). 없으면 null.
        /// </summary>
        private static RectTransform FindHealthBarFrame(RectTransform hpRect)
        {
            Transform parent = hpRect.parent;
            RectTransform enclosing = null;
            Rect hpBounds = LocalBounds(hpRect);
            for (int i = 0; i < parent.childCount; i++)
            {
                if (!(parent.GetChild(i) is RectTransform sibling) || sibling == hpRect
                    || !sibling.TryGetComponent(out UnityEngine.UI.Image _)
                    || sibling.TryGetComponent(out UnifiedHealthBarVisual _))
                {
                    continue;
                }

                if (sibling.name == HealthBarFrameName)
                {
                    return sibling;
                }

                Rect bounds = LocalBounds(sibling);
                const float tolerance = 0.5f;
                if (enclosing == null
                    && bounds.xMin <= hpBounds.xMin + tolerance && bounds.xMax >= hpBounds.xMax - tolerance
                    && bounds.yMin <= hpBounds.yMin + tolerance && bounds.yMax >= hpBounds.yMax - tolerance)
                {
                    enclosing = sibling;
                }
            }

            return enclosing;
        }

        /// <summary>부모 로컬 좌표 기준 사각형.</summary>
        private static Rect LocalBounds(RectTransform rect)
        {
            Rect r = rect.rect;
            Vector2 offset = rect.localPosition;
            return new Rect(r.position + offset, r.size);
        }

        private static Vector2 RectCenter(RectTransform rect)
        {
            return LocalBounds(rect).center;
        }

        /// <summary>체력바 머티리얼을 그로기 전용으로 복사 (없을 때만, 채움 색 = 주황). 원본이 없으면 null.</summary>
        private static Material EnsureGroggyMaterial(Material source)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(GroggyMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            if (source == null)
            {
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, GroggyMaterialPath))
            {
                GameLog.Warn($"[BossSetup] 머티리얼 복사 실패 ({sourcePath} → {GroggyMaterialPath}) → 체력바 머티리얼 그대로 사용");
                return null;
            }

            Material created = AssetDatabase.LoadAssetAtPath<Material>(GroggyMaterialPath);
            if (created != null && created.HasProperty(FillColorProperty))
            {
                created.SetColor(FillColorProperty, BarFillColor);
                EditorUtility.SetDirty(created);
                AssetDatabase.SaveAssets();
            }

            GameLog.Info($"[BossSetup] 그로기 머티리얼 생성: {GroggyMaterialPath} (채움 색 주황)", created);
            return created;
        }

        [MenuItem(GroggyBarMenu, true)]
        private static bool ValidateCreateGroggyBar()
        {
            return !EditorApplication.isPlaying;
        }

        /// <summary>보스 체력바(BossController.healthBar) → 씬의 Canvas → 새 Canvas 순으로 부모를 정한다.</summary>
        private static RectTransform ResolveBarParent(BossController boss, out bool isUnderHealthBar)
        {
            isUnderHealthBar = false;
            SerializedProperty healthBarProp = new SerializedObject(boss).FindProperty("healthBar");
            if (healthBarProp != null && healthBarProp.objectReferenceValue is Component healthBar
                && healthBar.transform is RectTransform healthRect)
            {
                isUnderHealthBar = true;
                return healthRect;
            }

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].isRootCanvas && canvases[i].renderMode != RenderMode.WorldSpace)
                {
                    return (RectTransform)canvases[i].transform;
                }
            }

            var canvasGo = new GameObject(
                "BossUICanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                canvasGo.layer = uiLayer;
            }

            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(canvasGo, "그로기 바 Canvas");
            GameLog.Info("[BossSetup] Screen Space Canvas 가 없어 'BossUICanvas' 를 새로 만듦", canvasGo);
            return (RectTransform)canvasGo.transform;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string GetPath(Transform target)
        {
            string path = target.name;
            for (Transform t = target.parent; t != null; t = t.parent)
            {
                path = t.name + "/" + path;
            }

            return path;
        }

        [MenuItem(CheckMenu)]
        private static void Check()
        {
            if (TryFindBoss(out BossController boss, out _))
            {
                GameLog.Info(BuildCheckReport(boss), boss.gameObject);
            }
        }

        /// <summary>선택한 오브젝트(또는 부모)의 BossController → 없으면 열린 씬에 하나뿐인 BossController.</summary>
        private static bool TryFindBoss(out BossController boss, out BossAttackPatterns patterns)
        {
            boss = null;
            patterns = null;
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                if (EditorUtility.IsPersistent(selected))
                {
                    GameLog.Error(selected, "[BossSetup] 프로젝트의 프리팹 에셋이 선택됨 → 씬의 보스를 선택할 것 (프리팹 원본은 수정하지 않음)");
                    return false;
                }

                boss = selected.GetComponentInParent<BossController>(true);
            }

            if (boss == null)
            {
                BossController[] found =
                    Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found.Length != 1)
                {
                    GameLog.Error(null,
                        $"[BossSetup] 열린 씬의 BossController {found.Length}개 → Hierarchy 에서 보스를 선택하고 다시 실행");
                    return false;
                }

                boss = found[0];
            }

            if (!boss.TryGetComponent(out patterns))
            {
                GameLog.Error(boss, "[BossSetup] 보스 오브젝트에 BossAttackPatterns 가 없음 → 중단 (Driver 가 레거시 패턴을 씀)");
                return false;
            }

            return true;
        }

        private static T GetOrAdd<T>(GameObject go, StringBuilder report) where T : Component
        {
            if (go.TryGetComponent(out T existing))
            {
                return existing;
            }

            report.AppendLine($"  + {typeof(T).Name} 추가");
            return Undo.AddComponent<T>(go);
        }

        /// <summary>비어 있으면 연결 (Undo 기록). 이미 값이 있으면 그대로 두고 보고만.</summary>
        private static void Assign(Component target, string fieldName, Object value, StringBuilder report)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
            {
                report.AppendLine($"  ! {target.GetType().Name}.{fieldName} 필드 없음 → 수동 확인");
                return;
            }

            if (prop.objectReferenceValue != null)
            {
                if (prop.objectReferenceValue != value)
                {
                    report.AppendLine(
                        $"  = {target.GetType().Name}.{fieldName} 기존 값 유지 ({prop.objectReferenceValue.name})");
                }

                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties(); // Undo 기록
            report.AppendLine($"  → {target.GetType().Name}.{fieldName} = {value.name}");
        }

        private static void SetBool(Component target, string fieldName, bool value, StringBuilder report)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null || prop.propertyType != SerializedPropertyType.Boolean)
            {
                report.AppendLine($"  ! {target.GetType().Name}.{fieldName} bool 필드 없음 → 수동 확인");
                return;
            }

            if (prop.boolValue == value)
            {
                return;
            }

            prop.boolValue = value;
            so.ApplyModifiedProperties();
            report.AppendLine($"  → {target.GetType().Name}.{fieldName} = {value}");
        }

        /// <summary>연결 상태 요약 (읽기 전용).</summary>
        private static string BuildCheckReport(BossController boss)
        {
            var builder = new StringBuilder("[BossSetup] 연결 상태\n");
            GameObject go = boss.gameObject;
            BossFlowDriver driver = go.GetComponent<BossFlowDriver>();

            CheckBool(builder, boss, "_useFlowV2", "BossController.Use Flow V2");
            CheckRef(builder, boss, "_flowDriver", "BossController.Flow Driver");
            int clockCount =
                Object.FindObjectsByType<GameClockDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            builder.AppendLine(clockCount == 1
                ? "  ✓ GameClockDriver"
                : clockCount == 0
                    ? "  ✗ GameClockDriver 없음 (v2 흐름이 멈춤) → 'v2 연결' 다시 실행"
                    : $"  ✗ GameClockDriver {clockCount}개 (하나만 둘 것)");
            CheckComponent(builder, driver, "BossFlowDriver");
            if (driver != null)
            {
                CheckRef(builder, driver, "_config", "  Config");
                CheckRef(builder, driver, "_boss", "  Boss");
                CheckRef(builder, driver, "_attackPatterns", "  Attack Patterns");
                CheckRef(builder, driver, "_patternTable", "  Pattern Table");
                CheckRef(builder, driver, "_jumpSlam", "  Jump Slam (비우면 P1 = 임시 레거시)");
                if (new SerializedObject(driver).FindProperty("_jumpSlam")?.objectReferenceValue is JumpSlamConfig slam)
                {
                    var slamSo = new SerializedObject(slam);
                    builder.AppendLine(
                        $"    {slam.name}: 방 x {slamSo.FindProperty("_roomMinX").floatValue:F2} ~ " +
                        $"{slamSo.FindProperty("_roomMaxX").floatValue:F2}, " +
                        $"바닥 y {slamSo.FindProperty("_groundY").floatValue:F2}, " +
                        $"천장 y {slamSo.FindProperty("_ceilingY").floatValue:F2}");
                }
            }

            BossTelegraphView telegraph = go.GetComponent<BossTelegraphView>();
            CheckComponent(builder, telegraph, "BossTelegraphView");
            if (telegraph != null)
            {
                CheckRef(builder, telegraph, "_driver", "  Driver");
            }

            ComboSignalView combo = go.GetComponent<ComboSignalView>();
            CheckComponent(builder, combo, "ComboSignalView");
            if (combo != null)
            {
                CheckRef(builder, combo, "_driver", "  Driver");
                CheckRef(builder, combo, "_steam", "  Steam (T59, 선택)");
                CheckRef(builder, combo, "_warningClip", "  Warning Clip (T59, 선택)");
            }

            BossGroggyDriver groggy = go.GetComponent<BossGroggyDriver>();
            CheckComponent(builder, groggy, "BossGroggyDriver");
            if (groggy != null)
            {
                CheckRef(builder, groggy, "_config", "  Config");
                CheckRef(builder, groggy, "_flowDriver", "  Flow Driver");
            }

            BossGroggyBar[] bars = Object.FindObjectsByType<BossGroggyBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            builder.AppendLine(bars.Length > 0 ? "  ✓ 그로기 바 UI (BossGroggyBar) 있음" : "  · 그로기 바 UI 없음 → 메뉴 '그로기 바 UI 만들기' (T67)");
            return builder.ToString();
        }

        private static void CheckComponent(StringBuilder builder, Component component, string label)
        {
            builder.AppendLine(component != null ? $"  ✓ {label}" : $"  ✗ {label} 없음");
        }

        private static void CheckRef(StringBuilder builder, Component target, string fieldName, string label)
        {
            SerializedProperty prop = new SerializedObject(target).FindProperty(fieldName);
            bool isSet = prop != null && prop.objectReferenceValue != null;
            builder.AppendLine(isSet ? $"  ✓ {label}" : $"  ✗ {label} 비어 있음");
        }

        private static void CheckBool(StringBuilder builder, Component target, string fieldName, string label)
        {
            SerializedProperty prop = new SerializedObject(target).FindProperty(fieldName);
            bool isOn = prop != null && prop.boolValue;
            builder.AppendLine(isOn ? $"  ✓ {label}" : $"  ✗ {label} 꺼짐");
        }
    }
}
