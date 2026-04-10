// ============================================================================
// VILL4GE - GrenadeProjectile.cs  (RE4-style)
// Timer 2s, hand/flash/incendiary grenade effects, visual burst and self-damage.
// ============================================================================
using UnityEngine;

public class GrenadeProjectile : MonoBehaviour
{
    public Player.GrenadeType type = Player.GrenadeType.Hand;
    public float damage = 220f;
    public float burnDamage = 150f;
    public float stunDuration = 2.4f;
    float timer = 2f;
    const float RADIUS = 6f;

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        ResolveExplosion();
        Destroy(gameObject);
    }

    void ResolveExplosion()
    {
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d >= RADIUS) continue;

            float falloff = 1f - d / RADIUS;
            switch (type)
            {
                case Player.GrenadeType.Flash:
                    e.ApplyFlash(Mathf.Lerp(.9f, stunDuration, falloff));
                    break;
                case Player.GrenadeType.Incendiary:
                    e.TakeDamage(damage * .55f * falloff, false);
                    if (e && !e.IsDead)
                        e.Ignite(Mathf.Lerp(burnDamage * .5f, burnDamage, falloff), 2.8f);
                    break;
                default:
                    e.TakeDamage(damage * falloff, false);
                    break;
            }
        }

        var p = GameManager.I?.player;
        if (p)
        {
            float pd = Vector3.Distance(transform.position, p.transform.position);
            if (pd < RADIUS)
            {
                float falloff = 1f - pd / RADIUS;
                if (type == Player.GrenadeType.Hand)
                    p.TakeDamage(60f * falloff);
                else if (type == Player.GrenadeType.Incendiary)
                    p.TakeDamage(35f * falloff);
            }
        }

        SpawnExplosionFx();
    }

    void SpawnExplosionFx()
    {
        var fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.transform.position = transform.position;
        Destroy(fx.GetComponent<Collider>());

        var lo = new GameObject("ExpLight");
        lo.transform.position = transform.position;
        var pl = lo.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.range = 15f;
        pl.intensity = 4f;

        switch (type)
        {
            case Player.GrenadeType.Flash:
                fx.transform.localScale = Vector3.one * RADIUS * .6f;
                fx.GetComponent<Renderer>().material = GameManager.MatEmissive(
                    new Color32(255, 255, 240, 200), new Color(4f, 4f, 3.5f));
                pl.color = new Color(.95f, .95f, 1f);
                break;
            case Player.GrenadeType.Incendiary:
                fx.transform.localScale = Vector3.one * RADIUS * .55f;
                fx.GetComponent<Renderer>().material = GameManager.MatEmissive(
                    new Color32(255, 105, 30, 200), new Color(4f, 1.2f, .15f));
                pl.color = new Color(1f, .45f, .12f);
                break;
            default:
                fx.transform.localScale = Vector3.one * RADIUS * .5f;
                fx.GetComponent<Renderer>().material = GameManager.MatEmissive(
                    new Color32(255, 120, 25, 200), new Color(4f, 1.5f, .2f));
                pl.color = new Color(1f, .6f, .15f);
                break;
        }

        Destroy(lo, .3f);
        Destroy(fx, .3f);
    }
}
