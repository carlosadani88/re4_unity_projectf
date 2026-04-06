"""
VILL4GE — RE4 Over-the-shoulder 3D (Pygame raycaster + Leon sprite)
"""
import pygame, math, random, sys

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
# 0=ground  1=stone wall  2=wood wall  3=fence  4=door
# Much bigger map (60x60) with large open village center
# ═══════════════════════════════════════════════════════════
M_W, M_H = 60, 60
MAP = [[0]*M_W for _ in range(M_H)]

def rect(v, x1, y1, x2, y2):
    for y in range(y1, y2+1):
        for x in range(x1, x2+1):
            if 0 <= y < M_H and 0 <= x < M_W:
                MAP[y][x] = v

def box(v, x, y, w, h):
    """Hollow box (walls only)"""
    for i in range(w):
        MAP[y][x+i] = v
        MAP[y+h-1][x+i] = v
    for j in range(h):
        MAP[y+j][x] = v
        MAP[y+j][x+w-1] = v

# Border walls
rect(1, 0, 0, M_W-1, 0)      # top
rect(1, 0, M_H-1, M_W-1, M_H-1)  # bottom
rect(1, 0, 0, 0, M_H-1)      # left
rect(1, M_W-1, 0, M_W-1, M_H-1)  # right

# ── Buildings (hollow, with door openings) ──
# Building 1: large stone house (top-left)
box(1, 4, 4, 8, 6)
MAP[7][8] = 4  # door on right side

# Building 2: wooden cabin (top-right area)
box(2, 40, 5, 6, 5)
MAP[7][40] = 4  # door left

# Building 3: stone house center-left
box(1, 8, 20, 7, 6)
MAP[23][14] = 4  # door right

# Building 4: big barn center-right
box(2, 35, 18, 10, 8)
MAP[22][35] = 4  # door left
MAP[22][44] = 4  # door right

# Building 5: small shack (bottom-left)
box(2, 5, 40, 5, 4)
MAP[42][9] = 4

# Building 6: church/large building (top-center)
box(1, 22, 3, 10, 7)
MAP[9][27] = 4

# Building 7: house mid-bottom
box(1, 20, 38, 8, 6)
MAP[38][24] = 4  # door on top

# Building 8: shed (far right)
box(2, 50, 30, 5, 4)
MAP[32][50] = 4

# Building 9: ruined house bottom-right
box(1, 45, 45, 7, 6)
MAP[48][45] = 4

# ── Fences ──
# Village center fence partial
rect(3, 15, 14, 30, 14)  # horizontal top fence
rect(3, 15, 14, 15, 18)  # left piece
rect(3, 30, 14, 30, 18)  # right piece
MAP[16][15] = 0  # fence gap (entrance)
MAP[16][30] = 0  # fence gap

# More fences around
rect(3, 3, 32, 3, 38)
rect(3, 48, 12, 55, 12)

def get_tile(x, y):
    ix, iy = int(x), int(y)
    if 0 <= iy < M_H and 0 <= ix < M_W:
        return MAP[iy][ix]
    return 1

WALL_COLORS = {
    1: (85, 75, 58),   # stone
    2: (100, 78, 52),  # wood
    3: (110, 95, 65),  # fence
    4: (60, 38, 22),   # door
}
WALL_DARK = {k: (int(v[0]*.75), int(v[1]*.75), int(v[2]*.75)) for k, v in WALL_COLORS.items()}

FLOOR_BASE = (78, 65, 45)
CEIL_COL = (32, 28, 22)

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

snd_shoot  = mk_snd(160, 100, 0.25, 'saw')
snd_hit    = mk_snd(100, 120, 0.15, 'sine')
snd_die    = mk_snd(80, 400, 0.2, 'saw')
snd_pick   = mk_snd(600, 150, 0.1, 'sine')
snd_reload = mk_snd(300, 80, 0.1, 'square')

# ═══════════════════════════════════════════════════════════
# FONTS
# ═══════════════════════════════════════════════════════════
font_big   = pygame.font.SysFont('Georgia', 58, bold=True)
font_med   = pygame.font.SysFont('Georgia', 26)
font_sm    = pygame.font.SysFont('Consolas', 17)
font_xs    = pygame.font.SysFont('Consolas', 13)
font_title = pygame.font.SysFont('Georgia', 90, bold=True)

# ═══════════════════════════════════════════════════════════
# LEON SPRITE (drawn procedurally — back view over-the-shoulder)
# Pre-rendered to a surface so we don't redraw every frame
# ═══════════════════════════════════════════════════════════
def make_leon_sprite():
    """Draw Leon's back + right arm holding handgun, returns Surface."""
    s = pygame.Surface((320, 500), pygame.SRCALPHA)

    # Shadow/outline
    pygame.draw.ellipse(s, (0,0,0,60), (40, 30, 220, 400))

    # Jacket body (brown leather, wide shoulders)
    body_pts = [(90,120),(60,180),(55,350),(130,420),(190,420),(265,350),(260,180),(230,120)]
    pygame.draw.polygon(s, (95, 72, 48), body_pts)
    pygame.draw.polygon(s, (80, 60, 38), body_pts, 3)

    # Jacket seam lines
    pygame.draw.line(s, (75, 55, 35), (160, 130), (160, 400), 2)
    pygame.draw.line(s, (75, 55, 35), (100, 180), (100, 350), 1)
    pygame.draw.line(s, (75, 55, 35), (220, 180), (220, 350), 1)

    # Collar / fur trim
    pygame.draw.ellipse(s, (180, 165, 130), (95, 105, 130, 35))
    pygame.draw.ellipse(s, (160, 145, 110), (100, 108, 120, 28))

    # Neck
    pygame.draw.rect(s, (195, 155, 110), (140, 80, 40, 40))

    # Head (back of head, dirty blonde hair)
    pygame.draw.ellipse(s, (138, 112, 64), (115, 20, 90, 85))
    # Hair texture lines
    for i in range(8):
        x = 130 + i * 8
        pygame.draw.line(s, (120, 95, 50), (x, 30), (x + random.randint(-5,5), 80), 1)
    # Ear (right side visible slightly)
    pygame.draw.ellipse(s, (195, 155, 110), (198, 55, 18, 25))

    # Right arm (extended forward, holding gun)
    arm_pts = [(230, 180), (260, 170), (295, 140), (310, 120), (305, 110), (260, 155), (225, 170)]
    pygame.draw.polygon(s, (90, 68, 44), arm_pts)
    pygame.draw.polygon(s, (75, 55, 35), arm_pts, 2)

    # Hand
    pygame.draw.circle(s, (195, 155, 110), (308, 115), 10)

    # Gun (handgun pointing forward)
    pygame.draw.rect(s, (50, 50, 48), (300, 105, 18, 12))   # slide
    pygame.draw.rect(s, (60, 58, 55), (295, 108, 30, 8))    # barrel
    pygame.draw.rect(s, (45, 43, 40), (300, 115, 12, 18))   # grip

    # Left arm (slightly visible, forward)
    larm_pts = [(90, 180), (70, 170), (55, 155), (50, 140), (55, 135), (75, 160), (95, 175)]
    pygame.draw.polygon(s, (90, 68, 44), larm_pts)

    # Belt
    pygame.draw.rect(s, (60, 42, 28), (70, 360, 180, 10))
    # Belt buckle
    pygame.draw.rect(s, (160, 140, 90), (150, 358, 16, 14))

    return s

leon_surf = make_leon_sprite()

# ═══════════════════════════════════════════════════════════
# BRIEFCASE / ATTACHE CASE (grid inventory like RE4)
# ═══════════════════════════════════════════════════════════
GRID_W, GRID_H = 10, 6
CELL = 48

class InvItem:
    def __init__(self, name, w, h, gx, gy, col, icon_char):
        self.name = name
        self.w = w    # grid width
        self.h = h    # grid height
        self.gx = gx  # grid x position
        self.gy = gy   # grid y position
        self.col = col
        self.icon = icon_char

    def cells(self):
        return [(self.gx + dx, self.gy + dy) for dy in range(self.h) for dx in range(self.w)]

DEFAULT_ITEMS = [
    InvItem('Handgun',       3, 2, 0, 0, (140, 140, 135), 'P'),
    InvItem('Ammo (HG)',     1, 1, 4, 0, (200, 190, 120), 'a'),
    InvItem('Ammo (HG)',     1, 1, 5, 0, (200, 190, 120), 'a'),
    InvItem('Knife',         1, 3, 0, 2, (170, 170, 165), 'K'),
    InvItem('First Aid',     1, 2, 1, 2, (100, 200, 100), '+'),
    InvItem('Herb (Green)',  1, 1, 4, 1, (60, 160, 60),   'G'),
]

class Briefcase:
    def __init__(self):
        self.items = [InvItem(i.name, i.w, i.h, i.gx, i.gy, i.col, i.icon) for i in DEFAULT_ITEMS]
        self.dragging = None
        self.drag_off = (0, 0)

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
                if cx < 0 or cx >= GRID_W or cy < 0 or cy >= GRID_H:
                    return False
                if (cx, cy) in occ:
                    return False
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
        self.items = [InvItem(i.name, i.w, i.h, i.gx, i.gy, i.col, i.icon) for i in DEFAULT_ITEMS]
        self.dragging = None

# ═══════════════════════════════════════════════════════════
# GAME OBJECTS
# ═══════════════════════════════════════════════════════════
class Player:
    def __init__(self):
        self.x = 20.5; self.y = 30.5  # center of village
        self.ang = -math.pi / 2       # facing north
        self.hp = 100; self.ammo = 30; self.max_ammo = 30
        self.kills = 0; self.wave = 1; self.ptas = 0
        self.inv = 0; self.scd = 0; self.flash = 0; self.bob = 0.0

class Enemy:
    def __init__(self, x, y, tough=False):
        self.x = x; self.y = y; self.tough = tough
        self.hp = 80 if tough else 35; self.mhp = self.hp
        self.spd = 1.5 if tough else 2.2 + random.random() * .8
        self.dmg = 18 if tough else 10
        self.acd = 0; self.fl = 0; self.stun = 0; self.alive = True

class Pickup:
    def __init__(self, x, y, tp='hp'):
        self.x = x; self.y = y; self.tp = tp
        self.bob = random.random() * 6.28; self.alive = True

class Particle:
    def __init__(self, x, y, z, vx, vy, vz, col, life):
        self.x=x; self.y=y; self.z=z; self.vx=vx; self.vy=vy; self.vz=vz
        self.col=col; self.life=life; self.ml=life

# ═══════════════════════════════════════════════════════════
# GAME STATE
# ═══════════════════════════════════════════════════════════
class Game:
    def __init__(self):
        self.state = 'title'
        self.player = Player()
        self.enemies = []
        self.pickups = []
        self.particles = []
        self.briefcase = Briefcase()
        self.e_left = 0; self.sp_t = 0; self.wave_t = 0; self.t = 0
        self.mouse_cap = False
        self._init_pickups()

    def _init_pickups(self):
        for _ in range(15):
            for _ in range(30):
                px = random.uniform(3, M_W-3)
                py = random.uniform(3, M_H-3)
                if get_tile(px, py) == 0:
                    self.pickups.append(Pickup(px, py, random.choice(['hp','ammo'])))
                    break

    def reset(self):
        self.player = Player()
        self.enemies.clear(); self.pickups.clear(); self.particles.clear()
        self.briefcase.reset()
        self.e_left = 5; self.sp_t = 0; self.wave_t = 0
        self.state = 'play'; self.mouse_cap = True
        pygame.mouse.set_visible(False); pygame.event.set_grab(True)
        self._init_pickups()

    def spawn_enemy(self):
        p = self.player
        for _ in range(30):
            a = random.random() * math.pi * 2
            d = random.uniform(12, 25)
            ex = p.x + math.cos(a) * d
            ey = p.y + math.sin(a) * d
            ex = max(2, min(M_W-2, ex))
            ey = max(2, min(M_H-2, ey))
            if get_tile(ex, ey) == 0:
                tough = random.random() < .1 + self.player.wave * .03
                self.enemies.append(Enemy(ex, ey, tough))
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
        sa = math.sin(ray_ang)
        ca = math.cos(ray_ang)
        if abs(ca) < 1e-6: ca = 1e-6
        if abs(sa) < 1e-6: sa = 1e-6

        # Horizontal intersections
        dh = MAX_DEPTH; wh = 0
        if sa > 0: yi = int(py) + 1; dy = 1
        else: yi = int(py) - 1e-4; dy = -1
        for _ in range(int(MAX_DEPTH)):
            d = (yi - py) / sa
            hx = px + d * ca
            if 0 < hx < M_W and 0 < yi < M_H:
                t = get_tile(hx, yi)
                if t > 0: dh = d; wh = t; break
            yi += dy
            if yi < 0 or yi >= M_H: break

        # Vertical intersections
        dv = MAX_DEPTH; wv = 0
        if ca > 0: xi = int(px) + 1; dx = 1
        else: xi = int(px) - 1e-4; dx = -1
        for _ in range(int(MAX_DEPTH)):
            d = (xi - px) / ca
            vy = py + d * sa
            if 0 < xi < M_W and 0 < vy < M_H:
                t = get_tile(xi, vy)
                if t > 0: dv = d; wv = t; break
            xi += dx
            if xi < 0 or xi >= M_W: break

        if dh < dv:
            depth = dh; wt = wh; side = 0
        else:
            depth = dv; wt = wv; side = 1

        depth *= math.cos(ray_ang - pa)
        depth = max(0.1, depth)
        results.append((depth, wt, side))
        ray_ang += RAY_STEP
    return results

# ═══════════════════════════════════════════════════════════
# DRAWING
# ═══════════════════════════════════════════════════════════
def draw_world(p):
    # Sky gradient (dark overcast RE4 style)
    for y in range(HH):
        f = y / HH
        r = int(32 + 15 * f)
        g = int(28 + 12 * f)
        b = int(22 + 10 * f)
        pygame.draw.line(screen, (r, g, b), (0, y), (W, y))

    # Floor gradient
    for y in range(HH, H):
        f = (y - HH) / HH
        shade = min(1.0, f * 1.8)
        r = int(FLOOR_BASE[0] * shade)
        g = int(FLOOR_BASE[1] * shade)
        b = int(FLOOR_BASE[2] * shade)
        pygame.draw.line(screen, (r, g, b), (0, y), (W, y))

    # Walls
    rays = cast_rays(p.x, p.y, p.ang)
    z_buf = []
    for i, (depth, wt, side) in enumerate(rays):
        wh = min(H * 1.5, PROJ_DIST / depth)
        y0 = HH - wh / 2
        x = int(i * SCALE)
        if wt == 0: wt = 1
        base = WALL_DARK[wt] if side == 0 else WALL_COLORS[wt]
        shade = max(0.12, 1 - depth / MAX_DEPTH)
        col = (int(base[0]*shade), int(base[1]*shade), int(base[2]*shade))

        # Draw wall stripe with slight texture variation
        pygame.draw.rect(screen, col, (x, int(y0), int(SCALE)+1, int(wh)))

        # Top/bottom edge highlight for depth
        if wh > 10:
            highlight = tuple(min(255, c + 15) for c in col)
            pygame.draw.line(screen, highlight, (x, int(y0)), (x + int(SCALE), int(y0)))
            darkline = tuple(max(0, c - 15) for c in col)
            pygame.draw.line(screen, darkline, (x, int(y0 + wh)), (x + int(SCALE), int(y0 + wh)))

        z_buf.append(depth)
    return z_buf

def w2s(wx, wy, px, py, pa):
    """World to screen projection. Returns (sx, sy, dist) or None."""
    dx = wx - px; dy = wy - py
    dist = math.sqrt(dx*dx + dy*dy)
    if dist < 0.2: return None
    ang = math.atan2(dy, dx)
    diff = ang - pa
    while diff > math.pi: diff -= 2*math.pi
    while diff < -math.pi: diff += 2*math.pi
    if abs(diff) > HALF_FOV + 0.15: return None
    sx = HW + diff / HALF_FOV * HW
    return (sx, HH, dist)

def draw_enemies(p, z_buf):
    sorted_e = sorted(G.enemies, key=lambda e: -((e.x-p.x)**2+(e.y-p.y)**2))
    for e in sorted_e:
        if not e.alive: continue
        res = w2s(e.x, e.y, p.x, p.y, p.ang)
        if not res: continue
        sx, sy, dist = res
        if dist < 0.5: continue

        proj_h = PROJ_DIST / dist
        # Body
        bw = int(proj_h * 0.5)
        bh = int(proj_h * 0.85)
        bx = int(sx - bw/2)
        by = int(sy - bh/2)

        # Z-buffer check (center column)
        bi = int(sx / SCALE)
        if 0 <= bi < len(z_buf) and z_buf[bi] < dist: continue

        shade = max(0.2, 1 - dist / MAX_DEPTH)
        bcol = (60,45,28) if e.tough else (100,82,60)
        if e.fl > 0: bcol = (255, 80, 80)
        bc = tuple(int(c * shade) for c in bcol)

        # Body rect
        pygame.draw.rect(screen, bc, (bx, by, bw, bh))
        pygame.draw.rect(screen, tuple(max(0,c-20) for c in bc), (bx, by, bw, bh), 2)

        # Head
        hr = max(3, int(proj_h * 0.18))
        hx, hy = int(sx), by - hr
        hcol = tuple(int(c * shade) for c in (170, 145, 110))
        if e.fl > 0: hcol = (255, 130, 100)
        pygame.draw.circle(screen, hcol, (hx, hy), hr)

        # Red Plaga eyes
        es = max(1, hr // 4)
        eo = max(1, hr // 3)
        pygame.draw.circle(screen, (255, 20, 10), (hx - eo, hy), es)
        pygame.draw.circle(screen, (255, 20, 10), (hx + eo, hy), es)

        # Arms (simple lines)
        pygame.draw.line(screen, bc, (bx, by + bh//4), (bx - bw//3, by + bh//2), max(2, bw//8))
        pygame.draw.line(screen, bc, (bx+bw, by + bh//4), (bx+bw + bw//3, by + bh//2), max(2, bw//8))

        # Tough enemy has weapon
        if e.tough and bw > 6:
            pygame.draw.line(screen, (80,70,50), (bx+bw+bw//3, by+bh//2), (bx+bw+bw//2, by), 3)

        # HP bar
        bar_w = max(10, bw)
        bar_y = hy - hr - 6
        pct = max(0, e.hp / e.mhp)
        pygame.draw.rect(screen, (0,0,0), (int(sx)-bar_w//2, bar_y, bar_w, 4))
        pygame.draw.rect(screen, (180,30,30), (int(sx)-bar_w//2, bar_y, int(bar_w*pct), 4))

def draw_pickups(p, z_buf):
    for pk in G.pickups:
        if not pk.alive: continue
        res = w2s(pk.x, pk.y, p.x, p.y, p.ang)
        if not res: continue
        sx, sy, dist = res
        if dist < 0.5: continue
        bi = int(sx / SCALE)
        if 0 <= bi < len(z_buf) and z_buf[bi] < dist: continue
        bob = math.sin(pk.bob) * 8
        proj = PROJ_DIST / dist
        r = max(2, int(proj * 0.12))
        col = (220, 40, 40) if pk.tp == 'hp' else (200, 190, 120)
        shade = max(0.3, 1 - dist / MAX_DEPTH)
        c = tuple(int(v*shade) for v in col)
        pygame.draw.circle(screen, c, (int(sx), int(sy + bob)), r)
        # Glow
        pygame.draw.circle(screen, tuple(min(255,v+40) for v in c), (int(sx), int(sy + bob)), max(1,r//2))

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
    """Draw Leon over-the-shoulder on the left side."""
    # Position: lower-left, slightly left of center
    bob_y = int(math.sin(p.bob * 4) * 3)
    lx = 30
    ly = H - leon_surf.get_height() + 60 + bob_y
    screen.blit(leon_surf, (lx, ly))

def draw_laser():
    """Draw laser sight from gun to center crosshair."""
    # Laser line from gun tip area to crosshair
    gx, gy = 355, H - 390  # approximate gun barrel position
    for i in range(0, 200, 8):
        t = i / 200
        x = int(gx + (HW - gx) * t)
        y = int(gy + (HH - gy) * t)
        pygame.draw.circle(screen, (255, 20, 10), (x, y), 1)

    # Crosshair dot
    pygame.draw.circle(screen, (255, 30, 10), (HW, HH), 3)
    pygame.draw.circle(screen, (255, 100, 50), (HW, HH), 1)

def draw_hud(p):
    # Cinematic black bars
    pygame.draw.rect(screen, (0,0,0), (0, 0, W, 35))
    pygame.draw.rect(screen, (0,0,0), (0, H-35, W, 35))

    # Stats (top-left)
    panel = pygame.Surface((190, 75), pygame.SRCALPHA)
    panel.fill((0, 0, 0, 150))
    screen.blit(panel, (15, 42))
    pygame.draw.rect(screen, (130, 110, 80, 100), (15, 42, 190, 75), 1)
    screen.blit(font_sm.render(f'Kills: {p.kills}', True, (220,210,190)), (22, 48))
    screen.blit(font_sm.render(f'Chapter: {p.wave}', True, (180,170,150)), (22, 68))
    screen.blit(font_sm.render(f'PTAS: {str(p.ptas).zfill(6)}', True, (200,180,120)), (22, 88))

    # Weapon panel (bottom-right, above cinematic bar)
    wpn_panel = pygame.Surface((150, 55), pygame.SRCALPHA)
    wpn_panel.fill((0, 0, 0, 150))
    screen.blit(wpn_panel, (W-170, H-100))
    pygame.draw.rect(screen, (130, 110, 80, 100), (W-170, H-100, 150, 55), 1)
    screen.blit(font_xs.render('HANDGUN', True, (160,150,130)), (W-162, H-96))
    ac = (200,190,150) if p.ammo > 5 else (255,80,60)
    screen.blit(font_med.render(f'{p.ammo}/{p.max_ammo}', True, ac), (W-162, H-80))

    # HP bar (bottom-left, as a circular gauge like RE4)
    cx, cy, rad = 80, H - 75, 28
    # Background circle
    pygame.draw.circle(screen, (0,0,0,180), (cx, cy), rad + 4)
    pygame.draw.circle(screen, (30, 28, 22), (cx, cy), rad + 2)
    # HP arc
    pct = max(0, p.hp / 100)
    end_ang = math.pi * 2 * pct
    col = (42, 140, 42) if pct > 0.3 else (200, 50, 50)
    if pct > 0:
        points = [(cx, cy)]
        for i in range(int(end_ang / 0.1) + 1):
            a = -math.pi/2 + i * 0.1
            if a > -math.pi/2 + end_ang: a = -math.pi/2 + end_ang
            points.append((cx + int(rad * math.cos(a)), cy + int(rad * math.sin(a))))
        if len(points) > 2:
            pygame.draw.polygon(screen, col, points)
    pygame.draw.circle(screen, (40, 35, 28), (cx, cy), rad - 6)
    # "Leon" text inside
    lt = font_xs.render('Leon', True, (180, 170, 150))
    screen.blit(lt, (cx - lt.get_width()//2, cy - lt.get_height()//2))

    # Muzzle flash overlay
    if p.flash > 0:
        alpha = min(255, int(180 * p.flash / 0.1))
        fs = pygame.Surface((W, H), pygame.SRCALPHA)
        fs.fill((255, 200, 50, alpha))
        screen.blit(fs, (0, 0))

    # Low hp red
    if p.hp < 30:
        pulse = int(30 + 20 * math.sin(G.t * 6))
        rs = pygame.Surface((W, H), pygame.SRCALPHA)
        rs.fill((120, 0, 0, pulse))
        screen.blit(rs, (0, 0))

    # Subtle vignette
    vig = pygame.Surface((W, H), pygame.SRCALPHA)
    for r in range(8):
        a = int(20 * (r / 8))
        rd = int(W * 0.65 - r * W * 0.04)
        pygame.draw.circle(vig, (0, 0, 0, a), (HW, HH), rd, int(W*0.04))
    screen.blit(vig, (0, 0))

# ═══════════════════════════════════════════════════════════
# BRIEFCASE DRAWING (RE4 style grid)
# ═══════════════════════════════════════════════════════════
def draw_briefcase():
    # Dark overlay
    overlay = pygame.Surface((W, H), pygame.SRCALPHA)
    overlay.fill((5, 3, 2, 230))
    screen.blit(overlay, (0, 0))

    bc = G.briefcase
    # Case frame
    gw = GRID_W * CELL + 20
    gh = GRID_H * CELL + 70
    ox = HW - gw // 2
    oy = HH - gh // 2

    # Outer frame (leather look)
    pygame.draw.rect(screen, (50, 38, 25), (ox-6, oy-6, gw+12, gh+12))
    pygame.draw.rect(screen, (90, 72, 48), (ox-4, oy-4, gw+8, gh+8), 3)
    pygame.draw.rect(screen, (25, 20, 14), (ox, oy, gw, gh))

    # Title
    t = font_med.render('ATTACHE CASE', True, (200, 180, 140))
    screen.blit(t, (ox + gw//2 - t.get_width()//2, oy + 8))

    # PTAS display (top-right corner like RE4)
    ptas_t = font_sm.render(f'{str(G.player.ptas).zfill(6)} PTAS', True, (200, 180, 120))
    screen.blit(ptas_t, (ox + gw - ptas_t.get_width() - 10, oy + 12))

    # Grid
    grid_ox = ox + 10
    grid_oy = oy + 50

    # Grid background
    for gy in range(GRID_H):
        for gx in range(GRID_W):
            rx = grid_ox + gx * CELL
            ry = grid_oy + gy * CELL
            pygame.draw.rect(screen, (35, 30, 22), (rx, ry, CELL-1, CELL-1))
            pygame.draw.rect(screen, (60, 50, 35), (rx, ry, CELL-1, CELL-1), 1)

    # Items
    for item in bc.items:
        rx = grid_ox + item.gx * CELL
        ry = grid_oy + item.gy * CELL
        rw = item.w * CELL - 2
        rh = item.h * CELL - 2

        if bc.dragging is item:
            mx, my = pygame.mouse.get_pos()
            rx = mx - bc.drag_off[0]
            ry = my - bc.drag_off[1]

        # Item background
        pygame.draw.rect(screen, (item.col[0]//3, item.col[1]//3, item.col[2]//3), (rx+1, ry+1, rw, rh))
        pygame.draw.rect(screen, item.col, (rx+1, ry+1, rw, rh), 2)

        # Icon/text
        icon = font_med.render(item.icon, True, item.col)
        screen.blit(icon, (rx + rw//2 - icon.get_width()//2, ry + rh//2 - icon.get_height()//2))

        # Name (below icon if >1 cell)
        if rh > CELL:
            nm = font_xs.render(item.name, True, (180, 170, 150))
            screen.blit(nm, (rx + 4, ry + rh - 18))

    # Instructions
    hint = font_xs.render('Click+Drag to move items  |  TAB/ESC to close', True, (100, 90, 70))
    screen.blit(hint, (ox + gw//2 - hint.get_width()//2, oy + gh - 18))

    # Leon portrait (right side, like RE4)
    px = ox + gw + 20
    py = oy + 20
    # Simple portrait frame
    pygame.draw.rect(screen, (50, 38, 25), (px, py, 120, 160))
    pygame.draw.rect(screen, (90, 72, 48), (px, py, 120, 160), 2)
    # Face (simplified)
    pygame.draw.ellipse(screen, (195, 155, 110), (px+25, py+20, 70, 85))
    pygame.draw.ellipse(screen, (138, 112, 64), (px+28, py+10, 64, 50))  # hair
    # Eyes
    pygame.draw.circle(screen, (60, 80, 110), (px+45, py+55), 5)
    pygame.draw.circle(screen, (60, 80, 110), (px+75, py+55), 5)
    pygame.draw.circle(screen, (20, 20, 20), (px+45, py+55), 2)
    pygame.draw.circle(screen, (20, 20, 20), (px+75, py+55), 2)
    # Name
    nm = font_sm.render('Leon S.', True, (200,190,170))
    screen.blit(nm, (px + 60 - nm.get_width()//2, py + 125))
    nm2 = font_xs.render('Kennedy', True, (160,150,130))
    screen.blit(nm2, (px + 60 - nm2.get_width()//2, py + 142))

# ═══════════════════════════════════════════════════════════
# TITLE SCREEN
# ═══════════════════════════════════════════════════════════
def draw_title():
    screen.fill((8, 6, 4))
    # Radial glow
    for r in range(250, 0, -3):
        a = max(0, int(20 * (1 - r/250)))
        pygame.draw.circle(screen, (50+a, 25+a//2, 10+a//3), (HW, HH-50), r)

    # VILL4GE
    t1 = font_title.render('VILL', True, (196, 176, 138))
    t2 = font_title.render('4', True, (170, 20, 20))
    t3 = font_title.render('GE', True, (196, 176, 138))
    tw = t1.get_width() + t2.get_width() + t3.get_width()
    x = HW - tw // 2; y = HH - 80
    screen.blit(t1, (x, y))
    screen.blit(t2, (x + t1.get_width(), y))
    screen.blit(t3, (x + t1.get_width() + t2.get_width(), y))

    pulse = int(120 + 60 * math.sin(G.t * 2))
    sub = font_med.render('Click to Start', True, (200, 186, 160))
    sub.set_alpha(pulse)
    screen.blit(sub, (HW - sub.get_width()//2, HH + 40))

    ctrl = font_xs.render('WASD move  |  Mouse aim  |  Click shoot  |  R reload  |  Shift sprint  |  TAB briefcase',
                          True, (100, 90, 70))
    screen.blit(ctrl, (HW - ctrl.get_width()//2, HH + 85))

def draw_death():
    overlay = pygame.Surface((W, H), pygame.SRCALPHA)
    overlay.fill((50, 0, 0, 200))
    screen.blit(overlay, (0, 0))
    t = font_big.render('YOU ARE DEAD', True, (230, 218, 200))
    screen.blit(t, (HW - t.get_width()//2, HH - 30))
    s = font_med.render('Click to restart', True, (200, 180, 160))
    screen.blit(s, (HW - s.get_width()//2, HH + 30))

# ═══════════════════════════════════════════════════════════
# GAME LOGIC
# ═══════════════════════════════════════════════════════════
def update(dt):
    p = G.player; G.t += dt
    if G.state != 'play': return

    keys = pygame.key.get_pressed()
    p.inv = max(0, p.inv - dt)
    p.scd = max(0, p.scd - dt)
    p.flash = max(0, p.flash - dt)

    # Mouse look
    if G.mouse_cap:
        mx, _ = pygame.mouse.get_rel()
        p.ang += mx * 0.003

    # Movement
    spd = 7.0 if keys[pygame.K_LSHIFT] else 4.5
    mvx = mvy = 0
    if keys[pygame.K_w] or keys[pygame.K_UP]:
        mvx += math.cos(p.ang) * spd * dt; mvy += math.sin(p.ang) * spd * dt
    if keys[pygame.K_s] or keys[pygame.K_DOWN]:
        mvx -= math.cos(p.ang) * spd * dt; mvy -= math.sin(p.ang) * spd * dt
    if keys[pygame.K_a] or keys[pygame.K_LEFT]:
        mvx += math.cos(p.ang - math.pi/2) * spd * 0.7 * dt
        mvy += math.sin(p.ang - math.pi/2) * spd * 0.7 * dt
    if keys[pygame.K_d] or keys[pygame.K_RIGHT]:
        mvx += math.cos(p.ang + math.pi/2) * spd * 0.7 * dt
        mvy += math.sin(p.ang + math.pi/2) * spd * 0.7 * dt

    if mvx or mvy: p.bob += dt * 8

    m = 0.25
    if get_tile(p.x + mvx + m, p.y) == 0 and get_tile(p.x + mvx - m, p.y) == 0: p.x += mvx
    if get_tile(p.x, p.y + mvy + m) == 0 and get_tile(p.x, p.y + mvy - m) == 0: p.y += mvy

    # Enemies
    for e in G.enemies:
        if not e.alive: continue
        e.fl = max(0, e.fl - dt); e.stun = max(0, e.stun - dt); e.acd = max(0, e.acd - dt)
        if e.stun > 0: continue
        dx = p.x - e.x; dy = p.y - e.y; d = math.sqrt(dx*dx+dy*dy)
        if d > 0.8:
            nx = e.x + (dx/d) * e.spd * dt; ny = e.y + (dy/d) * e.spd * dt
            if get_tile(nx, e.y) == 0: e.x = nx
            if get_tile(e.x, ny) == 0: e.y = ny
        elif e.acd <= 0:
            if p.inv <= 0:
                p.hp -= e.dmg; p.inv = 0.5; snd_hit.play()
                for _ in range(5):
                    G.particles.append(Particle(p.x, p.y, .3, (random.random()-.5)*2,
                        (random.random()-.5)*2, random.random()*2, (180,0,0), .5+random.random()*.5))
            e.acd = 1.2
    G.enemies = [e for e in G.enemies if e.alive]

    # Pickups
    for pk in G.pickups:
        if not pk.alive: continue
        pk.bob += dt * 3
        if (p.x-pk.x)**2 + (p.y-pk.y)**2 < 0.8:
            snd_pick.play()
            if pk.tp == 'hp': p.hp = min(100, p.hp + 25)
            else: p.ammo = min(p.max_ammo, p.ammo + 8)
            pk.alive = False
    G.pickups = [pk for pk in G.pickups if pk.alive]

    # Particles
    for pt in G.particles:
        pt.x += pt.vx * dt; pt.y += pt.vy * dt; pt.z += pt.vz * dt
        pt.vz -= 5 * dt; pt.life -= dt
    G.particles = [pt for pt in G.particles if pt.life > 0]

    # Spawn
    G.sp_t -= dt
    if G.e_left > 0 and G.sp_t <= 0:
        G.spawn_enemy(); G.e_left -= 1; G.sp_t = 1.5 + random.random()
    if G.e_left <= 0 and len(G.enemies) == 0:
        G.wave_t += dt
        if G.wave_t > 2:
            p.wave += 1; G.e_left = 4 + p.wave * 2; G.sp_t = 1; G.wave_t = 0
            for _ in range(3):
                for _ in range(20):
                    px2 = random.uniform(3, M_W-3); py2 = random.uniform(3, M_H-3)
                    if get_tile(px2, py2) == 0:
                        G.pickups.append(Pickup(px2, py2, random.choice(['hp','ammo']))); break

    if p.hp <= 0:
        G.state = 'dead'; G.mouse_cap = False
        pygame.mouse.set_visible(True); pygame.event.set_grab(False); snd_die.play()

def do_shoot():
    p = G.player
    if p.ammo <= 0 or p.scd > 0: return
    p.ammo -= 1; p.scd = 0.2; p.flash = 0.1; snd_shoot.play()

    best_d = 999; best_e = None
    for e in G.enemies:
        if not e.alive: continue
        dx = e.x - p.x; dy = e.y - p.y
        dist = math.sqrt(dx*dx+dy*dy)
        if dist < 0.5 or dist > 22: continue
        ang = math.atan2(dy, dx); diff = ang - p.ang
        while diff > math.pi: diff -= 2*math.pi
        while diff < -math.pi: diff += 2*math.pi
        tol = math.atan2(0.4, dist)
        if abs(diff) < tol + 0.03 and dist < best_d:
            blocked = False
            for s in range(int(dist * 4)):
                t = s / (dist * 4)
                if get_tile(p.x + dx*t, p.y + dy*t) > 0: blocked = True; break
            if not blocked: best_d = dist; best_e = e

    if best_e:
        best_e.hp -= 25; best_e.fl = 0.15; best_e.stun = 0.3; snd_hit.play()
        for _ in range(8):
            G.particles.append(Particle(best_e.x, best_e.y, .3, (random.random()-.5)*3,
                (random.random()-.5)*3, random.random()*3, (140,0,0), .7+random.random()*.5))
        if best_e.hp <= 0:
            best_e.alive = False; G.player.kills += 1; G.player.ptas += 180; snd_die.play()
            for _ in range(12):
                G.particles.append(Particle(best_e.x, best_e.y, .2, (random.random()-.5)*2,
                    (random.random()-.5)*2, random.random()*1.5, (100,0,0), 1.5+random.random()))
            if random.random() < .4:
                G.pickups.append(Pickup(best_e.x, best_e.y, random.choice(['hp','ammo'])))

# ═══════════════════════════════════════════════════════════
# MAIN LOOP
# ═══════════════════════════════════════════════════════════
running = True
while running:
    dt = clock.tick(FPS) / 1000
    if dt > 0.05: dt = 0.05

    for ev in pygame.event.get():
        if ev.type == pygame.QUIT: running = False

        if ev.type == pygame.MOUSEBUTTONDOWN and ev.button == 1:
            if G.state == 'title': G.reset()
            elif G.state == 'dead': G.reset()
            elif G.state == 'play': do_shoot()
            elif G.state == 'briefcase':
                mx, my = ev.pos
                bc = G.briefcase
                grid_ox = HW - (GRID_W * CELL + 20) // 2 + 10
                grid_oy = HH - (GRID_H * CELL + 70) // 2 + 50
                # Check if clicking on an item
                for item in bc.items:
                    rx = grid_ox + item.gx * CELL
                    ry = grid_oy + item.gy * CELL
                    rw = item.w * CELL
                    rh = item.h * CELL
                    if rx <= mx <= rx + rw and ry <= my <= ry + rh:
                        bc.dragging = item
                        bc.drag_off = (mx - rx, my - ry)
                        break

        if ev.type == pygame.MOUSEBUTTONUP and ev.button == 1:
            if G.state == 'briefcase' and G.briefcase.dragging:
                bc = G.briefcase
                item = bc.dragging
                mx, my = ev.pos
                grid_ox = HW - (GRID_W * CELL + 20) // 2 + 10
                grid_oy = HH - (GRID_H * CELL + 70) // 2 + 50
                # Calculate new grid position
                new_gx = round((mx - bc.drag_off[0] - grid_ox) / CELL)
                new_gy = round((my - bc.drag_off[1] - grid_oy) / CELL)
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
                if G.state == 'briefcase':
                    G.state = 'play'; G.mouse_cap = True
                    pygame.mouse.set_visible(False); pygame.event.set_grab(True)
                elif G.state == 'play':
                    G.mouse_cap = not G.mouse_cap
                    pygame.mouse.set_visible(not G.mouse_cap)
                    pygame.event.set_grab(G.mouse_cap)
            if ev.key == pygame.K_r and G.state == 'play':
                if G.player.ammo < G.player.max_ammo:
                    G.player.ammo = min(G.player.max_ammo, G.player.ammo + 10)
                    snd_reload.play()

    update(dt)

    if G.state == 'title':
        draw_title()
    elif G.state in ('play', 'dead', 'briefcase'):
        p = G.player
        z_buf = draw_world(p)
        draw_pickups(p, z_buf)
        draw_enemies(p, z_buf)
        draw_particles(p, z_buf)
        draw_leon(p)
        draw_laser()
        draw_hud(p)
        if G.state == 'dead': draw_death()
        elif G.state == 'briefcase': draw_briefcase()

    pygame.display.flip()

pygame.quit()
sys.exit()
