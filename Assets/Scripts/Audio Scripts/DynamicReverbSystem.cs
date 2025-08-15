using UnityEngine;
using System.Collections.Generic;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AudioSystems
{
    /// <summary>
    /// Dynamic Reverb System that analyzes room size using raycasts and controls FMOD reverb parameters
    /// </summary>
    [System.Serializable]
    public class RaycastSettings
    {
        [Header("Raycast Configuration")]
        [Range(4, 32)]
        public int horizontalRays = 16;
        
        [Range(1, 7)]
        public int verticalLayers = 3;
        
        [Range(1f, 100f)]
        public float maxRayDistance = 50f;
        
        [Header("Layer Detection")]
        public LayerMask wallLayerMask = -1;
        
        [Header("Performance")]
        [Range(0.1f, 5f)]
        public float updateFrequency = 2f;
        
        [Range(1, 10)]
        public int raysPerFrame = 4;
    }

    [System.Serializable]
    public class VolumeCalculation
    {
        [Header("Volume Settings")]
        [Range(0.1f, 2f)]
        public float smoothingFactor = 0.5f;
        
        [Range(1f, 1000f)]
        public float minRoomVolume = 8f;
        
        [Range(10f, 100000f)]
        public float maxRoomVolume = 50000f;
        
        [Header("Fallback Values")]
        public float defaultRoomSize = 100f;
        public float openAreaMultiplier = 2f;
    }

    [System.Serializable]
    public class FMODSettings
    {
        [Header("FMOD Integration")]
        public string roomSizeParameterName = "RoomSize";
        
        [Range(0f, 1f)]
        public float minParameterValue = 0f;
        
        [Range(0f, 1f)]
        public float maxParameterValue = 1f;
        
        public AnimationCurve volumeToParameterCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    [System.Serializable]
    public class DebugSettings
    {
        [Header("Debug Visualization")]
        public bool showRays = true;
        public bool showHitPoints = true;
        public bool showBoundingBox = true;
        public bool showVolumeInfo = true;
        
        [Header("Debug Colors")]
        public Color rayColor = Color.yellow;
        public Color hitColor = Color.red;
        public Color missColor = Color.green;
        public Color boundingBoxColor = Color.cyan;
    }

    public class DynamicReverbSystem : MonoBehaviour
    {
        [Header("System Settings")]
        public RaycastSettings raycastSettings = new RaycastSettings();
        public VolumeCalculation volumeSettings = new VolumeCalculation();
        public FMODSettings fmodSettings = new FMODSettings();
        public DebugSettings debugSettings = new DebugSettings();
        
        // Private variables
        private List<Vector3> hitPoints = new List<Vector3>();
        private List<RaycastData> raycastQueue = new List<RaycastData>();
        private Bounds currentBounds;
        private float currentVolume;
        private float targetVolume;
        private float lastUpdateTime;
        private int currentRayIndex;
        private bool isProcessingRays;
        
        // FMOD Event Instance (if using FMOD)
        private FMOD.Studio.EventInstance reverbEventInstance;
        private bool fmodInitialized;
        
        [System.Serializable]
        private struct RaycastData
        {
            public Vector3 origin;
            public Vector3 direction;
            public float distance;
        }
        
        // Public properties for debugging
        public float CurrentVolume => currentVolume;
        public int HitPointCount => hitPoints.Count;
        public Bounds CurrentBounds => currentBounds;
        
        private void Start()
        {
            InitializeFMOD();
            GenerateRaycastDirections();
            StartCoroutine(UpdateRoomDetection());
        }
        
        private void InitializeFMOD()
        {
            try
            {
                #if FMOD_STUDIO
                // Check if FMOD is available and initialized
                if (FMODUnity.RuntimeManager.IsInitialized)
                {
                    fmodInitialized = true;
                    Debug.Log("Dynamic Reverb System: FMOD initialized successfully");
                }
                else
                {
                    Debug.LogWarning("Dynamic Reverb System: FMOD not initialized");
                }
                #else
                Debug.LogWarning("Dynamic Reverb System: FMOD Studio not available");
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Dynamic Reverb System: FMOD initialization failed - {e.Message}");
                fmodInitialized = false;
            }
        }
        
        private void GenerateRaycastDirections()
        {
            raycastQueue.Clear();
            Vector3 origin = transform.position;
            
            // Generate horizontal rays
            for (int layer = 0; layer < raycastSettings.verticalLayers; layer++)
            {
                float verticalAngle = (layer - (raycastSettings.verticalLayers - 1) * 0.5f) * (60f / raycastSettings.verticalLayers);
                float y = Mathf.Sin(verticalAngle * Mathf.Deg2Rad);
                float horizontalRadius = Mathf.Cos(verticalAngle * Mathf.Deg2Rad);
                
                for (int i = 0; i < raycastSettings.horizontalRays; i++)
                {
                    float angle = (360f / raycastSettings.horizontalRays) * i;
                    float x = horizontalRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                    float z = horizontalRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                    
                    Vector3 direction = new Vector3(x, y, z).normalized;
                    
                    raycastQueue.Add(new RaycastData
                    {
                        origin = origin,
                        direction = direction,
                        distance = raycastSettings.maxRayDistance
                    });
                }
            }
            
            currentRayIndex = 0;
        }
        
        private IEnumerator UpdateRoomDetection()
        {
            while (true)
            {
                if (Time.time - lastUpdateTime >= (1f / raycastSettings.updateFrequency))
                {
                    yield return StartCoroutine(ProcessRaycastBatch());
                    lastUpdateTime = Time.time;
                }
                yield return null;
            }
        }
        
        private IEnumerator ProcessRaycastBatch()
        {
            if (raycastQueue.Count == 0)
            {
                GenerateRaycastDirections();
                yield break;
            }
            
            isProcessingRays = true;
            hitPoints.Clear();
            
            int raysProcessed = 0;
            int startIndex = currentRayIndex;
            
            while (raysProcessed < raycastSettings.raysPerFrame && currentRayIndex < raycastQueue.Count)
            {
                ProcessSingleRaycast(raycastQueue[currentRayIndex]);
                currentRayIndex++;
                raysProcessed++;
                
                // Yield control every few rays to prevent frame drops
                if (raysProcessed % 2 == 0)
                    yield return null;
            }
            
            // If we've processed all rays, calculate volume and reset
            if (currentRayIndex >= raycastQueue.Count)
            {
                CalculateRoomVolume();
                UpdateFMODParameter();
                currentRayIndex = 0;
                GenerateRaycastDirections(); // Regenerate for next cycle
            }
            
            isProcessingRays = false;
        }
        
        private void ProcessSingleRaycast(RaycastData rayData)
        {
            RaycastHit hit;
            Vector3 worldOrigin = transform.position + rayData.origin;
            
            if (Physics.Raycast(worldOrigin, rayData.direction, out hit, rayData.distance, raycastSettings.wallLayerMask))
            {
                hitPoints.Add(hit.point);
                
                if (debugSettings.showRays)
                {
                    Debug.DrawRay(worldOrigin, rayData.direction * hit.distance, debugSettings.hitColor, 1f / raycastSettings.updateFrequency);
                }
            }
            else
            {
                // No hit - assume open area, add point at max distance
                Vector3 openPoint = worldOrigin + rayData.direction * rayData.distance;
                hitPoints.Add(openPoint);
                
                if (debugSettings.showRays)
                {
                    Debug.DrawRay(worldOrigin, rayData.direction * rayData.distance, debugSettings.missColor, 1f / raycastSettings.updateFrequency);
                }
            }
        }
        
        private void CalculateRoomVolume()
        {
            if (hitPoints.Count < 3)
            {
                targetVolume = volumeSettings.defaultRoomSize;
                return;
            }
            
            // Calculate bounding box from hit points
            Vector3 min = hitPoints[0];
            Vector3 max = hitPoints[0];
            
            foreach (Vector3 point in hitPoints)
            {
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
            
            currentBounds = new Bounds((min + max) * 0.5f, max - min);
            
            // Calculate volume
            Vector3 size = currentBounds.size;
            float calculatedVolume = size.x * size.y * size.z;
            
            // Apply constraints and smoothing
            calculatedVolume = Mathf.Clamp(calculatedVolume, volumeSettings.minRoomVolume, volumeSettings.maxRoomVolume);
            targetVolume = calculatedVolume;
            
            // Smooth the transition
            currentVolume = Mathf.Lerp(currentVolume, targetVolume, volumeSettings.smoothingFactor * Time.deltaTime * raycastSettings.updateFrequency);
        }
        
        private void UpdateFMODParameter()
        {
            if (!fmodInitialized)
                return;
                
            try
            {
                #if FMOD_STUDIO
                // Normalize volume to 0-1 range
                float normalizedVolume = Mathf.InverseLerp(volumeSettings.minRoomVolume, volumeSettings.maxRoomVolume, currentVolume);
                
                // Apply curve mapping
                float parameterValue = fmodSettings.volumeToParameterCurve.Evaluate(normalizedVolume);
                parameterValue = Mathf.Lerp(fmodSettings.minParameterValue, fmodSettings.maxParameterValue, parameterValue);
                
                // Set FMOD global parameter
                FMOD.Studio.System studioSystem = FMODUnity.RuntimeManager.StudioSystem;
                FMOD.RESULT result = studioSystem.setParameterByName(fmodSettings.roomSizeParameterName, parameterValue);
                
                if (result != FMOD.RESULT.OK)
                {
                    Debug.LogWarning($"Dynamic Reverb System: Failed to set FMOD parameter '{fmodSettings.roomSizeParameterName}': {result}");
                }
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Dynamic Reverb System: Error updating FMOD parameter - {e.Message}");
            }
        }
        
        private void Update()
        {
            // Update position-based calculations if transform has moved significantly
            if (Vector3.Distance(transform.position, raycastQueue.Count > 0 ? raycastQueue[0].origin : transform.position) > 1f)
            {
                if (!isProcessingRays)
                {
                    GenerateRaycastDirections();
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!debugSettings.showVolumeInfo && !debugSettings.showBoundingBox && !debugSettings.showHitPoints)
                return;
                
            // Draw bounding box
            if (debugSettings.showBoundingBox && currentBounds.size.magnitude > 0)
            {
                Gizmos.color = debugSettings.boundingBoxColor;
                Gizmos.DrawWireCube(currentBounds.center, currentBounds.size);
            }
            
            // Draw hit points
            if (debugSettings.showHitPoints)
            {
                Gizmos.color = debugSettings.hitColor;
                foreach (Vector3 point in hitPoints)
                {
                    Gizmos.DrawSphere(point, 0.1f);
                }
            }
        }
        
        private void OnValidate()
        {
            // Ensure reasonable values
            raycastSettings.horizontalRays = Mathf.Max(4, raycastSettings.horizontalRays);
            raycastSettings.verticalLayers = Mathf.Max(1, raycastSettings.verticalLayers);
            volumeSettings.minRoomVolume = Mathf.Max(1f, volumeSettings.minRoomVolume);
            volumeSettings.maxRoomVolume = Mathf.Max(volumeSettings.minRoomVolume + 1f, volumeSettings.maxRoomVolume);
        }
        
        // Public methods for external control
        public void ForceUpdate()
        {
            if (!isProcessingRays)
            {
                StartCoroutine(ProcessRaycastBatch());
            }
        }
        
        public void SetUpdateFrequency(float frequency)
        {
            raycastSettings.updateFrequency = Mathf.Clamp(frequency, 0.1f, 10f);
        }
        
        public void SetMaxRayDistance(float distance)
        {
            raycastSettings.maxRayDistance = Mathf.Clamp(distance, 1f, 100f);
            GenerateRaycastDirections();
        }
        
        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(DynamicReverbSystem))]
    public class DynamicReverbSystemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DynamicReverbSystem system = (DynamicReverbSystem)target;
            
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);
            
            GUI.enabled = false;
            EditorGUILayout.FloatField("Current Volume", system.CurrentVolume);
            EditorGUILayout.IntField("Hit Points", system.HitPointCount);
            EditorGUILayout.Vector3Field("Bounds Size", system.CurrentBounds.size);
            GUI.enabled = true;
            
            EditorGUILayout.Space();
            if (GUILayout.Button("Force Update"))
            {
                system.ForceUpdate();
            }
        }
    }
    #endif
}