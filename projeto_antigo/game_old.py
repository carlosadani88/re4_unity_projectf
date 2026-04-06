"""
VILL4GE — RE4 Over-the-shoulder 3D (Pygame raycaster + Leon sprite)
Weapons: Handgun / Shotgun / Rifle / TMP — switch with 1-4 or scroll
Interact: E (doors/merchant) | G grenade | M minimap | TAB briefcase
"""
import pygame, math, random, sys, os

pygame.init()
pygame.mixer.init(frequency=22050, size=-16, channels=1, buffer=512)

W, H = 1280, 720
HW, HH = W // 2, H // 2
FPS = 60
screen = pygame.display.set_mode((W, H))
pygame.display.set_caption("VILL4GE - Over The Shoulder")
clock = pygame.time.Clock()

# ═══════════════════════════════════════════════════════════
# MAP — RE4 village: open area, scattered buildings, fences
# 0=ground  1=stone wall  2=wood wall  3=fence  4=door(closed)  5=door(open)
# 60×60 with large open village center
# ═══════════════════════════════════════════════════════════
M_W, M_H = 60, 60
MAP = [[0]*M_W for _ in range(M_H)]

def rect(v, x1, y1, x2, y2):
    for y in range(y1, y2+1):
        for x in range(x1, x2+1):
            if 0 <= y < M_H and 0 <= x < M_W:
                MAP[y][x] = v

def box(v, x, y, w, h):
    for i in range(w):
        MAP[y][x+i] = v; MAP[y+h-1][x+i] = v
    for j in range(h):
        MAP[y+j][x] = v; MAP[y+j][x+w-1] = v

# Border walls
rect(1, 0, 0, M_W-1, 0); rect(1, 0, M_H-1, M_W-1, M_H-1)
rect(1, 0, 0, 0, M_H-1); rect(1, M_W-1, 0, M_W-1, M_H-1)

# ── Buildings ──
box(1, 4, 4, 8, 6);   MAP[7][8] = 4
box(2, 40, 5, 6, 5);  MAP[7][40] = 4
box(1, 8, 20, 7, 6);  MAP[23][14] = 4
box(2, 35, 18, 10, 8); MAP[22][35] = 4; MAP[22][44] = 4
box(2, 5, 40, 5, 4);  MAP[42][9] = 4
box(1, 22, 3, 10, 7); MAP[9][27] = 4
box(1, 20, 38, 8, 6); MAP[38][24] = 4
box(2, 50, 30, 5, 4); MAP[32][50] = 4
box(1, 45, 45, 7, 6); MAP[48][45] = 4
# extra buildings for richer map
box(1, 14, 48, 6, 5); MAP[48][17] = 4          # building 10
box(2, 52, 15, 5, 4); MAP[17][52] = 4          # building 11
box(1, 28, 28, 6, 5); MAP[32][31] = 4          # building 12

# ── Fences ──
rect(3, 15, 14, 30, 14); rect(3, 15, 14, 15, 18); rect(3, 30, 14, 30, 18)
MAP[16][15] = 0; MAP[16][30] = 0
rect(3, 3, 32, 3, 38); rect(3, 48, 12, 55, 12)
# more fences
rect(3, 38, 38, 44, 38); rect(3, 10, 12, 10, 16)

def get_tile(x, y):
    ix, iy = int(x), int(y)
    if 0 <= iy < M_H and 0 <= ix < M_W: return MAP[iy][ix]
    return 1

def is_solid(x, y):
    t = get_tile(x, y)
    return t in (1, 2, 3, 4)  # 5 = open door = passable

WALL_COLORS = {
    1: (85, 75, 58),   # stone
    2: (100, 78, 52),  # wood
    3: (110, 95, 65),  # fence
    4: (60, 38, 22),   # door closed
    5: (40, 28, 15),   # door open (darker)
}
WALL_DARK = {k: (int(v[0]*.75), int(v[1]*.75), int(v[2]*.75)) for k, v in WALL_COLORS.items()}

FLOOR_BASE = (78, 65, 45)
CEIL_COL = (32, 28, 22)

# Ganado visual types (clothing variety like RE4 villagers)
GANADO_TYPES = [
    {'shirt':(100,82,55), 'pants':(65,50,32), 'skin':(185,155,120), 'hair':(50,35,20), 'band':False, 'blood':False, 'susp':True},
    {'shirt':(60,68,80),  'pants':(50,45,38), 'skin':(175,148,115), 'hair':(40,30,18), 'band':False, 'blood':False, 'susp':False},
    {'shirt':(95,78,48),  'pants':(55,45,30), 'skin':(170,140,108), 'hair':(55,40,25), 'band':True,  'blood':True,  'susp':False},
    {'shirt':(110,95,60), 'pants':(70,58,38), 'skin':(180,150,115), 'hair':(45,32,18), 'band':False, 'blood':False, 'susp':False},
    {'shirt':(140,130,115),'pants':(60,48,32),'skin':(175,145,110), 'hair':(48,35,20), 'band':False, 'blood':True,  'susp':False},
    {'shirt':(55,52,48),  'pants':(42,38,30), 'skin':(165,138,105), 'hair':(35,25,15), 'band':True,  'blood':True,  'susp':False},
]

# ═══════════════════════════════════════════════════════════
# WEAPONS
# ═══════════════════════════════════════════════════════════
WEAPONS = {
    'handgun':  {'name': 'Handgun',  'dmg': 25, 'cd': 0.22, 'spread': 0.02,
                 'pellets': 1, 'range': 22, 'ammo_key': 'hg', 'icon': 'P',
                 'mag': 15, 'bw': 3, 'bh': 2, 'col': (140,140,135)},
    'shotgun':  {'name': 'Shotgun',  'dmg': 18, 'cd': 0.7,  'spread': 0.12,
                 'pellets': 6, 'range': 10, 'ammo_key': 'sg', 'icon': 'S',
                 'mag': 6,  'bw': 5, 'bh': 2, 'col': (120,100,70)},
    'rifle':    {'name': 'Rifle',    'dmg': 80, 'cd': 1.2,  'spread': 0.005,
                 'pellets': 1, 'range': 30, 'ammo_key': 'rf', 'icon': 'R',
                 'mag': 5,  'bw': 6, 'bh': 1, 'col': (100,110,120)},
    'tmp':      {'name': 'TMP',      'dmg': 12, 'cd': 0.08, 'spread': 0.06,
                 'pellets': 1, 'range': 16, 'ammo_key': 'tm', 'icon': 'T',
                 'mag': 30, 'bw': 3, 'bh': 2, 'col': (130,130,140)},
}
WPN_ORDER = ['handgun', 'shotgun', 'rifle', 'tmp']

# ═══════════════════════════════════════════════════════════
# SOUNDS
# ═══════════════════════════════════════════════════════════
def mk_snd(freq, dur, vol=0.25, wave='saw'):
    sr = 22050; n = int(sr * dur / 1000)
    arr = bytearray(n * 2)
    for i in range(n):
        t = i / sr; env = max(0, 1 - t / (dur / 1000))
        if wave == 'saw': v = 2 * (freq * t % 1) - 1
        elif wave == 'square': v = 1 if math.sin(2*math.pi*freq*t) > 0 else -1
        else: v = math.sin(2 * math.pi * freq * t)
        val = int(v * env * vol * 32767)
        val = max(-32768, min(32767, val))
        arr[i*2] = val & 0xFF; arr[i*2+1] = (val >> 8) & 0xFF
    return pygame.mixer.Sound(buffer=bytes(arr))

snd_shoot    = mk_snd(160, 100, 0.25, 'saw')
snd_shotgun  = mk_snd(90, 180, 0.30, 'saw')
snd_rifle    = mk_snd(200, 250, 0.28, 'square')
snd_tmp      = mk_snd(220, 60, 0.18, 'saw')
snd_hit      = mk_snd(100, 120, 0.15, 'sine')
snd_die      = mk_snd(80, 400, 0.2, 'saw')
snd_pick     = mk_snd(600, 150, 0.1, 'sine')
snd_reload   = mk_snd(300, 80, 0.1, 'square')
snd_door     = mk_snd(120, 200, 0.12, 'square')
snd_grenade  = mk_snd(60, 500, 0.35, 'saw')
snd_step     = mk_snd(90, 40, 0.04, 'sine')
snd_chainsaw = mk_snd(55, 300, 0.20, 'saw')
snd_merchant = mk_snd(400, 100, 0.08, 'sine')
snd_wpn_switch = mk_snd(500, 40, 0.06, 'square')

WPN_SOUNDS = {'handgun': snd_shoot, 'shotgun': snd_shotgun, 'rifle': snd_rifle, 'tmp': snd_tmp}

# ═══════════════════════════════════════════════════════════
# TEXTURES (RE4 village models)
# ═══════════════════════════════════════════════════════════
TEX_SIZE = 128
_BASE_DIR = os.path.dirname(os.path.abspath(__file__))
_VIL_TEX = os.path.join(_BASE_DIR, 'Modelos', 'village-re4', 'textures')
_LEON_TEX = os.path.join(_BASE_DIR, 'Modelos', 'resident-evil-4-leon', 'textures')

# Fog color (RE4 grey-brown mist)
FOG_COLOR = (58, 52, 42)
FOG_DENSITY = 0.07

def _load_tex(fname, tint=None):
    path = os.path.join(_VIL_TEX, fname)
    if not os.path.exists(path): return None
    img = pygame.image.load(path).convert()
    img = pygame.transform.scale(img, (TEX_SIZE, TEX_SIZE))
    if tint:
        img.fill(tint, special_flags=pygame.BLEND_RGB_MULT)
    cols = [img.subsurface((x, 0, 1, TEX_SIZE)).copy() for x in range(TEX_SIZE)]
    return (img, cols)

# Wall textures — stone/wood/fence/door from real RE4 model files
WALL_TEX = {}
_tex_map = {
    1: ('20_c.jpeg', None),       # stone brick walls
    2: ('90_c.jpeg', None),       # weathered wood panels
    3: ('71_c.jpeg', None),       # wood plank fence
    4: ('59_c.jpeg', (140,110,80)),# dark rustic door (tinted)
}
for _wt, (_fn, _tint) in _tex_map.items():
    _t = _load_tex(_fn, _tint)
    if _t: WALL_TEX[_wt] = _t

# Scenery textures (pre-loaded for billboard sprites)
BARREL_TEX = None
_bt = os.path.join(_VIL_TEX, '37_c.jpeg')
if os.path.exists(_bt):
    BARREL_TEX = pygame.transform.scale(pygame.image.load(_bt).convert(), (64, 64))

CRATE_TEX = None
_ct = os.path.join(_VIL_TEX, '90_c.jpeg')
if os.path.exists(_ct):
    CRATE_TEX = pygame.transform.scale(pygame.image.load(_ct).convert(), (64, 64))

TREE_BARK_TEX = None
_tbt = os.path.join(_VIL_TEX, '7_c.jpeg')
if os.path.exists(_tbt):
    TREE_BARK_TEX = pygame.transform.scale(pygame.image.load(_tbt).convert(), (32, 64))

FOLIAGE_TEX = None
_flt = os.path.join(_VIL_TEX, 'f1_c.jpeg')
if os.path.exists(_flt):
    FOLIAGE_TEX = pygame.transform.scale(pygame.image.load(_flt).convert(), (80, 80))

GROUND_TEX = None
_gnd = os.path.join(_VIL_TEX, '48_c.jpeg')
if os.path.exists(_gnd):
    GROUND_TEX = pygame.transform.scale(pygame.image.load(_gnd).convert(), (64, 64))

# Sky panorama — wider for seamless scrolling, tinted moody
SKY_TEX = None
_sky_path = os.path.join(_VIL_TEX, 'sky_color.jpeg')
if os.path.exists(_sky_path):
    _sky_raw = pygame.image.load(_sky_path).convert()
    SKY_TEX = pygame.transform.scale(_sky_raw, (W * 4, H))
    # Darken + desaturate for overcast RE4 mood
    _mood = pygame.Surface(SKY_TEX.get_size()); _mood.fill((160, 155, 145))
    SKY_TEX.blit(_mood, (0, 0), special_flags=pygame.BLEND_RGB_MULT)

# Pre-rendered floor surface (layered textures + fog gradient)
FLOOR_SURF = pygame.Surface((W, H))
if GROUND_TEX:
    _ft = pygame.transform.scale(GROUND_TEX, (96, 96))
    for _tx in range(0, W, 96):
        for _ty in range(0, H, 96):
            FLOOR_SURF.blit(_ft, (_tx, _ty))
    # Near=brighter, far(top)=fog color
    _grad = pygame.Surface((W, H))
    for _y in range(H):
        _f = _y / H  # 0=top(far) 1=bottom(near)
        _sv = int(30 + _f * 200)
        _fr = int(FOG_COLOR[0] * (1 - _f) + _sv * _f)
        _fg = int(FOG_COLOR[1] * (1 - _f) + _sv * _f)
        _fb = int(FOG_COLOR[2] * (1 - _f) + _sv * _f)
        pygame.draw.line(_grad, (min(255,_fr), min(255,_fg), min(255,_fb)), (0, _y), (W, _y))
    FLOOR_SURF.blit(_grad, (0, 0), special_flags=pygame.BLEND_RGB_MULT)
else:
    for _y in range(H):
        _f = _y / H; _s = min(1.0, _f * 1.8)
        pygame.draw.line(FLOOR_SURF, (int(FLOOR_BASE[0]*_s), int(FLOOR_BASE[1]*_s), int(FLOOR_BASE[2]*_s)), (0, _y), (W, _y))

# Pre-baked vignette overlay (heavier RE4-style)
VIGNETTE = pygame.Surface((W, H), pygame.SRCALPHA)
for _r in range(25):
    _a = int(65 * (_r / 25) ** 1.5)
    _rd = int(W * 0.55 - _r * W * 0.022)
    if _rd > 0:
        pygame.draw.circle(VIGNETTE, (0, 0, 0, min(255, _a)), (HW, HH), _rd, max(1, int(W * 0.025)))

# Color grading overlay (darker desaturated RE4 tone)
COLOR_GRADE = pygame.Surface((W, H), pygame.SRCALPHA)
COLOR_GRADE.fill((12, 8, 3, 32))

# Fog gradient strip (for blending walls)
_FOG_CACHE = {}
def get_fog_surface(w, h, fog_factor):
    key = (w, h, int(fog_factor * 100))
    if key not in _FOG_CACHE:
        s = pygame.Surface((w, h))
        s.fill(FOG_COLOR)
        s.set_alpha(int(fog_factor * 255))
        _FOG_CACHE[key] = s
    return _FOG_CACHE[key]

# ═══════════════════════════════════════════════════════════
# FONTS
# ═══════════════════════════════════════════════════════════
font_big   = pygame.font.SysFont('Georgia', 58, bold=True)
font_med   = pygame.font.SysFont('Georgia', 26)
font_sm    = pygame.font.SysFont('Consolas', 17)
font_xs    = pygame.font.SysFont('Consolas', 13)
font_title = pygame.font.SysFont('Georgia', 90, bold=True)

# ═══════════════════════════════════════════════════════════
# LEON SPRITE (detailed back view using RE4 model texture colors)
# ═══════════════════════════════════════════════════════════
# Sample Leon's actual model texture colors
_leon_face_path = os.path.join(_LEON_TEX, 'leon_tex.png')
_leon_hair_path = os.path.join(_LEON_TEX, 'leon_kami_n.png')
_leon_body_path = os.path.join(_LEON_TEX, 'pl0004.png')
_leon_hand_path = os.path.join(_LEON_TEX, 'pl0005.png')

# Extract average colors from Leon's textures for accurate rendering
def _avg_color(path, rect_area=None):
    if not os.path.exists(path): return None
    img = pygame.image.load(path).convert()
    if rect_area:
        iw, ih = img.get_width(), img.get_height()
        rx, ry, rw, rh = rect_area
        rx = min(rx, iw - 1); ry = min(ry, ih - 1)
        rw = min(rw, iw - rx); rh = min(rh, ih - ry)
        if rw < 1 or rh < 1: return None
        img = img.subsurface(pygame.Rect(rx, ry, rw, rh))
    # Sample center pixels for average color (no numpy dependency)
    w2, h2 = img.get_width(), img.get_height()
    rs = gs = bs = 0; n = 0
    step = max(1, min(w2, h2) // 8)
    for yy in range(0, h2, step):
        for xx in range(0, w2, step):
            r, g, b, *_ = img.get_at((xx, yy))
            rs += r; gs += g; bs += b; n += 1
    if n == 0: return None
    return (rs // n, gs // n, bs // n)

LEON_JACKET = _avg_color(_leon_body_path, (20, 20, 100, 100)) or (82, 62, 42)
LEON_JACKET_DK = tuple(max(0, c - 20) for c in LEON_JACKET)
LEON_HAIR = _avg_color(_leon_hair_path, (40, 40, 80, 80)) or (95, 75, 45)
LEON_SKIN = _avg_color(_leon_face_path, (100, 120, 60, 40)) or (195, 165, 130)
LEON_HANDS = _avg_color(_leon_hand_path, (30, 30, 60, 60)) or (185, 155, 120)
LEON_BELT = (55, 40, 28)
LEON_PANTS = (58, 48, 35)

# Load Leon's face texture for portrait in briefcase
LEON_FACE_TEX = None
if os.path.exists(_leon_face_path):
    LEON_FACE_TEX = pygame.transform.scale(pygame.image.load(_leon_face_path).convert_alpha(), (80, 80))

# Load Leon jacket leather texture for shoulder rendering
LEON_JACKET_TEX = None
_jkt_tex_path = os.path.join(_LEON_TEX, 'pl0013.png')
if os.path.exists(_jkt_tex_path):
    LEON_JACKET_TEX = pygame.image.load(_jkt_tex_path).convert()
    LEON_JACKET_TEX = pygame.transform.scale(LEON_JACKET_TEX, (256, 256))

# Pre-baked film grain overlays (4 variations)
GRAIN_SURFACES = []
for _gi in range(4):
    _gs = pygame.Surface((W // 4, H // 4), pygame.SRCALPHA)
    for _pi in range(300):
        _gx = random.randint(0, W // 4 - 1)
        _gy = random.randint(0, H // 4 - 1)
        _gv = random.randint(0, 30)
        _gs.set_at((_gx, _gy), (_gv, _gv, _gv, 20))
    GRAIN_SURFACES.append(pygame.transform.scale(_gs, (W, H)))

def make_leon_sprite(weapon='handgun'):
    """Large over-the-shoulder Leon sprite (RE4 style)."""
    sw, sh = 520, 600
    s = pygame.Surface((sw, sh), pygame.SRCALPHA)
    # ═══ BODY/SHOULDER (textured leather jacket) ═══
    body_pts = [
        (155,90),(70,140),(20,220),(8,400),(20,550),(150,595),
        (370,595),(430,530),(445,380),(440,210),(405,135),(340,95),(250,78)
    ]
    if LEON_JACKET_TEX:
        _bs = pygame.Surface((sw, sh), pygame.SRCALPHA)
        pygame.draw.polygon(_bs, (255,255,255,255), body_pts)
        _tt = pygame.Surface((sw, sh))
        for _tx in range(0, sw, 256):
            for _ty in range(0, sh, 256):
                _tt.blit(LEON_JACKET_TEX, (_tx, _ty))
        _tt.fill((150,142,132), special_flags=pygame.BLEND_RGB_MULT)
        _bs.blit(_tt, (0,0), special_flags=pygame.BLEND_RGB_MULT)
        s.blit(_bs, (0,0))
    else:
        pygame.draw.polygon(s, LEON_JACKET, body_pts)
    # Jacket shading (3D depth)
    pygame.draw.line(s, (*LEON_JACKET_DK, 70), (220,100), (230,560), 2)
    pygame.draw.line(s, (*LEON_JACKET_DK, 60), (155,95), (220,90), 2)
    pygame.draw.line(s, (*LEON_JACKET_DK, 60), (340,100), (240,88), 2)
    for i in range(10):
        a = max(0, 50 - i * 5)
        pygame.draw.line(s, (0,0,0,a), (20+i*2, 220+i), (18+i*2, 540-i*2), 1)
    for i in range(6):
        c = tuple(min(255, v + 20 - i*3) for v in LEON_JACKET)
        pygame.draw.line(s, (*c, 40), (410-i*3, 150+i*5), (425-i*3, 360-i*8), 1)
    pygame.draw.polygon(s, (*LEON_JACKET_DK, 40), body_pts, 2)

    # ═══ FUR COLLAR (RE4 Leon signature) ═══
    for i in range(28):
        cx = 110 + i * 9; cy = 78 + random.randint(-5, 5)
        fw = random.randint(7, 14); fh = random.randint(14, 24)
        fc = random.choice([(148,132,102),(138,122,92),(158,142,112),(128,112,88)])
        pygame.draw.ellipse(s, fc, (cx, cy, fw, fh))
        for j in range(2):
            fx = cx + random.randint(0, fw)
            pygame.draw.line(s, tuple(max(0,c-18) for c in fc),
                             (fx, cy), (fx+random.randint(-3,3), cy+fh+3), 1)

    # ═══ NECK ═══
    pygame.draw.polygon(s, LEON_SKIN, [
        (195,42),(260,40),(270,62),(268,88),(190,90),(185,60)
    ])
    pygame.draw.line(s, tuple(max(0,c-28) for c in LEON_SKIN), (190,50), (265,48), 2)

    # ═══ HEAD (back/side view) ═══
    pygame.draw.ellipse(s, LEON_SKIN, (168,0,120,56))
    pygame.draw.ellipse(s, LEON_SKIN, (282,12,18,26))
    pygame.draw.ellipse(s, tuple(max(0,c-22) for c in LEON_SKIN), (284,15,13,20), 1)
    # Hair (swept back dirty blonde)
    hair_dk = tuple(max(0, c - 22) for c in LEON_HAIR)
    pygame.draw.ellipse(s, LEON_HAIR, (162,0,130,36))
    for i in range(20):
        xs = 170 + i * 6; xe = xs + random.randint(-5, 4)
        pygame.draw.line(s, hair_dk if i%3==0 else LEON_HAIR,
                         (xs, 0), (xe, 28+random.randint(-4,4)), 2 if i%2==0 else 1)
    pygame.draw.arc(s, tuple(min(255,c+28) for c in LEON_HAIR), (180,0,90,20), 0.3, 2.5, 2)

    # ═══ LEFT ARM (partially visible, resting at side) ═══
    larm = [(70,140),(42,190),(25,280),(22,360),(28,380),(40,300),(52,210),(78,152)]
    pygame.draw.polygon(s, LEON_JACKET_DK, larm)
    pygame.draw.polygon(s, tuple(max(0,c-10) for c in LEON_JACKET_DK), larm, 2)
    pygame.draw.circle(s, LEON_HANDS, (24, 370), 11)

    # ═══ RIGHT ARM + WEAPON (focal point — large & detailed) ═══
    if weapon == 'shotgun':
        rarm = [(405,135),(448,170),(485,220),(510,290),(504,302),(476,245),(442,195),(400,155)]
        pygame.draw.polygon(s, LEON_JACKET, rarm)
        pygame.draw.polygon(s, LEON_JACKET_DK, rarm, 2)
        pygame.draw.circle(s, LEON_HANDS, (508, 295), 15)
        pygame.draw.ellipse(s, LEON_HANDS, (470, 310, 30, 18))
        pygame.draw.rect(s, (50,46,40), (455,282,65,14))
        pygame.draw.rect(s, (45,40,35), (450,288,70,10))
        pygame.draw.rect(s, (55,50,42), (458,274,16,32))
        pygame.draw.rect(s, (42,38,32), (502,300,22,42))
        pygame.draw.line(s, (68,62,52), (450,284), (520,284), 1)
        pygame.draw.rect(s, (60,55,48), (448,280,5,8))
        pygame.draw.rect(s, (35,32,28), (486,290,18,8))
    elif weapon == 'rifle':
        rarm = [(405,132),(448,162),(488,208),(515,270),(508,282),(478,228),(445,185),(400,150)]
        pygame.draw.polygon(s, LEON_JACKET, rarm)
        pygame.draw.polygon(s, LEON_JACKET_DK, rarm, 2)
        pygame.draw.circle(s, LEON_HANDS, (512, 275), 15)
        pygame.draw.ellipse(s, LEON_HANDS, (475, 288, 28, 16))
        pygame.draw.rect(s, (52,48,42), (458,260,62,10))
        pygame.draw.rect(s, (48,44,38), (454,264,66,7))
        pygame.draw.rect(s, (40,42,48), (475,248,46,14))
        pygame.draw.circle(s, (55,58,68), (475, 255), 7)
        pygame.draw.circle(s, (45,48,58), (475, 255), 4)
        pygame.draw.circle(s, (55,58,68), (521, 255), 6)
        pygame.draw.rect(s, (42,38,32), (512,274,20,42))
        pygame.draw.line(s, (65,60,52), (454,262), (520,262), 1)
    elif weapon == 'tmp':
        rarm = [(405,138),(445,175),(478,228),(502,298),(496,310),(470,250),(440,200),(400,158)]
        pygame.draw.polygon(s, LEON_JACKET, rarm)
        pygame.draw.polygon(s, LEON_JACKET_DK, rarm, 2)
        pygame.draw.circle(s, LEON_HANDS, (500, 302), 14)
        pygame.draw.ellipse(s, LEON_HANDS, (465, 315, 26, 15))
        pygame.draw.rect(s, (55,55,58), (450,292,56,18))
        pygame.draw.rect(s, (60,58,55), (440,300,72,10))
        pygame.draw.rect(s, (45,42,40), (465,310,14,32))
        pygame.draw.rect(s, (42,40,38), (478,316,12,28))
        pygame.draw.line(s, (50,48,45), (506,295), (520,325), 3)
        pygame.draw.line(s, (72,68,62), (440,302), (512,302), 1)
        pygame.draw.rect(s, (48,45,42), (490,288,8,7))
    else:  # handgun (Silver Ghost)
        rarm = [(405,138),(445,178),(480,238),(506,308),(500,320),(472,262),(438,200),(400,158)]
        pygame.draw.polygon(s, LEON_JACKET, rarm)
        pygame.draw.polygon(s, LEON_JACKET_DK, rarm, 2)
        pygame.draw.circle(s, LEON_HANDS, (504, 312), 15)
        pygame.draw.ellipse(s, LEON_HANDS, (468, 322, 34, 20))
        pygame.draw.rect(s, (72,68,62), (470,302,42,20))
        pygame.draw.rect(s, (78,72,66), (462,310,56,12))
        pygame.draw.rect(s, (58,55,48), (476,322,26,34))
        pygame.draw.rect(s, (62,58,52), (458,312,10,10))
        for i in range(6):
            pygame.draw.line(s, (65,60,55), (474+i*6,302), (474+i*6,310), 1)
        pygame.draw.arc(s, (55,52,48), (474,322,18,16), 3.14, 6.28, 1)
        pygame.draw.line(s, (92,85,78), (470,302), (512,302), 1)
        pygame.draw.rect(s, (72,68,62), (460,308,5,5))
        pygame.draw.rect(s, (52,48,42), (476,354,26,6))

    # ═══ TACTICAL GEAR ═══
    pygame.draw.rect(s, (*LEON_BELT, 170), (85,520,330,14))
    pygame.draw.rect(s, (135,118,72,190), (245,517,22,19))
    pygame.draw.rect(s, (48,38,28,150), (155,498,10,36))
    pygame.draw.rect(s, (155,150,140,170), (153,494,14,10))
    pygame.draw.rect(s, (30,28,25,150), (100,142,16,22))
    pygame.draw.rect(s, (40,38,35,170), (103,139,10,7))
    pygame.draw.ellipse(s, (52,48,32,140), (375,518,16,14))
    pygame.draw.rect(s, (*LEON_PANTS, 120), (65,570,75,28))
    pygame.draw.rect(s, (*LEON_PANTS, 120), (310,570,75,28))

    return s

leon_surfs = {w: make_leon_sprite(w) for w in WPN_ORDER}

# ═══════════════════════════════════════════════════════════
# PRE-RENDERED GANADO (ENEMY) SPRITES — 3D-shaded human figures
# ═══════════════════════════════════════════════════════════
# Load burlap/fabric texture for Ganado clothing
GANADO_CLOTH_TEX = None
_ganado_cloth_path = os.path.join(_VIL_TEX, '94_c.jpeg')
if os.path.exists(_ganado_cloth_path):
    GANADO_CLOTH_TEX = pygame.image.load(_ganado_cloth_path).convert()
    GANADO_CLOTH_TEX = pygame.transform.scale(GANADO_CLOTH_TEX, (128, 128))

def _make_ganado_sprite(gt, is_chainsaw=False, is_tough=False):
    """Pre-render a single Ganado villager as a 128×256 Surface with 3D shading."""
    sw, sh = 128, 256
    s = pygame.Surface((sw, sh), pygame.SRCALPHA)
    sc = gt['shirt']; pc = gt['pants']; sk = gt['skin']; hc = gt['hair']
    if is_chainsaw:
        sc = (45, 38, 30); pc = (32, 30, 24); sk = (145, 115, 82); hc = (75, 58, 38)

    cx = sw // 2  # center-x = 64

    # ═══ LEGS ═══
    # Left leg
    ll_pts = [(cx-18, 155), (cx-6, 155), (cx-4, 245), (cx-20, 245)]
    pygame.draw.polygon(s, pc, ll_pts)
    # Right leg
    rl_pts = [(cx+6, 155), (cx+18, 155), (cx+20, 245), (cx+4, 245)]
    pygame.draw.polygon(s, pc, rl_pts)
    # Leg shading (cylinder)
    pygame.draw.line(s, tuple(max(0,c-25) for c in pc), (cx-12, 158), (cx-12, 242), 1)
    pygame.draw.line(s, tuple(min(255,c+15) for c in pc), (cx-8, 158), (cx-8, 242), 1)
    pygame.draw.line(s, tuple(max(0,c-25) for c in pc), (cx+12, 158), (cx+12, 242), 1)
    pygame.draw.line(s, tuple(min(255,c+15) for c in pc), (cx+8, 158), (cx+8, 242), 1)
    # Shoes
    pygame.draw.ellipse(s, (38,32,22), (cx-22, 240, 20, 12))
    pygame.draw.ellipse(s, (38,32,22), (cx+2, 240, 20, 12))

    # ═══ TORSO ═══
    torso_pts = [(cx-24, 70), (cx+24, 70), (cx+22, 158), (cx-22, 158)]
    # Apply cloth texture if available
    if GANADO_CLOTH_TEX:
        _mask = pygame.Surface((sw, sh), pygame.SRCALPHA)
        pygame.draw.polygon(_mask, (255,255,255,255), torso_pts)
        _tex = pygame.Surface((sw, sh))
        _tex.blit(GANADO_CLOTH_TEX, (0, 0))
        _tex.blit(GANADO_CLOTH_TEX, (0, 128))
        _tex.fill(sc, special_flags=pygame.BLEND_RGB_MULT)
        _mask.blit(_tex, (0,0), special_flags=pygame.BLEND_RGB_MULT)
        s.blit(_mask, (0,0))
    else:
        pygame.draw.polygon(s, sc, torso_pts)
    # 3D shading — left shadow strip, right highlight
    for i in range(8):
        a = max(0, 45 - i * 6)
        pygame.draw.line(s, (0,0,0,a), (cx-24+i, 72), (cx-22+i, 156), 1)
    for i in range(5):
        c = tuple(min(255, v + 18 - i*4) for v in sc)
        pygame.draw.line(s, (*c, 35), (cx+18-i, 74), (cx+16-i, 154), 1)
    # Shirt wrinkles
    pygame.draw.line(s, tuple(max(0,c-18) for c in sc), (cx-4, 80), (cx-8, 150), 1)
    pygame.draw.line(s, tuple(max(0,c-14) for c in sc), (cx+6, 85), (cx+3, 148), 1)
    pygame.draw.polygon(s, tuple(max(0,c-12) for c in sc), torso_pts, 1)

    # Suspenders
    if gt.get('susp') and not is_chainsaw:
        suc = (55, 45, 30)
        pygame.draw.line(s, suc, (cx-12, 70), (cx-10, 155), 2)
        pygame.draw.line(s, suc, (cx+12, 70), (cx+10, 155), 2)

    # Belt
    pygame.draw.rect(s, (48,38,25), (cx-24, 152, 48, 6))
    pygame.draw.rect(s, (95,82,55), (cx-4, 151, 8, 8))  # buckle

    # Blood stains
    if gt.get('blood'):
        pygame.draw.circle(s, (110, 18, 12), (cx-8, 105), 5)
        pygame.draw.circle(s, (100, 15, 10), (cx+10, 120), 4)
        pygame.draw.circle(s, (90, 12, 8), (cx-14, 130), 3)

    # ═══ ARMS ═══
    arm_c = sc; arm_dk = tuple(max(0,c-20) for c in sc)
    # Left arm
    la = [(cx-24, 72), (cx-38, 78), (cx-42, 140), (cx-38, 145), (cx-32, 140), (cx-28, 82)]
    pygame.draw.polygon(s, arm_c, la)
    pygame.draw.polygon(s, arm_dk, la, 1)
    pygame.draw.circle(s, sk, (cx-40, 142), 6)  # hand
    # Right arm
    ra = [(cx+24, 72), (cx+38, 78), (cx+42, 140), (cx+38, 145), (cx+32, 140), (cx+28, 82)]
    pygame.draw.polygon(s, arm_c, ra)
    pygame.draw.polygon(s, arm_dk, ra, 1)
    pygame.draw.circle(s, sk, (cx+40, 142), 6)  # hand

    # ═══ NECK ═══
    pygame.draw.rect(s, sk, (cx-8, 50, 16, 22))
    pygame.draw.line(s, tuple(max(0,c-25) for c in sk), (cx-4, 52), (cx-4, 68), 1)

    # ═══ HEAD ═══
    head_r = 22
    if is_chainsaw:
        # Burlap sack head
        sack_c = (125, 100, 62)
        pygame.draw.circle(s, sack_c, (cx, 28), head_r)
        pygame.draw.circle(s, tuple(max(0,c-15) for c in sack_c), (cx, 28), head_r, 2)
        # Eye holes (dark)
        pygame.draw.circle(s, (8,5,3), (cx-8, 26), 4)
        pygame.draw.circle(s, (8,5,3), (cx+8, 26), 4)
        # Glowing red pupils
        pygame.draw.circle(s, (180,20,10), (cx-8, 26), 2)
        pygame.draw.circle(s, (180,20,10), (cx+8, 26), 2)
        # Stitches
        for sx2 in range(cx-16, cx+16, 6):
            pygame.draw.line(s, (80,65,40), (sx2, 12), (sx2+2, 18), 1)
        # Mouth stitches
        for sx2 in range(cx-10, cx+10, 4):
            pygame.draw.line(s, (65,50,32), (sx2, 35), (sx2+2, 38), 1)
    else:
        # Normal head with 3D volume
        pygame.draw.circle(s, sk, (cx, 28), head_r)
        # 3D shading on face
        for i in range(6):
            a = max(0, 30 - i * 5)
            pygame.draw.arc(s, (0,0,0,a), (cx-head_r+i, 10, head_r*2-i*2, 36), 2.5, 4.0, 1)
        # Highlight on right cheek
        pygame.draw.circle(s, tuple(min(255,c+18) for c in sk), (cx+6, 24), 8)
        # Hair (top and sides)
        pygame.draw.ellipse(s, hc, (cx-head_r+2, 4, head_r*2-4, 22))
        for i in range(8):
            hx = cx - 16 + i * 4
            hl = 8 + (i % 3) * 3
            pygame.draw.line(s, tuple(max(0,c-12) for c in hc),
                             (hx, 6), (hx + (i%2)*2-1, 6+hl), 1)
        # Eyes
        pygame.draw.circle(s, (210, 205, 190), (cx-7, 26), 3)
        pygame.draw.circle(s, (210, 205, 190), (cx+7, 26), 3)
        pygame.draw.circle(s, (215, 35, 12), (cx-7, 26), 2)
        pygame.draw.circle(s, (215, 35, 12), (cx+7, 26), 2)
        pygame.draw.circle(s, (10, 5, 3), (cx-7, 26), 1)
        pygame.draw.circle(s, (10, 5, 3), (cx+7, 26), 1)
        # Mouth
        pygame.draw.line(s, tuple(max(0,c-35) for c in sk), (cx-6, 38), (cx+6, 38), 1)
        # Brow shadow
        pygame.draw.line(s, tuple(max(0,c-20) for c in sk), (cx-12, 21), (cx+12, 21), 1)
        # Bandage
        if gt.get('band'):
            pygame.draw.line(s, (185,175,155), (cx-head_r, 18), (cx+head_r, 18), 4)
            if gt.get('blood'):
                pygame.draw.circle(s, (145,30,22), (cx-6, 18), 3)

    # ═══ WEAPON / TOOL ═══
    if is_chainsaw:
        # Chainsaw held in right hand
        pygame.draw.line(s, (85, 65, 38), (cx+40, 142), (cx+60, 100), 4)
        pygame.draw.rect(s, (155, 152, 148), (cx+52, 86, 20, 14))  # blade
        pygame.draw.rect(s, (145, 42, 28), (cx+42, 96, 16, 12))    # motor
        pygame.draw.rect(s, (60, 52, 38), (cx+46, 108, 10, 12))    # handle
    elif is_tough:
        # Axe / pitchfork
        pygame.draw.line(s, (75, 58, 38), (cx+40, 142), (cx+52, 60), 3)
        pts = [(cx+46, 58), (cx+58, 50), (cx+62, 60), (cx+52, 64)]
        pygame.draw.polygon(s, (128, 125, 118), pts)

    return s

# Pre-render all Ganado variants (6 regular + chainsaw + tough)
GANADO_SPRITES = []
for _gt in GANADO_TYPES:
    GANADO_SPRITES.append(_make_ganado_sprite(_gt))
GANADO_CHAINSAW_SPRITE = _make_ganado_sprite(GANADO_TYPES[0], is_chainsaw=True)
GANADO_TOUGH_SPRITES = [_make_ganado_sprite(_gt, is_tough=True) for _gt in GANADO_TYPES]

# Shadow disc (pre-baked, used under all sprites)
_SHADOW_BASE = pygame.Surface((64, 24), pygame.SRCALPHA)
pygame.draw.ellipse(_SHADOW_BASE, (0, 0, 0, 70), (0, 0, 64, 24))

# ═══════════════════════════════════════════════════════════
# SCENERY SPRITES (barrels, crates, trees, torches, well)
# ═══════════════════════════════════════════════════════════
class SceneryObj:
    def __init__(self, x, y, kind):
        self.x = x; self.y = y; self.kind = kind
        self.anim = random.random() * 6.28

# Place scenery objects on the map (avoid walls)
def _gen_scenery():
    objs = []
    placements = [
        # (x, y, kind)   — hand-placed for village atmosphere
        (6.5, 10.5, 'barrel'), (7.5, 10.5, 'barrel'), (12.5, 5.5, 'tree'),
        (18.5, 10.5, 'crate'), (19.5, 10.5, 'crate'), (19.5, 11.5, 'crate'),
        (25.5, 12.5, 'torch'), (16.5, 16.5, 'torch'), (30.5, 16.5, 'torch'),
        (35.5, 10.5, 'tree'),  (38.5, 12.5, 'barrel'),
        (3.5, 18.5, 'tree'),   (3.5, 25.5, 'tree'),
        (13.5, 30.5, 'barrel'), (14.5, 30.5, 'crate'),
        (42.5, 28.5, 'tree'),  (50.5, 22.5, 'tree'),
        (10.5, 42.5, 'torch'), (24.5, 35.5, 'crate'),
        (36.5, 40.5, 'tree'),  (48.5, 38.5, 'barrel'),
        (55.5, 45.5, 'tree'),  (8.5, 50.5, 'tree'),
        (30.5, 50.5, 'barrel'), (32.5, 50.5, 'barrel'),
        (20.5, 22.5, 'torch'), (40.5, 25.5, 'torch'),
        (22.5, 18.5, 'well'),
        (46.5, 10.5, 'tree'),  (56.5, 20.5, 'tree'),
        (15.5, 38.5, 'crate'), (5.5, 15.5, 'barrel'),
        (28.5, 42.5, 'tree'),
    ]
    for x, y, kind in placements:
        if 0 < x < M_W and 0 < y < M_H and get_tile(x, y) == 0:
            objs.append(SceneryObj(x, y, kind))
    return objs

SCENERY = _gen_scenery()

# Merchant location
MERCHANT_POS = (27.5, 12.5)

# ═══════════════════════════════════════════════════════════
# BRIEFCASE / ATTACHE CASE (grid inventory like RE4)
# ═══════════════════════════════════════════════════════════
GRID_W, GRID_H = 10, 6
CELL = 48

class InvItem:
    def __init__(self, name, w, h, gx, gy, col, icon_char):
        self.name = name; self.w = w; self.h = h
        self.gx = gx; self.gy = gy; self.col = col; self.icon = icon_char
    def cells(self):
        return [(self.gx + dx, self.gy + dy) for dy in range(self.h) for dx in range(self.w)]

DEFAULT_ITEMS = [
    InvItem('Handgun',       3, 2, 0, 0, (140,140,135), 'P'),
    InvItem('Shotgun',       5, 2, 3, 0, (120,100,70),  'S'),
    InvItem('Ammo (HG)',     1, 1, 0, 2, (200,190,120), 'a'),
    InvItem('Ammo (HG)',     1, 1, 1, 2, (200,190,120), 'a'),
    InvItem('Ammo (SG)',     1, 1, 2, 2, (190,170,100), 's'),
    InvItem('Knife',         1, 3, 0, 3, (170,170,165), 'K'),
    InvItem('First Aid',     1, 2, 9, 0, (100,200,100), '+'),
    InvItem('Herb (Green)',  1, 1, 9, 2, (60,160,60),   'G'),
    InvItem('Grenade',       1, 1, 8, 0, (160,160,80),  '*'),
    InvItem('Grenade',       1, 1, 8, 1, (160,160,80),  '*'),
]

class Briefcase:
    def __init__(self):
        self.items = [InvItem(i.name,i.w,i.h,i.gx,i.gy,i.col,i.icon) for i in DEFAULT_ITEMS]
        self.dragging = None; self.drag_off = (0, 0)

    def occupied(self, skip=None):
        cells = set()
        for it in self.items:
            if it is skip: continue
            cells.update(it.cells())
        return cells

    def can_place(self, item, gx, gy):
        occ = self.occupied(skip=item)
        for dy in range(item.h):
            for dx in range(item.w):
                cx, cy = gx + dx, gy + dy
                if cx < 0 or cx >= GRID_W or cy < 0 or cy >= GRID_H: return False
                if (cx, cy) in occ: return False
        return True

    def add_item(self, name, w, h, col, icon):
        occ = self.occupied()
        for gy in range(GRID_H):
            for gx in range(GRID_W):
                ok = True
                for dy in range(h):
                    for dx in range(w):
                        if (gx+dx, gy+dy) in occ or gx+dx >= GRID_W or gy+dy >= GRID_H:
                            ok = False; break
                    if not ok: break
                if ok:
                    self.items.append(InvItem(name, w, h, gx, gy, col, icon))
                    return True
        return False

    def reset(self):
        self.items = [InvItem(i.name,i.w,i.h,i.gx,i.gy,i.col,i.icon) for i in DEFAULT_ITEMS]
        self.dragging = None

# ═══════════════════════════════════════════════════════════
# GAME OBJECTS
# ═══════════════════════════════════════════════════════════
class Player:
    def __init__(self):
        self.x = 20.5; self.y = 30.5
        self.ang = -math.pi / 2
        self.pitch = 0  # vertical look offset in pixels
        self.hp = 100; self.max_hp = 100
        self.ammo = {'hg': 15, 'sg': 6, 'rf': 5, 'tm': 30}
        self.max_ammo = {'hg': 15, 'sg': 6, 'rf': 5, 'tm': 30}
        self.weapon = 'handgun'
        self.grenades = 2
        self.kills = 0; self.wave = 1; self.ptas = 0
        self.inv = 0; self.scd = 0; self.flash = 0; self.bob = 0.0
        self.step_cd = 0.0
        self.sensitivity = 0.003

class Enemy:
    def __init__(self, x, y, tough=False, chainsaw=False):
        self.x = x; self.y = y; self.tough = tough; self.chainsaw = chainsaw
        if chainsaw:
            self.hp = 200; self.mhp = 200; self.spd = 1.8; self.dmg = 40
        elif tough:
            self.hp = 80; self.mhp = 80; self.spd = 1.5; self.dmg = 18
        else:
            self.hp = 35; self.mhp = 35; self.spd = 2.2 + random.random()*.8; self.dmg = 10
        self.acd = 0; self.fl = 0; self.stun = 0; self.alive = True
        self.saw_cd = 0.0  # chainsaw attack cooldown
        self.vis = random.randint(0, len(GANADO_TYPES)-1)
        self.seed = random.randint(0, 999999)

class Pickup:
    def __init__(self, x, y, tp='hp'):
        self.x = x; self.y = y; self.tp = tp
        self.bob = random.random() * 6.28; self.alive = True

class Particle:
    def __init__(self, x, y, z, vx, vy, vz, col, life):
        self.x=x;self.y=y;self.z=z;self.vx=vx;self.vy=vy;self.vz=vz
        self.col=col;self.life=life;self.ml=life

class Grenade:
    def __init__(self, x, y, ang):
        self.x = x; self.y = y; self.spd = 10
        self.vx = math.cos(ang) * self.spd; self.vy = math.sin(ang) * self.spd
        self.z = 0.3; self.vz = 4.5  # arc physics: initial height + upward velocity
        self.timer = 1.5; self.alive = True

# ═══════════════════════════════════════════════════════════
# GAME STATE
# ═══════════════════════════════════════════════════════════
class Game:
    def __init__(self):
        self.state = 'title'
        self.player = Player()
        self.enemies = []; self.pickups = []; self.particles = []
        self.grenades_active = []
        self.briefcase = Briefcase()
        self.e_left = 0; self.sp_t = 0; self.wave_t = 0; self.t = 0
        self.mouse_cap = False
        self.show_minimap = True
        self.show_merchant = False
        self.msg = ''; self.msg_t = 0
        self.rain = [(random.randint(0, W), random.randint(0, H), random.uniform(3, 7)) for _ in range(200)]
        self._init_pickups()

    def _init_pickups(self):
        for _ in range(20):
            for _ in range(30):
                px = random.uniform(3, M_W-3); py = random.uniform(3, M_H-3)
                if get_tile(px, py) == 0:
                    tp = random.choices(['hp','ammo','ptas','grenade'], weights=[4,4,2,1])[0]
                    self.pickups.append(Pickup(px, py, tp)); break

    def show_msg(self, text, dur=2.5):
        self.msg = text; self.msg_t = dur

    def reset(self):
        self.player = Player()
        self.enemies.clear(); self.pickups.clear(); self.particles.clear()
        self.grenades_active.clear()
        self.briefcase.reset()
        self.e_left = 5; self.sp_t = 0; self.wave_t = 0
        self.state = 'play'; self.mouse_cap = True
        self.show_merchant = False; self.msg = ''; self.msg_t = 0
        pygame.mouse.set_visible(False); pygame.event.set_grab(True)
        self._init_pickups()
        self.show_msg('Chapter 1 — The Village', 3)

    def spawn_enemy(self):
        p = self.player
        for _ in range(30):
            a = random.random() * math.pi * 2
            d = random.uniform(12, 25)
            ex = max(2, min(M_W-2, p.x + math.cos(a)*d))
            ey = max(2, min(M_H-2, p.y + math.sin(a)*d))
            if get_tile(ex, ey) == 0:
                chainsaw = random.random() < 0.02 + self.player.wave * 0.01
                tough = (not chainsaw) and random.random() < .1 + self.player.wave * .03
                self.enemies.append(Enemy(ex, ey, tough, chainsaw))
                return

G = Game()

# ═══════════════════════════════════════════════════════════
# RAYCASTING
# ═══════════════════════════════════════════════════════════
FOV = math.pi / 3
HALF_FOV = FOV / 2
NUM_RAYS = W // 2
RAY_STEP = FOV / NUM_RAYS
MAX_DEPTH = 30
SCALE = W / NUM_RAYS
PROJ_DIST = HW / math.tan(HALF_FOV)

def cast_rays(px, py, pa):
    results = []
    ray_ang = pa - HALF_FOV
    for _ in range(NUM_RAYS):
        sa = math.sin(ray_ang); ca = math.cos(ray_ang)
        if abs(ca) < 1e-6: ca = 1e-6
        if abs(sa) < 1e-6: sa = 1e-6

        dh = MAX_DEPTH; wh = 0; hx_hit = 0.0
        if sa > 0: yi = int(py) + 1; dy = 1
        else: yi = int(py) - 1e-4; dy = -1
        for _ in range(int(MAX_DEPTH)):
            d = (yi - py) / sa; hx = px + d * ca
            if 0 < hx < M_W and 0 < yi < M_H:
                t = get_tile(hx, yi)
                if t > 0 and t != 5: dh = d; wh = t; hx_hit = hx; break
            yi += dy
            if yi < 0 or yi >= M_H: break

        dv = MAX_DEPTH; wv = 0; vy_hit = 0.0
        if ca > 0: xi = int(px) + 1; dx = 1
        else: xi = int(px) - 1e-4; dx = -1
        for _ in range(int(MAX_DEPTH)):
            d = (xi - px) / ca; vy = py + d * sa
            if 0 < xi < M_W and 0 < vy < M_H:
                t = get_tile(xi, vy)
                if t > 0 and t != 5: dv = d; wv = t; vy_hit = vy; break
            xi += dx
            if xi < 0 or xi >= M_W: break

        if dh < dv: depth = dh; wt = wh; side = 0; h_pos = hx_hit
        else:       depth = dv; wt = wv; side = 1; h_pos = vy_hit
        depth *= math.cos(ray_ang - pa)
        depth = max(0.1, depth)
        results.append((depth, wt, side, h_pos))
        ray_ang += RAY_STEP
    return results

# ═══════════════════════════════════════════════════════════
# DRAWING
# ═══════════════════════════════════════════════════════════
def draw_world(p):
    horizon = int(HH + p.pitch)
    screen.fill(FOG_COLOR)
    # Sky — panoramic texture scrolling with player angle
    if SKY_TEX:
        sh = SKY_TEX.get_height()
        sw = SKY_TEX.get_width()
        off = int((p.ang % (2 * math.pi)) / (2 * math.pi) * sw)
        sky_y = horizon - sh
        screen.blit(SKY_TEX, (-off, sky_y))
        if off + W > sw:
            screen.blit(SKY_TEX, (sw - off, sky_y))
    else:
        # Gradient fallback with fog color blending
        for y in range(max(0, horizon)):
            f = y / max(1, horizon)
            r = int(FOG_COLOR[0] * 0.6 + 15 * f)
            g = int(FOG_COLOR[1] * 0.6 + 12 * f)
            b = int(FOG_COLOR[2] * 0.6 + 10 * f)
            pygame.draw.line(screen, (r, g, b), (0, y), (W, y))
    # Floor — textured with distance fog baked in
    screen.blit(FLOOR_SURF, (0, horizon))

    rays = cast_rays(p.x, p.y, p.ang)
    z_buf = []
    for i, (depth, wt, side, hit_pos) in enumerate(rays):
        wh = min(H * 1.5, PROJ_DIST / depth)
        y0 = horizon - wh / 2; x = int(i * SCALE)
        if wt == 0: wt = 1
        shade = max(0.08, 1 - depth / MAX_DEPTH)
        fog_f = min(0.88, (depth / MAX_DEPTH) ** 1.4)
        sw_int = int(SCALE) + 1
        h_int = max(1, int(wh))

        if wt in WALL_TEX:
            tex_shade = shade * (0.72 if side == 0 else 1.0)
            _, cols = WALL_TEX[wt]
            tx = int((hit_pos % 1.0) * TEX_SIZE) % TEX_SIZE
            scaled = pygame.transform.scale(cols[tx], (sw_int, h_int))
            # Combined shade + fog in one multiply pass
            fog_shade = tex_shade * (1 - fog_f)
            sv = int(255 * fog_shade)
            fr = int(FOG_COLOR[0] * fog_f + sv)
            fg = int(FOG_COLOR[1] * fog_f + sv)
            fb = int(FOG_COLOR[2] * fog_f + sv)
            scaled.fill((min(255,fr), min(255,fg), min(255,fb)), special_flags=pygame.BLEND_RGB_MULT)
            screen.blit(scaled, (x, int(y0)))
        else:
            base = WALL_DARK.get(wt, WALL_DARK[1]) if side == 0 else WALL_COLORS.get(wt, WALL_COLORS[1])
            # Blend wall color toward fog
            r = int(base[0]*shade*(1-fog_f) + FOG_COLOR[0]*fog_f)
            g = int(base[1]*shade*(1-fog_f) + FOG_COLOR[1]*fog_f)
            b = int(base[2]*shade*(1-fog_f) + FOG_COLOR[2]*fog_f)
            col = (min(255,r), min(255,g), min(255,b))
            pygame.draw.rect(screen, col, (x, int(y0), sw_int, h_int))
            if wh > 10 and fog_f < 0.5:
                hl = tuple(min(255, c+12) for c in col)
                dk = tuple(max(0, c-12) for c in col)
                pygame.draw.line(screen, hl, (x, int(y0)), (x+sw_int, int(y0)))
                pygame.draw.line(screen, dk, (x, int(y0+wh)), (x+sw_int, int(y0+wh)))
        z_buf.append(depth)

    # Rain effect — angled with subtle variety
    for idx in range(len(G.rain)):
        rx, ry, rs = G.rain[idx]
        ry += rs * 14
        if ry > H: ry = random.randint(-20, 0); rx = random.randint(0, W)
        G.rain[idx] = (rx, ry, rs)
        ra = max(30, int(35 + rs * 12))
        pygame.draw.line(screen, (100, 110, 125, min(255, ra)),
                         (int(rx), int(ry)), (int(rx)-2, int(ry+rs*4)), 1)

    return z_buf

def w2s(wx, wy, px, py, pa):
    dx = wx - px; dy = wy - py; dist = math.sqrt(dx*dx+dy*dy)
    if dist < 0.2: return None
    ang = math.atan2(dy, dx); diff = ang - pa
    while diff > math.pi: diff -= 2*math.pi
    while diff < -math.pi: diff += 2*math.pi
    if abs(diff) > HALF_FOV + 0.15: return None
    sx = HW + diff / HALF_FOV * HW
    proj = PROJ_DIST / dist
    ground_y = (HH + G.player.pitch) + proj * 0.5
    return (sx, ground_y, dist)

def draw_scenery(p, z_buf):
    """Draw barrels, crates, trees, torches, well as textured billboard sprites."""
    sorted_s = sorted(SCENERY, key=lambda s: -((s.x-p.x)**2+(s.y-p.y)**2))
    for obj in sorted_s:
        obj.anim += 0.02
        res = w2s(obj.x, obj.y, p.x, p.y, p.ang)
        if not res: continue
        sx, sy, dist = res
        if dist < 0.5 or dist > MAX_DEPTH: continue
        bi = int(sx / SCALE)
        if 0 <= bi < len(z_buf) and z_buf[bi] < dist: continue
        proj = PROJ_DIST / dist
        shade = max(0.15, 1 - dist / MAX_DEPTH)
        fog_f = min(0.85, (dist / MAX_DEPTH) ** 1.3)

        # Shadow disc under all scenery
        _shw = max(6, int(proj * 0.22)); _shh = max(3, int(_shw * 0.3))
        _sh = pygame.transform.smoothscale(_SHADOW_BASE, (_shw, _shh))
        screen.blit(_sh, (int(sx) - _shw//2, int(sy) - _shh//2))

        if obj.kind == 'barrel':
            bw = max(6, int(proj * 0.28)); bh = max(8, int(proj * 0.4))
            bx = int(sx - bw/2); by = int(sy - bh)
            if BARREL_TEX:
                bs = pygame.transform.smoothscale(BARREL_TEX, (bw, bh))
                sv = int(shade * 255)
                bs.fill((sv, sv, sv), special_flags=pygame.BLEND_RGB_MULT)
                screen.blit(bs, (bx, by))
            else:
                bc = tuple(int(c * shade) for c in (80, 55, 30))
                pygame.draw.ellipse(screen, bc, (bx, by, bw, bh))
            # Metal band highlights
            if bh > 12:
                for band_y in [by + bh//3, by + bh*2//3]:
                    bc2 = tuple(int(c*shade) for c in (55,55,48))
                    pygame.draw.line(screen, bc2, (bx+2, band_y), (bx+bw-2, band_y), 2)

        elif obj.kind == 'crate':
            bw = max(6, int(proj * 0.32)); bh = max(6, int(proj * 0.32))
            bx = int(sx - bw/2); by = int(sy - bh)
            if CRATE_TEX:
                cs = pygame.transform.smoothscale(CRATE_TEX, (bw, bh))
                sv = int(shade * 255)
                cs.fill((sv, sv, sv), special_flags=pygame.BLEND_RGB_MULT)
                screen.blit(cs, (bx, by))
            else:
                bc = tuple(int(c * shade) for c in (100, 80, 50))
                pygame.draw.rect(screen, bc, (bx, by, bw, bh))
            # Corner brackets and X mark
            if bw > 8:
                dk = tuple(int(c*shade*0.6) for c in (100, 80, 50))
                pygame.draw.rect(screen, dk, (bx, by, bw, bh), 2)
                pygame.draw.line(screen, dk, (bx+3, by+3), (bx+bw-3, by+bh-3), 1)
                pygame.draw.line(screen, dk, (bx+bw-3, by+3), (bx+3, by+bh-3), 1)

        elif obj.kind == 'tree':
            tw = max(5, int(proj * 0.12)); th = max(15, int(proj * 0.7))
            tx = int(sx - tw/2); ty = int(sy - th)
            # Trunk with bark texture
            if TREE_BARK_TEX and tw > 3:
                ts = pygame.transform.smoothscale(TREE_BARK_TEX, (tw, th))
                sv = int(shade * 255)
                ts.fill((sv, sv, sv), special_flags=pygame.BLEND_RGB_MULT)
                screen.blit(ts, (tx, ty))
            else:
                tc = tuple(int(c * shade) for c in (50, 32, 16))
                pygame.draw.rect(screen, tc, (tx, ty, tw, th))
            # Foliage canopy with texture
            cr = max(10, int(proj * 0.4))
            if FOLIAGE_TEX and cr > 6:
                fs = pygame.transform.smoothscale(FOLIAGE_TEX, (cr * 2, cr * 2))
                sv = int(shade * 220)
                fs.fill((sv, sv, sv), special_flags=pygame.BLEND_RGB_MULT)
                fs.set_colorkey((0, 0, 0))
                screen.blit(fs, (int(sx) - cr, ty - cr))
            else:
                fc = tuple(int(c * shade) for c in (28, 55, 22))
                pygame.draw.circle(screen, fc, (int(sx), ty - cr//3), cr)
                fc2 = tuple(int(c * shade) for c in (22, 45, 18))
                pygame.draw.circle(screen, fc2, (int(sx)-cr//3, ty), cr*2//3)
                pygame.draw.circle(screen, fc2, (int(sx)+cr//3, ty), cr*2//3)
            # Trunk base roots
            if tw > 5:
                rc = tuple(int(c * shade * 0.8) for c in (50, 32, 16))
                pygame.draw.line(screen, rc, (tx - 2, int(sy)), (tx + tw//3, int(sy) - th//8), 2)
                pygame.draw.line(screen, rc, (tx + tw + 2, int(sy)), (tx + tw*2//3, int(sy) - th//8), 2)

        elif obj.kind == 'torch':
            tw = max(3, int(proj * 0.05)); th = max(10, int(proj * 0.38))
            tx = int(sx - tw/2); ty = int(sy - th)
            pygame.draw.rect(screen, tuple(int(c*shade) for c in (55,40,22)), (tx, ty, tw, th))
            # Flame (animated, multi-layered)
            fr = max(3, int(proj * 0.1))
            flicker = int(math.sin(obj.anim * 8) * fr * 0.35)
            flicker2 = int(math.cos(obj.anim * 12) * fr * 0.15)
            # Outer flame
            pygame.draw.circle(screen, (200, 100, 15), (int(sx)+flicker2, ty - fr + flicker), fr)
            # Mid flame
            pygame.draw.circle(screen, (255, 160, 30), (int(sx), ty - fr + flicker), int(fr*0.7))
            # Inner hot core
            pygame.draw.circle(screen, (255, 220, 80), (int(sx), ty - fr + flicker), max(1,int(fr*0.35)))
            # Dynamic ground glow
            if dist < 10:
                gs = pygame.Surface((fr*10, fr*6), pygame.SRCALPHA)
                ga = int(45 * (1 - dist/10))
                pygame.draw.ellipse(gs, (255, 140, 35, ga), (0, 0, fr*10, fr*6))
                screen.blit(gs, (int(sx)-fr*5, ty-fr*2))

        elif obj.kind == 'well':
            bw = max(8, int(proj * 0.38)); bh = max(6, int(proj * 0.28))
            bx = int(sx - bw/2); by = int(sy - bh)
            bc = tuple(int(c * shade) for c in (65, 60, 50))
            # Stone rim
            pygame.draw.ellipse(screen, bc, (bx, by-bh//2, bw, bh//2))
            # Wall
            wc = tuple(int(c*shade*0.85) for c in (65, 60, 50))
            pygame.draw.rect(screen, wc, (bx+2, by-bh//4, bw-4, bh//2))
            # Dark water
            pygame.draw.ellipse(screen, (15, 18, 28), (bx+4, by-bh//2+2, bw-8, bh//3))
            # Subtle water highlight
            if bw > 12:
                pygame.draw.arc(screen, (30, 40, 55), (bx+bw//3, by-bh//2+4, bw//3, bh//6), 0, 3.14, 1)
            # Posts + roof beam
            pygame.draw.line(screen, bc, (bx+3, by-bh), (bx+3, by-bh//4), 2)
            pygame.draw.line(screen, bc, (bx+bw-3, by-bh), (bx+bw-3, by-bh//4), 2)
            rc = tuple(int(c*shade) for c in (85, 55, 28))
            pygame.draw.line(screen, rc, (bx-2, by-bh), (bx+bw+2, by-bh), 3)

def draw_merchant(p, z_buf):
    """Draw the merchant NPC — detailed hooded figure with glowing eyes."""
    mx, my = MERCHANT_POS
    res = w2s(mx, my, p.x, p.y, p.ang)
    if not res: return
    sx, sy, dist = res
    if dist < 0.5 or dist > MAX_DEPTH: return
    bi = int(sx / SCALE)
    if 0 <= bi < len(z_buf) and z_buf[bi] < dist: return
    proj = PROJ_DIST / dist; shade = max(0.2, 1 - dist/MAX_DEPTH)
    # Shadow disc under merchant
    _mshw = max(8, int(proj * 0.3)); _mshh = max(3, int(_mshw * 0.28))
    _msh = pygame.transform.smoothscale(_SHADOW_BASE, (_mshw, _mshh))
    screen.blit(_msh, (int(sx) - _mshw//2, int(sy) - _mshh//2))
    # Body (dark purple cloak with folds)
    bw = int(proj * 0.38); bh = int(proj * 0.65)
    bx = int(sx - bw/2); by = int(sy - bh)
    bc = tuple(int(c*shade) for c in (55, 25, 75))
    pygame.draw.rect(screen, bc, (bx, by, bw, bh))
    # Cloak folds
    fold_c = tuple(max(0,c-12) for c in bc)
    for fx in range(bx+bw//5, bx+bw, bw//4):
        pygame.draw.line(screen, fold_c, (fx, by+bh//6), (fx+2, by+bh), 1)
    pygame.draw.rect(screen, tuple(max(0,c-18) for c in bc), (bx, by, bw, bh), 2)
    # Cloak bottom hem
    hem_c = tuple(max(0,c-8) for c in bc)
    pygame.draw.line(screen, hem_c, (bx, by+bh-2), (bx+bw, by+bh-2), 2)
    # Hood (pointed)
    hr = max(4, int(proj * 0.15))
    hood_c = tuple(int(c*shade) for c in (48, 22, 62))
    hood_pts = [(int(sx)-hr, by+hr//3), (int(sx), by-hr), (int(sx)+hr, by+hr//3)]
    pygame.draw.polygon(screen, hood_c, hood_pts)
    pygame.draw.circle(screen, hood_c, (int(sx), by+hr//4), hr)
    # Glowing eyes (animated pulse)
    es = max(2, hr//3); eo = max(2, hr//2)
    glow = 0.7 + 0.3 * math.sin(G.t * 3)
    eye_c = (int(200*glow), int(170*glow), int(40*glow))
    pygame.draw.circle(screen, eye_c, (int(sx)-eo, by+hr//4), es)
    pygame.draw.circle(screen, eye_c, (int(sx)+eo, by+hr//4), es)
    pygame.draw.circle(screen, (255, 220, 80), (int(sx)-eo, by+hr//4), max(1, es//2))
    pygame.draw.circle(screen, (255, 220, 80), (int(sx)+eo, by+hr//4), max(1, es//2))
    # Backpack / carpet roll
    if bw > 10:
        bp_c = tuple(int(c*shade) for c in (90, 65, 35))
        pygame.draw.rect(screen, bp_c, (bx+bw-bw//5, by+bh//4, bw//5, bh//2))
    # "E" prompt
    if dist < 3:
        pt = font_sm.render('[E] Merchant', True, (200, 180, 120))
        screen.blit(pt, (int(sx) - pt.get_width()//2, by - hr - 25))

def draw_enemies(p, z_buf):
    sorted_e = sorted(G.enemies, key=lambda e: -((e.x-p.x)**2+(e.y-p.y)**2))
    for e in sorted_e:
        if not e.alive: continue
        res = w2s(e.x, e.y, p.x, p.y, p.ang)
        if not res: continue
        sx, sy, dist = res
        if dist < 0.5: continue
        proj_h = PROJ_DIST / dist
        bi = int(sx / SCALE)
        if 0 <= bi < len(z_buf) and z_buf[bi] < dist: continue
        shade = max(0.2, 1 - dist/MAX_DEPTH)
        sm = 1.3 if e.chainsaw else 1.0
        bw = max(4, int(proj_h * 0.3 * sm)); bh = max(6, int(proj_h * 0.55 * sm))
        bx = int(sx - bw/2); by = int(sy - bh)

        # Pick pre-rendered sprite
        vi = e.vis % len(GANADO_TYPES)
        if e.chainsaw:
            base_spr = GANADO_CHAINSAW_SPRITE
        elif e.tough:
            base_spr = GANADO_TOUGH_SPRITES[vi]
        else:
            base_spr = GANADO_SPRITES[vi]

        # Scale sprite to projection size
        if bw < 3 or bh < 5: continue
        spr = pygame.transform.smoothscale(base_spr, (bw, bh))

        # Apply shade + fog via tint
        sv = int(shade * 255)
        spr.fill((sv, sv, sv, 255), special_flags=pygame.BLEND_RGBA_MULT)
        # Flash white on hit
        if e.fl > 0:
            spr.fill((80, 30, 10, 0), special_flags=pygame.BLEND_RGB_ADD)

        # Shadow disc on ground
        shw = max(6, int(bw * 0.9)); shh = max(3, int(bw * 0.25))
        shadow = pygame.transform.smoothscale(_SHADOW_BASE, (shw, shh))
        screen.blit(shadow, (int(sx) - shw//2, int(sy) - shh//2))

        # Blit enemy sprite
        screen.blit(spr, (bx, by))

        # HP bar
        hr = max(3, int(proj_h * 0.12 * sm))
        bar_w = max(10, bw); bar_y2 = by - 8; pct = max(0, e.hp/e.mhp)
        hp_col = (200,50,30) if not e.chainsaw else (180,30,180)
        pygame.draw.rect(screen, (0,0,0), (int(sx)-bar_w//2, bar_y2, bar_w, 4))
        pygame.draw.rect(screen, hp_col, (int(sx)-bar_w//2, bar_y2, int(bar_w*pct), 4))

def draw_pickups(p, z_buf):
    for pk in G.pickups:
        if not pk.alive: continue
        res = w2s(pk.x, pk.y, p.x, p.y, p.ang)
        if not res: continue
        sx, sy, dist = res
        if dist < 0.5: continue
        bi = int(sx / SCALE)
        if 0 <= bi < len(z_buf) and z_buf[bi] < dist: continue
        bob = math.sin(pk.bob) * 8; proj = PROJ_DIST / dist
        r = max(2, int(proj * 0.12)); shade = max(0.3, 1 - dist/MAX_DEPTH)
        col_map = {'hp': (220,40,40), 'ammo': (200,190,120), 'ptas': (220,200,60), 'grenade': (100,120,80)}
        col = col_map.get(pk.tp, (200,190,120))
        c = tuple(int(v*shade) for v in col)
        pickup_y = int(sy - r * 3 + bob)
        pygame.draw.circle(screen, c, (int(sx), pickup_y), r)
        pygame.draw.circle(screen, tuple(min(255,v+40) for v in c), (int(sx), pickup_y), max(1,r//2))

def draw_grenades_active(p, z_buf):
    for gr in G.grenades_active:
        if not gr.alive: continue
        res = w2s(gr.x, gr.y, p.x, p.y, p.ang)
        if not res: continue
        sx, sy, dist = res
        if dist < 0.3: continue
        proj = PROJ_DIST / dist; r = max(2, int(proj * 0.08))
        # Visual z offset — grenade rises above ground
        z_off = int(gr.z * proj * 0.6)
        # Shadow on ground
        _gshw = max(4, r * 2); _gshh = max(2, r)
        _gsh = pygame.transform.smoothscale(_SHADOW_BASE, (_gshw, _gshh))
        screen.blit(_gsh, (int(sx) - _gshw//2, int(sy) - _gshh//2))
        # Grenade body at elevated position
        pygame.draw.circle(screen, (100, 110, 70), (int(sx), int(sy - r) - z_off), r)
        pygame.draw.circle(screen, (130, 140, 90), (int(sx)-1, int(sy - r) - z_off - 1), max(1, r//2))

def draw_particles(p, z_buf):
    for pt in G.particles:
        res = w2s(pt.x, pt.y, p.x, p.y, p.ang)
        if not res: continue
        sx, sy, dist = res
        if dist < 0.3: continue
        a = max(0, pt.life / pt.ml)
        s = max(1, int(PROJ_DIST / dist * 0.06 * a))
        col = tuple(max(0, int(c*a)) for c in pt.col)
        oy = int(sy - pt.z * PROJ_DIST / dist)
        pygame.draw.circle(screen, col, (int(sx), oy), s)

def draw_leon(p):
    surf = leon_surfs.get(p.weapon, leon_surfs['handgun'])
    keys = pygame.key.get_pressed()
    moving = keys[pygame.K_w] or keys[pygame.K_s] or keys[pygame.K_a] or keys[pygame.K_d]
    if moving:
        bob_y = int(math.sin(p.bob * 4) * 10)
        bob_x = int(math.cos(p.bob * 2) * 6)
    else:
        bob_y = int(math.sin(G.t * 1.5) * 3)
        bob_x = int(math.sin(G.t * 0.8) * 2)
    recoil = 0
    if p.flash > 0:
        recoil = int(-45 * (p.flash / 0.12))
    elif p.scd > 0:
        wpn_cd = WEAPONS[p.weapon]['cd']
        recoil = int(-22 * max(0, (p.scd / wpn_cd - 0.3) / 0.7))
    # Position: bottom-left, partially off-screen like RE4 over-the-shoulder
    lx = -40 + bob_x
    ly = H - surf.get_height() + 100 + bob_y + recoil
    screen.blit(surf, (lx, ly))

def draw_laser():
    # RE4-style laser sight — thin line from weapon to center + bright dot
    gx, gy = 420, H - 190
    steps = 35
    for i in range(steps):
        t = i / steps
        x = int(gx + (HW - gx) * t); y = int(gy + (HH - gy) * t)
        a = int(30 + 50 * t)
        pygame.draw.circle(screen, (255, 20, 10), (x, y), 1)
    # Bright laser dot at center (RE4 signature)
    pygame.draw.circle(screen, (255, 8, 5), (HW, HH), 4)
    pygame.draw.circle(screen, (255, 50, 25), (HW, HH), 2)
    pygame.draw.circle(screen, (255, 180, 120), (HW, HH), 1)

def draw_minimap(p):
    if not G.show_minimap: return
    mms = 3  # scale pixels per tile
    mmr = 18  # radius in tiles
    mmx, mmy = W - mmr*2*mms - 15, 45  # top-right corner
    ms = pygame.Surface((mmr*2*mms, mmr*2*mms), pygame.SRCALPHA)
    ms.fill((0, 0, 0, 140))
    cx, cy = mmr*mms, mmr*mms
    for dy in range(-mmr, mmr):
        for dx in range(-mmr, mmr):
            tx, ty = int(p.x) + dx, int(p.y) + dy
            if 0 <= tx < M_W and 0 <= ty < M_H:
                t = MAP[ty][tx]
                if t == 1: c = (100, 90, 70, 200)
                elif t == 2: c = (110, 85, 55, 200)
                elif t == 3: c = (90, 80, 55, 180)
                elif t == 4: c = (70, 45, 25, 200)
                elif t == 5: c = (50, 40, 30, 100)
                else: continue
                px2 = cx + dx * mms; py2 = cy + dy * mms
                pygame.draw.rect(ms, c, (px2, py2, mms, mms))
    # Enemies as red dots
    for e in G.enemies:
        if not e.alive: continue
        edx = (e.x - p.x) * mms; edy = (e.y - p.y) * mms
        if abs(edx) < mmr*mms and abs(edy) < mmr*mms:
            ec = (255, 50, 50) if not e.chainsaw else (220, 50, 200)
            pygame.draw.circle(ms, ec, (int(cx+edx), int(cy+edy)), 2)
    # Merchant
    mdx = (MERCHANT_POS[0] - p.x) * mms; mdy = (MERCHANT_POS[1] - p.y) * mms
    if abs(mdx) < mmr*mms and abs(mdy) < mmr*mms:
        pygame.draw.circle(ms, (200, 180, 50), (int(cx+mdx), int(cy+mdy)), 3)
    # Player arrow
    pygame.draw.circle(ms, (40, 200, 40), (cx, cy), 3)
    ax = cx + int(math.cos(p.ang)*8); ay = cy + int(math.sin(p.ang)*8)
    pygame.draw.line(ms, (40, 200, 40), (cx, cy), (ax, ay), 2)
    # Border
    pygame.draw.rect(ms, (90, 75, 55, 200), (0, 0, mmr*2*mms, mmr*2*mms), 1)
    screen.blit(ms, (mmx, mmy))

def draw_hud(p):
    # Cinematic black bars (thinner for more screen space)
    pygame.draw.rect(screen, (0,0,0), (0, 0, W, 28))
    pygame.draw.rect(screen, (0,0,0), (0, H-28, W, 28))

    # Minimal RE4-style HUD — no stats panel, just essential info
    # Chapter/PTAS shown subtly in top bar
    screen.blit(font_xs.render(f'CH.{p.wave}', True, (110,100,80)), (15, 8))
    screen.blit(font_xs.render(f'{str(p.ptas).zfill(6)} PTAS', True, (110,100,80)), (75, 8))

    # Weapon + ammo (bottom-right, compact RE4 style)
    wpn = WEAPONS[p.weapon]
    ak = wpn['ammo_key']
    wpn_panel = pygame.Surface((140, 52), pygame.SRCALPHA)
    wpn_panel.fill((0, 0, 0, 120)); screen.blit(wpn_panel, (W-155, H-82))
    pygame.draw.rect(screen, (100, 85, 60, 80), (W-155, H-82, 140, 52), 1)
    screen.blit(font_xs.render(wpn['name'].upper(), True, (140,130,110)), (W-148, H-80))
    ac = (200,190,150) if p.ammo[ak] > 3 else (255,80,60)
    screen.blit(font_med.render(f'{p.ammo[ak]}/{p.max_ammo[ak]}', True, ac), (W-148, H-68))
    if p.grenades > 0:
        screen.blit(font_xs.render(f'G:{p.grenades}', True, (140,140,90)), (W-65, H-80))

    # HP gauge (bottom-left, compact circular like RE4)
    cx, cy, rad = 55, H - 58, 22
    pygame.draw.circle(screen, (25, 22, 18), (cx, cy), rad + 2)
    pct = max(0, p.hp / p.max_hp); end_ang = math.pi * 2 * pct
    col = (42, 140, 42) if pct > 0.3 else (200, 50, 50)
    if pct > 0:
        points = [(cx, cy)]
        for i2 in range(int(end_ang / 0.1) + 1):
            a = -math.pi/2 + i2 * 0.1
            if a > -math.pi/2 + end_ang: a = -math.pi/2 + end_ang
            points.append((cx + int(rad*math.cos(a)), cy + int(rad*math.sin(a))))
        if len(points) > 2: pygame.draw.polygon(screen, col, points)
    pygame.draw.circle(screen, (35, 30, 24), (cx, cy), rad - 5)
    lt = font_xs.render('LIFE', True, (160, 150, 135))
    screen.blit(lt, (cx - lt.get_width()//2, cy - lt.get_height()//2))

    # Muzzle flash overlay
    if p.flash > 0:
        alpha = min(255, int(160 * p.flash / 0.12))
        fs = pygame.Surface((W, H), pygame.SRCALPHA)
        fs.fill((255, 200, 50, alpha)); screen.blit(fs, (0, 0))

    # Low hp red pulse
    if p.hp < 30:
        pulse = int(30 + 20 * math.sin(G.t * 6))
        rs = pygame.Surface((W, H), pygame.SRCALPHA)
        rs.fill((120, 0, 0, pulse)); screen.blit(rs, (0, 0))

    # Vignette + Color grading + Film grain (pre-baked)
    screen.blit(VIGNETTE, (0, 0))
    screen.blit(COLOR_GRADE, (0, 0))
    # Film grain (cycles through pre-baked variations)
    gi = int(G.t * 12) % len(GRAIN_SURFACES)
    screen.blit(GRAIN_SURFACES[gi], (0, 0))

    # Door / interaction prompt
    fx = p.x + math.cos(p.ang) * 1.5; fy = p.y + math.sin(p.ang) * 1.5
    ft = get_tile(fx, fy)
    if ft == 4:
        pt = font_sm.render('[E] Open door', True, (200, 180, 120))
        screen.blit(pt, (HW - pt.get_width()//2, HH + 60))
    elif ft == 5:
        pt = font_sm.render('[E] Close door', True, (200, 180, 120))
        screen.blit(pt, (HW - pt.get_width()//2, HH + 60))

    # On-screen messages
    if G.msg_t > 0:
        a_msg = min(255, int(255 * min(1, G.msg_t / 0.5)))
        mt = font_med.render(G.msg, True, (220, 200, 160))
        mt.set_alpha(a_msg)
        screen.blit(mt, (HW - mt.get_width()//2, 130))

# ═══════════════════════════════════════════════════════════
# MERCHANT SHOP
# ═══════════════════════════════════════════════════════════
SHOP_ITEMS = [
    ('Rifle',       3000, 'rifle',    'rf', 5),
    ('TMP',         2000, 'tmp',      'tm', 30),
    ('Ammo (HG)',   300,  'ammo_hg',  'hg', 15),
    ('Ammo (SG)',   500,  'ammo_sg',  'sg', 6),
    ('Ammo (RF)',   800,  'ammo_rf',  'rf', 5),
    ('Ammo (TMP)', 400,   'ammo_tm',  'tm', 30),
    ('First Aid',   1500, 'heal',     None, 0),
    ('Grenade',     2000, 'grenade',  None, 0),
]

def draw_merchant_shop():
    overlay = pygame.Surface((W, H), pygame.SRCALPHA)
    overlay.fill((5, 3, 2, 220)); screen.blit(overlay, (0, 0))
    # Title
    t = font_big.render("What are ya buyin'?", True, (200, 180, 140))
    screen.blit(t, (HW - t.get_width()//2, 50))
    # PTAS display
    pt = font_med.render(f'PTAS: {str(G.player.ptas).zfill(6)}', True, (200, 180, 120))
    screen.blit(pt, (HW - pt.get_width()//2, 115))
    # Items
    for i, (name, price, kind, ak, amt) in enumerate(SHOP_ITEMS):
        y = 160 + i * 50; x = HW - 200
        hover = False
        mx, my = pygame.mouse.get_pos()
        if x <= mx <= x+400 and y <= my <= y+42: hover = True
        bg = (50, 40, 28, 200) if hover else (30, 25, 18, 180)
        ss = pygame.Surface((400, 42), pygame.SRCALPHA); ss.fill(bg)
        screen.blit(ss, (x, y))
        can_afford = G.player.ptas >= price
        nc = (220, 210, 190) if can_afford else (120, 100, 80)
        pc = (200, 190, 120) if can_afford else (120, 100, 80)
        screen.blit(font_sm.render(name, True, nc), (x + 10, y + 12))
        pr = font_sm.render(f'{price} PTAS', True, pc)
        screen.blit(pr, (x + 390 - pr.get_width(), y + 12))
        pygame.draw.rect(screen, (90, 75, 50) if hover else (60, 50, 35), (x, y, 400, 42), 1)
    # Hint
    hint = font_xs.render('Click to buy  |  ESC to leave', True, (100, 90, 70))
    screen.blit(hint, (HW - hint.get_width()//2, 160 + len(SHOP_ITEMS) * 50 + 20))

def merchant_buy(idx):
    if idx < 0 or idx >= len(SHOP_ITEMS): return
    name, price, kind, ak, amt = SHOP_ITEMS[idx]
    p = G.player
    if p.ptas < price: return
    if kind.startswith('ammo_'):
        p.ammo[ak] = min(99, p.ammo[ak] + amt)
    elif kind == 'heal':
        p.hp = min(p.max_hp, p.hp + 50)
    elif kind == 'grenade':
        p.grenades += 1
    elif kind in ('rifle', 'tmp'):
        pass  # weapon already unlocked; gives a full mag
        p.ammo[ak] = min(99, p.ammo[ak] + amt)
    p.ptas -= price
    snd_merchant.play()
    G.show_msg(f'Bought {name}!', 1.5)

# ═══════════════════════════════════════════════════════════
# BRIEFCASE DRAWING (RE4 style grid)
# ═══════════════════════════════════════════════════════════
def draw_briefcase():
    overlay = pygame.Surface((W,H), pygame.SRCALPHA)
    overlay.fill((5,3,2,230)); screen.blit(overlay,(0,0))
    bc = G.briefcase
    gw = GRID_W * CELL + 20; gh = GRID_H * CELL + 70
    ox = HW - gw//2; oy = HH - gh//2
    pygame.draw.rect(screen, (50,38,25), (ox-6,oy-6,gw+12,gh+12))
    pygame.draw.rect(screen, (90,72,48), (ox-4,oy-4,gw+8,gh+8), 3)
    pygame.draw.rect(screen, (25,20,14), (ox,oy,gw,gh))
    t = font_med.render('ATTACHE CASE', True, (200,180,140))
    screen.blit(t, (ox + gw//2 - t.get_width()//2, oy + 8))
    ptas_t = font_sm.render(f'{str(G.player.ptas).zfill(6)} PTAS', True, (200,180,120))
    screen.blit(ptas_t, (ox + gw - ptas_t.get_width() - 10, oy + 12))
    grid_ox = ox + 10; grid_oy = oy + 50
    for gy in range(GRID_H):
        for gx in range(GRID_W):
            rx = grid_ox + gx*CELL; ry = grid_oy + gy*CELL
            pygame.draw.rect(screen, (35,30,22), (rx,ry,CELL-1,CELL-1))
            pygame.draw.rect(screen, (60,50,35), (rx,ry,CELL-1,CELL-1), 1)
    for item in bc.items:
        rx = grid_ox + item.gx*CELL; ry = grid_oy + item.gy*CELL
        rw = item.w*CELL - 2; rh = item.h*CELL - 2
        if bc.dragging is item:
            mx2, my2 = pygame.mouse.get_pos()
            rx = mx2 - bc.drag_off[0]; ry = my2 - bc.drag_off[1]
        pygame.draw.rect(screen, (item.col[0]//3, item.col[1]//3, item.col[2]//3), (rx+1,ry+1,rw,rh))
        pygame.draw.rect(screen, item.col, (rx+1,ry+1,rw,rh), 2)
        icon = font_med.render(item.icon, True, item.col)
        screen.blit(icon, (rx + rw//2 - icon.get_width()//2, ry + rh//2 - icon.get_height()//2))
        if rh > CELL:
            nm = font_xs.render(item.name, True, (180,170,150))
            screen.blit(nm, (rx+4, ry+rh-18))
    hint = font_xs.render('Click+Drag to move items  |  TAB/ESC to close', True, (100,90,70))
    screen.blit(hint, (ox + gw//2 - hint.get_width()//2, oy + gh - 18))
    # Leon portrait with actual face texture
    px2 = ox + gw + 20; py2 = oy + 20
    pygame.draw.rect(screen, (50,38,25), (px2,py2,120,160))
    pygame.draw.rect(screen, (90,72,48), (px2,py2,120,160), 2)
    if LEON_FACE_TEX:
        face_scaled = pygame.transform.scale(LEON_FACE_TEX, (100, 100))
        screen.blit(face_scaled, (px2 + 10, py2 + 10))
    else:
        pygame.draw.ellipse(screen, LEON_SKIN, (px2+25,py2+20,70,85))
        pygame.draw.ellipse(screen, LEON_HAIR, (px2+28,py2+10,64,50))
        pygame.draw.circle(screen, (60,80,110), (px2+45,py2+55), 5)
        pygame.draw.circle(screen, (60,80,110), (px2+75,py2+55), 5)
        pygame.draw.circle(screen, (20,20,20), (px2+45,py2+55), 2)
        pygame.draw.circle(screen, (20,20,20), (px2+75,py2+55), 2)
    nm2 = font_sm.render('Leon S.', True, (200,190,170))
    screen.blit(nm2, (px2+60-nm2.get_width()//2, py2+125))
    nm3 = font_xs.render('Kennedy', True, (160,150,130))
    screen.blit(nm3, (px2+60-nm3.get_width()//2, py2+142))

# ═══════════════════════════════════════════════════════════
# TITLE / DEATH SCREENS
# ═══════════════════════════════════════════════════════════
def draw_title():
    screen.fill((8, 6, 4))
    for r in range(250, 0, -3):
        a = max(0, int(20 * (1 - r/250)))
        pygame.draw.circle(screen, (50+a, 25+a//2, 10+a//3), (HW, HH-50), r)
    t1 = font_title.render('VILL', True, (196,176,138))
    t2 = font_title.render('4', True, (170,20,20))
    t3 = font_title.render('GE', True, (196,176,138))
    tw = t1.get_width()+t2.get_width()+t3.get_width()
    x = HW - tw//2; y = HH - 80
    screen.blit(t1, (x, y)); screen.blit(t2, (x+t1.get_width(), y))
    screen.blit(t3, (x+t1.get_width()+t2.get_width(), y))
    pulse = int(120 + 60 * math.sin(G.t * 2))
    sub = font_med.render('Click to Start', True, (200,186,160)); sub.set_alpha(pulse)
    screen.blit(sub, (HW - sub.get_width()//2, HH + 40))
    ctrl = font_xs.render(
        'WASD move | Mouse aim | Click shoot | 1-4 weapons | G grenade | R reload | Shift sprint | TAB briefcase | M minimap | E interact',
        True, (100, 90, 70))
    screen.blit(ctrl, (HW - ctrl.get_width()//2, HH + 85))

def draw_death():
    overlay = pygame.Surface((W,H), pygame.SRCALPHA)
    overlay.fill((50,0,0,200)); screen.blit(overlay,(0,0))
    t = font_big.render('YOU ARE DEAD', True, (230,218,200))
    screen.blit(t, (HW - t.get_width()//2, HH - 30))
    # Stats
    st = font_sm.render(f'Kills: {G.player.kills}  |  Chapters: {G.player.wave}  |  PTAS: {G.player.ptas}', True, (180,170,150))
    screen.blit(st, (HW - st.get_width()//2, HH + 10))
    s = font_med.render('Click to restart', True, (200,180,160))
    screen.blit(s, (HW - s.get_width()//2, HH + 45))

# ═══════════════════════════════════════════════════════════
# GAME LOGIC
# ═══════════════════════════════════════════════════════════
def update(dt):
    p = G.player; G.t += dt
    if G.msg_t > 0: G.msg_t -= dt
    if G.state != 'play': return

    keys = pygame.key.get_pressed()
    p.inv = max(0, p.inv - dt); p.scd = max(0, p.scd - dt); p.flash = max(0, p.flash - dt)
    p.step_cd = max(0, p.step_cd - dt)

    # Mouse look
    if G.mouse_cap:
        mx, my = pygame.mouse.get_rel()
        p.ang += mx * p.sensitivity
        p.pitch = max(-200, min(200, p.pitch - my * 0.5))

    # Movement
    spd = 7.0 if keys[pygame.K_LSHIFT] else 4.5
    mvx = mvy = 0
    if keys[pygame.K_w] or keys[pygame.K_UP]:
        mvx += math.cos(p.ang)*spd*dt; mvy += math.sin(p.ang)*spd*dt
    if keys[pygame.K_s] or keys[pygame.K_DOWN]:
        mvx -= math.cos(p.ang)*spd*dt; mvy -= math.sin(p.ang)*spd*dt
    if keys[pygame.K_a] or keys[pygame.K_LEFT]:
        mvx += math.cos(p.ang - math.pi/2)*spd*0.7*dt
        mvy += math.sin(p.ang - math.pi/2)*spd*0.7*dt
    if keys[pygame.K_d] or keys[pygame.K_RIGHT]:
        mvx += math.cos(p.ang + math.pi/2)*spd*0.7*dt
        mvy += math.sin(p.ang + math.pi/2)*spd*0.7*dt

    moving = (mvx != 0 or mvy != 0)
    if moving:
        p.bob += dt * 8
        if p.step_cd <= 0:
            snd_step.play(); p.step_cd = 0.35 if not keys[pygame.K_LSHIFT] else 0.25

    m = 0.25
    if not is_solid(p.x + mvx + m, p.y) and not is_solid(p.x + mvx - m, p.y): p.x += mvx
    if not is_solid(p.x, p.y + mvy + m) and not is_solid(p.x, p.y + mvy - m): p.y += mvy

    # Enemies
    for e in G.enemies:
        if not e.alive: continue
        e.fl = max(0, e.fl-dt); e.stun = max(0, e.stun-dt); e.acd = max(0, e.acd-dt)
        if e.stun > 0: continue
        dx = p.x-e.x; dy = p.y-e.y; d = math.sqrt(dx*dx+dy*dy)
        if d > 0.8:
            nx = e.x + (dx/d)*e.spd*dt; ny = e.y + (dy/d)*e.spd*dt
            if not is_solid(nx, e.y): e.x = nx
            if not is_solid(e.x, ny): e.y = ny
        elif e.acd <= 0:
            if p.inv <= 0:
                p.hp -= e.dmg; p.inv = 0.5; snd_hit.play()
                if e.chainsaw: snd_chainsaw.play()
                for _ in range(5):
                    G.particles.append(Particle(p.x,p.y,.3,(random.random()-.5)*2,
                        (random.random()-.5)*2,random.random()*2,(180,0,0),.5+random.random()*.5))
            e.acd = 1.2 if not e.chainsaw else 0.8
    G.enemies = [e for e in G.enemies if e.alive]

    # Grenades
    for gr in G.grenades_active:
        if not gr.alive: continue
        gr.timer -= dt
        nx = gr.x + gr.vx * dt; ny = gr.y + gr.vy * dt
        # Arc physics: gravity pulls z down; bounce off ground
        gr.vz -= 14.0 * dt  # gravity
        gr.z += gr.vz * dt
        if gr.z <= 0:
            gr.z = 0; gr.vz = abs(gr.vz) * 0.3  # bounce with damping
            if abs(gr.vz) < 0.5: gr.vz = 0
        if is_solid(nx, ny):
            gr.vx *= -0.5; gr.vy *= -0.5
        else:
            gr.x = nx; gr.y = ny
        gr.vx *= 0.96; gr.vy *= 0.96  # friction
        if gr.timer <= 0:
            gr.alive = False; snd_grenade.play()
            # Explosion particles
            for _ in range(25):
                G.particles.append(Particle(gr.x,gr.y,.3,(random.random()-.5)*6,
                    (random.random()-.5)*6,random.random()*5,(255,150,30),1+random.random()))
            for _ in range(15):
                G.particles.append(Particle(gr.x,gr.y,.2,(random.random()-.5)*3,
                    (random.random()-.5)*3,random.random()*2,(80,80,80),1.5+random.random()))
            # Damage enemies in radius
            for e in G.enemies:
                if not e.alive: continue
                d2 = (e.x-gr.x)**2 + (e.y-gr.y)**2
                if d2 < 16:  # radius 4
                    dmg = int(120 * (1 - math.sqrt(d2)/4))
                    e.hp -= dmg; e.fl = 0.3; e.stun = 0.8
                    if e.hp <= 0:
                        e.alive = False; p.kills += 1; p.ptas += 250
    G.grenades_active = [g for g in G.grenades_active if g.alive]

    # Pickups
    for pk in G.pickups:
        if not pk.alive: continue
        pk.bob += dt * 3
        if (p.x-pk.x)**2 + (p.y-pk.y)**2 < 0.8:
            snd_pick.play(); pk.alive = False
            if pk.tp == 'hp': p.hp = min(p.max_hp, p.hp + 25)
            elif pk.tp == 'ammo':
                ak = WEAPONS[p.weapon]['ammo_key']
                p.ammo[ak] = min(99, p.ammo[ak] + 8)
            elif pk.tp == 'ptas': p.ptas += random.randint(200, 500)
            elif pk.tp == 'grenade': p.grenades += 1
    G.pickups = [pk for pk in G.pickups if pk.alive]

    # Particles
    for pt in G.particles:
        pt.x += pt.vx*dt; pt.y += pt.vy*dt; pt.z += pt.vz*dt
        pt.vz -= 5*dt; pt.life -= dt
    G.particles = [pt for pt in G.particles if pt.life > 0]

    # Spawn
    G.sp_t -= dt
    if G.e_left > 0 and G.sp_t <= 0:
        G.spawn_enemy(); G.e_left -= 1; G.sp_t = 1.5 + random.random()
    if G.e_left <= 0 and len(G.enemies) == 0:
        G.wave_t += dt
        if G.wave_t > 2:
            p.wave += 1; G.e_left = 4 + p.wave * 2; G.sp_t = 1; G.wave_t = 0
            G.show_msg(f'Chapter {p.wave} — The Village', 3)
            for _ in range(4):
                for _ in range(20):
                    px2 = random.uniform(3,M_W-3); py2 = random.uniform(3,M_H-3)
                    if get_tile(px2,py2) == 0:
                        tp = random.choices(['hp','ammo','ptas','grenade'], weights=[4,4,2,1])[0]
                        G.pickups.append(Pickup(px2,py2,tp)); break

    if p.hp <= 0:
        G.state = 'dead'; G.mouse_cap = False
        pygame.mouse.set_visible(True); pygame.event.set_grab(False); snd_die.play()

def do_shoot():
    p = G.player; wpn = WEAPONS[p.weapon]
    ak = wpn['ammo_key']
    if p.ammo[ak] <= 0 or p.scd > 0: return
    p.ammo[ak] -= 1; p.scd = wpn['cd']; p.flash = 0.12
    WPN_SOUNDS[p.weapon].play()

    for _ in range(wpn['pellets']):
        aim_ang = p.ang + random.uniform(-wpn['spread'], wpn['spread'])
        best_d = 999; best_e = None
        for e in G.enemies:
            if not e.alive: continue
            dx = e.x - p.x; dy = e.y - p.y; dist = math.sqrt(dx*dx+dy*dy)
            if dist < 0.5 or dist > wpn['range']: continue
            ang = math.atan2(dy, dx); diff = ang - aim_ang
            while diff > math.pi: diff -= 2*math.pi
            while diff < -math.pi: diff += 2*math.pi
            tol = math.atan2(0.4, dist)
            if abs(diff) < tol + 0.03 and dist < best_d:
                blocked = False
                for s in range(int(dist*4)):
                    t = s / (dist*4)
                    if is_solid(p.x+dx*t, p.y+dy*t): blocked = True; break
                if not blocked: best_d = dist; best_e = e
        if best_e:
            # Headshot bonus (small random chance)
            bonus = 1.5 if random.random() < 0.15 else 1.0
            best_e.hp -= int(wpn['dmg'] * bonus)
            best_e.fl = 0.15; best_e.stun = 0.3; snd_hit.play()
            if bonus > 1:
                G.show_msg('HEADSHOT!', 0.8)
            for _ in range(8):
                G.particles.append(Particle(best_e.x,best_e.y,.3,(random.random()-.5)*3,
                    (random.random()-.5)*3,random.random()*3,(140,0,0),.7+random.random()*.5))
            if best_e.hp <= 0:
                best_e.alive = False; p.kills += 1
                p.ptas += 300 if best_e.chainsaw else (220 if best_e.tough else 180)
                snd_die.play()
                for _ in range(12):
                    G.particles.append(Particle(best_e.x,best_e.y,.2,(random.random()-.5)*2,
                        (random.random()-.5)*2,random.random()*1.5,(100,0,0),1.5+random.random()))
                drop = random.random()
                if drop < 0.3:
                    G.pickups.append(Pickup(best_e.x, best_e.y, 'hp'))
                elif drop < 0.55:
                    G.pickups.append(Pickup(best_e.x, best_e.y, 'ammo'))
                elif drop < 0.7:
                    G.pickups.append(Pickup(best_e.x, best_e.y, 'ptas'))

def interact_door():
    p = G.player
    fx = p.x + math.cos(p.ang)*1.5; fy = p.y + math.sin(p.ang)*1.5
    ix, iy = int(fx), int(fy)
    if 0 <= iy < M_H and 0 <= ix < M_W:
        t = MAP[iy][ix]
        if t == 4:
            MAP[iy][ix] = 5; snd_door.play()  # open
        elif t == 5:
            MAP[iy][ix] = 4; snd_door.play()  # close

def throw_grenade():
    p = G.player
    if p.grenades <= 0: return
    p.grenades -= 1
    G.grenades_active.append(Grenade(p.x, p.y, p.ang))
    G.show_msg('Grenade!', 0.8)

# ═══════════════════════════════════════════════════════════
# MAIN LOOP
# ═══════════════════════════════════════════════════════════
running = True
while running:
    dt = clock.tick(FPS) / 1000
    if dt > 0.05: dt = 0.05

    for ev in pygame.event.get():
        if ev.type == pygame.QUIT: running = False

        if ev.type == pygame.MOUSEBUTTONDOWN:
            if ev.button == 1:
                if G.state == 'title': G.reset()
                elif G.state == 'dead': G.reset()
                elif G.state == 'play': do_shoot()
                elif G.state == 'merchant':
                    mx2, my2 = ev.pos
                    for i in range(len(SHOP_ITEMS)):
                        y = 160 + i * 50; x = HW - 200
                        if x <= mx2 <= x+400 and y <= my2 <= y+42:
                            merchant_buy(i); break
                elif G.state == 'briefcase':
                    mx2, my2 = ev.pos; bc = G.briefcase
                    grid_ox = HW - (GRID_W*CELL+20)//2 + 10
                    grid_oy = HH - (GRID_H*CELL+70)//2 + 50
                    for item in bc.items:
                        rx = grid_ox + item.gx*CELL; ry = grid_oy + item.gy*CELL
                        rw = item.w*CELL; rh = item.h*CELL
                        if rx <= mx2 <= rx+rw and ry <= my2 <= ry+rh:
                            bc.dragging = item
                            bc.drag_off = (mx2-rx, my2-ry); break

            # Scroll wheel — weapon switch
            if ev.button == 4 and G.state == 'play':
                idx = WPN_ORDER.index(G.player.weapon)
                G.player.weapon = WPN_ORDER[(idx+1) % len(WPN_ORDER)]
                snd_wpn_switch.play()
            elif ev.button == 5 and G.state == 'play':
                idx = WPN_ORDER.index(G.player.weapon)
                G.player.weapon = WPN_ORDER[(idx-1) % len(WPN_ORDER)]
                snd_wpn_switch.play()

        if ev.type == pygame.MOUSEBUTTONUP and ev.button == 1:
            if G.state == 'briefcase' and G.briefcase.dragging:
                bc = G.briefcase; item = bc.dragging; mx2, my2 = ev.pos
                grid_ox = HW - (GRID_W*CELL+20)//2 + 10
                grid_oy = HH - (GRID_H*CELL+70)//2 + 50
                new_gx = round((mx2-bc.drag_off[0]-grid_ox)/CELL)
                new_gy = round((my2-bc.drag_off[1]-grid_oy)/CELL)
                if bc.can_place(item, new_gx, new_gy):
                    item.gx = new_gx; item.gy = new_gy
                bc.dragging = None

        if ev.type == pygame.KEYDOWN:
            if ev.key == pygame.K_TAB:
                if G.state == 'play':
                    G.state = 'briefcase'; G.mouse_cap = False
                    pygame.mouse.set_visible(True); pygame.event.set_grab(False)
                elif G.state == 'briefcase':
                    G.state = 'play'; G.mouse_cap = True
                    pygame.mouse.set_visible(False); pygame.event.set_grab(True)
            if ev.key == pygame.K_ESCAPE:
                if G.state in ('briefcase', 'merchant'):
                    G.state = 'play'; G.mouse_cap = True; G.show_merchant = False
                    pygame.mouse.set_visible(False); pygame.event.set_grab(True)
                elif G.state == 'play':
                    G.mouse_cap = not G.mouse_cap
                    pygame.mouse.set_visible(not G.mouse_cap)
                    pygame.event.set_grab(G.mouse_cap)
            if ev.key == pygame.K_r and G.state == 'play':
                wpn = WEAPONS[G.player.weapon]; ak = wpn['ammo_key']
                if G.player.ammo[ak] < G.player.max_ammo[ak]:
                    G.player.ammo[ak] = min(99, G.player.ammo[ak] + wpn['mag'])
                    snd_reload.play()
            if ev.key == pygame.K_e and G.state == 'play':
                # Check merchant proximity
                p = G.player
                md = math.sqrt((p.x-MERCHANT_POS[0])**2 + (p.y-MERCHANT_POS[1])**2)
                if md < 3:
                    G.state = 'merchant'; G.mouse_cap = False
                    pygame.mouse.set_visible(True); pygame.event.set_grab(False)
                    snd_merchant.play()
                else:
                    interact_door()
            if ev.key == pygame.K_g and G.state == 'play':
                throw_grenade()
            if ev.key == pygame.K_m and G.state == 'play':
                G.show_minimap = not G.show_minimap
            # Weapon switch 1-4
            if ev.key == pygame.K_1 and G.state == 'play':
                G.player.weapon = 'handgun'; snd_wpn_switch.play()
            if ev.key == pygame.K_2 and G.state == 'play':
                G.player.weapon = 'shotgun'; snd_wpn_switch.play()
            if ev.key == pygame.K_3 and G.state == 'play':
                G.player.weapon = 'rifle'; snd_wpn_switch.play()
            if ev.key == pygame.K_4 and G.state == 'play':
                G.player.weapon = 'tmp'; snd_wpn_switch.play()

    update(dt)

    if G.state == 'title':
        draw_title()
    elif G.state in ('play', 'dead', 'briefcase', 'merchant'):
        p = G.player
        z_buf = draw_world(p)
        draw_scenery(p, z_buf)
        draw_pickups(p, z_buf)
        draw_grenades_active(p, z_buf)
        draw_merchant(p, z_buf)
        draw_enemies(p, z_buf)
        draw_particles(p, z_buf)
        draw_leon(p)
        draw_laser()
        draw_hud(p)
        draw_minimap(p)
        if G.state == 'dead': draw_death()
        elif G.state == 'briefcase': draw_briefcase()
        elif G.state == 'merchant': draw_merchant_shop()

    pygame.display.flip()

pygame.quit()
sys.exit()
