using UnityEditor;
using UnityEngine;

public class EditorOnlyAttribute : PropertyAttribute { }

public class RuntimeOnlyAttribute : PropertyAttribute { }


[CustomEditor(typeof(MonoBehaviour) ,true)]
public class PlayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var prop = serializedObject.GetIterator();
        prop.NextVisible(true); // script 필드 skip

        GUI.enabled = false;
        EditorGUILayout.PropertyField(prop);
        GUI.enabled = true;

        while (prop.NextVisible(false))
        {
            var fieldInfo = target.GetType().GetField(
                prop.name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            );

            bool isEditorOnly = fieldInfo?.GetCustomAttributes(typeof(EditorOnlyAttribute), false).Length > 0;
            bool isRuntimeOnly = fieldInfo?.GetCustomAttributes(typeof(RuntimeOnlyAttribute), false).Length > 0;

            if (EditorApplication.isPlaying)
            {
                if (isEditorOnly) continue; // 플레이 중 숨김
            }
            else
            {
                if (isRuntimeOnly) continue; // 에디터 중 숨김
            }

            EditorGUILayout.PropertyField(prop, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}