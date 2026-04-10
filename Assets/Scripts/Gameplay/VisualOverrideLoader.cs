using UnityEngine;

public static class VisualOverrideLoader
{
    public static GameObject LoadPrefab(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return null;
        return Resources.Load<GameObject>(resourcePath);
    }

    public static GameObject InstantiatePrefab(string resourcePath, Transform parent, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        var prefab = LoadPrefab(resourcePath);
        if (!prefab) return null;

        var instance = Object.Instantiate(prefab, parent);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEulerAngles);
        instance.transform.localScale = localScale;
        StripPhysics(instance);
        return instance;
    }

    public static GameObject InstantiateWorldPrefab(string resourcePath, Vector3 position, Vector3 eulerAngles, Vector3 scale)
    {
        var prefab = LoadPrefab(resourcePath);
        if (!prefab) return null;

        var instance = Object.Instantiate(prefab);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(eulerAngles);
        instance.transform.localScale = scale;
        StripPhysics(instance);
        return instance;
    }

    public static void StripPhysics(GameObject root)
    {
        if (!root) return;

        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            Object.Destroy(collider);

        foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            Object.Destroy(rigidbody);
    }
}
