#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(WeatherSystemManager))]
[CanEditMultipleObjects]
public class WeatherSystemManagerEditor : Editor
{
    private static bool s_showFMODSettings = false;

    // FMOD property names (must match WeatherSystemManager fields)
    private static readonly string[] FMOD_PROP_ORDER = new[]
    {
        "rainLoopEvent",
        "rainImpactEvent",
        "windEvent",
        "windDirectionEvent",
        "thunderEvent",
        "rainParameterName",
        "windParameterName",
        "windDegreesParameterName",
        "rainOcclusionEQParameterName",
        "rainOcclusionVolumeParameterName",
        "thunderLevelParameterName"
    };

    private static readonly HashSet<string> FMOD_PROP_SET = new HashSet<string>(FMOD_PROP_ORDER);

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw all non-FMOD properties (in declared order) and inject the button after 'thunderLevel'
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            // Skip FMOD settings here; we'll draw them in a foldout below
            if (FMOD_PROP_SET.Contains(iterator.name))
            {
                enterChildren = false;
                continue;
            }

            using (new EditorGUI.DisabledScope(iterator.name == "m_Script"))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            // Insert the button directly under Thunder Settings (right after 'thunderLevel')
            if (iterator.name == "thunderLevel")
            {
                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Generate Thunder", GUILayout.Height(24)))
                    {
                        foreach (var t in targets)
                        {
                            var mgr = t as WeatherSystemManager;
                            if (mgr != null) mgr.GenerateThunder();
                        }
                    }
                }

                if (Application.isPlaying)
                {
                    bool showHint = false;
                    foreach (var t in targets)
                    {
                        var mgr = t as WeatherSystemManager;
                        if (mgr != null && mgr.thunderLevel == ThunderLevel.None)
                        {
                            showHint = true; break;
                        }
                    }
                    if (showHint)
                    {
                        EditorGUILayout.HelpBox("ThunderLevel is None", MessageType.Info);
                    }
                }
            }

            enterChildren = false;
        }

        EditorGUILayout.Space(8);
        DrawFMODFoldout();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFMODFoldout()
    {
        // Collapsible FMOD Settings section
        s_showFMODSettings = EditorGUILayout.BeginFoldoutHeaderGroup(s_showFMODSettings, "FMOD");
        if (s_showFMODSettings)
        {
            EditorGUI.indentLevel++;
            foreach (string propName in FMOD_PROP_ORDER)
            {
                var p = serializedObject.FindProperty(propName);
                if (p != null)
                {
                    EditorGUILayout.PropertyField(p, true);
                }
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
#endif