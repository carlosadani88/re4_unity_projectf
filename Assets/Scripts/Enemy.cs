// ============================================================================
// VILL4GE — Enemy.cs  (RE4-faithful)
// 4 tipos: Villager (foice), Pitchfork, Heavy (machado), Chainsaw (Dr. Salvador).
// Corpo articulado, stagger, headshot explosion, barra HP, grab attack.
// ============================================================================
using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour
{
    public enum EType { Villager, Pitchfork, Heavy, Chainsaw }
    public EType type;

    public float hp, maxHp, speed, damage, attackRate, attackRange;
    public float autoAlertRange = 7.5f;
    float nextAttack, staggerTimer;
    bool isDead;
    bool isGrabbing;
    Vector3 knockbackVel;
    Coroutine burnRoutine;

    // Propriedade pública para sistema de chute
    public bool IsStaggered => staggerTimer > 0 && !isDead;

    // ── Estado público para patrol/spawner ────────────────────────────────
    /// <summary>True quando o inimigo está morto.</summary>
    public bool IsDead => isDead;

    /// <summary>True quando o inimigo está ativamente perseguindo o jogador.</summary>
    public bool IsChasing => _alerted && !isDead && staggerTimer <= 0 && knockbackVel.magnitude < .1f;
    public bool IsAlerted => _alerted;

    bool _alerted = false;

    /// <summary>
    /// Chamado pelo EnemyPatrol para iniciar perseguição imediata.
    /// O Update() já persegue o jogador por padrão; este método apenas
    /// pode ser usado para resetar estados de patrulha.
    /// </summary>
    public void Alert(Transform target) { _alerted = true; }
    public void SetAlerted(bool alerted) => _alerted = alerted;

    CharacterController cc;
    GameObject body, head, legL, legR, armL, armR, weaponObj;
    GameObject hpBarBg, hpBarFill;

    Vector3 lastPos; float stuckTimer;

    // ====================================================================
    public void Init(EType t, bool alertedOnSpawn = false)
    {
        type = t;
        _alerted = alertedOnSpawn;
        switch (t)
        {
            case EType.Villager:   hp = maxHp = 60;  speed = 2.2f; damage = 12; attackRange = 2f;   break;
            case EType.Pitchfork:  hp = maxHp = 50;  speed = 2.8f; damage = 15; attackRange = 2.8f; break;
            case EType.Heavy:      hp = maxHp = 140; speed = 1.6f; damage = 28; attackRange = 2.2f; break;
            case EType.Chainsaw:   hp = maxHp = 350; speed = 3.2f; damage = 60; attackRange = 2f;   break;
        }
        attackRate = type == EType.Chainsaw ? .6f : 1.2f;

        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.center = Vector3.up * .9f; cc.radius = .35f;

        BuildBody();
    }

    IEnumerator GrabAnim()
    {
        float t = 0f;
        while (t < .25f && isGrabbing)
        {
            t += Time.deltaTime;
            AnimateGrabHold();
            yield return null;
        }
    }

    bool CanAttemptGrab(Player player, float dist)
    {
        if (!player || player.IsGrabbed || type == EType.Chainsaw) return false;
        if (dist > attackRange * .92f) return false;

        float chance = type == EType.Heavy ? .35f : .2f;
        return Random.value < chance;
    }

    public void ForceGrabBreak(Vector3 knockback, bool stagger)
    {
        isGrabbing = false;
        if (stagger)
            staggerTimer = Mathf.Max(staggerTimer, .7f);
        knockbackVel = knockback;
        knockbackVel.y = Mathf.Max(knockbackVel.y, 1.5f);
    }

    // ====================================================================
    // CORPO (Ganado RE4-style)
    // ====================================================================
    void BuildBody()
    {
        if (TryBuildVisualOverride())
            return;

        Color32 clothCol, pantsCol, skinCol;
        switch (type)
        {
            case EType.Heavy:
                clothCol = new Color32(70, 35, 30, 255);
                pantsCol = new Color32(50, 40, 30, 255);
                skinCol  = new Color32(160, 130, 100, 255);
                break;
            case EType.Chainsaw:
                clothCol = new Color32(100, 85, 65, 255);
                pantsCol = new Color32(60, 55, 45, 255);
                skinCol  = new Color32(145, 120, 90, 255);
                break;
            default:
                clothCol = new Color32(85, 70, 50, 255);
                pantsCol = new Color32(55, 48, 38, 255);
                skinCol  = new Color32(168, 138, 108, 255);
                break;
        }
        var mCloth = GameManager.Mat(clothCol);
        var mPants = GameManager.Mat(pantsCol);
        var mSkin  = GameManager.Mat(skinCol);

        // Pernas
        legL = Prim(PrimitiveType.Cube, new Vector3(-.15f, .4f, 0), new Vector3(.18f, .8f, .2f), mPants, false);
        legR = Prim(PrimitiveType.Cube, new Vector3(.15f, .4f, 0),  new Vector3(.18f, .8f, .2f), mPants, false);
        // Torso
        body = Prim(PrimitiveType.Cube, new Vector3(0, 1.15f, 0), new Vector3(.5f, .75f, .28f), mCloth, true);
        // Braços
        armL = Prim(PrimitiveType.Cube, new Vector3(-.32f, 1.1f, 0), new Vector3(.12f, .6f, .14f), mCloth, false);
        armR = Prim(PrimitiveType.Cube, new Vector3(.32f, 1.1f, 0),  new Vector3(.12f, .6f, .14f), mCloth, false);
        // Cabeça
        head = Prim(PrimitiveType.Sphere, new Vector3(0, 1.78f, 0), Vector3.one * .3f, mSkin, true);
        head.name = "Head";

        // Chapéu/máscara dependendo do tipo
        if (type == EType.Chainsaw)
        {
            // Saco na cabeça (Dr. Salvador)
            var bag = Prim(PrimitiveType.Cube, new Vector3(0, 1.82f, 0), new Vector3(.32f, .35f, .32f),
                GameManager.Mat(new Color32(120, 100, 70, 255)), false);
        }
        else if (type == EType.Villager || type == EType.Pitchfork)
        {
            // Chapéu de camponês
            var brim = Prim(PrimitiveType.Cylinder, new Vector3(0, 1.95f, 0), new Vector3(.4f, .02f, .4f),
                GameManager.Mat(new Color32(80, 60, 35, 255)), false);
            var crown = Prim(PrimitiveType.Cylinder, new Vector3(0, 2.05f, 0), new Vector3(.2f, .1f, .2f),
                GameManager.Mat(new Color32(80, 60, 35, 255)), false);
        }

        // Arma na mão
        BuildWeapon();

        // HP Bar
        hpBarBg = MakeQuad(new Vector3(0, 2.3f, 0), new Vector3(.6f, .06f, 1), new Color32(0, 0, 0, 180));
        hpBarFill = MakeQuad(new Vector3(0, 0, -.01f), new Vector3(.95f, .8f, 1),
            type == EType.Chainsaw ? new Color32(200, 50, 200, 255) : new Color32(180, 20, 20, 255));
        hpBarFill.transform.SetParent(hpBarBg.transform, false);
    }

    bool TryBuildVisualOverride()
    {
        string path = null;
        switch (type)
        {
            case EType.Villager: path = "Overrides/EnemyGanado"; break;
            case EType.Pitchfork: path = "Overrides/EnemyGanadoPitchfork"; break;
            case EType.Heavy: path = "Overrides/EnemyGanadoHeavy"; break;
            case EType.Chainsaw: path = "Overrides/EnemyChainsaw"; break;
        }

        var visual = VisualOverrideLoader.InstantiatePrefab(
            path,
            transform,
            new Vector3(0, -.9f, 0),
            Vector3.zero,
            Vector3.one);

        if (!visual) return false;

        visual.name = "VisualOverride";
        BuildInvisibleRig();
        return true;
    }

    void BuildInvisibleRig()
    {
        legL = HiddenPrim(PrimitiveType.Cube, new Vector3(-.15f, .4f, 0), new Vector3(.18f, .8f, .2f), false, "LegL");
        legR = HiddenPrim(PrimitiveType.Cube, new Vector3(.15f, .4f, 0),  new Vector3(.18f, .8f, .2f), false, "LegR");
        body = HiddenPrim(PrimitiveType.Cube, new Vector3(0, 1.15f, 0), new Vector3(.5f, .75f, .28f), true, "Body");
        armL = HiddenPrim(PrimitiveType.Cube, new Vector3(-.32f, 1.1f, 0), new Vector3(.12f, .6f, .14f), false, "ArmL");
        armR = HiddenPrim(PrimitiveType.Cube, new Vector3(.32f, 1.1f, 0),  new Vector3(.12f, .6f, .14f), false, "ArmR");
        head = HiddenPrim(PrimitiveType.Sphere, new Vector3(0, 1.78f, 0), Vector3.one * .3f, true, "Head");

        hpBarBg = MakeQuad(new Vector3(0, 2.3f, 0), new Vector3(.6f, .06f, 1), new Color32(0, 0, 0, 180));
        hpBarFill = MakeQuad(new Vector3(0, 0, -.01f), new Vector3(.95f, .8f, 1),
            type == EType.Chainsaw ? new Color32(200, 50, 200, 255) : new Color32(180, 20, 20, 255));
        hpBarFill.transform.SetParent(hpBarBg.transform, false);
    }

    GameObject HiddenPrim(PrimitiveType pt, Vector3 lp, Vector3 ls, bool keepCol, string objName)
    {
        var g = GameObject.CreatePrimitive(pt);
        g.name = objName;
        g.transform.SetParent(transform);
        g.transform.localPosition = lp;
        g.transform.localScale = ls;
        var renderer = g.GetComponent<Renderer>();
        if (renderer) renderer.enabled = false;
        if (!keepCol) Destroy(g.GetComponent<Collider>());
        return g;
    }

    void BuildWeapon()
    {
        switch (type)
        {
            case EType.Villager: // Foice
                weaponObj = Prim(PrimitiveType.Cube, new Vector3(.35f, .8f, .15f), new Vector3(.04f, .6f, .04f),
                    GameManager.Mat(new Color32(90, 70, 45, 255)), false);
                var blade = Prim(PrimitiveType.Cube, new Vector3(.35f, 1.15f, .2f), new Vector3(.2f, .04f, .04f),
                    GameManager.Mat(new Color32(150, 150, 155, 255)), false);
                break;
            case EType.Pitchfork: // Forcado
                weaponObj = Prim(PrimitiveType.Cube, new Vector3(.35f, .9f, .2f), new Vector3(.04f, 1.2f, .04f),
                    GameManager.Mat(new Color32(85, 65, 40, 255)), false);
                for (int i = -1; i <= 1; i++)
                {
                    var prong = Prim(PrimitiveType.Cube, new Vector3(.35f + i * .05f, 1.6f, .2f), new Vector3(.02f, .2f, .02f),
                        GameManager.Mat(new Color32(140, 140, 145, 255)), false);
                }
                break;
            case EType.Heavy: // Machado
                weaponObj = Prim(PrimitiveType.Cube, new Vector3(.38f, .85f, .15f), new Vector3(.04f, .7f, .04f),
                    GameManager.Mat(new Color32(80, 60, 35, 255)), false);
                var axeHead = Prim(PrimitiveType.Cube, new Vector3(.38f, 1.25f, .15f), new Vector3(.22f, .15f, .04f),
                    GameManager.Mat(new Color32(120, 120, 128, 255)), false);
                break;
            case EType.Chainsaw: // MOTOSSERRA!
                weaponObj = Prim(PrimitiveType.Cube, new Vector3(.35f, .9f, .3f), new Vector3(.08f, .08f, .7f),
                    GameManager.Mat(new Color32(160, 50, 20, 255)), false);
                var bar = Prim(PrimitiveType.Cube, new Vector3(.35f, .9f, .7f), new Vector3(.03f, .06f, .35f),
                    GameManager.Mat(new Color32(140, 140, 145, 255)), false);
                break;
        }
    }

    GameObject Prim(PrimitiveType pt, Vector3 lp, Vector3 ls, Material mat, bool keepCol)
    {
        var g = GameObject.CreatePrimitive(pt);
        g.transform.SetParent(transform); g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<Renderer>().material = mat;
        if (!keepCol) Destroy(g.GetComponent<Collider>());
        return g;
    }
    GameObject MakeQuad(Vector3 lp, Vector3 ls, Color32 c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.transform.SetParent(transform); g.transform.localPosition = lp; g.transform.localScale = ls;
        Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().material = GameManager.Mat(c);
        return g;
    }

    // ====================================================================
    // UPDATE — IA
    // ====================================================================
    void Update()
    {
        if (isDead) return;
        if (!GameManager.I || (GameManager.I.State != GameManager.GState.Playing &&
            GameManager.I.State != GameManager.GState.WaveIntro)) return;
        var player = GameManager.I.player;
        if (!player) return;

        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0;
        float dist = dir.magnitude;

        if (!_alerted && dist <= autoAlertRange)
            _alerted = true;

        if (player.IsGrabbed && player.CurrentGrabber == this)
        {
            isGrabbing = true;
            if (dist > .15f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 10f);
            AnimateGrabHold();
            UpdateHpBar();
            return;
        }
        else
        {
            isGrabbing = false;
        }

        // Stagger
        if (staggerTimer > 0) { staggerTimer -= Time.deltaTime; AnimateStagger(); }

        // Knockback
        if (knockbackVel.magnitude > .1f)
        {
            cc.Move(knockbackVel * Time.deltaTime);
            knockbackVel = Vector3.Lerp(knockbackVel, Vector3.zero, Time.deltaTime * 8);
            return; // não anda enquanto leva knockback
        }

        if (staggerTimer > 0) return;

        if (!_alerted)
        {
            AnimateIdle();
            UpdateHpBar();
            return;
        }

        if (dist > .2f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4);

        if (dist > attackRange)
        {
            Vector3 move = dir.normalized * speed;

            // Anti-stuck
            if (Vector3.Distance(transform.position, lastPos) < .02f) stuckTimer += Time.deltaTime; else stuckTimer = 0;
            if (stuckTimer > 1)
            {
                move = Quaternion.Euler(0, Random.Range(60, 120) * (Random.value > .5f ? 1 : -1), 0) * move;
                stuckTimer = 0;
            }
            lastPos = transform.position;

            move.y = -12;
            cc.Move(move * Time.deltaTime);
            AnimateWalk();
        }
        else
        {
            if (Time.time >= nextAttack)
            {
                nextAttack = Time.time + attackRate;
                if (CanAttemptGrab(player, dist) && player.TryBeginGrab(this, type == EType.Heavy ? 2.8f : 2.2f, type == EType.Heavy ? 11f : 8f, type == EType.Heavy ? 9 : 7))
                {
                    isGrabbing = true;
                    StartCoroutine(GrabAnim());
                }
                else
                {
                    StartCoroutine(AttackAnim());
                    player.TakeDamage(damage);
                }
            }
        }

        UpdateHpBar();
    }

    void UpdateHpBar()
    {
        if (hpBarBg && Camera.main)
        {
            hpBarBg.transform.LookAt(Camera.main.transform);
            float pct = hp / maxHp;
            hpBarFill.transform.localScale = new Vector3(.95f * pct, .8f, 1);
            hpBarFill.transform.localPosition = new Vector3(-.475f * (1 - pct), 0, -.01f);
        }
    }

    // ── Animações procedurais ──────────────────────────────────────────────
    public void ApplyFlash(float duration)
    {
        if (isDead) return;
        _alerted = true;
        if (isGrabbing && GameManager.I && GameManager.I.player)
            GameManager.I.player.ReleaseGrabFromEnemy(this, true);
        staggerTimer = Mathf.Max(staggerTimer, type == EType.Chainsaw ? duration * .65f : duration);
        knockbackVel = Vector3.zero;
        StartCoroutine(FlashWhite());
    }

    public void Ignite(float totalDamage, float duration)
    {
        if (isDead) return;
        _alerted = true;
        if (burnRoutine != null) StopCoroutine(burnRoutine);
        burnRoutine = StartCoroutine(BurnRoutine(totalDamage, duration));
    }

    IEnumerator BurnRoutine(float totalDamage, float duration)
    {
        float tick = .35f;
        float elapsed = 0f;

        while (elapsed < duration && !isDead)
        {
            yield return new WaitForSeconds(tick);
            elapsed += tick;
            if (isDead) yield break;

            hp -= totalDamage * (tick / Mathf.Max(.1f, duration));
            staggerTimer = Mathf.Max(staggerTimer, .15f);
            StartCoroutine(FlashBurn());

            if (hp <= 0)
            {
                isDead = true;
                if (isGrabbing && GameManager.I && GameManager.I.player)
                    GameManager.I.player.ReleaseGrabFromEnemy(this, true);
                if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.EnemyDeath, transform.position);
                GameManager.I.OnEnemyDied(transform.position, type);
                Destroy(gameObject);
                yield break;
            }
        }

        burnRoutine = null;
    }

    void AnimateIdle()
    {
        float t = Time.time * 1.5f + transform.position.x * .15f;
        if (armL) armL.transform.localRotation = Quaternion.Euler(Mathf.Sin(t) * 6, 0, 0);
        if (armR) armR.transform.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 6, 0, 0);
        if (body) body.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * .5f) * 2.5f);
        if (head) head.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(t * .8f) * 12f, 0);
    }

    void AnimateWalk()
    {
        float t = Time.time * speed * 2.5f;
        if (legL) legL.transform.localRotation = Quaternion.Euler(Mathf.Sin(t) * 35, 0, 0);
        if (legR) legR.transform.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 35, 0, 0);
        if (armL) armL.transform.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 20, 0, 0);
        if (armR) armR.transform.localRotation = Quaternion.Euler(Mathf.Sin(t) * 20, 0, 0);
        // Corpo balança levemente
        if (body) body.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * .5f) * 3);
    }

    void AnimateStagger()
    {
        if (body) body.transform.localPosition = new Vector3(0, 1.15f, -.1f);
        if (head) head.transform.localPosition = new Vector3(0, 1.65f, -.1f);
    }

    void AnimateGrabHold()
    {
        if (armL) armL.transform.localRotation = Quaternion.Euler(-55, 10, 0);
        if (armR) armR.transform.localRotation = Quaternion.Euler(-55, -10, 0);
        if (body) body.transform.localRotation = Quaternion.Euler(8, 0, 0);
    }

    IEnumerator AttackAnim()
    {
        // Levantar braço de arma
        if (armR)
        {
            armR.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            yield return new WaitForSeconds(.15f);
            armR.transform.localRotation = Quaternion.Euler(30, 0, 0);
            yield return new WaitForSeconds(.1f);
            armR.transform.localRotation = Quaternion.identity;
        }
    }

    // ====================================================================
    // DANO
    // ====================================================================
    public void TakeDamage(float dmg, bool headshot)
    {
        if (isDead) return;
        _alerted = true;
        if (isGrabbing && GameManager.I && GameManager.I.player)
            GameManager.I.player.ReleaseGrabFromEnemy(this, true);
        hp -= dmg;

        if (hp <= 0)
        {
            isDead = true;
            if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.EnemyDeath, transform.position);
            GameManager.I.OnEnemyDied(transform.position, type);
            if (headshot) StartCoroutine(HeadshotDeath()); else Destroy(gameObject);
            return;
        }

        // Stagger (não para Chainsaw exceto headshot)
        if (type != EType.Chainsaw || headshot)
            staggerTimer = .35f;

        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.EnemyHurt, transform.position);
        StartCoroutine(FlashRed());
    }

    // ====================================================================
    // CHUTE (Kick damage from Player)
    // ====================================================================
    public void TakeKickDamage(float dmg, Vector3 knockback)
    {
        if (isDead) return;
        _alerted = true;
        if (isGrabbing && GameManager.I && GameManager.I.player)
            GameManager.I.player.ReleaseGrabFromEnemy(this, true);
        hp -= dmg;

        // Knockback forte
        knockbackVel = knockback;
        knockbackVel.y = 2f; // levanta um pouco

        // Stagger longo pós-chute
        staggerTimer = .8f;

        // Visual: flash branco de impacto
        StartCoroutine(FlashKick());

        if (hp <= 0)
        {
            isDead = true;
            if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.EnemyDeath, transform.position);
            GameManager.I.OnEnemyDied(transform.position, type);
            StartCoroutine(KickDeath(knockback));
            return;
        }
    }

    IEnumerator FlashKick()
    {
        if (!body) yield break;
        var r = body.GetComponent<Renderer>();
        Color orig = r.material.color;
        r.material.color = Color.white;
        yield return new WaitForSeconds(.08f);
        r.material.color = new Color(1, .7f, .3f);
        yield return new WaitForSeconds(.08f);
        if (r) r.material.color = orig;
    }

    IEnumerator FlashWhite()
    {
        if (!body) yield break;
        var r = body.GetComponent<Renderer>();
        Color orig = r.material.color;
        r.material.color = Color.white;
        yield return new WaitForSeconds(.12f);
        if (r) r.material.color = orig;
    }

    IEnumerator FlashBurn()
    {
        if (!body) yield break;
        var r = body.GetComponent<Renderer>();
        Color orig = r.material.color;
        r.material.color = new Color(1f, .45f, .08f);
        yield return new WaitForSeconds(.1f);
        if (r) r.material.color = orig;
    }

    IEnumerator KickDeath(Vector3 dir)
    {
        // Corpo voa para trás com o chute
        float t = 0;
        Vector3 vel = dir.normalized * 6f + Vector3.up * 3f;
        while (t < .6f)
        {
            t += Time.deltaTime;
            vel.y -= 12f * Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            transform.Rotate(-180 * Time.deltaTime, 0, Random.Range(-30, 30) * Time.deltaTime);
            yield return null;
        }
        yield return new WaitForSeconds(.3f);
        Destroy(gameObject);
    }

    IEnumerator FlashRed()
    {
        if (!body) yield break;
        var r = body.GetComponent<Renderer>();
        Color orig = r.material.color;
        r.material.color = Color.red;
        yield return new WaitForSeconds(.1f);
        if (r) r.material.color = orig;
    }

    IEnumerator HeadshotDeath()
    {
        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.EnemyDeath, transform.position);
        // Cabeça explode (RE4!)
        if (head)
        {
            // Partículas de sangue
            for (int i = 0; i < 6; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.transform.position = head.transform.position + Random.insideUnitSphere * .15f;
                p.transform.localScale = Vector3.one * Random.Range(.04f, .1f);
                Destroy(p.GetComponent<Collider>());
                p.GetComponent<Renderer>().material = GameManager.Mat(new Color32(120, 15, 10, 255));
                var rb = p.AddComponent<Rigidbody>();
                rb.AddForce(Random.insideUnitSphere * 3 + Vector3.up * 2, ForceMode.Impulse);
                Destroy(p, 1.5f);
            }
            Destroy(head);
        }
        // Corpo cai (girar para trás)
        float t = 0;
        while (t < .5f)
        {
            t += Time.deltaTime;
            transform.Rotate(-120 * Time.deltaTime, 0, 0);
            yield return null;
        }
        yield return new WaitForSeconds(.5f);
        Destroy(gameObject);
    }
}
