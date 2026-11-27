// ─────────────────────────────────────────────────────────────────────────────
//  Unmachine UI v5 — 런타임 부트스트랩
//
//  ▸ 씬 파일/프리팹/팀원 스크립트를 하나도 수정하지 않는다.
//  ▸ 게임 시작 시 RuntimeInitializeOnLoadMethod 로 "[UI v5]" 오브젝트를 만들어 DontDestroyOnLoad.
//  ▸ 씬이 바뀔 때마다 PlayerState / BossController / DialogueManager 등을 타입으로 찾아
//    값을 "읽기만" 해서 v5 UI 를 그린다.
//  ▸ 기존 UI 는 CanvasGroup.alpha 로 화면에서만 가린다 (로직·활성상태 그대로).
//  ▸ 기존 UI 가 없던 씬(예: 거점)에는 v5 HUD 도 띄우지 않는다.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Reflection;
using BossSystem;
using DialogSystem.CinematicDialogue;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnMachine.Boss;

namespace Unmachine.UIv5
{
    [DefaultExecutionOrder(1000)] // 게임 로직 Update 이후에 읽는다
    [AddComponentMenu("")]
    public sealed class V5Root : MonoBehaviour
    {
        static V5Root _inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_inst != null || !V5Settings.IsEnabled) return;
            var go = new GameObject("[UI v5]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<V5Root>();
        }

        V5Settings _s;
        Canvas _hudCanvas, _dlgCanvas;
        V5PlayerHud _hud;
        V5BossBar _bossBar;
        V5BossPrompt _prompt;
        V5DialogueBand _band;

        // 씬 바인딩 (전부 읽기 전용)
        PlayerState _player;
        BossController _boss;
        DamageHandler _legacyBoss;
        BossGroggyDriver _groggy;
        DialogueManager _dialogue;
        GameObject _cinematicTop, _hudPanel;

        float _scanT;
        bool _showV5 = true;

        void Awake()
        {
            _s = V5Settings.Load();
            _hudCanvas = MakeCanvas("HUD", _s.hudSortingOrder);
            _dlgCanvas = MakeCanvas("Dialogue", _s.dialogueSortingOrder);

            if (_s.bossBar)
            {
                _bossBar = V5BossBar.Build(_hudCanvas.transform, _s);
                _prompt = V5BossPrompt.Build((RectTransform)_hudCanvas.transform, _s);
            }
            if (_s.playerHud) _hud = V5PlayerHud.Build(_hudCanvas.transform, _s);
            if (_s.dialogueBand) _band = V5DialogueBand.Build(_dlgCanvas.transform, _s);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start() { Scan(); _scanT = 0.5f; }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            V5LegacyMask.ReleaseAll();
            if (_inst == this) _inst = null;
        }

        Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            // GraphicRaycaster 없음 → 클릭/입력을 절대 가로채지 않는다
            return c;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _player = null; _boss = null; _legacyBoss = null; _groggy = null; _dialogue = null;
            _cinematicTop = null; _hudPanel = null;
            if (_bossBar != null) _bossBar.ResetState();
            _scanT = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_s.compareToggleKey != KeyCode.None && Input.GetKeyDown(_s.compareToggleKey))
            {
                _showV5 = !_showV5;
                _hudCanvas.enabled = _dlgCanvas.enabled = _showV5;
                if (!_showV5) V5LegacyMask.ReleaseAll();
                _scanT = 0f;
                Debug.Log("[UI v5] " + (_showV5 ? "v5 UI" : "기존 UI") + " 표시 (" + _s.compareToggleKey + ")");
            }
#endif
            _scanT -= dt;
            if (_scanT <= 0f) { _scanT = 0.5f; Scan(); }
            if (!_showV5) return;

            bool dialogueOpen = _band != null && _band.IsOpen;
            bool cinematic = _cinematicTop != null && _cinematicTop.activeInHierarchy;
            bool gameplay = !dialogueOpen && !cinematic;

            if (_hud != null) _hud.SetVisible(gameplay);

            if (_bossBar != null)
            {
                var b = SampleBoss();
                _bossBar.SetVisible(gameplay);
                _bossBar.Tick(b, dt);
                if (_prompt != null)
                {
                    _prompt.SetTarget(_boss != null ? _boss.transform : null);
                    _prompt.Tick(gameplay && b.present && b.groggy, dt);
                }
            }
        }

        V5BossSample SampleBoss()
        {
            var b = new V5BossSample { phaseSplit = BossController.Phase2HpRatio, phase = 1 };
            if (_boss != null && _boss.isActiveAndEnabled)
            {
                var st = _boss.GetCurrentState();
                b.present = true;
                b.engaged = true;
                b.hp = Mathf.Clamp01(_boss.DebugHpRatio);
                b.phase = _boss.GetCurrentPhase();
                b.invulnerable = _boss.IsInvulnerable;
                b.groggy = st == BossState.Groggy;
                b.stunned = st == BossState.Stunned;
                b.punishable = st == BossState.Punishable;
                b.dead = st == BossState.Dead || b.hp <= 0f;
                b.groggyGauge = _groggy != null ? _groggy.Normalized : 0f;
            }
            else if (_legacyBoss != null && _legacyBoss.isActiveAndEnabled)
            {
                b.present = true;
                b.engaged = true;
                b.hp = Mathf.Clamp01(_legacyBoss.HPRatio);
                b.phaseSplit = 0f; // 구 보스엔 페이즈 경계 없음
                b.invulnerable = _legacyBoss.IsInvincible;
                b.punishable = _legacyBoss.IsDealTimeActive;
                b.dead = b.hp <= 0f;
            }
            return b;
        }

        // ── 씬 스캔 (0.5초마다, 늦게 스폰되는 보스/플레이어 대응) ──
        void Scan()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerState>();
            if (_dialogue == null) _dialogue = DialogueManager.Instance != null ? DialogueManager.Instance : FindFirstObjectByType<DialogueManager>();
            if (_dialogue != null)
            {
                _cinematicTop = V5Reflect.Get<GameObject>(_dialogue, "_cinematicTopBar");
                _hudPanel = V5Reflect.Get<GameObject>(_dialogue, "_playerHUDPanel");
            }
            if (_boss == null) _boss = FindFirstObjectByType<BossController>();
            if (_boss == null && _legacyBoss == null) _legacyBoss = FindFirstObjectByType<DamageHandler>();
            if (_groggy == null && _boss != null) _groggy = FindFirstObjectByType<BossGroggyDriver>();

            var playerBridges = FindObjectsByType<PlayerStateBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool legacyHudInScene = playerBridges.Length > 0 || _hudPanel != null;

            if (_hud != null) _hud.Bind(legacyHudInScene ? _player : null);
            if (_band != null) _band.Bind(_dialogue);

            if (!_showV5 || !_s.hideLegacy) return;

            // ── 기존 UI 가리기 (v5 가 대신 그리는 것만) ──
            if (_hud != null && _hud.HasPlayer)
            {
                foreach (var br in playerBridges) V5LegacyFinder.HideGroup(br, UiRefs(br), IsForeign);
                if (_hudPanel != null && !IsForeign(_hudPanel.transform)) V5LegacyMask.Apply(_hudPanel);
            }
            // 보스바는 v5 가 대신하므로 보스가 없는 씬에서도 기존 보스바는 가린다
            if (_bossBar != null)
            {
                foreach (var c in FindObjectsByType<BossHealthBarBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    V5LegacyFinder.HideGroup(c, UiRefs(c), IsForeign);
                foreach (var c in FindObjectsByType<BossHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    V5LegacyFinder.HideGroup(c, UiRefs(c), IsForeign);
                foreach (var c in FindObjectsByType<BossGroggyBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    V5LegacyFinder.HideGroup(c, UiRefs(c), IsForeign);
            }
            if (_band != null && _band.IsBound) V5LegacyMask.Apply(_band.SourcePanel);
        }

        /// <summary>남의 UI(대화창·시스템메시지·레터박스)까지 품은 노드인지.</summary>
        bool IsForeign(Transform t)
        {
            if (t == null) return true;
            if (t.GetComponentInChildren<SystemMessageUI>(true) != null) return true;
            if (t.GetComponentInChildren<DialogueManager>(true) != null) return true;
            var panel = _band != null ? _band.SourcePanel : null;
            if (panel != null && (panel.transform == t || panel.transform.IsChildOf(t))) return true;
            if (_cinematicTop != null && _cinematicTop.transform.IsChildOf(t)) return true;
            return false;
        }

        /// <summary>컴포넌트가 직렬화로 물고 있는 UI 참조들 (RectTransform 을 가진 것만).</summary>
        static List<Component> UiRefs(Component c)
        {
            var list = new List<Component>();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var t = c.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var fi in t.GetFields(F))
                {
                    if (typeof(Component).IsAssignableFrom(fi.FieldType))
                    {
                        var v = fi.GetValue(c) as Component;
                        if (v != null && v.transform is RectTransform) list.Add(v);
                    }
                    else if (fi.FieldType == typeof(GameObject))
                    {
                        var g = fi.GetValue(c) as GameObject;
                        if (g != null && g.transform is RectTransform) list.Add(g.transform);
                    }
                }
            return list;
        }
    }
}
