using UnityEditor;
using UnityEngine;

namespace Unmachine.UIv5.EditorTools
{
    /// <summary>
    /// 이 PC 에서만 v5 를 켜고 끄는 메뉴. EditorPrefs 에 저장 → git 에 안 올라감.
    /// </summary>
    public static class V5EditorMenu
    {
        const string Root = "Unmachine/UI v5/";

        [MenuItem(Root + "이 PC에서 켜기", false, 0)]
        static void On() { EditorPrefs.SetInt(V5Settings.EditorPrefKey, 1); Debug.Log("[UI v5] 이 PC에서 켬 (다음 플레이부터)"); }

        [MenuItem(Root + "이 PC에서 끄기", false, 1)]
        static void Off() { EditorPrefs.SetInt(V5Settings.EditorPrefKey, 0); Debug.Log("[UI v5] 이 PC에서 끔 (다음 플레이부터)"); }

        [MenuItem(Root + "이 PC 설정 초기화 (에셋 기본값 사용)", false, 2)]
        static void Reset() { EditorPrefs.DeleteKey(V5Settings.EditorPrefKey); Debug.Log("[UI v5] 이 PC 설정 초기화 → UIv5Settings.enabledByDefault 사용"); }

        [MenuItem(Root + "이 PC에서 켜기", true)]
        static bool OnValidate() { Menu.SetChecked(Root + "이 PC에서 켜기", V5Settings.IsEnabled); return true; }

        [MenuItem(Root + "이 PC에서 끄기", true)]
        static bool OffValidate() { Menu.SetChecked(Root + "이 PC에서 끄기", !V5Settings.IsEnabled); return true; }

        [MenuItem(Root + "설정 에셋 선택", false, 20)]
        static void SelectSettings()
        {
            var s = Resources.Load<V5Settings>(V5Settings.ResourcePath);
            if (s == null) { Debug.LogWarning("[UI v5] Resources/UIv5Settings.asset 없음"); return; }
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }
    }
}
