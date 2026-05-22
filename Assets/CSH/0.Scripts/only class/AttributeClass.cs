using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EditorOnlyAttribute : PropertyAttribute { }

public class RuntimeOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(EditorOnlyAttribute))]
public class EditorOnlyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorApplication.isPlaying
            ? 0
            : EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}

[CustomPropertyDrawer(typeof(RuntimeOnlyAttribute))]
public class RuntimeOnlyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorApplication.isPlaying
            ? EditorGUI.GetPropertyHeight(property, label, true)
            : 0;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (EditorApplication.isPlaying)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}

#endif

#region Custom Editor 기존 코드
//(EditorOnlyAttribute, RuntimeOnlyAttribute 정의 및 PlayerEditor 클래스) - 단일 CustomEditor에서 처리하는 방식
//using UnityEngine;


//#if UNITY_EDITOR
//using UnityEditor;
//#endif 

//public class EditorOnlyAttribute : PropertyAttribute { }

//public class RuntimeOnlyAttribute : PropertyAttribute { }


//#if UNITY_EDITOR
//[CustomEditor(typeof(MonoBehaviour) ,true)]
//public class PlayerEditor : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        var prop = serializedObject.GetIterator();
//        prop.NextVisible(true); // script 필드 skip

//        GUI.enabled = false;
//        EditorGUILayout.PropertyField(prop);
//        GUI.enabled = true;

//        while (prop.NextVisible(false))
//        {
//            var fieldInfo = target.GetType().GetField(
//                prop.name,
//                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
//            );

//            bool isEditorOnly = fieldInfo?.GetCustomAttributes(typeof(EditorOnlyAttribute), false).Length > 0;
//            bool isRuntimeOnly = fieldInfo?.GetCustomAttributes(typeof(RuntimeOnlyAttribute), false).Length > 0;

//            if (EditorApplication.isPlaying)
//            {
//                if (isEditorOnly) continue; // 플레이 중 숨김
//            }
//            else
//            {
//                if (isRuntimeOnly) continue; // 에디터 중 숨김
//            }

//            EditorGUILayout.PropertyField(prop, true);
//        }

//        serializedObject.ApplyModifiedProperties();
//    }
//}

//#endif

#endregion