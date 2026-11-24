using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnMachine.EditorTools
{
    /// <summary>
    /// 보스 테스트 씬(Test_*.unity)을 새로 만드는 에디터 창.
    /// AI_RULES 0장 2번 예외: 새 Test_* 씬만 만든다. 기존 씬은 열거나 저장하지 않고, 같은 이름이면 중단한다.
    /// </summary>
    public sealed class BossTestSceneBuilder : EditorWindow
    {
        private const string MenuPath = "UnMachine/Test Scene/Boss Arena";
        private const string DefaultPlayerPrefabPath = "Assets/CSH/8.Prefabs/Player/Player.prefab";
        private const string RequiredFilePrefix = "Test_";
        private const string GroundLayerName = "ground";
        private const string BuiltinSpritePath = "UI/Skin/UISprite.psd";
        private const string PlayerFieldName = "player";

        // URP 2D Global Light. 어셈블리 직접 참조 없이 만들려고 이름으로 찾는다.
        private const string Light2DTypeName =
            "UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime";
        private const int Light2DGlobalType = 4; // Light2D.LightType.Global

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

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<BossTestSceneBuilder>("Boss Test Scene");
        }

        private void OnEnable()
        {
            if (_playerPrefab == null)
            {
                _playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
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
            if (GUILayout.Button("테스트 씬 만들기", GUILayout.Height(30f)))
            {
                TryBuild();
            }
        }

        private void TryBuild()
        {
            if (!ValidatePath(out string error))
            {
                EditorUtility.DisplayDialog("테스트 씬", error, "확인");
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

            if (File.Exists(path))
            {
                error = $"{path} 가 이미 있다. 덮어쓰지 않는다. 다른 이름을 쓰거나 기존 파일을 직접 지운다.";
                return false;
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                error = $"폴더가 없다: {directory}";
                return false;
            }

            return true;
        }

        private void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 좌표 기준: 바닥 윗면 y = 0, 방 가로 중앙 x = 0
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

            // 이후 작업(T3 BossArenaConfig 등)에서 위치 기준으로 쓸 빈 마커
            Transform markers = new GameObject("Markers").transform;
            Vector3 bossSpawn = new Vector3(0f, _bossHeightAboveGround, 0f);
            Vector3 playerSpawn = new Vector3(-halfWidth + _playerOffsetFromLeftWall, _playerHeightAboveGround, 0f);
            CreateMarker(markers, "Marker_BossSpawn", bossSpawn);
            CreateMarker(markers, "Marker_PlayerSpawn", playerSpawn);
            CreateMarker(markers, "Marker_MirrorBottomCenter", new Vector3(0f, 0.5f, 0f)); // spec: boss.md 2장 2P 거울

            CreateCamera(new Vector3(0f, _arenaHeight * 0.5f, -10f));
            CreateGlobalLight();

            Transform player = InstantiatePrefab(_playerPrefab, playerSpawn);
            Transform bossTransform = InstantiatePrefab(_bossPrefab, bossSpawn);
            GameObject boss = bossTransform != null ? bossTransform.gameObject : null;

            if (boss != null && player != null)
            {
                AssignPlayerReference(boss.GetComponentInChildren<BossController>(true), player);
                AssignPlayerReference(boss.GetComponentInChildren<BossAttackPatterns>(true), player);
            }

            if (!EditorSceneManager.SaveScene(scene, _scenePath))
            {
                GameLog.Error(null, $"[TestScene] 저장 실패: {_scenePath}");
                return;
            }

            GameLog.Info($"[TestScene] 생성 완료: {_scenePath} (보스 {(boss != null ? "배치" : "마커만")}, " +
                         $"플레이어 {(player != null ? "배치" : "없음")})");
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

        private void CreateCamera(Vector3 position)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = position;

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = _arenaHeight * 0.5f + _cameraMargin; // 방 세로 전체 + 여백
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _cameraBackground;

            go.AddComponent<AudioListener>();
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
            if (target == null)
            {
                return;
            }

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(PlayerFieldName);
            if (prop == null)
            {
                return;
            }

            prop.objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
