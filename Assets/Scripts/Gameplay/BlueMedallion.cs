using UnityEngine;

public class BlueMedallion : MonoBehaviour
{
    int medallionIndex = -1;
    bool destroyed;
    float baseY;

    public void Init(int index)
    {
        medallionIndex = index;
        baseY = transform.position.y;

        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = .35f;
    }

    void Update()
    {
        var p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 2.5f + medallionIndex) * .04f;
        transform.position = p;
        transform.Rotate(0, 90f * Time.deltaTime, 0);
    }

    public void Hit()
    {
        if (destroyed) return;
        destroyed = true;
        GameManager.I?.OnBlueMedallionDestroyed(medallionIndex);
        Destroy(gameObject);
    }
}
