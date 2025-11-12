using UnityEngine;

[DisallowMultipleComponent]
public class OcclusionMaterial : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private string materialName = "Default";

    public string MaterialName => materialName;
}