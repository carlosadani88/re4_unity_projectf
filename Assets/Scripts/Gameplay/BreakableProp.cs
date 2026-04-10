using UnityEngine;
using System.Collections;

public class BreakableProp : MonoBehaviour
{
    public enum PropType { Barrel, Crate }

    public PropType type;
    public float maxHp = 28f;
    public float lootChance = .55f;

    float hp;
    bool broken;
    Renderer[] cachedRenderers;

    void Awake()
    {
        if (hp <= 0f) hp = maxHp;
        CacheParts();
    }

    public void Init(PropType propType, float durability, float dropChance)
    {
        type = propType;
        maxHp = durability;
        lootChance = dropChance;
        hp = maxHp;
        CacheParts();
    }

    void CacheParts()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public void Damage(float dmg, Vector3 hitPoint, Vector3 forceDir)
    {
        if (broken) return;

        hp -= dmg;
        StartCoroutine(HitFlash());

        if (hp <= 0f)
            Break(hitPoint, forceDir);
    }

    IEnumerator HitFlash()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0) yield break;

        Color[] original = new Color[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (!cachedRenderers[i]) continue;
            original[i] = cachedRenderers[i].material.color;
            cachedRenderers[i].material.color = Color.Lerp(original[i], Color.white, .45f);
        }

        yield return new WaitForSeconds(.05f);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i])
                cachedRenderers[i].material.color = original[i];
        }
    }

    void Break(Vector3 hitPoint, Vector3 forceDir)
    {
        broken = true;

        if (AudioManager.I)
            AudioManager.I.PlaySFX(AudioManager.SFX.KnifeHit, transform.position);

        SpawnDebris(hitPoint, forceDir);

        if (Random.value < lootChance)
        {
            var pickup = new GameObject("Pickup");
            pickup.transform.position = transform.position + Vector3.up * .35f;
            pickup.AddComponent<Pickup>().Init(richDrop: true);
        }

        Destroy(gameObject);
    }

    void SpawnDebris(Vector3 hitPoint, Vector3 forceDir)
    {
        Material debrisMat = null;
        if (cachedRenderers != null && cachedRenderers.Length > 0 && cachedRenderers[0] != null)
            debrisMat = cachedRenderers[0].material;

        int pieces = type == PropType.Barrel ? 6 : 5;
        Vector3 origin = transform.position + Vector3.up * .45f;

        for (int i = 0; i < pieces; i++)
        {
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.transform.position = origin + Random.insideUnitSphere * .28f;
            debris.transform.localScale = Vector3.one * Random.Range(.12f, .24f);
            debris.transform.rotation = Random.rotation;

            var renderer = debris.GetComponent<Renderer>();
            renderer.material = debrisMat != null ? debrisMat : GameManager.Mat(new Color32(120, 90, 58, 255));

            var rb = debris.AddComponent<Rigidbody>();
            Vector3 blastDir = (debris.transform.position - hitPoint).normalized + forceDir.normalized * .8f;
            blastDir.y = Mathf.Abs(blastDir.y) + .3f;
            rb.AddForce(blastDir * Random.Range(2.5f, 5f), ForceMode.Impulse);

            Destroy(debris, 2.5f);
        }
    }
}
