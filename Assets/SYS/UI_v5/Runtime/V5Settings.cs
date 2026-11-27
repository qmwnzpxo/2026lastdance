// ─────────────────────────────────────────────────────────────────────────────
//  Unmachine UI v5 — 설정
//  Resources/UIv5Settings.asset 하나로 동작한다. 씬/프리팹을 건드리지 않는다.
//  에디터에서는 메뉴 [Unmachine/UI v5/이 PC에서 켜기·끄기]로 PC별로만 끌 수 있다
//  (EditorPrefs 저장이라 git에 안 올라가고 팀원에게 영향 없음).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Unmachine.UIv5
{
    [Serializable]
    public class V5SpeakerStyle
    {
        [Tooltip("speakerName 에 이 문자열이 들어 있으면 적용")] public string match = "";
        public Sprite portrait;
        [Tooltip("portrait 스프라이트 안에서 얼굴 영역 (0~1 UV)")] public Rect portraitUV = new Rect(0, 0, 1, 1);
        [Tooltip("이름 옆 태그 (예: MERC, SYS). 비우면 표시 안 함")] public string tag = "";
        [Tooltip("이름 틱 색. alpha 0 이면 기본 시그널 옐로")] public Color accent = new Color(0, 0, 0, 0);
    }

    [CreateAssetMenu(menuName = "Unmachine/UI v5 Settings", fileName = "UIv5Settings")]
    public class V5Settings : ScriptableObject
    {
        public const string ResourcePath = "UIv5Settings";
        public const string EditorPrefKey = "Unmachine.UIv5.Enabled";

        [Header("켜기")]
        [Tooltip("빌드/기본값. 에디터에선 EditorPrefs(이 PC 전용)가 우선")]
        public bool enabledByDefault = true;
        [Tooltip("플레이 중 v5 ↔ 기존 UI 비교 전환 키 (에디터·개발빌드 전용)")]
        public KeyCode compareToggleKey = KeyCode.F8;

        [Header("모듈")]
        public bool playerHud = true;
        public bool bossBar = true;
        public bool dialogueBand = true;
        [Tooltip("기존 UI(Player UI, 보스바, 대화창)를 화면에서만 숨김. 오브젝트/로직은 그대로")]
        public bool hideLegacy = true;

        [Header("폰트 (비우면 TMP 기본 폰트)")]
        public TMP_FontAsset numberFont;     // 숫자·영문 (RobotoCondensed-Bold)
        public TMP_FontAsset krBoldFont;     // 이름·태그 (Galmuri11-Bold)
        public TMP_FontAsset krBodyFont;     // 대사 본문 (GalmuriMono11)

        [Header("플레이어")]
        public Sprite playerPortrait;
        public Rect playerPortraitUV = new Rect(0.30f, 0.48f, 0.40f, 0.40f);
        [Range(0.05f, 0.6f)] public float lowHpRatio = 0.3f;
        public KeyCode chipSwitchKeyLabel = KeyCode.B;

        [Header("보스")]
        public string bossName = "판타스틱 베이비";
        public string bossSubtitle = "FANTASTIC BABY / MERC-BUILT ASSAULT MECH";
        [Tooltip("true: 화면 하단 바닥 영역 / false: 상단 (boss.md 15장 원안)")]
        public bool bossBarAtBottom = true;
        public string rakusenKeyLabel = "RMB";
        public string rakusenLabel = "낙섬!";

        [Header("대화")]
        public List<V5SpeakerStyle> speakers = new List<V5SpeakerStyle>();

        [Header("정렬 (기존 캔버스 대부분 0)")]
        [Tooltip("페이드·로딩 같은 팀원 캔버스(0) 아래로 깔리도록 -1")]
        public int hudSortingOrder = -1;
        [Tooltip("시네마틱 레터박스(0) 위에 대화 밴드가 보이도록 1")]
        public int dialogueSortingOrder = 1;

        static V5Settings _cached;

        public static V5Settings Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<V5Settings>(ResourcePath);
            if (_cached == null) _cached = CreateInstance<V5Settings>();
            return _cached;
        }

        /// <summary>현재 PC/빌드에서 v5 를 켤지.</summary>
        public static bool IsEnabled
        {
            get
            {
#if UNITY_EDITOR
                int pref = UnityEditor.EditorPrefs.GetInt(EditorPrefKey, -1);
                if (pref >= 0) return pref == 1;
#endif
                return Load().enabledByDefault;
            }
        }

        public V5SpeakerStyle FindSpeaker(string speakerName)
        {
            if (string.IsNullOrEmpty(speakerName)) return null;
            for (int i = 0; i < speakers.Count; i++)
            {
                var s = speakers[i];
                if (s != null && !string.IsNullOrEmpty(s.match) && speakerName.Contains(s.match)) return s;
            }
            return null;
        }
    }
}
