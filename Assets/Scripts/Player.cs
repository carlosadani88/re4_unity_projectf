// ============================================================================
// VILL4GE — Player.cs  (RE4-faithful)
// Over-the-shoulder camera (RE4 signature), mira laser, 5 armas, faca,
// granada, ervas, sprint, chuva, efeitos de recuo, footsteps headbob.
// ============================================================================
using UnityEngine;
using System.Collections;

public class Player : MonoBehaviour
{
    [System.Serializable]
    public class WeaponData
    {
        public string name;
        public int dmg;
        public float fireRate, reloadTime;
        public int magSize, ammoInMag, ammoReserve;
        public float spread;
        public bool explosive, owned;
    }

    // ── Estado ─────────────────────────────────────────────────────────────
    public float hp = 100, maxHp = 100;
    public int money, herbs, grenades = 3;
    public WeaponData[] weapons;
    public int curWeapon;
    public float dmgMultiplier = 1f;
    float nextFire;
    bool reloading, aiming;

    // ── Kick RE4 ────────────────────────────────────────────────────────────
    bool kicking;
    float kickCooldown;
    const float KICK_DAMAGE = 45f;
    const float KICK_RANGE = 2.8f;
    const float KICK_KNOCKBACK = 8f;
    const float KICK_COOLDOWN = .8f;
    const float KICK_ARC = .6f; // dot threshold (cone frontal)

    // ── Componentes ────────────────────────────────────────────────────────
    CharacterController cc;
    Transform camPivot, camT;
    float yaw, pitch, yVel;
    GameObject viewModel, laserDot;
    LineRenderer laserLine;
    float headBob;

    // ── Config RE4 ─────────────────────────────────────────────────────────
    const float SPEED = 4.5f, SPRINT_MULT = 1.7f, AIM_SPEED_MULT = 0.35f;
    const float GRAVITY = 18f, SENS = 2.2f, AIM_SENS = 1.2f;
    // Over-the-shoulder offsets
    readonly Vector3 CAM_OFFSET_HIP  = new Vector3(0.4f, 1.65f, -0.6f);
    readonly Vector3 CAM_OFFSET_AIM  = new Vector3(0.35f, 1.6f, -0.25f);

    void Awake()
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.center = Vector3.up * 0.9f; cc.radius = 0.3f;

        // Pivot para câmera (gira com yaw do jogador)
        camPivot = new GameObject("CamPivot").transform;
        camPivot.SetParent(transform);
        camPivot.localPosition = Vector3.zero;

        // Câmera
        Camera cam = Camera.main;
        if (cam) { camT = cam.transform; } else { var co = new GameObject("Cam"); cam = co.AddComponent<Camera>(); co.AddComponent<AudioListener>(); camT = co.transform; }
        camT.SetParent(camPivot);
        camT.localPosition = CAM_OFFSET_HIP;
        cam.fieldOfView = 70; cam.nearClipPlane = 0.1f; cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(28, 24, 20, 255);

        InitWeapons();
        CreateViewModel();
        CreateLaser();
        SetupRain();
    }

    void InitWeapons()
    {
        weapons = new WeaponData[]
        {
            new WeaponData { name="Handgun",  dmg=18,  fireRate=.28f, reloadTime=1.5f, magSize=12, ammoInMag=12, ammoReserve=60,  spread=.02f, owned=true },
            new WeaponData { name="Shotgun",  dmg=90,  fireRate=.9f,  reloadTime=2f,   magSize=6,  ammoInMag=6,  ammoReserve=24,  spread=.18f, owned=false },
            new WeaponData { name="Rifle",    dmg=50,  fireRate=.12f, reloadTime=2.5f, magSize=10, ammoInMag=10, ammoReserve=30,  spread=.01f, owned=false },
            new WeaponData { name="TMP",      dmg=10,  fireRate=.06f, reloadTime=1.8f, magSize=50, ammoInMag=50, ammoReserve=200, spread=.06f, owned=false },
            new WeaponData { name="Rocket",   dmg=350, fireRate=1.5f, reloadTime=3f,   magSize=1,  ammoInMag=1,  ammoReserve=3,   spread=0,    owned=false, explosive=true },
        };
        curWeapon = 0;
    }

    // ====================================================================
    // VIEW MODEL (arma na mão de Leon)
    // ====================================================================
    void CreateViewModel()
    {
        if (viewModel) Destroy(viewModel);
        viewModel = new GameObject("ViewModel");
        viewModel.transform.SetParent(camT);

        // Corpo da arma
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.transform.SetParent(viewModel.transform);
        Destroy(barrel.GetComponent<Collider>());
        // Grip
        var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.transform.SetParent(viewModel.transform);
        Destroy(grip.GetComponent<Collider>());

        var matGun = GameManager.Mat(new Color32(35, 35, 38, 255));
        var matGrip = GameManager.Mat(new Color32(55, 42, 28, 255));

        float len = 0.3f, wd = 0.04f;
        Vector3 basePos = new Vector3(0.25f, -0.2f, 0.35f);
        switch (curWeapon)
        {
            case 0: len = .28f; wd = .035f; break;
            case 1: len = .50f; wd = .05f;  break;
            case 2: len = .55f; wd = .035f; break;
            case 3: len = .25f; wd = .04f;  break;
            case 4: len = .65f; wd = .07f;  break;
        }
        barrel.transform.localPosition = basePos;
        barrel.transform.localScale = new Vector3(wd, wd, len);
        barrel.GetComponent<Renderer>().material = matGun;

        grip.transform.localPosition = basePos + new Vector3(0, -.06f, -.05f);
        grip.transform.localRotation = Quaternion.Euler(15, 0, 0);
        grip.transform.localScale = new Vector3(wd * .8f, .08f, wd);
        grip.GetComponent<Renderer>().material = matGrip;
    }

    // ====================================================================
    // LASER SIGHT (RE4 signature!)
    // ====================================================================
    void CreateLaser()
    {
        var lo = new GameObject("Laser");
        lo.transform.SetParent(camT);
        laserLine = lo.AddComponent<LineRenderer>();
        laserLine.startWidth = 0.003f; laserLine.endWidth = 0.003f;
        laserLine.material = GameManager.MatEmissive(new Color32(255, 20, 20, 255), Color.red * 3);
        laserLine.positionCount = 2;
        laserLine.enabled = false;

        laserDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        laserDot.transform.localScale = Vector3.one * 0.04f;
        Destroy(laserDot.GetComponent<Collider>());
        laserDot.GetComponent<Renderer>().material = GameManager.MatEmissive(
            new Color32(255, 30, 30, 255), Color.red * 5);
        laserDot.SetActive(false);
    }

    // ====================================================================
    // RAIN
    // ====================================================================
    void SetupRain()
    {
        var ro = new GameObject("Rain");
        ro.transform.SetParent(transform);
        ro.transform.localPosition = Vector3.up * 18f;
        var ps = ro.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.maxParticles = 3000; main.startLifetime = 2.2f; main.startSpeed = 20;
        main.startSize = 0.025f;
        main.startColor = new Color(.6f, .65f, .75f, .3f);
        main.gravityModifier = .6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(22, 0, 22);
        var em = ps.emission; em.rateOverTime = 800;
        var psr = ro.GetComponent<ParticleSystemRenderer>();
        psr.material = GameManager.Mat(new Color32(150, 160, 185, 70));
    }

    // ====================================================================
    // UPDATE
    // ====================================================================
    void Update()
    {
        if (!GameManager.I || GameManager.I.State != GameManager.GState.Playing &&
            GameManager.I.State != GameManager.GState.WaveIntro) return;

        if (AttacheCase.I && AttacheCase.I.IsOpen) return; // maleta aberta = pause

        HandleMouse();
        HandleMovement();
        HandleAiming();
        HandleShooting();
        HandleWeaponSwitch();
        HandleItems();
        HandleInteract();
        HandleKick();
        HandleCase();
        UpdateCamera();
        UpdateLaser();
    }

    void HandleMouse()
    {
        float sens = aiming ? AIM_SENS : SENS;
        yaw += Input.GetAxis("Mouse X") * sens;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sens, -75, 75);
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal"), v = Input.GetAxis("Vertical");
        bool sprint = Input.GetKey(KeyCode.LeftShift) && !aiming && v > 0;
        float spd = SPEED * (sprint ? SPRINT_MULT : 1f) * (aiming ? AIM_SPEED_MULT : 1f);

        Vector3 move = (transform.right * h + transform.forward * v).normalized * spd;
        if (cc.isGrounded) yVel = -2f; else yVel -= GRAVITY * Time.deltaTime;
        move.y = yVel;
        cc.Move(move * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0, yaw, 0);

        // Head bob ao andar
        if (cc.isGrounded && (Mathf.Abs(h) + Mathf.Abs(v)) > .1f)
        {
            headBob += Time.deltaTime * spd * 1.8f;
        }
    }

    void HandleAiming()
    {
        aiming = Input.GetMouseButton(1); // RMB = aim (RE4 style)
    }

    void HandleShooting()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextFire && !reloading)
        {
            if (!aiming) { Melee(); return; } // Faca se não está mirando
            Shoot();
        }
        if (Input.GetKeyDown(KeyCode.R) && !reloading) StartCoroutine(Reload());
    }

    void HandleWeaponSwitch()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0) SwitchWeapon(scroll > 0 ? 1 : -1);
        for (int i = 0; i < 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && weapons[i].owned)
            {
                // Só pode usar se está na maleta
                if (AttacheCase.I && !AttacheCase.I.HasWeapon(i)) continue;
                curWeapon = i;
                CreateViewModel();
            }
        }
    }

    void HandleItems()
    {
        if (Input.GetKeyDown(KeyCode.G) && grenades > 0) ThrowGrenade();
        if (Input.GetKeyDown(KeyCode.H) && herbs > 0 && hp < maxHp) { herbs--; hp = Mathf.Min(hp + 40, maxHp); }
    }

    void HandleInteract()
    {
        if (Input.GetKeyDown(KeyCode.E) && GameManager.I.State == GameManager.GState.Merchant &&
            Vector3.Distance(transform.position, GameManager.I.merchantPos) < 4f && GameUI.I)
            GameUI.I.ToggleMerchant();
    }

    // ====================================================================
    // CÂMERA OVER-THE-SHOULDER
    // ====================================================================
    void UpdateCamera()
    {
        camPivot.localRotation = Quaternion.Euler(pitch, 0, 0);
        Vector3 target = aiming ? CAM_OFFSET_AIM : CAM_OFFSET_HIP;
        // Bob
        if (!aiming)
            target.y += Mathf.Sin(headBob) * .03f;
        camT.localPosition = Vector3.Lerp(camT.localPosition, target, Time.deltaTime * 10);

        // FOV
        Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, aiming ? 55 : 70, Time.deltaTime * 8);
    }

    // ====================================================================
    // LASER SIGHT
    // ====================================================================
    void UpdateLaser()
    {
        laserLine.enabled = aiming;
        laserDot.SetActive(aiming);
        if (!aiming) return;

        Vector3 origin = camT.position + camT.forward * .5f;
        Vector3 dir = camT.forward;
        Vector3 end;

        if (Physics.Raycast(new Ray(camT.position, dir), out RaycastHit hit, 100))
            end = hit.point;
        else
            end = camT.position + dir * 100;

        laserLine.SetPosition(0, origin);
        laserLine.SetPosition(1, end);
        laserDot.transform.position = end;
    }

    // ====================================================================
    // TIRO
    // ====================================================================
    void Shoot()
    {
        var w = weapons[curWeapon];
        if (w.ammoInMag <= 0) { StartCoroutine(Reload()); return; }
        w.ammoInMag--;
        nextFire = Time.time + w.fireRate;

        int pellets = w.name == "Shotgun" ? 8 : 1;
        for (int p = 0; p < pellets; p++)
        {
            Vector3 dir = camT.forward;
            if (w.spread > 0)
            {
                dir += camT.right * Random.Range(-w.spread, w.spread);
                dir += camT.up * Random.Range(-w.spread, w.spread);
                dir.Normalize();
            }
            if (Physics.Raycast(new Ray(camT.position, dir), out RaycastHit hit, 100))
            {
                var enemy = hit.collider.GetComponentInParent<Enemy>();
                if (enemy)
                {
                    float dmg = w.dmg * dmgMultiplier / pellets * (pellets > 1 ? 1.5f : 1f);
                    bool headshot = hit.collider.gameObject.name == "Head";
                    if (headshot) dmg *= 3f;
                    enemy.TakeDamage(dmg, headshot);
                }
                if (w.explosive) Explode(hit.point, w.dmg * dmgMultiplier);
                else SpawnHitFX(hit.point, hit.normal);
            }
        }

        // Recoil
        pitch -= Random.Range(.8f, 2f);
        yaw += Random.Range(-.3f, .3f);
        // Muzzle flash
        StartCoroutine(MuzzleFlash());
    }

    IEnumerator MuzzleFlash()
    {
        var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        f.transform.position = camT.position + camT.forward * .8f + camT.right * .25f - camT.up * .15f;
        f.transform.localScale = Vector3.one * .1f;
        Destroy(f.GetComponent<Collider>());
        f.GetComponent<Renderer>().material = GameManager.MatEmissive(
            new Color32(255, 200, 80, 255), new Color(5, 3, 0.5f));
        yield return new WaitForSeconds(.04f);
        Destroy(f);
    }

    void SpawnHitFX(Vector3 pos, Vector3 normal)
    {
        // Faísca
        var fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.transform.position = pos + normal * .02f;
        fx.transform.localScale = Vector3.one * .05f;
        Destroy(fx.GetComponent<Collider>());
        fx.GetComponent<Renderer>().material = GameManager.MatEmissive(
            new Color32(255, 220, 80, 255), Color.yellow * 2);
        Destroy(fx, .08f);
    }

    void Explode(Vector3 pos, float dmg)
    {
        float r = 5.5f;
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(pos, e.transform.position);
            if (d < r) e.TakeDamage(dmg * (1 - d / r), false);
        }
        var fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.transform.position = pos; fx.transform.localScale = Vector3.one * r * .6f;
        Destroy(fx.GetComponent<Collider>());
        fx.GetComponent<Renderer>().material = GameManager.MatEmissive(
            new Color32(255, 130, 30, 200), new Color(4, 1.5f, .2f));
        Destroy(fx, .3f);
    }

    // ====================================================================
    // CHUTE RE4 (Roundhouse Kick)
    // ====================================================================
    void HandleKick()
    {
        if (kickCooldown > 0) kickCooldown -= Time.deltaTime;
        if (kicking) return;

        // Detectar inimigo staggered perto
        Enemy kickTarget = FindStaggeredEnemy();

        // Mostrar prompt
        if (GameUI.I)
            GameUI.I.ShowKickPrompt(kickTarget != null);

        // F = chutar
        if (Input.GetKeyDown(KeyCode.F) && kickTarget != null && kickCooldown <= 0)
        {
            StartCoroutine(PerformKick(kickTarget));
        }
    }

    Enemy FindStaggeredEnemy()
    {
        Enemy best = null;
        float bestDist = KICK_RANGE;
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (!e.IsStaggered) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d > KICK_RANGE) continue;
            Vector3 dir = (e.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) < KICK_ARC) continue;
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    IEnumerator PerformKick(Enemy target)
    {
        kicking = true;
        kickCooldown = KICK_COOLDOWN;

        // Visual: perna do chute
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.transform.SetParent(camT);
        leg.transform.localPosition = new Vector3(.15f, -.6f, .6f);
        leg.transform.localScale = new Vector3(.12f, .12f, .5f);
        Destroy(leg.GetComponent<Collider>());
        leg.GetComponent<Renderer>().material = GameManager.Mat(new Color32(55, 48, 38, 255));

        // Boot
        var boot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boot.transform.SetParent(leg.transform);
        boot.transform.localPosition = new Vector3(0, 0, .5f);
        boot.transform.localScale = new Vector3(1.2f, 1.3f, .4f);
        Destroy(boot.GetComponent<Collider>());
        boot.GetComponent<Renderer>().material = GameManager.Mat(new Color32(35, 28, 20, 255));

        // Animação: chutar para frente
        float t = 0;
        while (t < .15f)
        {
            t += Time.deltaTime;
            float pct = t / .15f;
            leg.transform.localRotation = Quaternion.Euler(-60 * pct, 20 * pct, 0);
            leg.transform.localPosition = new Vector3(.15f, -.6f + .3f * pct, .6f + .3f * pct);
            yield return null;
        }

        // Aplicar dano en área (todos os inimigos no cone)
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d > KICK_RANGE + .5f) continue;
            Vector3 dir = (e.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) < KICK_ARC - .1f) continue;

            // Dano + knockback
            e.TakeKickDamage(KICK_DAMAGE * dmgMultiplier, transform.forward * KICK_KNOCKBACK);
        }

        // Impacto visual
        SpawnKickImpact(transform.position + transform.forward * 1.5f + Vector3.up * .8f);

        // Retrair perna
        t = 0;
        while (t < .15f)
        {
            t += Time.deltaTime;
            float pct = 1 - t / .15f;
            leg.transform.localRotation = Quaternion.Euler(-60 * pct, 20 * pct, 0);
            yield return null;
        }

        Destroy(leg);
        kicking = false;
    }

    void SpawnKickImpact(Vector3 pos)
    {
        // Onda de impacto
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.position = pos;
        ring.transform.localScale = new Vector3(.4f, .02f, .4f);
        Destroy(ring.GetComponent<Collider>());
        ring.GetComponent<Renderer>().material = GameManager.MatEmissive(
            new Color32(255, 200, 120, 200), new Color(2, 1, .2f));
        StartCoroutine(ExpandRing(ring));
    }

    IEnumerator ExpandRing(GameObject ring)
    {
        float t = 0;
        while (t < .2f && ring)
        {
            t += Time.deltaTime;
            float s = .4f + t * 8f;
            ring.transform.localScale = new Vector3(s, .02f, s);
            var r = ring.GetComponent<Renderer>();
            if (r) { var c = r.material.color; c.a = 1 - t / .2f; r.material.color = c; }
            yield return null;
        }
        if (ring) Destroy(ring);
    }

    // ====================================================================
    // ATTACHÉ CASE (maleta)
    // ====================================================================
    void HandleCase()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && AttacheCase.I)
            AttacheCase.I.Toggle();
    }

    // ====================================================================
    // FACA (melee RE4)
    // ====================================================================
    void Melee()
    {
        nextFire = Time.time + .4f;
        // Slash visual
        StartCoroutine(KnifeSlash());
        // Dano em cone curto
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d > 2.5f) continue;
            Vector3 dir = (e.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) > .5f)
                e.TakeDamage(30 * dmgMultiplier, false);
        }
    }

    IEnumerator KnifeSlash()
    {
        var knife = GameObject.CreatePrimitive(PrimitiveType.Cube);
        knife.transform.SetParent(camT);
        knife.transform.localPosition = new Vector3(.3f, -.15f, .5f);
        knife.transform.localScale = new Vector3(.02f, .02f, .35f);
        Destroy(knife.GetComponent<Collider>());
        knife.GetComponent<Renderer>().material = GameManager.Mat(new Color32(180, 180, 190, 255));
        float t = 0;
        while (t < .2f)
        {
            t += Time.deltaTime;
            knife.transform.localRotation = Quaternion.Euler(0, Mathf.Lerp(-40, 40, t / .2f), 0);
            yield return null;
        }
        Destroy(knife);
    }

    // ====================================================================
    // RELOAD
    // ====================================================================
    IEnumerator Reload()
    {
        var w = weapons[curWeapon];
        if (w.ammoReserve <= 0 || w.ammoInMag >= w.magSize) yield break;
        reloading = true;
        yield return new WaitForSeconds(w.reloadTime);
        int need = w.magSize - w.ammoInMag, avail = Mathf.Min(need, w.ammoReserve);
        w.ammoInMag += avail; w.ammoReserve -= avail;
        reloading = false;
    }

    void SwitchWeapon(int dir)
    {
        int n = curWeapon;
        for (int i = 0; i < 5; i++)
        {
            n = (n + dir + 5) % 5;
            if (weapons[n].owned && (!AttacheCase.I || AttacheCase.I.HasWeapon(n)))
            {
                curWeapon = n; CreateViewModel(); return;
            }
        }
    }

    // ====================================================================
    // GRANADA
    // ====================================================================
    void ThrowGrenade()
    {
        grenades--;
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.transform.position = camT.position + camT.forward * .6f;
        g.transform.localScale = Vector3.one * .12f;
        g.GetComponent<Renderer>().material = GameManager.Mat(new Color32(50, 55, 40, 255));
        var rb = g.AddComponent<Rigidbody>(); rb.mass = .5f;
        rb.AddForce((camT.forward * 16 + camT.up * 5) , ForceMode.Impulse);
        g.AddComponent<GrenadeProjectile>().damage = 220;
    }

    // ====================================================================
    // DANO
    // ====================================================================
    public void TakeDamage(float dmg)
    {
        hp -= dmg;
        if (GameUI.I) GameUI.I.FlashDamage();
        if (hp <= 0) { hp = 0; GameManager.I.PlayerDied(); }
    }
}
