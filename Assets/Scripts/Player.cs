// ============================================================================
// VILL4GE — Player.cs  (RE4-faithful)
// Over-the-shoulder camera (RE4 signature), mira laser, 5 armas, faca,
// granada, ervas, sprint, chuva, efeitos de recuo, footsteps headbob.
// ============================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    [System.Serializable]
    public class WeaponData
    {
        public string name;
        public int baseDmg;
        public int dmg;
        public float fireRate;
        public float baseReloadTime, reloadTime;
        public int baseMagSize;
        public int magSize, ammoInMag, ammoReserve;
        public float baseSpread, spread;
        public int basePierce, pierceCount;
        public bool explosive, owned, exclusiveUnlocked;
        public int firepowerLevel, reloadLevel, capacityLevel;
    }

    public enum UpgradeCategory { Firepower, Reload, Capacity }
    public enum GrenadeType { Hand, Flash, Incendiary }

    // ── Estado ─────────────────────────────────────────────────────────────
    public float hp = 100, maxHp = 100;
    public int money, herbs, grenades = 3;
    public int redHerbs, yellowHerbs;
    public int flashGrenades, incendiaryGrenades;
    public int spinels, rubies, pendants;
    public WeaponData[] weapons;
    public int curWeapon;
    public float dmgMultiplier = 1f;
    public GrenadeType selectedGrenade = GrenadeType.Hand;
    public bool punisherUnlocked, punisherSpecialUnlocked;
    float nextFire;
    bool reloading, aiming;
    int reloadToken;
    int shotsFiredThisWave, shotsHitThisWave;
    float damageTakenThisWave;
    float quickTurnCooldown;
    bool grabbed;
    float grabTimer, grabDamageTimer, grabTickDamage;
    int grabMashProgress, grabMashTarget;
    Enemy grabber;

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
    GameObject characterVisual;
    LineRenderer laserLine;
    float headBob;

    // ── Config RE4 ─────────────────────────────────────────────────────────
    const float SPEED = 4.5f, SPRINT_MULT = 1.7f, AIM_SPEED_MULT = 0.35f;
    const float GRAVITY = 18f, SENS = 2.2f, AIM_SENS = 1.2f;
    const float MAX_HP_LIMIT = 160f;
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
        CreateCharacterVisual();
        CreateViewModel();
        CreateLaser();
        SetupRain();
    }

    void CreateCharacterVisual()
    {
        if (characterVisual) Destroy(characterVisual);

        characterVisual = VisualOverrideLoader.InstantiatePrefab(
            "Overrides/PlayerLeon",
            transform,
            new Vector3(0, -.9f, .18f),
            Vector3.zero,
            Vector3.one);

        if (characterVisual)
            characterVisual.name = "PlayerVisual";
    }

    void InitWeapons()
    {
        weapons = new WeaponData[]
        {
            new WeaponData { name="Handgun",  baseDmg=18,  dmg=18,  fireRate=.28f, baseReloadTime=1.5f, reloadTime=1.5f, baseMagSize=12, magSize=12, ammoInMag=12, ammoReserve=60,  baseSpread=.02f, spread=.02f, basePierce=0, pierceCount=0, owned=true },
            new WeaponData { name="Shotgun",  baseDmg=90,  dmg=90,  fireRate=.9f,  baseReloadTime=2f,   reloadTime=2f,   baseMagSize=6,  magSize=6,  ammoInMag=6,  ammoReserve=24,  baseSpread=.18f, spread=.18f, owned=false },
            new WeaponData { name="Rifle",    baseDmg=50,  dmg=50,  fireRate=.12f, baseReloadTime=2.5f, reloadTime=2.5f, baseMagSize=10, magSize=10, ammoInMag=10, ammoReserve=30,  baseSpread=.01f, spread=.01f, owned=false },
            new WeaponData { name="TMP",      baseDmg=10,  dmg=10,  fireRate=.06f, baseReloadTime=1.8f, reloadTime=1.8f, baseMagSize=50, magSize=50, ammoInMag=50, ammoReserve=200, baseSpread=.06f, spread=.06f, owned=false },
            new WeaponData { name="Rocket",   baseDmg=350, dmg=350, fireRate=1.5f,  baseReloadTime=3f,   reloadTime=3f,   baseMagSize=1,  magSize=1,  ammoInMag=1,  ammoReserve=3,   baseSpread=0, spread=0, owned=false, explosive=true },
        };
        curWeapon = 0;
        ApplyHandgunVariant();
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
        if (!GameManager.I) return;

        if (GameManager.I.State == GameManager.GState.Merchant)
        {
            if (AttacheCase.I && AttacheCase.I.IsOpen)
            {
                HandleCase();
                return;
            }
            HandleInteract();
            HandleCase();
            return;
        }

        if (GameManager.I.State != GameManager.GState.Playing &&
            GameManager.I.State != GameManager.GState.WaveIntro) return;

        if (AttacheCase.I && AttacheCase.I.IsOpen) return; // maleta aberta = pause
        if (grabbed)
        {
            HandleGrab();
            UpdateCamera();
            UpdateLaser();
            return;
        }

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
        if (quickTurnCooldown > 0f) quickTurnCooldown -= Time.deltaTime;
        bool quickTurn = !aiming &&
            quickTurnCooldown <= 0f &&
            v < -.35f &&
            (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift));
        if (quickTurn)
        {
            yaw += 180f;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            quickTurnCooldown = .35f;
            if (GameUI.I) GameUI.I.ShowStatusMessage("QUICK TURN", new Color32(215, 185, 120, 255), .35f);
        }

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
                EquipWeapon(i);
            }
        }
    }

    void HandleItems()
    {
        if (Input.GetKeyDown(KeyCode.B)) CycleGrenadeType();
        if (Input.GetKeyDown(KeyCode.G)) ThrowSelectedGrenade();
        if (Input.GetKeyDown(KeyCode.H)) UseHealingShortcut();
    }

    void HandleInteract()
    {
        if (Input.GetKeyDown(KeyCode.E) && GameManager.I.State == GameManager.GState.Merchant &&
            Vector3.Distance(transform.position, GameManager.I.merchantPos) < 4f && GameUI.I)
            GameUI.I.ToggleMerchant();
    }

    void HandleGrab()
    {
        aiming = false;
        CancelReload();
        grabTimer -= Time.deltaTime;
        grabDamageTimer -= Time.deltaTime;

        if (grabber)
        {
            Vector3 toGrabber = grabber.transform.position - transform.position;
            toGrabber.y = 0f;
            if (toGrabber.sqrMagnitude > .01f)
            {
                Vector3 faceDir = toGrabber.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 12f);
                yaw = transform.eulerAngles.y;

                Vector3 anchor = grabber.transform.position - faceDir * .8f;
                anchor.y = transform.position.y;
                cc.Move((anchor - transform.position) * Mathf.Min(1f, Time.deltaTime * 12f));
            }
        }

        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E))
        {
            grabMashProgress++;
            if (GameUI.I)
                GameUI.I.ShowStatusMessage($"BREAK FREE {grabMashProgress}/{grabMashTarget}", new Color32(230, 210, 120, 255), .2f);
        }

        if (grabMashProgress >= grabMashTarget)
        {
            EndGrab(true);
            return;
        }

        if (grabDamageTimer <= 0f)
        {
            grabDamageTimer = .55f;
            TakeDamage(grabTickDamage);
            if (hp <= 0f) return;
        }

        if (grabTimer <= 0f)
        {
            TakeDamage(grabTickDamage * 1.4f);
            if (hp <= 0f) return;
            EndGrab(false);
        }
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
        if (w.ammoInMag <= 0)
        {
            if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.GunEmpty);
            StartCoroutine(Reload());
            return;
        }
        w.ammoInMag--;
        nextFire = Time.time + w.fireRate;
        shotsFiredThisWave++;
        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.Gunshot);

        int pellets = GetPelletCount(curWeapon);
        bool hitEnemyThisShot = false;
        for (int p = 0; p < pellets; p++)
        {
            Vector3 dir = camT.forward;
            if (w.spread > 0)
            {
                dir += camT.right * Random.Range(-w.spread, w.spread);
                dir += camT.up * Random.Range(-w.spread, w.spread);
                dir.Normalize();
            }

            bool spawnImpact = false;
            Vector3 impactPoint = Vector3.zero;
            Vector3 impactNormal = Vector3.up;
            if (w.pierceCount > 0)
            {
                RaycastHit[] hits = Physics.RaycastAll(new Ray(camT.position, dir), 100f);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                int enemiesHit = 0;
                var piercedEnemies = new HashSet<Enemy>();
                var hitMedallions = new HashSet<BlueMedallion>();

                foreach (var hit in hits)
                {
                    var medallion = hit.collider.GetComponentInParent<BlueMedallion>();
                    if (medallion)
                    {
                        if (hitMedallions.Add(medallion))
                            medallion.Hit();
                        impactPoint = hit.point;
                        impactNormal = hit.normal;
                        spawnImpact = true;
                        continue;
                    }

                    var enemy = hit.collider.GetComponentInParent<Enemy>();
                    if (enemy)
                    {
                        if (!piercedEnemies.Add(enemy))
                            continue;
                        float dmg = w.dmg * dmgMultiplier / pellets * (pellets > 1 ? 1.5f : 1f);
                        bool headshot = hit.collider.gameObject.name == "Head";
                        if (headshot) dmg *= GetHeadshotMultiplier(curWeapon);
                        enemy.TakeDamage(dmg, headshot);
                        hitEnemyThisShot = true;
                        impactPoint = hit.point;
                        impactNormal = hit.normal;
                        spawnImpact = true;
                        enemiesHit++;
                        if (enemiesHit > w.pierceCount) break;
                        continue;
                    }

                    var breakable = hit.collider.GetComponentInParent<BreakableProp>();
                    if (breakable)
                    {
                        breakable.Damage(Mathf.Max(12f, w.dmg * .6f), hit.point, dir + hit.normal * .25f);
                        impactPoint = hit.point;
                        impactNormal = hit.normal;
                        spawnImpact = true;
                        break;
                    }

                    impactPoint = hit.point;
                    impactNormal = hit.normal;
                    spawnImpact = true;
                    break;
                }
            }
            else if (Physics.Raycast(new Ray(camT.position, dir), out RaycastHit hit, 100))
            {
                var medallion = hit.collider.GetComponentInParent<BlueMedallion>();
                if (medallion)
                {
                    medallion.Hit();
                }
                else
                {
                    var enemy = hit.collider.GetComponentInParent<Enemy>();
                    if (enemy)
                    {
                        float dmg = w.dmg * dmgMultiplier / pellets * (pellets > 1 ? 1.5f : 1f);
                        bool headshot = hit.collider.gameObject.name == "Head";
                        if (headshot) dmg *= GetHeadshotMultiplier(curWeapon);
                        enemy.TakeDamage(dmg, headshot);
                        hitEnemyThisShot = true;
                    }
                    else
                    {
                        var breakable = hit.collider.GetComponentInParent<BreakableProp>();
                        if (breakable)
                            breakable.Damage(Mathf.Max(12f, w.dmg * .6f), hit.point, dir + hit.normal * .25f);
                    }
                }

                impactPoint = hit.point;
                impactNormal = hit.normal;
                spawnImpact = true;
            }

            if (spawnImpact)
            {
                if (w.explosive) Explode(impactPoint, w.dmg * dmgMultiplier);
                else SpawnHitFX(impactPoint, impactNormal);
            }
        }

        if (hitEnemyThisShot)
            shotsHitThisWave++;

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
        foreach (var prop in FindObjectsByType<BreakableProp>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(pos, prop.transform.position);
            if (d < r)
                prop.Damage(dmg * .45f * (1 - d / r), pos, (prop.transform.position - pos).normalized + Vector3.up * .2f);
        }
        var fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.transform.position = pos; fx.transform.localScale = Vector3.one * r * .6f;
        Destroy(fx.GetComponent<Collider>());
        fx.GetComponent<Renderer>().material = GameManager.MatEmissive(
            new Color32(255, 130, 30, 200), new Color(4, 1.5f, .2f));
        Destroy(fx, .3f);
    }

    enum ContextMeleeType { Kick, Suplex }

    // ====================================================================
    // CHUTE RE4 (Roundhouse Kick / Suplex)
    // ====================================================================
    void HandleKick()
    {
        if (kickCooldown > 0) kickCooldown -= Time.deltaTime;
        if (kicking) return;

        // Detectar inimigo staggered perto
        ContextMeleeType meleeType;
        Enemy kickTarget = FindStaggeredEnemy(out meleeType);

        // Mostrar prompt
        if (GameUI.I)
            GameUI.I.ShowKickPrompt(kickTarget != null, GetContextMeleeLabel(meleeType));

        // F = chutar
        if (Input.GetKeyDown(KeyCode.F) && kickTarget != null && kickCooldown <= 0)
        {
            StartCoroutine(PerformKick(kickTarget, meleeType));
        }
    }

    Enemy FindStaggeredEnemy(out ContextMeleeType meleeType)
    {
        meleeType = ContextMeleeType.Kick;
        Enemy best = null;
        ContextMeleeType bestType = ContextMeleeType.Kick;
        float bestDist = KICK_RANGE;
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (!e.IsStaggered) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d > KICK_RANGE) continue;
            Vector3 dir = (e.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) < KICK_ARC) continue;
            ContextMeleeType candidateType = CanSuplex(e, d) ? ContextMeleeType.Suplex : ContextMeleeType.Kick;
            if (best == null ||
                candidateType > bestType ||
                (candidateType == bestType && d < bestDist))
            {
                bestDist = d;
                best = e;
                bestType = candidateType;
            }
        }
        meleeType = bestType;
        return best;
    }

    bool CanSuplex(Enemy enemy, float distance)
    {
        if (!enemy || enemy.type == Enemy.EType.Heavy || enemy.type == Enemy.EType.Chainsaw)
            return false;
        if (distance > KICK_RANGE - .25f)
            return false;

        Vector3 enemyToPlayer = (transform.position - enemy.transform.position).normalized;
        return Vector3.Dot(enemy.transform.forward, enemyToPlayer) < -.35f;
    }

    string GetContextMeleeLabel(ContextMeleeType meleeType)
    {
        return meleeType == ContextMeleeType.Suplex ? "SUPLEX" : "KICK";
    }

    IEnumerator PerformKick(Enemy target, ContextMeleeType meleeType)
    {
        kicking = true;
        kickCooldown = KICK_COOLDOWN;
        bool isSuplex = meleeType == ContextMeleeType.Suplex;

        if (GameUI.I)
            GameUI.I.ShowStatusMessage(isSuplex ? "SUPLEX" : "ROUNDHOUSE KICK",
                new Color32(255, 215, 110, 255), .75f);

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
            if (isSuplex)
            {
                leg.transform.localRotation = Quaternion.Euler(-25 * pct, 40 * pct, -25 * pct);
                leg.transform.localPosition = new Vector3(.08f, -.48f + .22f * pct, .68f + .12f * pct);
            }
            else
            {
                leg.transform.localRotation = Quaternion.Euler(-60 * pct, 20 * pct, 0);
                leg.transform.localPosition = new Vector3(.15f, -.6f + .3f * pct, .6f + .3f * pct);
            }
            yield return null;
        }

        if (isSuplex && target)
        {
            target.TakeKickDamage(KICK_DAMAGE * 1.8f * dmgMultiplier, -transform.forward * (KICK_KNOCKBACK * .7f));

            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (!e || e == target) continue;
                float d = Vector3.Distance(target.transform.position, e.transform.position);
                if (d > 1.8f) continue;
                Vector3 dir = (e.transform.position - target.transform.position).normalized;
                e.TakeKickDamage(KICK_DAMAGE * .6f * dmgMultiplier, (dir + Vector3.up * .2f) * (KICK_KNOCKBACK * .65f));
            }
        }
        else
        {
            // Aplicar dano em área (todos os inimigos no cone)
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d > KICK_RANGE + .5f) continue;
                Vector3 dir = (e.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, dir) < KICK_ARC - .1f) continue;

                // Dano + knockback
                e.TakeKickDamage(KICK_DAMAGE * dmgMultiplier, transform.forward * KICK_KNOCKBACK);
            }
        }

        DamageNearbyProps(isSuplex ? 2.2f : 2.8f, isSuplex ? 40f : 32f, transform.forward);
        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.KickHit);

        // Impacto visual
        SpawnKickImpact((isSuplex && target ? target.transform.position : transform.position + transform.forward * 1.5f) + Vector3.up * .8f);

        // Retrair perna
        t = 0;
        while (t < .15f)
        {
            t += Time.deltaTime;
            float pct = 1 - t / .15f;
            if (isSuplex)
                leg.transform.localRotation = Quaternion.Euler(-25 * pct, 40 * pct, -25 * pct);
            else
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
        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.Knife);
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

        DamageNearbyProps(2.5f, 30f, transform.forward);
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
        int weaponSlot = curWeapon;
        int myReloadToken = ++reloadToken;
        var w = weapons[weaponSlot];
        if (w.ammoReserve <= 0 || w.ammoInMag >= w.magSize) yield break;
        reloading = true;
        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.Reload);
        yield return new WaitForSeconds(w.reloadTime);
        if (!reloading || myReloadToken != reloadToken || weaponSlot != curWeapon) yield break;
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
                EquipWeapon(n);
                return;
            }
        }
    }

    // ====================================================================
    // GRANADA
    // ====================================================================
    void ThrowSelectedGrenade()
    {
        if (!HasAnyGrenades()) return;
        if (GetGrenadeCount(selectedGrenade) <= 0 && !SelectAvailableGrenade(selectedGrenade))
            return;

        switch (selectedGrenade)
        {
            case GrenadeType.Flash:
                flashGrenades--;
                break;
            case GrenadeType.Incendiary:
                incendiaryGrenades--;
                break;
            default:
                grenades--;
                break;
        }

        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.Grenade);
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.transform.position = camT.position + camT.forward * .6f;
        g.transform.localScale = Vector3.one * .12f;
        g.GetComponent<Renderer>().material = GameManager.Mat(GetGrenadeVisualColor(selectedGrenade));
        var rb = g.AddComponent<Rigidbody>(); rb.mass = .5f;
        rb.AddForce((camT.forward * 16 + camT.up * 5) , ForceMode.Impulse);
        var projectile = g.AddComponent<GrenadeProjectile>();
        projectile.type = selectedGrenade;
        switch (selectedGrenade)
        {
            case GrenadeType.Flash:
                projectile.damage = 20f;
                projectile.stunDuration = 2.8f;
                break;
            case GrenadeType.Incendiary:
                projectile.damage = 110f;
                projectile.burnDamage = 150f;
                break;
            default:
                projectile.damage = 220f;
                break;
        }

        if (GameUI.I)
            GameUI.I.ShowStatusMessage(GetGrenadeThrowMessage(selectedGrenade), GetGrenadeHudColor(selectedGrenade), .9f);

        if (GetGrenadeCount(selectedGrenade) <= 0)
            SelectAvailableGrenade(selectedGrenade);
    }

    // ====================================================================
    // DANO
    // ====================================================================
    public void TakeDamage(float dmg)
    {
        hp -= dmg;
        damageTakenThisWave += dmg;
        if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.PlayerHurt);
        if (GameUI.I) GameUI.I.FlashDamage();
        if (hp <= 0)
        {
            hp = 0;
            grabbed = false;
            grabber = null;
            if (AudioManager.I) AudioManager.I.PlaySFX(AudioManager.SFX.PlayerDeath);
            GameManager.I.PlayerDied();
        }
    }

    void EquipWeapon(int slot)
    {
        CancelReload();
        curWeapon = slot;
        CreateViewModel();
        if (GameUI.I)
            GameUI.I.ShowStatusMessage(weapons[slot].name.ToUpper(), new Color32(215, 185, 120, 255), .75f);
    }

    void CancelReload()
    {
        if (!reloading) return;
        reloading = false;
        reloadToken++;
    }

    void DamageNearbyProps(float range, float damage, Vector3 forward)
    {
        foreach (var prop in FindObjectsByType<BreakableProp>(FindObjectsSortMode.None))
        {
            if (!prop || !prop.gameObject.activeInHierarchy) continue;

            Vector3 toProp = prop.transform.position - transform.position;
            float dist = toProp.magnitude;
            if (dist > range || dist <= .01f) continue;
            if (Vector3.Dot(forward, toProp.normalized) < .35f) continue;

            prop.Damage(damage, prop.transform.position, forward);
        }
    }

    public void BeginWaveStats()
    {
        shotsFiredThisWave = 0;
        shotsHitThisWave = 0;
        damageTakenThisWave = 0f;
    }

    public float GetWaveAccuracy()
    {
        if (shotsFiredThisWave <= 0) return 0f;
        return (float)shotsHitThisWave / shotsFiredThisWave;
    }

    public float GetWaveDamageTaken() => damageTakenThisWave;
    public bool IsGrabbed => grabbed;
    public Enemy CurrentGrabber => grabber;

    public bool CanUseHealingShortcut()
    {
        if (herbs <= 0) return false;
        if (yellowHerbs > 0 && maxHp < MAX_HP_LIMIT) return true;
        return hp < maxHp - .01f;
    }

    public string GetHealingShortcutLabel()
    {
        if (herbs > 0 && redHerbs > 0 && yellowHerbs > 0 && (hp < maxHp - .01f || maxHp < MAX_HP_LIMIT))
            return "Mix G+R+Y";
        if (herbs > 0 && yellowHerbs > 0 && (hp < maxHp - .01f || maxHp < MAX_HP_LIMIT))
            return "Mix G+Y";
        if (herbs > 0 && redHerbs > 0 && hp < maxHp - .01f)
            return "Mix G+R";
        if (herbs > 0 && hp < maxHp - .01f)
            return "Use Green Herb";
        return "Use Herb";
    }

    public bool UseHealingShortcut()
    {
        if (herbs <= 0) return false;

        string message = null;
        if (herbs > 0 && redHerbs > 0 && yellowHerbs > 0 && (hp < maxHp - .01f || maxHp < MAX_HP_LIMIT))
        {
            herbs--;
            redHerbs--;
            yellowHerbs--;
            ApplyMaxHpBoost(10f);
            hp = maxHp;
            message = "MIXED HERBS G+R+Y";
        }
        else if (herbs > 0 && yellowHerbs > 0 && (hp < maxHp - .01f || maxHp < MAX_HP_LIMIT))
        {
            herbs--;
            yellowHerbs--;
            ApplyMaxHpBoost(10f);
            hp = Mathf.Min(hp + 70f, maxHp);
            message = "MIXED HERBS G+Y";
        }
        else if (herbs > 0 && redHerbs > 0 && hp < maxHp - .01f)
        {
            herbs--;
            redHerbs--;
            hp = maxHp;
            message = "MIXED HERBS G+R";
        }
        else if (herbs > 0 && hp < maxHp - .01f)
        {
            herbs--;
            hp = Mathf.Min(hp + 40f, maxHp);
            message = "GREEN HERB USED";
        }

        if (string.IsNullOrEmpty(message))
            return false;

        if (GameUI.I) GameUI.I.ShowStatusMessage(message, new Color32(105, 210, 120, 255), 1.2f);
        return true;
    }

    void ApplyMaxHpBoost(float amount)
    {
        float boosted = Mathf.Min(MAX_HP_LIMIT, maxHp + amount);
        float delta = boosted - maxHp;
        maxHp = boosted;
        if (delta > 0f)
            hp = Mathf.Min(maxHp, hp + delta);
    }

    public bool HasAnyGrenades()
    {
        return grenades + flashGrenades + incendiaryGrenades > 0;
    }

    public int GetGrenadeCount(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Flash: return flashGrenades;
            case GrenadeType.Incendiary: return incendiaryGrenades;
            default: return grenades;
        }
    }

    public bool SelectAvailableGrenade(GrenadeType startType)
    {
        if (!HasAnyGrenades()) return false;

        int start = (int)startType;
        for (int i = 0; i < 3; i++)
        {
            var type = (GrenadeType)((start + i) % 3);
            if (GetGrenadeCount(type) > 0)
            {
                selectedGrenade = type;
                return true;
            }
        }

        return false;
    }

    public void ValidateGrenadeSelection()
    {
        if (GetGrenadeCount(selectedGrenade) > 0) return;
        SelectAvailableGrenade(GrenadeType.Hand);
    }

    public void CycleGrenadeType()
    {
        if (!HasAnyGrenades()) return;

        int start = ((int)selectedGrenade + 1) % 3;
        for (int i = 0; i < 3; i++)
        {
            var type = (GrenadeType)((start + i) % 3);
            if (GetGrenadeCount(type) <= 0) continue;

            selectedGrenade = type;
            if (GameUI.I)
                GameUI.I.ShowStatusMessage(GetGrenadeSelectMessage(type), GetGrenadeHudColor(type), .9f);
            return;
        }
    }

    public string GetHerbInventorySummary()
    {
        return $"G {herbs}  |  R {redHerbs}  |  Y {yellowHerbs}";
    }

    public string GetGrenadeInventorySummary()
    {
        return $"H {grenades}  |  F {flashGrenades}  |  I {incendiaryGrenades}";
    }

    public string GetSelectedGrenadeLabel()
    {
        switch (selectedGrenade)
        {
            case GrenadeType.Flash: return "Flash";
            case GrenadeType.Incendiary: return "Incendiary";
            default: return "Hand";
        }
    }

    Color32 GetGrenadeHudColor(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Flash: return new Color32(210, 220, 255, 255);
            case GrenadeType.Incendiary: return new Color32(240, 135, 70, 255);
            default: return new Color32(175, 210, 120, 255);
        }
    }

    Color32 GetGrenadeVisualColor(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Flash: return new Color32(190, 200, 220, 255);
            case GrenadeType.Incendiary: return new Color32(165, 75, 35, 255);
            default: return new Color32(50, 55, 40, 255);
        }
    }

    string GetGrenadeSelectMessage(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Flash: return "FLASH GRENADE READY";
            case GrenadeType.Incendiary: return "INCENDIARY GRENADE READY";
            default: return "HAND GRENADE READY";
        }
    }

    string GetGrenadeThrowMessage(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Flash: return "FLASH GRENADE THROWN";
            case GrenadeType.Incendiary: return "INCENDIARY GRENADE THROWN";
            default: return "HAND GRENADE THROWN";
        }
    }

    public bool TryBeginGrab(Enemy enemy, float duration, float tickDamage, int mashTarget)
    {
        if (grabbed || !enemy || hp <= 0f) return false;

        grabbed = true;
        grabber = enemy;
        grabTimer = Mathf.Max(.8f, duration);
        grabTickDamage = Mathf.Max(1f, tickDamage);
        grabDamageTimer = .35f;
        grabMashTarget = Mathf.Max(3, mashTarget);
        grabMashProgress = 0;
        aiming = false;
        CancelReload();

        if (GameUI.I)
            GameUI.I.ShowStatusMessage("GRABBED - MASH F OR E", new Color32(220, 110, 90, 255), 1.3f);
        return true;
    }

    public void ReleaseGrabFromEnemy(Enemy enemy, bool escaped)
    {
        if (!grabbed || grabber != enemy) return;
        EndGrab(escaped);
    }

    void EndGrab(bool escaped)
    {
        Enemy source = grabber;
        grabbed = false;
        grabber = null;
        grabTimer = 0f;
        grabDamageTimer = 0f;
        grabTickDamage = 0f;
        grabMashProgress = 0;
        grabMashTarget = 0;

        if (source)
            source.ForceGrabBreak((transform.position - source.transform.position).normalized * 4.5f, escaped);

        if (escaped && GameUI.I)
            GameUI.I.ShowStatusMessage("BROKE FREE", new Color32(220, 210, 120, 255), .85f);
    }

    int GetPelletCount(int weaponIdx)
    {
        if (weaponIdx == 1)
            return weapons[weaponIdx].exclusiveUnlocked ? 10 : 8;
        return 1;
    }

    float GetHeadshotMultiplier(int weaponIdx)
    {
        if (weaponIdx == 2 && weapons[weaponIdx].exclusiveUnlocked) return 4.5f;
        if (weaponIdx == 0 && weapons[weaponIdx].exclusiveUnlocked) return 4f;
        return 3f;
    }

    public bool UnlockPunisher(bool specialPierce)
    {
        bool changed = !punisherUnlocked || (specialPierce && !punisherSpecialUnlocked);
        punisherUnlocked = true;
        punisherSpecialUnlocked |= specialPierce;
        ApplyHandgunVariant();
        RefreshWeaponStats(0);
        return changed;
    }

    void ApplyHandgunVariant()
    {
        if (weapons == null || weapons.Length == 0) return;

        var handgun = weapons[0];
        if (punisherUnlocked)
        {
            handgun.name = "Punisher";
            handgun.baseDmg = 20;
            handgun.fireRate = .24f;
            handgun.baseReloadTime = 1.35f;
            handgun.baseMagSize = 15;
            handgun.baseSpread = .018f;
            handgun.basePierce = punisherSpecialUnlocked ? 4 : 1;
        }
        else
        {
            handgun.name = "Handgun";
            handgun.baseDmg = 18;
            handgun.fireRate = .28f;
            handgun.baseReloadTime = 1.5f;
            handgun.baseMagSize = 12;
            handgun.baseSpread = .02f;
            handgun.basePierce = 0;
        }
    }

    public bool CanUpgradeWeapon(int weaponIdx, UpgradeCategory category)
    {
        if (weaponIdx < 0 || weaponIdx >= weapons.Length) return false;
        var weapon = weapons[weaponIdx];
        if (!weapon.owned || weaponIdx == 4) return false;

        switch (category)
        {
            case UpgradeCategory.Firepower: return weapon.firepowerLevel < 3;
            case UpgradeCategory.Reload: return weapon.reloadLevel < 3;
            case UpgradeCategory.Capacity: return weapon.capacityLevel < 3;
            default: return false;
        }
    }

    public int GetUpgradeCost(int weaponIdx, UpgradeCategory category)
    {
        if (weaponIdx < 0 || weaponIdx >= weapons.Length) return 0;

        var weapon = weapons[weaponIdx];
        int bias;
        switch (weaponIdx)
        {
            case 0: bias = 0; break;
            case 1: bias = 260; break;
            case 2: bias = 420; break;
            case 3: bias = 320; break;
            default: bias = 900; break;
        }

        switch (category)
        {
            case UpgradeCategory.Firepower: return 850 + bias + weapon.firepowerLevel * 650;
            case UpgradeCategory.Reload: return 700 + bias + weapon.reloadLevel * 500;
            case UpgradeCategory.Capacity: return 650 + bias + weapon.capacityLevel * 450;
            default: return 0;
        }
    }

    public bool UpgradeWeapon(int weaponIdx, UpgradeCategory category)
    {
        if (!CanUpgradeWeapon(weaponIdx, category)) return false;

        var weapon = weapons[weaponIdx];
        int oldMag = weapon.magSize;

        switch (category)
        {
            case UpgradeCategory.Firepower:
                weapon.firepowerLevel++;
                break;
            case UpgradeCategory.Reload:
                weapon.reloadLevel++;
                break;
            case UpgradeCategory.Capacity:
                weapon.capacityLevel++;
                break;
        }

        RefreshWeaponStats(weaponIdx);

        if (category == UpgradeCategory.Capacity && weapon.magSize > oldMag)
            weapon.ammoInMag = weapon.magSize;

        return true;
    }

    public bool CanUpgradeExclusive(int weaponIdx)
    {
        if (weaponIdx < 0 || weaponIdx >= weapons.Length || weaponIdx == 4) return false;
        var weapon = weapons[weaponIdx];
        if (!weapon.owned || weapon.exclusiveUnlocked) return false;
        return weapon.firepowerLevel >= 3 && weapon.reloadLevel >= 3 && weapon.capacityLevel >= 3;
    }

    public int GetExclusiveUpgradeCost(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return 7800;
            case 1: return 9200;
            case 2: return 10800;
            case 3: return 8600;
            default: return 0;
        }
    }

    public bool UpgradeExclusive(int weaponIdx)
    {
        if (!CanUpgradeExclusive(weaponIdx)) return false;
        weapons[weaponIdx].exclusiveUnlocked = true;
        RefreshWeaponStats(weaponIdx);
        return true;
    }

    public string GetExclusiveUpgradeName(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return "Exclusive Tune-Up: Precision";
            case 1: return "Exclusive Tune-Up: Wide Spread";
            case 2: return "Exclusive Tune-Up: Magnum Scope";
            case 3: return "Exclusive Tune-Up: Stable Frame";
            default: return "Exclusive Tune-Up";
        }
    }

    public string GetExclusiveUpgradeDetail(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return weapons[weaponIdx].exclusiveUnlocked ? "Higher damage and stronger headshots." : "Boost handgun power and headshot bonus.";
            case 1: return weapons[weaponIdx].exclusiveUnlocked ? "Extra pellets for crowd control." : "Adds more pellets to every shotgun blast.";
            case 2: return weapons[weaponIdx].exclusiveUnlocked ? "Massive headshot multiplier." : "Raises rifle headshot damage dramatically.";
            case 3: return weapons[weaponIdx].exclusiveUnlocked ? "Tighter spread with more force." : "Improves TMP stability and damage.";
            default: return "";
        }
    }

    public void RefreshAllWeaponStats()
    {
        for (int i = 0; i < weapons.Length; i++)
            RefreshWeaponStats(i);
    }

    public string GetWeaponLevelSummary(int weaponIdx)
    {
        if (weaponIdx < 0 || weaponIdx >= weapons.Length) return "";
        var weapon = weapons[weaponIdx];
        string exclusive = weaponIdx == 4 ? "EX N/A" :
            weapon.exclusiveUnlocked ? "EX YES" :
            CanUpgradeExclusive(weaponIdx) ? "EX READY" : "EX LOCKED";
        return $"FP {weapon.firepowerLevel}/3  |  RL {weapon.reloadLevel}/3  |  CP {weapon.capacityLevel}/3  |  {exclusive}";
    }

    void RefreshWeaponStats(int weaponIdx)
    {
        if (weaponIdx < 0 || weaponIdx >= weapons.Length) return;

        if (weaponIdx == 0)
            ApplyHandgunVariant();

        var weapon = weapons[weaponIdx];
        float damageMult = 1f + weapon.firepowerLevel * .18f;
        float spreadMult = 1f;
        if (weapon.exclusiveUnlocked)
        {
            switch (weaponIdx)
            {
                case 0: damageMult *= 1.35f; spreadMult *= .75f; break;
                case 1: damageMult *= 1.25f; break;
                case 2: damageMult *= 1.45f; break;
                case 3: damageMult *= 1.22f; spreadMult *= .55f; break;
            }
        }

        weapon.dmg = Mathf.RoundToInt(weapon.baseDmg * damageMult);
        weapon.reloadTime = Mathf.Max(.35f, weapon.baseReloadTime * (1f - weapon.reloadLevel * .12f));
        weapon.magSize = weapon.baseMagSize + CapacityStep(weaponIdx) * weapon.capacityLevel;
        weapon.spread = weapon.baseSpread * spreadMult;
        weapon.pierceCount = weapon.basePierce;
        weapon.ammoInMag = Mathf.Min(weapon.ammoInMag, weapon.magSize);
    }

    int CapacityStep(int weaponIdx)
    {
        switch (weaponIdx)
        {
            case 0: return 2;
            case 1: return 1;
            case 2: return 2;
            case 3: return 10;
            default: return 0;
        }
    }

    public void AddTreasure(Pickup.TreasureType treasureType, int amount = 1)
    {
        amount = Mathf.Max(1, amount);
        switch (treasureType)
        {
            case Pickup.TreasureType.Spinel: spinels += amount; break;
            case Pickup.TreasureType.Ruby: rubies += amount; break;
            case Pickup.TreasureType.Pendant: pendants += amount; break;
        }
    }

    public int GetTreasureCount()
    {
        return spinels + rubies + pendants;
    }

    public int GetTreasureSellValue()
    {
        return spinels * 900 + rubies * 2400 + pendants * 3600;
    }

    public string GetTreasureSummary()
    {
        return $"Spinel {spinels}  |  Ruby {rubies}  |  Pendant {pendants}";
    }

    public int SellAllTreasures()
    {
        int total = GetTreasureSellValue();
        if (total <= 0) return 0;

        spinels = 0;
        rubies = 0;
        pendants = 0;
        money += total;
        return total;
    }

    // ── SaveSystem helpers ────────────────────────────────────────────────
    /// <summary>Alias for money (pesetas) — used by SaveSystem.</summary>
    public int pesetas { get => money; set => money = value; }

    /// <summary>Wrapper used by SaveSystem to switch to a specific weapon slot.</summary>
    public void SwitchToSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 0, weapons.Length - 1);
        if (weapons[slot].owned)
        {
            EquipWeapon(slot);
        }
    }

    /// <summary>Alias so external scripts can read the current weapon index.</summary>
    public int currentWeapon { get => curWeapon; set { curWeapon = value; } }
}
