using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnMachine.Boss;

namespace UnMachine.EditorTools
{
    /// <summary>
    /// 보스 테스트 씬(Test_*.unity)을 새로 만드는 에디터 창.
    /// AI_RULES 0장 2번 예외: 새 Test_* 씬만 만든다. 기존 씬은 열거나 저장하지 않고, 같은 이름이면 중단한다.
    /// </summary>
    public sealed class BossTestSceneBuilder : EditorWindow
    {
        private const string MenuPath = "UnMachine/Test Scene/Boss Arena";
        private const string QuickMenuPath = "UnMachine/Test Scene/Boss Arena (기본값으로 바로 생성)";
        private const string DefaultPlayerPrefabPath = "Assets/CSH/8.Prefabs/Player/Player.prefab";
        private const string DefaultBossPrefabPath = "Assets/LHM/2_Prefabs/Boss.prefab";

        // v2 흐름 (T31): 프로젝트에 BossFlowConfig 가 하나도 없을 때만 여기에 새로 만든다 (기존 에셋은 덮어쓰지 않음)
        private const string DefaultFlowConfigPath = "Assets/LHM/3_ScriptableObject/BossFlowConfig.asset";
        private const string FlowDriverConfigField = "_config";
        private const string FlowDriverBossField = "_boss";
        private const string FlowDriverPatternsField = "_attackPatterns";
        private const string FlowDriverPatternTableField = "_patternTable";
        private const string TelegraphViewDriverField = "_driver";
        private const string DefaultJumpSlamConfigPath = "Assets/LHM/3_ScriptableObject/JumpSlamConfig.asset";
        private const string FlowDriverJumpSlamField = "_jumpSlam";
        private const string DefaultGroggyConfigPath = "Assets/LHM/3_ScriptableObject/GroggyConfig.asset";
        private const string GroggyConfigField = "_config";
        private const string GroggyBossField = "_boss";
        private const string GroggyFlowDriverField = "_flowDriver";
        private const string GroggyPatternsField = "_attackPatterns";
        // T38: 프로젝트에 PatternTableConfig 가 하나도 없을 때만 새로 만든다
        private const string DefaultPatternTablePath = "Assets/LHM/3_ScriptableObject/PatternTableConfig.asset";
        private const string BossUseFlowV2Field = "_useFlowV2";
        private const string BossFlowDriverField = "_flowDriver";
        private const string RequiredFilePrefix = "Test_";
        private const string GroundLayerName = "ground";
        private const string BuiltinSpritePath = "UI/Skin/UISprite.psd";
        private const string PlayerFieldName = "player";

        // CameraFollow(CSH) 직렬화 필드 이름 — PlayerDash·BlueDash 가 Camera.main 부모의 CameraFollow 를 찾는다
        private const string CameraFollowMainCameraField = "mainCamera";
        private const string CameraFollowZoomField = "cmZoom";
        private const string CameraFollowGamePlayField = "cmGamePlay";
        private const string CameraFollowTargetField = "target";
        private const float CameraDistanceZ = -10f;

        // URP 2D Global Light. 어셈블리 직접 참조 없이 만들려고 이름으로 찾는다.
        private const string Light2DTypeName =
            "UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime";
        private const int Light2DGlobalType = 4; // Light2D.LightType.Global

        // 보스 체력바: Scenes_Main/Chapter_1_Boss 의 SYS 체력바와 같은 구조·값
        // Canvas ─ Boss Health Bar ─ Bored(검은 테두리) + Boss HP(UnifiedHealthBarVisual + BossHealthBarBridge)
        private const string BossHealthMaterialPath = "Assets/SYS/Materials/Boss Health.mat";
        private static readonly Vector2 CanvasReferenceResolution = new Vector2(1920f, 1080f);
        private const float CanvasReferencePixelsPerUnit = 36f;
        private static readonly Vector2 BossBarAnchor = new Vector2(0.084f, 0.056f);
        private static readonly Vector2 BossBarPosition = new Vector2(49.85034f, -50.00003f);
        private static readonly Vector2 BossBarFrameSize = new Vector2(1600f, 70f);
        private static readonly Vector2 BossBarFillSize = new Vector2(1600f, 60f);
        private static readonly Vector2 DealTimeShakeStrength = new Vector2(30f, 14f);
        private const float DealTimeShakeDuration = 0.8f;

        private const float SpecMinWidth = 40f;  // spec: boss.md 2장 가로 40 이상
        private const float SpecMinHeight = 30f; // spec: boss.md 2장 세로 30 이상

        [SerializeField] private string _scenePath = "Assets/LHM/Test_Boss.unity";
        [SerializeField] private GameObject _bossPrefab;
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private float _arenaWidth = SpecMinWidth;
        [SerializeField] private float _arenaHeight = SpecMinHeight;
        [SerializeField] private float _wallThickness = 1f;
        [SerializeField] private float _bossHeightAboveGround = 3f;
        [SerializeField] private float _playerOffsetFromLeftWall = 6f;
        [SerializeField] private float _playerHeightAboveGround = 1f;
        [SerializeField] private float _cameraMargin = 2f;
        [SerializeField] private Color _blockColor = new Color(0.35f, 0.35f, 0.4f);
        [SerializeField] private Color _cameraBackground = new Color(0.08f, 0.08f, 0.12f);
        [SerializeField] private bool _setupFlowV2 = true;
        [SerializeField] private BossFlowConfig _flowConfig;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<BossTestSceneBuilder>("Boss Test Scene");
        }

        /// <summary>창 없이 기본값(기본 프리팹·v2 On)으로 바로 만든다. 작업마다 빠른 씬 테스트용.</summary>
        [MenuItem(QuickMenuPath)]
        private static void QuickBuild()
        {
            var builder = CreateInstance<BossTestSceneBuilder>(); // OnEnable 에서 기본 프리팹을 채운다
            try
            {
                builder.TryBuild();
            }
            finally
            {
                DestroyImmediate(builder);
            }
        }

        private void OnEnable()
        {
            if (_playerPrefab == null)
            {
                _playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
            }

            if (_bossPrefab == null)
            {
                _bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBossPrefabPath);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("새 테스트 씬", EditorStyles.boldLabel);
            _scenePath = EditorGUILayout.TextField("저장 경로", _scenePath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("프리팹 (배치만 함)", EditorStyles.boldLabel);
            _bossPrefab = (GameObject)EditorGUILayout.ObjectField("보스 프리팹", _bossPrefab, typeof(GameObject), false);
            _playerPrefab = (GameObject)EditorGUILayout.ObjectField(
                "플레이어 프리팹", _playerPrefab, typeof(GameObject), false);

            if (_bossPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "보스 프리팹이 없으면 위치 마커만 만든다.\n" +
                    "LHM.unity 의 Boss 오브젝트를 Project 창으로 드래그해 프리팹을 만든 뒤 지정하면 함께 배치된다.",
                    MessageType.Info);
            }
            else if (_bossPrefab.GetComponentInChildren<BossController>(true) == null)
            {
                EditorGUILayout.HelpBox("지정한 보스 프리팹에 BossController 가 없다.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("보스룸 (spec: boss.md 2장)", EditorStyles.boldLabel);
            _arenaWidth = EditorGUILayout.FloatField("가로", _arenaWidth);
            _arenaHeight = EditorGUILayout.FloatField("세로", _arenaHeight);
            _wallThickness = EditorGUILayout.FloatField("벽 두께", _wallThickness);
            _bossHeightAboveGround = EditorGUILayout.FloatField("보스 착지 높이", _bossHeightAboveGround);
            _playerOffsetFromLeftWall = EditorGUILayout.FloatField("플레이어 왼쪽 벽 거리", _playerOffsetFromLeftWall);
            _playerHeightAboveGround = EditorGUILayout.FloatField("플레이어 높이", _playerHeightAboveGround);
            _cameraMargin = EditorGUILayout.FloatField("카메라 여백", _cameraMargin);
            _blockColor = EditorGUILayout.ColorField("벽 색", _blockColor);
            _cameraBackground = EditorGUILayout.ColorField("배경 색", _cameraBackground);

            if (_arenaWidth < SpecMinWidth || _arenaHeight < SpecMinHeight)
            {
                EditorGUILayout.HelpBox(
                    $"기획서 규격({SpecMinWidth}×{SpecMinHeight} 이상)보다 작다.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("보스 v2 흐름 (T31)", EditorStyles.boldLabel);
            _setupFlowV2 = EditorGUILayout.Toggle("BossFlowDriver 연결 + useFlowV2 On", _setupFlowV2);
            if (_setupFlowV2)
            {
                _flowConfig = (BossFlowConfig)EditorGUILayout.ObjectField(
                    "Flow Config", _flowConfig, typeof(BossFlowConfig), false);
                if (_flowConfig == null)
                {
                    EditorGUILayout.HelpBox(
                        "비워 두면 프로젝트의 BossFlowConfig 를 찾아 쓰고, 하나도 없으면 새로 만든다:\n" +
                        DefaultFlowConfigPath,
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("테스트 씬 만들기", GUILayout.Height(30f)))
            {
                TryBuild();
            }
        }

        private void TryBuild()
        {
            if (!ValidatePath(out string error))
            {
                if (error != null)
                {
                    EditorUtility.DisplayDialog("테스트 씬", error, "확인");
                }

                return;
            }

            if (_arenaWidth <= 0f || _arenaHeight <= 0f || _wallThickness <= 0f)
            {
                EditorUtility.DisplayDialog("테스트 씬", "가로·세로·벽 두께는 0보다 커야 한다.", "확인");
                return;
            }

            // 현재 열린 씬의 변경 사항은 사용자에게 저장 여부를 묻는다 (자동 저장하지 않음).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        private bool ValidatePath(out string error)
        {
            error = null;
            string path = _scenePath == null ? string.Empty : _scenePath.Replace('\\', '/');
            _scenePath = path;

            if (!path.StartsWith("Assets/") || !path.EndsWith(".unity"))
            {
                error = "저장 경로는 Assets/ 로 시작하고 .unity 로 끝나야 한다.";
                return false;
            }

            if (!Path.GetFileName(path).StartsWith(RequiredFilePrefix))
            {
                error = $"파일 이름은 {RequiredFilePrefix} 로 시작해야 한다 (AI_RULES 0장 2번).";
                return false;
            }

            if (AssetExists(path))
            {
                // 같은 이름은 덮어쓰지 않는다 (AI_RULES 0장 2번) → "Test_Boss 1.unity" 처럼 새 이름으로 만든다
                string unique = AssetDatabase.GenerateUniqueAssetPath(path);
                bool accepted = EditorUtility.DisplayDialog("테스트 씬",
                    $"{path} 가 이미 있다. 덮어쓰지 않고 새 파일로 만든다:\n{unique}", "만들기", "취소");
                if (!accepted)
                {
                    return false; // error = null → 추가 대화상자 없음
                }

                path = unique;
                _scenePath = unique;
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(ToFullPath(directory)))
            {
                error = $"폴더가 없다: {directory}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 에셋 경로("Assets/…")에 파일이 있는지. AssetDatabase 와 프로젝트 기준 절대 경로를 모두 확인한다.
        /// File.Exists 에 상대 경로를 넘기면 프로세스 현재 폴더 기준이라, 그 폴더가 바뀌면 기존 씬을 못 찾고 덮어쓴다 (2026-11-26 발생).
        /// </summary>
        private static bool AssetExists(string assetPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets);
            return !string.IsNullOrEmpty(guid) || File.Exists(ToFullPath(assetPath));
        }

        /// <summary>"Assets/…" 경로 → 프로젝트 루트 기준 절대 경로.</summary>
        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, assetPath));
        }

        private void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 좌표 기준: 바닥 윗면 y = 0, 방 가로 중앙 x = 0
            CreateArena();
            CreateMarkers(out Vector3 bossSpawn, out Vector3 playerSpawn);
            CreateGlobalLight();
            CreateRuntimeServices();

            Transform player = InstantiatePrefab(_playerPrefab, playerSpawn);
            Transform bossTransform = InstantiatePrefab(_bossPrefab, bossSpawn);
            GameObject boss = bossTransform != null ? bossTransform.gameObject : null;

            Vector3 cameraPosition = new Vector3(0f, _arenaHeight * 0.5f, CameraDistanceZ);
            if (player != null)
            {
                CreateCameraRig(cameraPosition, player);
            }
            else
            {
                CreateCamera(null, cameraPosition); // 플레이어가 없으면 CameraFollow 없이 고정 카메라만
            }

            if (boss != null && player != null)
            {
                AssignPlayerReference(boss.GetComponentInChildren<BossController>(true), player);
                AssignPlayerReference(boss.GetComponentInChildren<BossAttackPatterns>(true), player);
            }

            bool isFlowV2Ready = boss != null && _setupFlowV2 && SetupFlowV2(boss);
            bool hasHealthBar = boss != null && CreateBossHealthBar(boss);

            if (!EditorSceneManager.SaveScene(scene, _scenePath))
            {
                GameLog.Error(null, $"[TestScene] 저장 실패: {_scenePath}");
                return;
            }

            GameLog.Info($"[TestScene] 생성 완료: {_scenePath} (보스 {(boss != null ? "배치" : "마커만")}, " +
                         $"플레이어 {(player != null ? "배치" : "없음")}, v2 흐름 {(isFlowV2Ready ? "On" : "Off")}, " +
                         $"체력바 {(hasHealthBar ? "SYS" : "없음")})");
        }

        /// <summary>
        /// Chapter_1_Boss 와 같은 SYS 보스 체력바를 만든다:
        /// Canvas ─ Boss Health Bar ─ Bored(검은 테두리) + Boss HP(UnifiedHealthBarVisual + BossHealthBarBridge ← 보스 DamageHandler).
        /// </summary>
        /// <returns>만들었으면 true.</returns>
        private static bool CreateBossHealthBar(GameObject boss)
        {
            var damageHandler = boss.GetComponentInChildren<BossSystem.DamageHandler>(true);
            if (damageHandler == null)
            {
                GameLog.Warn("[TestScene] 보스에 DamageHandler 가 없어 체력바를 만들지 않았다");
                return false;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(BossHealthMaterialPath);
            if (material == null)
            {
                GameLog.Warn($"[TestScene] 체력바 머티리얼이 없어 체력바를 만들지 않았다: {BossHealthMaterialPath}");
                return false;
            }

            int uiLayer = LayerMask.NameToLayer("UI");
            var canvasGo = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            if (uiLayer >= 0)
            {
                canvasGo.layer = uiLayer;
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CanvasReferenceResolution;
            scaler.referencePixelsPerUnit = CanvasReferencePixelsPerUnit;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            RectTransform group = CreateUiRect("Boss Health Bar", canvasGo.transform, uiLayer);
            group.anchorMin = group.anchorMax = BossBarAnchor;
            group.pivot = new Vector2(0.5f, 0f);
            group.anchoredPosition = BossBarPosition;
            group.sizeDelta = new Vector2(100f, 100f);

            // 테두리 (Image 기본 흰 사각형을 검게)
            RectTransform frame = CreateUiRect("Bored", group, uiLayer);
            PlaceLeftAnchored(frame, BossBarFrameSize);
            frame.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.black;

            // 체력 (셰이더 바)
            RectTransform fill = CreateUiRect("Boss HP", group, uiLayer);
            PlaceLeftAnchored(fill, BossBarFillSize);
            var fillImage = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
            fillImage.material = material;

            var visual = fill.gameObject.AddComponent<UnifiedHealthBarVisual>();
            var visualSo = new SerializedObject(visual);
            visualSo.FindProperty("rootCanvas").objectReferenceValue = canvas;
            visualSo.FindProperty("sourceMaterial").objectReferenceValue = material;
            visualSo.FindProperty("barSize").vector2Value = BossBarFillSize;
            visualSo.ApplyModifiedPropertiesWithoutUndo();

            var bridge = fill.gameObject.AddComponent<BossHealthBarBridge>();
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("damageHandler").objectReferenceValue = damageHandler;
            bridgeSo.FindProperty("healthBar").objectReferenceValue = visual;
            bridgeSo.FindProperty("enableGhostFill").boolValue = true;
            bridgeSo.FindProperty("enableGlow").boolValue = false;
            bridgeSo.FindProperty("enableInnerGlow").boolValue = false;
            bridgeSo.FindProperty("enableShimmer").boolValue = false;
            bridgeSo.FindProperty("enableShake").boolValue = false;
            bridgeSo.FindProperty("enableDealTimeGlow").boolValue = true;
            bridgeSo.FindProperty("dealTimeShakeStrength").vector2Value = DealTimeShakeStrength;
            bridgeSo.FindProperty("dealTimeShakeDuration").floatValue = DealTimeShakeDuration;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static RectTransform CreateUiRect(string name, Transform parent, int layer)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (layer >= 0)
            {
                go.layer = layer;
            }

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Chapter_1_Boss 의 Bored·Boss HP 배치: 부모 왼쪽 가운데 기준, 왼쪽 끝이 부모 왼쪽에 닿게.</summary>
        private static void PlaceLeftAnchored(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(size.x * 0.5f, 0f);
        }

        /// <summary>
        /// 새 씬의 보스 인스턴스에 BossFlowDriver 를 붙이고 참조·토글을 채운다 (T31).
        /// 프리팹 원본은 수정하지 않는다 (씬 인스턴스 오버라이드만).
        /// </summary>
        /// <returns>연결을 끝냈으면 true.</returns>
        private bool SetupFlowV2(GameObject boss)
        {
            BossController controller = boss.GetComponentInChildren<BossController>(true);
            BossAttackPatterns patterns = boss.GetComponentInChildren<BossAttackPatterns>(true);
            if (controller == null || patterns == null)
            {
                GameLog.Warn("[TestScene] 보스에 BossController·BossAttackPatterns 가 없어 v2 흐름을 연결하지 않았다");
                return false;
            }

            BossFlowConfig config = ResolveOrCreateConfig(_flowConfig, DefaultFlowConfigPath);
            PatternTableConfig patternTable = ResolveOrCreateConfig<PatternTableConfig>(null, DefaultPatternTablePath);
            if (config == null || patternTable == null)
            {
                return false;
            }

            // 프리팹에 이미 있으면 그대로 쓴다 (나중에 프리팹에 넣는 경우 대비)
            var driver = controller.GetComponent<BossFlowDriver>();
            if (driver == null)
            {
                driver = controller.gameObject.AddComponent<BossFlowDriver>(); // 에디터에선 Awake 가 돌지 않는다
            }

            AssignReference(driver, FlowDriverConfigField, config);
            AssignReference(driver, FlowDriverBossField, controller);
            AssignReference(driver, FlowDriverPatternsField, patterns);
            AssignReference(driver, FlowDriverPatternTableField, patternTable);

            // T46 P1 점프 내려찍기 (없으면 기본값으로 생성. 아레나 크기를 바꿨으면 방 x·천장 y 를 맞출 것)
            JumpSlamConfig jumpSlam = ResolveOrCreateConfig<JumpSlamConfig>(null, DefaultJumpSlamConfigPath);
            if (jumpSlam != null)
            {
                AssignReference(driver, FlowDriverJumpSlamField, jumpSlam);
            }

            // T39 예고 색
            var telegraphView = controller.GetComponent<BossTelegraphView>();
            if (telegraphView == null)
            {
                telegraphView = controller.gameObject.AddComponent<BossTelegraphView>();
            }

            AssignReference(telegraphView, TelegraphViewDriverField, driver);

            // T58 콤보 시작 신호 (증기·경고음 연결은 T59 에디터 작업)
            var comboSignal = controller.GetComponent<ComboSignalView>();
            if (comboSignal == null)
            {
                comboSignal = controller.gameObject.AddComponent<ComboSignalView>();
            }

            AssignReference(comboSignal, TelegraphViewDriverField, driver); // 같은 필드 이름 "_driver"

            // T61 그로기 게이지 (바 UI 는 에디터 작업)
            GroggyConfig groggyConfig = ResolveOrCreateConfig<GroggyConfig>(null, DefaultGroggyConfigPath);
            if (groggyConfig != null)
            {
                var groggy = controller.GetComponent<BossGroggyDriver>();
                if (groggy == null)
                {
                    groggy = controller.gameObject.AddComponent<BossGroggyDriver>();
                }

                AssignReference(groggy, GroggyConfigField, groggyConfig);
                AssignReference(groggy, GroggyBossField, controller);
                AssignReference(groggy, GroggyFlowDriverField, driver);
                AssignReference(groggy, GroggyPatternsField, patterns);
            }
            AssignReference(controller, BossFlowDriverField, driver);
            AssignBool(controller, BossUseFlowV2Field, true);
            return true;
        }

        /// <summary>
        /// 지정 에셋 → 프로젝트에서 찾은 첫 T 에셋 → 없으면 기본 경로에 새로 만든다 (기존 에셋은 수정하지 않음).
        /// </summary>
        internal static T ResolveOrCreateConfig<T>(T assigned, string defaultPath) where T : ScriptableObject
        {
            string typeName = typeof(T).Name;
            if (assigned != null)
            {
                return assigned;
            }

            string[] guids = AssetDatabase.FindAssets("t:" + typeName);
            if (guids.Length > 0)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (guids.Length > 1)
                {
                    GameLog.Warn($"[TestScene] {typeName} 가 {guids.Length}개 → 첫 번째 사용: {foundPath}");
                }

                return AssetDatabase.LoadAssetAtPath<T>(foundPath);
            }

            string directory = Path.GetDirectoryName(defaultPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(ToFullPath(directory)))
            {
                GameLog.Error(null, $"[TestScene] Config 폴더가 없어 {typeName} 를 만들지 못했다: {directory}");
                return null;
            }

            // 기본값 = 필드 초기값 (boss.md 임시 수치)
            string path = AssetDatabase.GenerateUniqueAssetPath(defaultPath);
            var created = CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            GameLog.Info($"[TestScene] {typeName} 새로 생성: {path}");
            return created;
        }

        private void CreateArena()
        {
            float halfWidth = _arenaWidth * 0.5f;
            float t = _wallThickness;

            Transform arena = new GameObject("Arena").transform;
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer < 0)
            {
                GameLog.Warn($"[TestScene] '{GroundLayerName}' 레이어가 없어 Default 로 만든다. 플레이어 착지 판정 확인 필요");
            }

            Sprite blockSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinSpritePath);

            CreateBlock(arena, "Ground", new Vector2(0f, -t * 0.5f), new Vector2(_arenaWidth + t * 2f, t),
                groundLayer, blockSprite);
            CreateBlock(arena, "Ceiling", new Vector2(0f, _arenaHeight + t * 0.5f),
                new Vector2(_arenaWidth + t * 2f, t), groundLayer, blockSprite);
            CreateBlock(arena, "WallLeft", new Vector2(-halfWidth - t * 0.5f, _arenaHeight * 0.5f),
                new Vector2(t, _arenaHeight), groundLayer, blockSprite);
            CreateBlock(arena, "WallRight", new Vector2(halfWidth + t * 0.5f, _arenaHeight * 0.5f),
                new Vector2(t, _arenaHeight), groundLayer, blockSprite);
        }

        /// <summary>이후 작업(T3 BossArenaConfig 등)에서 위치 기준으로 쓸 빈 마커.</summary>
        private void CreateMarkers(out Vector3 bossSpawn, out Vector3 playerSpawn)
        {
            float halfWidth = _arenaWidth * 0.5f;
            Transform markers = new GameObject("Markers").transform;
            bossSpawn = new Vector3(0f, _bossHeightAboveGround, 0f);
            playerSpawn = new Vector3(-halfWidth + _playerOffsetFromLeftWall, _playerHeightAboveGround, 0f);
            CreateMarker(markers, "Marker_BossSpawn", bossSpawn);
            CreateMarker(markers, "Marker_PlayerSpawn", playerSpawn);
            CreateMarker(markers, "Marker_MirrorBottomCenter", new Vector3(0f, 0.5f, 0f)); // spec: boss.md 2장 2P 거울
        }

        private void CreateBlock(Transform parent, string name, Vector2 center, Vector2 size, int layer, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            if (layer >= 0)
            {
                go.layer = layer;
            }

            if (sprite != null)
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Sliced; // 스케일 대신 size 로 크기 지정 (콜라이더와 1:1)
                renderer.size = size;
                renderer.color = _blockColor;
            }

            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
        }

        private static void CreateMarker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
        }

        private float ArenaOrthoSize => _arenaHeight * 0.5f + _cameraMargin; // 방 세로 전체 + 여백

        private Camera CreateCamera(Transform parent, Vector3 position)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ArenaOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _cameraBackground;

            go.AddComponent<AudioListener>();
            return cam;
        }

        /// <summary>
        /// 실제 게임 씬과 같은 카메라 구조: CameraRig(CameraFollow) ─ Main Camera(Brain) + CM_GamePlay + CM_Zoom.
        /// PlayerDash.Awake 가 Camera.main.transform.parent 의 CameraFollow 를 찾으므로 이 구조가 필요하다.
        /// </summary>
        private void CreateCameraRig(Vector3 position, Transform player)
        {
            Transform rig = new GameObject("CameraRig").transform;
            Camera cam = CreateCamera(rig, position);
            cam.gameObject.AddComponent<CinemachineBrain>();

            // 바닥에 선 플레이어 기준으로 방 세로 중앙이 화면 중앙에 오게
            var offset = new Vector3(0f, _arenaHeight * 0.5f - _playerHeightAboveGround, CameraDistanceZ);
            CinemachineCamera gamePlay = CreateVirtualCamera(rig, "CM_GamePlay", position, player, offset);
            CinemachineCamera zoom = CreateVirtualCamera(rig, "CM_Zoom", position, player, offset);
            zoom.gameObject.SetActive(false); // CameraFollow.Start 와 같은 초기 상태

            // CameraFollow 는 마지막에 추가: 에디터에서 AddComponent 해도 Awake 는 플레이 때만 실행된다
            var follow = rig.gameObject.AddComponent<CameraFollow>();
            AssignReference(follow, CameraFollowMainCameraField, cam);
            AssignReference(follow, CameraFollowZoomField, zoom);
            AssignReference(follow, CameraFollowGamePlayField, gamePlay);
            AssignReference(follow, CameraFollowTargetField, player);
        }

        private CinemachineCamera CreateVirtualCamera(
            Transform parent, string name, Vector3 position, Transform target, Vector3 followOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var vcam = go.AddComponent<CinemachineCamera>();
            LensSettings lens = vcam.Lens;
            lens.OrthographicSize = ArenaOrthoSize;
            vcam.Lens = lens;
            vcam.Follow = target;

            // CameraFollow.SetCameraDumping·ShakeCamera·줌이 CinemachineFollow 를 GetComponent 한다
            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = followOffset;
            return vcam;
        }

        /// <summary>
        /// 플레이어·보스 코드가 쓰는 전역 매니저를 루트 오브젝트로 만든다.
        /// essential object 프리팹은 매니저가 자식이라 DontDestroyOnLoad 경고가 나서 쓰지 않는다.
        /// </summary>
        private static void CreateRuntimeServices()
        {
            new GameObject("CustomTimeManager").AddComponent<CustomTimeManager>();
            new GameObject("HitstopManager").AddComponent<HitstopManager>();
            new GameObject("AudioManager").AddComponent<AudioManager>(); // 오디오 소스는 스스로 만든다
            new GameObject("GameClock").AddComponent<GameClockDriver>(); // T28
        }

        private static void CreateGlobalLight()
        {
            System.Type lightType = System.Type.GetType(Light2DTypeName);
            if (lightType == null)
            {
                GameLog.Warn("[TestScene] Light2D 타입을 찾지 못해 Global Light 2D 를 만들지 않았다. 필요하면 수동 추가");
                return;
            }

            var go = new GameObject("Global Light 2D");
            Component light = go.AddComponent(lightType);
            var so = new SerializedObject(light);

            SerializedProperty typeProp = so.FindProperty("m_LightType");
            if (typeProp != null)
            {
                typeProp.intValue = Light2DGlobalType;
            }

            // 모든 Sorting Layer 에 적용 (플레이어·보스가 Default 외 레이어를 써도 어둡지 않게)
            SerializedProperty layersProp = so.FindProperty("m_ApplyToSortingLayers");
            if (layersProp != null && layersProp.isArray)
            {
                SortingLayer[] sortingLayers = SortingLayer.layers;
                layersProp.arraySize = sortingLayers.Length;
                for (int i = 0; i < sortingLayers.Length; i++)
                {
                    layersProp.GetArrayElementAtIndex(i).intValue = sortingLayers[i].id;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform InstantiatePrefab(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                GameLog.Warn($"[TestScene] 프리팹 배치 실패: {prefab.name}");
                return null;
            }

            instance.transform.position = position;
            return instance.transform;
        }

        /// <summary>대상 컴포넌트의 직렬화 필드 "player" 에 플레이어를 연결한다 (새 씬 안의 인스턴스만 수정).</summary>
        private static void AssignPlayerReference(Component target, Transform player)
        {
            AssignReference(target, PlayerFieldName, player);
        }

        /// <summary>새 씬 안 컴포넌트의 직렬화 참조 필드를 채운다. 필드가 없으면 경고만 남긴다.</summary>
        private static void AssignReference(Component target, string fieldName, Object value)
        {
            if (target == null)
            {
                return;
            }

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                GameLog.Warn($"[TestScene] {target.GetType().Name} 에 '{fieldName}' 필드가 없다. 수동 연결 필요");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>새 씬 안 컴포넌트의 직렬화 bool 필드를 채운다. 필드가 없으면 경고만 남긴다.</summary>
        private static void AssignBool(Component target, string fieldName, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null || prop.propertyType != SerializedPropertyType.Boolean)
            {
                GameLog.Warn($"[TestScene] {target.GetType().Name} 에 bool '{fieldName}' 필드가 없다. 수동 설정 필요");
                return;
            }

            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
