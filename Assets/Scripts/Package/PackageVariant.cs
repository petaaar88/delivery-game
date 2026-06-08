using UnityEngine;

[System.Serializable]
public class PackageVariant
{
    public GameObject prefab;
    [Tooltip("Component on this GameObject that implements IPackageEffect. Leave empty for no effect.")]
    public MonoBehaviour effect;
}
