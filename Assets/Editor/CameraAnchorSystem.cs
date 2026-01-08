using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace EditorTools
{
    [System.Serializable]
    public class CameraAnchor
    {
        public Vector3 position;
        public Quaternion rotation;
        public float size;
        public bool isOrthographic;
        
        public CameraAnchor(Vector3 pos, Quaternion rot, float sz, bool ortho)
        {
            position = pos;
            rotation = rot;
            size = sz;
            isOrthographic = ortho;
        }
    }

    [System.Serializable]
    public class CameraAnchorData
    {
        public List<CameraAnchor> anchors = new List<CameraAnchor>(9);
        
        public CameraAnchorData()
        {
            // Initialize with 9 empty slots
            for (int i = 0; i < 9; i++)
            {
                anchors.Add(null);
            }
        }
    }

    [InitializeOnLoad]
    public class CameraAnchorSystem
    {
        private static CameraAnchorData anchorData;
        private static string savePath => Path.Combine(Application.dataPath, "../Library/CameraAnchors.json");

        static CameraAnchorSystem()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadAnchors();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;
            
            if (e.type == EventType.KeyDown && (e.control || e.command))
            {
                // Check for number keys 1-9
                if (e.keyCode >= KeyCode.Alpha1 && e. keyCode <= KeyCode.Alpha9)
                {
                    int index = e.keyCode - KeyCode.Alpha1;
                    
                    if (e.shift)
                    {
                        // Shift + Ctrl/Cmd + Number:   Save anchor
                        SaveAnchor(sceneView, index);
                        e.Use();
                    }
                    else
                    {
                        // Ctrl/Cmd + Number:  Load anchor
                        LoadAnchor(sceneView, index);
                        e.Use();
                    }
                }
            }
        }

        public static void SaveAnchor(SceneView sceneView, int index)
        {
            if (anchorData == null)
                anchorData = new CameraAnchorData();

            anchorData.anchors[index] = new CameraAnchor(
                sceneView.pivot,
                sceneView.rotation,
                sceneView.size,
                sceneView.orthographic
            );

            SaveAnchorsToFile();
            Debug.Log($"Camera anchor {index + 1} saved at position {sceneView.pivot}, rotation {sceneView.rotation.  eulerAngles}");
            
            // Refresh the window if it's open
            if (EditorWindow.HasOpenInstances<CameraAnchorWindow>())
            {
                EditorWindow.GetWindow<CameraAnchorWindow>().Repaint();
            }
        }

        private static void LoadAnchor(SceneView sceneView, int index)
        {
            if (anchorData == null || anchorData.anchors[index] == null)
            {
                Debug.LogWarning($"No camera anchor saved in slot {index + 1}.   Use Shift+Ctrl/Cmd+{index + 1} to save one.");
                return;
            }

            CameraAnchor anchor = anchorData.anchors[index];
            
            sceneView.pivot = anchor.position;
            sceneView.rotation = anchor.rotation;
            sceneView.size = anchor.size;
            sceneView.orthographic = anchor.isOrthographic;
            
            sceneView.Repaint();
            Debug.Log($"Switched to camera anchor {index + 1}");
        }

        private static void SaveAnchorsToFile()
        {
            try
            {
                string json = JsonUtility.ToJson(anchorData, true);
                File.WriteAllText(savePath, json);
            }
            catch (System.Exception e)
            {
                Debug.  LogError($"Failed to save camera anchors: {e.  Message}");
            }
        }

        private static void LoadAnchors()
        {
            if (File. Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    anchorData = JsonUtility.FromJson<CameraAnchorData>(json);
                    Debug.Log("Camera anchors loaded");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load camera anchors: {e.Message}");
                    anchorData = new CameraAnchorData();
                }
            }
            else
            {
                anchorData = new CameraAnchorData();
            }
        }

        public static void ResetAllAnchors()
        {
            anchorData = new CameraAnchorData();
            SaveAnchorsToFile();
            Debug.  Log("All camera anchors have been reset");
        }

        public static CameraAnchorData GetAnchorData()
        {
            return anchorData;
        }

        public static void DeleteAnchor(int index)
        {
            if (anchorData != null && index >= 0 && index < anchorData.anchors.Count)
            {
                anchorData.anchors[index] = null;
                SaveAnchorsToFile();
                Debug.Log($"Camera anchor {index + 1} deleted");
            }
        }
    }

    public class CameraAnchorWindow :   EditorWindow
    {
        private Vector2 scrollPosition;
        private bool showAllSlots = false;

        [MenuItem("Window/Camera Anchor Manager")]
        public static void ShowWindow()
        {
            GetWindow<CameraAnchorWindow>("Camera Anchors");
        }

        private void OnGUI()
        {
            GUILayout.Label("Camera Anchor Manager", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Ctrl/Cmd + 1-9: Switch to anchor\n" +
                "Shift + Ctrl/Cmd + 1-9: Save current view as anchor",
                MessageType.Info
            );

            GUILayout.Space(10);

            var anchorData = CameraAnchorSystem.GetAnchorData();

            if (anchorData != null)
            {
                // Count how many anchors are saved
                int savedAnchorCount = 0;
                for (int i = 0; i < 9; i++)
                {
                    if (anchorData.  anchors[i] != null)
                    {
                        savedAnchorCount++;
                    }
                }

                // Toggle to show all slots
                showAllSlots = EditorGUILayout.Toggle("Show All Anchor Slots", showAllSlots);
                GUILayout.Space(5);

                // Only show the reset button if there are saved anchors
                if (savedAnchorCount > 0)
                {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Reset All Anchors", GUILayout.Height(30)))
                    {
                        if (EditorUtility.DisplayDialog("Reset All Anchors",
                            "Are you sure you want to delete all saved camera anchors? This cannot be undone.",
                            "Reset", "Cancel"))
                        {
                            CameraAnchorSystem.ResetAllAnchors();
                            Repaint();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.Space(10);
                }

                scrollPosition = EditorGUILayout.  BeginScrollView(scrollPosition);

                if (savedAnchorCount == 0 && ! showAllSlots)
                {
                    // No anchors saved - show help message
                    GUILayout.Space(20);
                    EditorGUILayout. LabelField("No anchors saved yet", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox(
                        "Enable 'Show All Anchor Slots' to see save buttons, or press Shift + Ctrl/Cmd + 1-9 to save an anchor.",
                        MessageType.  Info
                    );
                }
                else
                {
                    // Show anchors based on toggle
                    for (int i = 0; i < 9; i++)
                    {
                        if (showAllSlots || anchorData. anchors[i] != null)
                        {
                            DrawAnchorSlot(i, anchorData.anchors[i]);
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAnchorSlot(int index, CameraAnchor anchor)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (anchor != null)
            {
                // Saved anchor
                EditorGUILayout.LabelField($"Anchor {index + 1}", EditorStyles. boldLabel);
                
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Position:", anchor.position.ToString("F2"));
                EditorGUILayout.LabelField("Rotation:", anchor.rotation.eulerAngles.ToString("F2"));
                EditorGUILayout.LabelField("Size:", anchor.size.ToString("F2"));
                EditorGUILayout.  LabelField("Orthographic:", anchor.isOrthographic.ToString());
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Go to Anchor"))
                {
                    SceneView sceneView = SceneView. lastActiveSceneView;
                    if (sceneView != null)
                    {
                        sceneView.pivot = anchor.position;
                        sceneView. rotation = anchor.rotation;
                        sceneView.size = anchor.size;
                        sceneView. orthographic = anchor.isOrthographic;
                        sceneView.Repaint();
                    }
                }

                GUI. backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout. Button($"Save to {index + 1}", GUILayout.Width(80)))
                {
                    SceneView sceneView = SceneView. lastActiveSceneView;
                    if (sceneView != null)
                    {
                        CameraAnchorSystem.SaveAnchor(sceneView, index);
                        Repaint();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("No Scene View",
                            "Please open a Scene view before saving an anchor.",
                            "OK");
                    }
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("Delete Anchor",
                        $"Are you sure you want to delete anchor {index + 1}? ",
                        "Delete", "Cancel"))
                    {
                        CameraAnchorSystem.DeleteAnchor(index);
                        Repaint();
                    }
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
            }
            else
            {
                // Empty slot (only shown when showAllSlots is true)
                EditorGUILayout.LabelField($"Anchor {index + 1}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Empty slot", EditorStyles.centeredGreyMiniLabel);
                
                GUILayout.Space(5);
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button($"Save to Anchor {index + 1}"))
                {
                    SceneView sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null)
                    {
                        CameraAnchorSystem.SaveAnchor(sceneView, index);
                        Repaint();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("No Scene View",
                            "Please open a Scene view before saving an anchor.",
                            "OK");
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }
    }
}