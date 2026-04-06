// ============================================================================
// VILL4GE — Pickup.cs  (RE4-style)
// Drops giratórios com brilho emissivo. Ervas verdes, munição, pesetas.
// ============================================================================
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum PType { Herb, Ammo, Money }
    public PType type;
    int value;
    float baseY;

    public void Init()
    {
        float r = Random.value;
        if (r < .3f)       { type = PType.Herb;  value = 1; }
        else if (r < .6f)  { type = PType.Ammo;  value = 20; }
        else                { type = PType.Money; value = Random.Range(100, 350); }

        Color32 col; PrimitiveType shape;
        switch (type)
        {
            case PType.Herb:  col = new Color32(25, 160, 35, 255); shape = PrimitiveType.Sphere; break;
            case PType.Ammo:  col = new Color32(180, 160, 45, 255); shape = PrimitiveType.Cube; break;
            default:          col = new Color32(210, 190, 50, 255); shape = PrimitiveType.Cylinder; break;
        }

        var prim = GameObject.CreatePrimitive(shape);
        prim.transform.SetParent(transform);
        prim.transform.localPosition = Vector3.zero;
        prim.transform.localScale = type == PType.Herb ? new Vector3(.18f, .22f, .18f) : Vector3.one * .2f;
        Destroy(prim.GetComponent<Collider>());
        prim.GetComponent<Renderer>().material = GameManager.MatEmissive(col, (Color)col * .4f);

        baseY = transform.position.y;
        var sc = gameObject.AddComponent<SphereCollider>(); sc.radius = 1.2f; sc.isTrigger = true;
        var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true;
        Destroy(gameObject, 25f);
    }

    void Update()
    {
        var p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 3) * .12f;
        transform.position = p;
        transform.Rotate(0, 100 * Time.deltaTime, 0);
    }

    void OnTriggerEnter(Collider other)
    {
        var pl = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (!pl) return;
        switch (type)
        {
            case PType.Herb:  pl.herbs += value; break;
            case PType.Ammo:  pl.weapons[pl.curWeapon].ammoReserve += value; break;
            case PType.Money: pl.money += value; break;
        }
        Destroy(gameObject);
    }
}
