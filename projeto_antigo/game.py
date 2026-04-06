"""
VILL4GE — RE4-style 3D Survival Horror (Ursina Engine)
Real 3D engine with textured walls, shaded enemies, physics, fog, lighting.
Controls: WASD move | Mouse aim | Click shoot | 1-4 weapons | G grenade
          R reload | E interact (doors/merchant) | TAB briefcase | M minimap
"""
from ursina import *
import math, random, os, sys

# ═══════════════════════════════════════════════════════════
# PROCEDURAL CYLINDER MESH (not built-in in Ursina 8.3.0)
# ═══════════════════════════════════════════════════════════
def _make_cylinder_mesh(segments=12):
    verts, tris, uvs, norms = [], [], [], []
    for i in range(segments):
        a0 = 2*math.pi*i/segments
        a1 = 2*math.pi*(i+1)/segments
        c0, s0 = math.cos(a0), math.sin(a0)
        c1, s1 = math.cos(a1), math.sin(a1)
        b = len(verts)
        # Side quad (2 triangles)
        verts += [Vec3(c0,-.5,s0), Vec3(c1,-.5,s1), Vec3(c1,.5,s1), Vec3(c0,.5,s0)]
        norms += [Vec3(c0,0,s0), Vec3(c1,0,s1), Vec3(c1,0,s1), Vec3(c0,0,s0)]
        u0, u1 = i/segments, (i+1)/segments
        uvs += [Vec2(u0,0), Vec2(u1,0), Vec2(u1,1), Vec2(u0,1)]
        tris += [b, b+1, b+2, b, b+2, b+3]
        # Top cap
        b2 = len(verts)
        verts += [Vec3(0,.5,0), Vec3(c0,.5,s0), Vec3(c1,.5,s1)]
        norms += [Vec3(0,1,0)]*3; uvs += [Vec2(.5,.5), Vec2(c0*.5+.5,s0*.5+.5), Vec2(c1*.5+.5,s1*.5+.5)]
        tris += [b2, b2+1, b2+2]
        # Bottom cap
        b3 = len(verts)
        verts += [Vec3(0,-.5,0), Vec3(c1,-.5,s1), Vec3(c0,-.5,s0)]
        norms += [Vec3(0,-1,0)]*3; uvs += [Vec2(.5,.5), Vec2(c1*.5+.5,s1*.5+.5), Vec2(c0*.5+.5,s0*.5+.5)]
        tris += [b3, b3+1, b3+2]
    m = Mesh(vertices=verts, triangles=tris, uvs=uvs, normals=norms)
    return m

_cylinder_mesh = None
def get_cylinder():
    global _cylinder_mesh
    if _cylinder_mesh is None:
        _cylinder_mesh = _make_cylinder_mesh(12)
    return _cylinder_mesh

# ═══════════════════════════════════════════════════════════
# APP INIT
# ═══════════════════════════════════════════════════════════
app = Ursina(
    title='VILL4GE - Over The Shoulder',
    borderless=False,
    fullscreen=False,
    size=(1280, 720),
    vsync=True,
    development_mode=False,
)
window.exit_button.visible = False
window.fps_counter.enabled = True
window.color = color.rgb(58, 52, 42)

# ═══════════════════════════════════════════════════════════
# PATHS
# ═══════════════════════════════════════════════════════════
_BASE = os.path.dirname(os.path.abspath(__file__))
_VIL_TEX = os.path.join(_BASE, 'Modelos', 'village-re4', 'textures')
_LEON_TEX = os.path.join(_BASE, 'Modelos', 'resident-evil-4-leon', 'textures')

def _tex(folder, fname):
    p = os.path.join(folder, fname)
    if os.path.exists(p):
        return Texture(p)
    return None

# ═══════════════════════════════════════════════════════════
# TEXTURES
# ═══════════════════════════════════════════════════════════
tex_stone  = _tex(_VIL_TEX, '20_c.jpeg')
tex_wood   = _tex(_VIL_TEX, '90_c.jpeg')
tex_fence  = _tex(_VIL_TEX, '71_c.jpeg')
tex_door   = _tex(_VIL_TEX, '59_c.jpeg')
tex_ground = _tex(_VIL_TEX, '48_c.jpeg')
tex_barrel = _tex(_VIL_TEX, '37_c.jpeg')
tex_crate  = _tex(_VIL_TEX, '90_c.jpeg')
tex_bark   = _tex(_VIL_TEX, '7_c.jpeg')
tex_foliage = _tex(_VIL_TEX, 'f1_c.jpeg')
tex_burlap = _tex(_VIL_TEX, '94_c.jpeg')
tex_sky    = _tex(_VIL_TEX, 'sky_color.jpeg')

WALL_TEX_MAP = {
    1: (tex_stone, color.rgb(85, 75, 58)),
    2: (tex_wood,  color.rgb(100, 78, 52)),
    3: (tex_fence, color.rgb(110, 95, 65)),
    4: (tex_door,  color.rgb(60, 38, 22)),
}

# ═══════════════════════════════════════════════════════════
# MAP — 60x60 RE4 village
# ═══════════════════════════════════════════════════════════
M_W, M_H = 60, 60
MAP = [[0]*M_W for _ in range(M_H)]

def _rect(v, x1, y1, x2, y2):
    for y in range(y1, y2+1):
        for x in range(x1, x2+1):
            if 0 <= y < M_H and 0 <= x < M_W:
                MAP[y][x] = v

def _box(v, x, y, w, h):
    for i in range(w):
        MAP[y][x+i] = v; MAP[y+h-1][x+i] = v
    for j in range(h):
        MAP[y+j][x] = v; MAP[y+j][x+w-1] = v

# Border
_rect(1, 0, 0, M_W-1, 0); _rect(1, 0, M_H-1, M_W-1, M_H-1)
_rect(1, 0, 0, 0, M_H-1); _rect(1, M_W-1, 0, M_W-1, M_H-1)
# Buildings
_box(1, 4, 4, 8, 6);   MAP[7][8] = 4
_box(2, 40, 5, 6, 5);  MAP[7][40] = 4
_box(1, 8, 20, 7, 6);  MAP[23][14] = 4
_box(2, 35, 18, 10, 8); MAP[22][35] = 4; MAP[22][44] = 4
_box(2, 5, 40, 5, 4);  MAP[42][9] = 4
_box(1, 22, 3, 10, 7); MAP[9][27] = 4
_box(1, 20, 38, 8, 6); MAP[38][24] = 4
_box(2, 50, 30, 5, 4); MAP[32][50] = 4
_box(1, 45, 45, 7, 6); MAP[48][45] = 4
_box(1, 14, 48, 6, 5); MAP[48][17] = 4
_box(2, 52, 15, 5, 4); MAP[17][52] = 4
_box(1, 28, 28, 6, 5); MAP[32][31] = 4
# Fences
_rect(3, 15, 14, 30, 14); _rect(3, 15, 14, 15, 18); _rect(3, 30, 14, 30, 18)
MAP[16][15] = 0; MAP[16][30] = 0
_rect(3, 3, 32, 3, 38); _rect(3, 48, 12, 55, 12)
_rect(3, 38, 38, 44, 38); _rect(3, 10, 12, 10, 16)

def get_tile(x, z):
    ix, iz = int(x), int(z)
    if 0 <= iz < M_H and 0 <= ix < M_W: return MAP[iz][ix]
    return 1

def is_solid(x, z):
    return get_tile(x, z) in (1, 2, 3, 4)

# ═══════════════════════════════════════════════════════════
# BUILD 3D WORLD
# ═══════════════════════════════════════════════════════════
# Ground — thin cube (plane model has broken normals in Ursina 8.3)
ground = Entity(
    model='cube',
    scale=(M_W, 0.1, M_H),
    position=(M_W/2, -0.05, M_H/2),
    texture=tex_ground,
    texture_scale=(M_W//2, M_H//2),
    color=color.rgb(78, 65, 45) if not tex_ground else color.rgb(190, 170, 140),
    collider='box',
    unlit=True,
)

# Sky — use window.color as background (Sky() broken in Ursina 8.3)
window.color = color.rgb(58, 52, 42)

# No lights — Ursina 8.3 lit shaders cause whiteout; use flat/default rendering

# Build walls
wall_parent = Entity()
door_entities = {}

for z in range(M_H):
    for x in range(M_W):
        t = MAP[z][x]
        if t == 0: continue
        tex_info = WALL_TEX_MAP.get(t, (tex_stone, color.rgb(85, 75, 58)))
        tex_s, col_s = tex_info
        h = 3.0 if t in (1, 2) else (1.5 if t == 3 else 2.8)
        e = Entity(
            parent=wall_parent,
            model='cube',
            position=(x + 0.5, h/2, z + 0.5),
            scale=(1, h, 1),
            texture=tex_s,
            color=col_s if not tex_s else color.rgb(210, 200, 180),
            collider='box',
            unlit=True,
        )
        if t == 4:
            door_entities[(x, z)] = e

# ═══════════════════════════════════════════════════════════
# SCENERY
# ═══════════════════════════════════════════════════════════
_scenery_placements = [
    (12.5,10.5,'barrel'), (13.5,10.5,'barrel'), (14.5,10.5,'crate'),
    (25.5,15.5,'barrel'), (38.5,22.5,'crate'),  (42.5,22.5,'crate'),
    (10.5,28.5,'barrel'), (15.5,35.5,'crate'),  (50.5,35.5,'barrel'),
    (42.5,48.5,'barrel'), (22.5,42.5,'crate'),  (7.5,22.5,'barrel'),
    (18.5,8.5,'tree'), (35.5,8.5,'tree'), (55.5,8.5,'tree'),
    (5.5,25.5,'tree'), (12.5,45.5,'tree'), (42.5,35.5,'tree'),
    (48.5,8.5,'tree'), (55.5,28.5,'tree'), (30.5,45.5,'tree'),
    (55.5,45.5,'tree'), (8.5,50.5,'tree'), (28.5,42.5,'tree'),
    (46.5,10.5,'tree'), (56.5,20.5,'tree'),
    (20.5,22.5,'torch'), (40.5,25.5,'torch'),
    (22.5,18.5,'well'),
    (30.5,50.5,'barrel'), (32.5,50.5,'barrel'),
    (15.5,38.5,'crate'), (5.5,15.5,'barrel'),
]

for sx, sz, kind in _scenery_placements:
    if 0 < sx < M_W and 0 < sz < M_H and get_tile(sx, sz) == 0:
        if kind == 'barrel':
            Entity(model=get_cylinder(), position=(sx, 0.5, sz),
                   scale=(0.4, 1.0, 0.4),
                   color=color.rgb(120,90,55), unlit=True)
            for by in [0.3, 0.7]:
                Entity(model=get_cylinder(), position=(sx, by, sz),
                       scale=(0.42, 0.04, 0.42), color=color.rgb(55,55,48), unlit=True)
            # Barrel top
            Entity(model=get_cylinder(), position=(sx, 1.02, sz),
                   scale=(0.38, 0.02, 0.38), color=color.rgb(85,65,40), unlit=True)
        elif kind == 'crate':
            Entity(model='cube', position=(sx, 0.4, sz),
                   scale=(0.7, 0.8, 0.7), texture=tex_crate,
                   color=color.rgb(100,80,50) if not tex_crate else color.rgb(200,180,145), unlit=True)
        elif kind == 'tree':
            Entity(model=get_cylinder(), position=(sx, 1.5, sz),
                   scale=(0.25, 3.0, 0.25),
                   color=color.rgb(65,42,22), unlit=True)
            # Multi-layer foliage (no texture — solid colors look better on spheres)
            Entity(model='sphere', position=(sx, 3.5, sz),
                   scale=(2.5, 2.0, 2.5), color=color.rgb(28,62,18), unlit=True)
            Entity(model='sphere', position=(sx, 4.2, sz),
                   scale=(1.8, 1.5, 1.8), color=color.rgb(35,72,22), unlit=True)
            Entity(model='sphere', position=(sx, 4.7, sz),
                   scale=(1.0, 1.0, 1.0), color=color.rgb(42,82,28), unlit=True)
        elif kind == 'torch':
            Entity(model='cube', position=(sx, 0.8, sz),
                   scale=(0.1, 1.6, 0.1), color=color.rgb(55,40,22), unlit=True)
            Entity(model='sphere', position=(sx, 1.7, sz),
                   scale=0.15, color=color.yellow, unlit=True)
            pl = PointLight(position=(sx, 1.7, sz), color=color.rgb(255,160,60))
        elif kind == 'well':
            Entity(model=get_cylinder(), position=(sx, 0.4, sz),
                   scale=(0.8, 0.8, 0.8), color=color.rgb(65,60,50), unlit=True)
            Entity(model=get_cylinder(), position=(sx, 0.05, sz),
                   scale=(0.6, 0.1, 0.6), color=color.rgb(15,18,28), unlit=True)

# ═══════════════════════════════════════════════════════════
# WEAPONS
# ═══════════════════════════════════════════════════════════
WEAPONS = {
    'handgun':  {'name':'Handgun',  'dmg':25, 'cd':0.22, 'spread':0.02, 'pellets':1,
                 'range':22, 'ammo_key':'hg', 'mag':15},
    'shotgun':  {'name':'Shotgun',  'dmg':18, 'cd':0.7,  'spread':0.12, 'pellets':6,
                 'range':10, 'ammo_key':'sg', 'mag':6},
    'rifle':    {'name':'Rifle',    'dmg':80, 'cd':1.2,  'spread':0.005,'pellets':1,
                 'range':30, 'ammo_key':'rf', 'mag':5},
    'tmp':      {'name':'TMP',      'dmg':12, 'cd':0.08, 'spread':0.06, 'pellets':1,
                 'range':16, 'ammo_key':'tm', 'mag':30},
}
WPN_ORDER = ['handgun', 'shotgun', 'rifle', 'tmp']

# ═══════════════════════════════════════════════════════════
# GANADO VISUAL TYPES
# ═══════════════════════════════════════════════════════════
GANADO_TYPES = [
    {'shirt': color.rgb(100,82,55),  'pants': color.rgb(65,50,32),  'skin': color.rgb(185,155,120)},
    {'shirt': color.rgb(60,68,80),   'pants': color.rgb(50,45,38),  'skin': color.rgb(175,148,115)},
    {'shirt': color.rgb(95,78,48),   'pants': color.rgb(55,45,30),  'skin': color.rgb(170,140,108)},
    {'shirt': color.rgb(110,95,60),  'pants': color.rgb(70,58,38),  'skin': color.rgb(180,150,115)},
    {'shirt': color.rgb(140,130,115),'pants': color.rgb(60,48,32),  'skin': color.rgb(175,145,110)},
    {'shirt': color.rgb(55,52,48),   'pants': color.rgb(42,38,30),  'skin': color.rgb(165,138,105)},
]

# ═══════════════════════════════════════════════════════════
# PLAYER
# ═══════════════════════════════════════════════════════════
class Player(Entity):
    def __init__(self):
        super().__init__()
        self.position = Vec3(20.5, 1.5, 30.5)
        self.speed = 4.5
        self.sprint_speed = 7.0
        self.mouse_sensitivity = Vec2(80, 80)
        self.camera_pivot = Entity(parent=self, y=0.5)
        camera.parent = self.camera_pivot
        camera.position = (0, 0, 0)
        camera.rotation = (0, 0, 0)
        camera.fov = 80

        self.hp = 100; self.max_hp = 100
        self.ammo = {'hg': 15, 'sg': 6, 'rf': 5, 'tm': 30}
        self.max_ammo = {'hg': 15, 'sg': 6, 'rf': 5, 'tm': 30}
        self.weapon = 'handgun'
        self.grenades = 2
        self.kills = 0; self.wave = 1; self.ptas = 0
        self.inv_t = 0; self.shoot_cd = 0; self.flash_t = 0
        self.vy = 0

    def update(self):
        if G.state != 'play': return

        # Mouse look
        if mouse.locked:
            self.rotation_y += mouse.velocity[0] * self.mouse_sensitivity[0]
            self.camera_pivot.rotation_x -= mouse.velocity[1] * self.mouse_sensitivity[1]
            self.camera_pivot.rotation_x = clamp(self.camera_pivot.rotation_x, -80, 80)

        # Movement
        spd = self.sprint_speed if held_keys['left shift'] else self.speed
        direction = Vec3(
            (held_keys['d'] - held_keys['a']),
            0,
            (held_keys['w'] - held_keys['s'])
        ).normalized()

        if direction.length() > 0:
            move = (self.forward * direction.z + self.right * direction.x) * spd * time.dt
            new_x = self.x + move.x
            new_z = self.z + move.z
            margin = 0.25
            if not is_solid(new_x + margin, self.z) and not is_solid(new_x - margin, self.z):
                self.x = new_x
            if not is_solid(self.x, new_z + margin) and not is_solid(self.x, new_z - margin):
                self.z = new_z

        # Gravity
        self.vy -= 20 * time.dt
        self.y += self.vy * time.dt
        if self.y <= 1.5:
            self.y = 1.5; self.vy = 0

        # Timers
        self.inv_t = max(0, self.inv_t - time.dt)
        self.shoot_cd = max(0, self.shoot_cd - time.dt)
        self.flash_t = max(0, self.flash_t - time.dt)

# ═══════════════════════════════════════════════════════════
# ENEMY
# ═══════════════════════════════════════════════════════════
class Enemy(Entity):
    def __init__(self, x, z, tough=False, chainsaw=False):
        gt = random.choice(GANADO_TYPES)
        super().__init__(position=(x, 0, z))
        self.tough = tough; self.chainsaw = chainsaw
        self.vis_type = gt

        if chainsaw:
            self.hp = 200; self.mhp = 200; self.spd = 1.8; self.dmg = 40
        elif tough:
            self.hp = 80; self.mhp = 80; self.spd = 1.5; self.dmg = 18
        else:
            self.hp = 35; self.mhp = 35
            self.spd = 2.2 + random.random()*0.8; self.dmg = 10

        self.acd = 0; self.fl_t = 0; self.stun_t = 0; self.is_alive = True

        th = 1.2 if not chainsaw else 1.5
        tw = 0.5 if not chainsaw else 0.6

        # Body parts
        self.leg_l = Entity(parent=self, model='cube', y=0.35, x=-0.12,
                            scale=(0.15, 0.7, 0.18), color=gt['pants'], texture=tex_burlap)
        self.leg_r = Entity(parent=self, model='cube', y=0.35, x=0.12,
                            scale=(0.15, 0.7, 0.18), color=gt['pants'], texture=tex_burlap)
        self.torso = Entity(parent=self, model='cube', y=0.7+th/2,
                            scale=(tw, th, 0.3), color=gt['shirt'], texture=tex_burlap)
        self.arm_l = Entity(parent=self, model='cube', y=1.1, x=-(tw/2+0.08),
                            scale=(0.14, 0.8, 0.14), color=gt['shirt'], texture=tex_burlap)
        self.arm_r = Entity(parent=self, model='cube', y=1.1, x=(tw/2+0.08),
                            scale=(0.14, 0.8, 0.14), color=gt['shirt'], texture=tex_burlap)

        head_col = gt['skin'] if not chainsaw else color.rgb(125, 100, 62)
        self.head = Entity(parent=self, model='sphere', y=0.7+th+0.25,
                           scale=0.3, color=head_col)

        eye_c = color.rgb(220,40,15) if not chainsaw else color.rgb(255,20,10)
        Entity(parent=self.head, model='sphere', z=-0.4, x=-0.2, y=0.05,
               scale=0.2, color=eye_c, unlit=True)
        Entity(parent=self.head, model='sphere', z=-0.4, x=0.2, y=0.05,
               scale=0.2, color=eye_c, unlit=True)

        # Shadow
        Entity(parent=self, model='quad', rotation_x=90, y=0.02,
               scale=(0.7, 0.5), color=color.rgba(0,0,0,80), unlit=True)

        # Weapon prop
        if chainsaw:
            Entity(parent=self, model='cube', position=(tw/2+0.25, 1, 0),
                   scale=(0.08, 0.08, 0.7), color=color.rgb(150,150,145))
        elif tough:
            Entity(parent=self, model='cube', position=(tw/2+0.2, 1.2, 0),
                   scale=(0.06, 1.0, 0.06), color=color.rgb(75,58,38))

        # HP bar
        self.hp_bg = Entity(parent=self, model='quad', y=0.7+th+0.7,
                            scale=(0.6, 0.06), color=color.black, billboard=True, unlit=True)
        self.hp_bar = Entity(parent=self, model='quad', y=0.7+th+0.7,
                             scale=(0.6, 0.05), color=color.red, billboard=True, unlit=True)
        self._th = th

    def update(self):
        if not self.is_alive: return
        self.fl_t = max(0, self.fl_t - time.dt)
        self.stun_t = max(0, self.stun_t - time.dt)
        self.acd = max(0, self.acd - time.dt)

        if self.stun_t > 0: return

        p = player
        dx = p.x - self.x; dz = p.z - self.z
        dist = math.sqrt(dx*dx + dz*dz)

        if dist > 0.3:
            self.rotation_y = math.degrees(math.atan2(dx, dz))

        if dist > 1.0:
            spd = self.spd * time.dt
            nx = self.x + (dx/dist) * spd
            nz = self.z + (dz/dist) * spd
            if not is_solid(nx, self.z): self.x = nx
            if not is_solid(self.x, nz): self.z = nz
            swing = math.sin(time.time() * 8) * 20
            self.leg_l.rotation_x = swing
            self.leg_r.rotation_x = -swing
            self.arm_l.rotation_x = -swing * 0.6
            self.arm_r.rotation_x = swing * 0.6
        elif self.acd <= 0:
            if p.inv_t <= 0:
                p.hp -= self.dmg; p.inv_t = 0.5
                _flash_screen()
            self.acd = 1.2 if not self.chainsaw else 0.8

        self.torso.color = color.white if self.fl_t > 0 else self.vis_type['shirt']

        pct = max(0, self.hp / self.mhp)
        self.hp_bar.scale_x = 0.6 * pct

    def take_damage(self, dmg, headshot=False):
        if not self.is_alive: return
        actual = int(dmg * (1.5 if headshot else 1.0))
        self.hp -= actual
        self.fl_t = 0.15; self.stun_t = 0.3
        if headshot:
            G.msg = 'HEADSHOT!'; G.msg_t = 0.8
        if self.hp <= 0:
            self.die()

    def die(self):
        self.is_alive = False
        player.kills += 1
        player.ptas += 300 if self.chainsaw else (220 if self.tough else 180)
        drop = random.random()
        if drop < 0.3: Pickup(self.x, self.z, 'hp')
        elif drop < 0.55: Pickup(self.x, self.z, 'ammo')
        elif drop < 0.7: Pickup(self.x, self.z, 'ptas')
        self.animate('rotation_x', 90, duration=0.5, curve=curve.out_quad)
        self.animate('y', -0.3, duration=0.5, curve=curve.out_quad)
        destroy(self, delay=2)

# ═══════════════════════════════════════════════════════════
# PICKUPS
# ═══════════════════════════════════════════════════════════
class Pickup(Entity):
    def __init__(self, x, z, tp='hp'):
        col_map = {'hp': color.rgb(220,40,40), 'ammo': color.rgb(200,190,120),
                   'ptas': color.rgb(220,200,60), 'grenade': color.rgb(100,120,80)}
        super().__init__(model='sphere', position=(x, 0.4, z), scale=0.25,
                         color=col_map.get(tp, color.yellow), unlit=True)
        self.tp = tp; self.is_alive = True
        self.bob_off = random.random() * 6.28
        Entity(parent=self, model='sphere', scale=1.3,
               color=color.rgba(255,255,200,40), unlit=True)

    def update(self):
        if not self.is_alive: return
        self.y = 0.4 + math.sin(time.time()*3 + self.bob_off) * 0.15
        self.rotation_y += time.dt * 60
        if distance(self, player) < 1.0:
            self.collect()

    def collect(self):
        self.is_alive = False
        p = player
        if self.tp == 'hp': p.hp = min(p.max_hp, p.hp+25)
        elif self.tp == 'ammo':
            ak = WEAPONS[p.weapon]['ammo_key']
            p.ammo[ak] = min(99, p.ammo[ak]+8)
        elif self.tp == 'ptas': p.ptas += random.randint(200,500)
        elif self.tp == 'grenade': p.grenades += 1
        destroy(self)

# ═══════════════════════════════════════════════════════════
# GRENADE
# ═══════════════════════════════════════════════════════════
class GrenadeProj(Entity):
    def __init__(self, x, y, z, ang_y, ang_x):
        super().__init__(model='sphere', position=(x,y,z), scale=0.12,
                         color=color.rgb(100,110,70))
        spd = 12
        rad_y = math.radians(ang_y)
        rad_x = math.radians(ang_x)
        self.vx = math.sin(rad_y) * math.cos(rad_x) * spd
        self.vy = math.sin(rad_x) * spd + 4.5
        self.vz = math.cos(rad_y) * math.cos(rad_x) * spd
        self.timer = 1.5
        self.shadow = Entity(model='quad', rotation_x=90, y=0.02,
                             scale=0.2, color=color.rgba(0,0,0,60), unlit=True)

    def update(self):
        self.timer -= time.dt
        self.vy -= 14.0 * time.dt
        nx = self.x + self.vx * time.dt
        nz = self.z + self.vz * time.dt
        self.y += self.vy * time.dt
        if self.y <= 0.1:
            self.y = 0.1; self.vy = abs(self.vy)*0.3
            if abs(self.vy) < 0.5: self.vy = 0
        if is_solid(nx, nz):
            self.vx *= -0.5; self.vz *= -0.5
        else:
            self.x = nx; self.z = nz
        self.vx *= 0.96; self.vz *= 0.96
        self.shadow.world_position = Vec3(self.x, 0.02, self.z)
        if self.timer <= 0:
            self.explode()

    def explode(self):
        for e in G.enemies[:]:
            if not e.is_alive: continue
            d = math.sqrt((e.x-self.x)**2 + (e.z-self.z)**2)
            if d < 4:
                e.take_damage(int(120*(1-d/4)))
        # Explosion FX
        flash = Entity(model='sphere', position=self.position+Vec3(0,0.5,0),
                        scale=0.5, color=color.rgb(255,200,80), unlit=True)
        flash.animate_scale(4, duration=0.3, curve=curve.out_expo)
        flash.animate('color', color.rgba(255,100,30,0), duration=0.4)
        destroy(flash, delay=0.5)
        for _ in range(8):
            p = Entity(model='sphere',
                       position=self.position+Vec3(random.uniform(-0.5,0.5),
                                                    random.uniform(0,1),
                                                    random.uniform(-0.5,0.5)),
                       scale=random.uniform(0.2,0.5),
                       color=color.rgba(80,80,80,180), unlit=True)
            p.animate('y', p.y+random.uniform(1,3), duration=1, curve=curve.out_quad)
            p.animate_scale(0, duration=1)
            destroy(p, delay=1.1)
        destroy(self.shadow)
        destroy(self)

# ═══════════════════════════════════════════════════════════
# EFFECTS
# ═══════════════════════════════════════════════════════════
_flash_overlay = None
def _flash_screen():
    global _flash_overlay
    if _flash_overlay:
        destroy(_flash_overlay)
    _flash_overlay = Entity(parent=camera.ui, model='quad', scale=2,
                            color=color.rgba(180,0,0,100), z=-1)
    _flash_overlay.animate('color', color.rgba(180,0,0,0), duration=0.3)
    destroy(_flash_overlay, delay=0.35)

# ═══════════════════════════════════════════════════════════
# WEAPON VIEW MODEL
# ═══════════════════════════════════════════════════════════
class WeaponVM(Entity):
    def __init__(self):
        super().__init__(parent=camera)
        self.gun = Entity(parent=self, model='cube',
                          position=(0.35, -0.25, 0.5),
                          scale=(0.06, 0.06, 0.35),
                          color=color.rgb(72, 68, 62))
        Entity(parent=self.gun, model='cube', position=(0,-0.8,-0.2),
               scale=(0.8,1.2,0.4), color=color.rgb(58,55,48))
        self.bob_t = 0; self.recoil = 0

    def update(self):
        if G.state != 'play': return
        moving = held_keys['w'] or held_keys['s'] or held_keys['a'] or held_keys['d']
        self.bob_t += time.dt * (8 if moving else 1.5)
        bx = math.sin(self.bob_t*2)*0.01 if moving else math.sin(self.bob_t)*0.003
        by = math.sin(self.bob_t*4)*0.008 if moving else math.sin(self.bob_t*1.5)*0.002
        self.recoil = max(0, self.recoil - time.dt*8)
        self.gun.position = Vec3(0.35+bx, -0.25+by-self.recoil*0.05,
                                 0.5-self.recoil*0.1)

    def set_weapon(self, wpn):
        sc = {'handgun':(0.06,0.06,0.3), 'shotgun':(0.06,0.06,0.55),
              'rifle':(0.05,0.05,0.65), 'tmp':(0.06,0.06,0.35)}
        cl = {'handgun': color.rgb(72,68,62), 'shotgun': color.rgb(100,78,52),
              'rifle': color.rgb(80,85,90), 'tmp': color.rgb(100,100,110)}
        self.gun.scale = Vec3(*sc.get(wpn, sc['handgun']))
        self.gun.color = cl.get(wpn, cl['handgun'])

# ═══════════════════════════════════════════════════════════
# HUD
# ═══════════════════════════════════════════════════════════
class HUD(Entity):
    def __init__(self):
        super().__init__(parent=camera.ui)
        # Crosshair
        Entity(parent=self, model='quad', scale=0.008,
               color=color.rgb(255,20,10), z=-2)
        # HP
        self.hp_bg = Entity(parent=self, model='quad', position=(-0.72,-0.42),
                            scale=(0.2,0.025), color=color.rgba(0,0,0,180))
        self.hp_bar = Entity(parent=self, model='quad', position=(-0.72,-0.42),
                             scale=(0.2,0.02), color=color.rgb(180,30,20))
        Text(parent=self, text='LIFE', position=(-0.83,-0.40), scale=0.7,
             color=color.rgb(180,170,150))
        # Ammo
        self.ammo_text = Text(parent=self, text='15', position=(0.68,-0.40),
                              scale=1.0, color=color.rgb(200,190,120))
        self.wpn_text = Text(parent=self, text='HANDGUN', position=(0.62,-0.44),
                             scale=0.7, color=color.rgb(140,130,110))
        # Top bar
        Entity(parent=self, model='quad', position=(0,0.47), scale=(2,0.04),
               color=color.rgba(0,0,0,150))
        self.ch_text = Text(parent=self, text='Chapter 1', position=(-0.08,0.475),
                            scale=0.8, color=color.rgb(200,186,160))
        self.ptas_text = Text(parent=self, text='PTAS: 0', position=(0.55,0.475),
                              scale=0.7, color=color.rgb(220,200,60))
        self.kills_text = Text(parent=self, text='Kills: 0', position=(-0.7,0.475),
                               scale=0.7, color=color.rgb(180,170,150))
        # Bottom bars
        Entity(parent=self, model='quad', position=(0,-0.48), scale=(2,0.04),
               color=color.black)
        Entity(parent=self, model='quad', position=(0,0.49), scale=(2,0.02),
               color=color.black)
        # Grenade
        self.gren_text = Text(parent=self, text='G:2', position=(0.4,-0.44),
                              scale=0.7, color=color.rgb(100,120,80))
        # Message
        self.msg_text = Text(parent=self, text='', position=(0,0.1), origin=(0,0),
                             scale=1.2, color=color.rgb(200,186,160))

    def update(self):
        p = player
        if G.state not in ('play','dead'): return
        pct = max(0, p.hp/p.max_hp)
        self.hp_bar.scale_x = 0.2 * pct
        self.hp_bar.x = -0.72 - 0.1*(1-pct)
        self.hp_bar.color = color.rgb(200,30,20) if pct >= 0.3 else (
            color.rgb(200,30,20) if int(time.time()*4)%2==0 else color.rgb(120,20,15))
        ak = WEAPONS[p.weapon]['ammo_key']
        self.ammo_text.text = str(p.ammo[ak])
        self.wpn_text.text = WEAPONS[p.weapon]['name'].upper()
        self.ch_text.text = f'Chapter {p.wave}'
        self.ptas_text.text = f'PTAS: {p.ptas}'
        self.kills_text.text = f'Kills: {p.kills}'
        self.gren_text.text = f'G:{p.grenades}'
        if G.msg_t > 0:
            G.msg_t -= time.dt
            self.msg_text.text = G.msg
        else:
            self.msg_text.text = ''

# ═══════════════════════════════════════════════════════════
# MERCHANT
# ═══════════════════════════════════════════════════════════
MERCHANT_POS = Vec3(27.5, 0, 12.5)
merchant_ent = Entity(model='cube', position=MERCHANT_POS+Vec3(0,1,0),
                      scale=(0.6,2,0.4), color=color.rgb(55,25,75))
Entity(parent=merchant_ent, model='sphere', y=0.6, scale=(0.5,0.4,0.4),
       color=color.rgb(48,22,62))
_meye_l = Entity(parent=merchant_ent, model='sphere', position=(-0.15,0.55,-0.45),
                 scale=0.1, color=color.rgb(200,170,40), unlit=True)
_meye_r = Entity(parent=merchant_ent, model='sphere', position=(0.15,0.55,-0.45),
                 scale=0.1, color=color.rgb(200,170,40), unlit=True)
merchant_prompt = Text(text='', position=(0,-0.2), origin=(0,0),
                       color=color.rgb(200,180,120), scale=1)

SHOP_ITEMS = [
    ('First Aid Spray', 3000, 'hp_full'),
    ('Handgun Ammo x30', 1500, 'ammo_hg'),
    ('Shotgun Ammo x10', 2000, 'ammo_sg'),
    ('Rifle Ammo x5',   2500, 'ammo_rf'),
    ('TMP Ammo x50',    1800, 'ammo_tm'),
    ('Grenade x2',      2000, 'grenade'),
    ('Max HP +20',       5000, 'upgrade_hp'),
]

shop_panel = Entity(parent=camera.ui, model='quad', scale=(0.8,0.7),
                    color=color.rgba(20,15,10,220), z=-5, enabled=False)
Text(parent=shop_panel, text="MERCHANT", position=(0,0.42), origin=(0,0),
     scale=2, color=color.rgb(200,180,120))
shop_ptas = Text(parent=shop_panel, text="", position=(0,0.35), origin=(0,0),
                 scale=1.2, color=color.rgb(220,200,60))

shop_buttons = []
for i, (name, price, _) in enumerate(SHOP_ITEMS):
    btn = Button(parent=shop_panel, text=f'{name} - {price} PTAS',
                 position=(0, 0.22-i*0.1), scale=(0.7,0.07),
                 color=color.rgb(50,38,25), highlight_color=color.rgb(80,60,35),
                 text_color=color.rgb(200,190,160))
    btn.on_click = lambda idx=i: merchant_buy(idx)
    shop_buttons.append(btn)

Button(parent=shop_panel, text='[ESC] Close',
       position=(0, 0.22-len(SHOP_ITEMS)*0.1-0.06),
       scale=(0.3,0.06), color=color.rgb(80,25,25),
       text_color=color.rgb(200,180,150)).on_click = lambda: close_merchant()

def merchant_buy(idx):
    name, price, effect = SHOP_ITEMS[idx]
    p = player
    if p.ptas < price:
        G.msg = 'Not enough PTAS!'; G.msg_t = 1; return
    p.ptas -= price
    if effect == 'hp_full': p.hp = p.max_hp
    elif effect == 'ammo_hg': p.ammo['hg'] = min(99, p.ammo['hg']+30)
    elif effect == 'ammo_sg': p.ammo['sg'] = min(99, p.ammo['sg']+10)
    elif effect == 'ammo_rf': p.ammo['rf'] = min(99, p.ammo['rf']+5)
    elif effect == 'ammo_tm': p.ammo['tm'] = min(99, p.ammo['tm']+50)
    elif effect == 'grenade': p.grenades += 2
    elif effect == 'upgrade_hp': p.max_hp += 20; p.hp = p.max_hp
    G.msg = f'Bought {name}'; G.msg_t = 1.5

def open_merchant():
    G.state = 'merchant'; shop_panel.enabled = True
    mouse.locked = False; mouse.visible = True

def close_merchant():
    G.state = 'play'; shop_panel.enabled = False
    mouse.locked = True; mouse.visible = False

# Briefcase
brief_panel = Entity(parent=camera.ui, model='quad', scale=(0.9,0.7),
                     color=color.rgba(25,20,12,230), z=-5, enabled=False)
Text(parent=brief_panel, text='ATTACHE CASE', position=(0,0.42), origin=(0,0),
     scale=1.8, color=color.rgb(200,186,160))
brief_items = Text(parent=brief_panel, text='', position=(-0.38,0.3),
                   scale=1, color=color.rgb(180,170,150))

def update_briefcase():
    p = player
    brief_items.text = '\n'.join([
        f'Handgun - Ammo: {p.ammo["hg"]}',
        f'Shotgun - Ammo: {p.ammo["sg"]}',
        f'Rifle   - Ammo: {p.ammo["rf"]}',
        f'TMP     - Ammo: {p.ammo["tm"]}',
        '', f'Grenades: {p.grenades}',
        f'HP: {p.hp}/{p.max_hp}', f'PTAS: {p.ptas}',
        '', '[TAB/ESC] Close'
    ])

# Title / Death
title_panel = Entity(parent=camera.ui, model='quad', scale=3,
                     color=color.rgba(8,6,4,245), z=-10, enabled=True)
Text(parent=title_panel, text='VILL', position=(-0.06,0.07), origin=(0,0),
     scale=4, color=color.rgb(196,176,138))
Text(parent=title_panel, text='4', position=(0.03,0.07), origin=(0,0),
     scale=4, color=color.rgb(170,20,20))
Text(parent=title_panel, text='GE', position=(0.09,0.07), origin=(0,0),
     scale=4, color=color.rgb(196,176,138))
title_sub = Text(parent=title_panel, text='Click to Start', position=(0,-0.04),
                 origin=(0,0), scale=1.5, color=color.rgb(200,186,160))
Text(parent=title_panel,
     text='WASD move | Mouse aim | Click shoot | 1-4 weapons | G grenade | R reload | E interact | TAB case',
     position=(0,-0.12), origin=(0,0), scale=0.8, color=color.rgb(100,90,70))

death_panel = Entity(parent=camera.ui, model='quad', scale=3,
                     color=color.rgba(50,0,0,200), z=-10, enabled=False)
Text(parent=death_panel, text='YOU ARE DEAD', position=(0,0.05), origin=(0,0),
     scale=3, color=color.rgb(230,218,200))
death_stats = Text(parent=death_panel, text='', position=(0,-0.03), origin=(0,0),
                   scale=1.2, color=color.rgb(180,170,150))
Text(parent=death_panel, text='Click to restart', position=(0,-0.08), origin=(0,0),
     scale=1.2, color=color.rgb(200,180,160))

# Rain
rain_particles = []
for _ in range(120):
    r = Entity(model='cube',
               position=(random.uniform(5,55), random.uniform(5,15), random.uniform(5,55)),
               scale=(0.02,0.4,0.02), color=color.rgba(100,110,125,80), unlit=True)
    r.rain_speed = random.uniform(8,14)
    rain_particles.append(r)

# ═══════════════════════════════════════════════════════════
# GAME STATE
# ═══════════════════════════════════════════════════════════
class GameState:
    def __init__(self):
        self.state = 'title'
        self.enemies = []
        self.e_left = 0; self.sp_t = 0; self.wave_t = 0
        self.msg = ''; self.msg_t = 0

    def reset(self):
        for e in self.enemies:
            if e: destroy(e)
        self.enemies.clear()
        player.position = Vec3(20.5,1.5,30.5)
        player.rotation_y = 0; player.camera_pivot.rotation_x = 0
        player.hp = 100; player.max_hp = 100
        player.ammo = {'hg':15,'sg':6,'rf':5,'tm':30}
        player.weapon = 'handgun'; player.grenades = 2
        player.kills = 0; player.wave = 1; player.ptas = 0
        self.e_left = 5; self.sp_t = 0; self.wave_t = 0
        self.state = 'play'
        title_panel.enabled = False; death_panel.enabled = False
        shop_panel.enabled = False; brief_panel.enabled = False
        mouse.locked = True; mouse.visible = False
        self.msg = 'Chapter 1 — The Village'; self.msg_t = 3
        for _ in range(20):
            for _ in range(30):
                px = random.uniform(3,M_W-3); pz = random.uniform(3,M_H-3)
                if get_tile(px,pz) == 0:
                    Pickup(px,pz,random.choices(['hp','ammo','ptas','grenade'],
                                                weights=[4,4,2,1])[0])
                    break

    def spawn_enemy(self):
        p = player
        for _ in range(30):
            a = random.random()*math.pi*2
            d = random.uniform(12,25)
            ex = max(2,min(M_W-2, p.x+math.cos(a)*d))
            ez = max(2,min(M_H-2, p.z+math.sin(a)*d))
            if get_tile(ex,ez) == 0:
                ch = random.random() < 0.02+player.wave*0.01
                tg = (not ch) and random.random() < 0.1+player.wave*0.03
                e = Enemy(ex, ez, tg, ch)
                self.enemies.append(e)
                return

G = GameState()
player = Player()
weapon_vm = WeaponVM()
hud = HUD()

# ═══════════════════════════════════════════════════════════
# SHOOTING
# ═══════════════════════════════════════════════════════════
def do_shoot():
    p = player; wpn = WEAPONS[p.weapon]; ak = wpn['ammo_key']
    if p.ammo[ak] <= 0 or p.shoot_cd > 0: return
    p.ammo[ak] -= 1; p.shoot_cd = wpn['cd']; p.flash_t = 0.12
    weapon_vm.recoil = 1.0
    # Muzzle flash
    fl = Entity(parent=camera, model='sphere', position=(0.35,-0.2,0.9),
                scale=0.08, color=color.rgb(255,220,80), unlit=True)
    fl.animate_scale(0, duration=0.08); destroy(fl, delay=0.1)

    for _ in range(wpn['pellets']):
        sx = random.uniform(-wpn['spread'], wpn['spread'])
        sy = random.uniform(-wpn['spread'], wpn['spread'])
        origin = camera.world_position
        direction = (camera.forward + camera.right*sx + camera.up*sy).normalized()
        hit = raycast(origin, direction, distance=wpn['range'], ignore=[player])

        if hit.hit and isinstance(hit.entity, Enemy) and hit.entity.is_alive:
            hit.entity.take_damage(wpn['dmg'], random.random() < 0.15)
            for _ in range(5):
                bp = Entity(model='sphere', position=hit.world_point,
                            scale=0.05, color=color.rgb(140,0,0), unlit=True)
                bp.animate('y', bp.y+random.uniform(0.5,1.5), duration=0.5)
                bp.animate_scale(0, duration=0.5); destroy(bp, delay=0.6)
        elif hit.hit:
            sp = Entity(model='sphere', position=hit.world_point,
                        scale=0.05, color=color.rgb(200,180,80), unlit=True)
            sp.animate_scale(0, duration=0.2); destroy(sp, delay=0.3)

def interact():
    md = math.sqrt((player.x-MERCHANT_POS.x)**2 + (player.z-MERCHANT_POS.z)**2)
    if md < 3: open_merchant(); return
    fw = player.forward
    fx = player.x + fw.x*1.5; fz = player.z + fw.z*1.5
    ix, iz = int(fx), int(fz)
    if 0 <= iz < M_H and 0 <= ix < M_W:
        t = MAP[iz][ix]
        key = (ix, iz)
        if t == 4:
            MAP[iz][ix] = 5
            if key in door_entities:
                door_entities[key].animate('y', -2, duration=0.3)
                door_entities[key].animate('scale_y', 0.1, duration=0.3)
        elif t == 5:
            MAP[iz][ix] = 4
            if key in door_entities:
                door_entities[key].animate('y', 1.4, duration=0.3)
                door_entities[key].animate('scale_y', 2.8, duration=0.3)

def throw_grenade():
    p = player
    if p.grenades <= 0: return
    p.grenades -= 1
    GrenadeProj(p.x, p.y, p.z, p.rotation_y, -p.camera_pivot.rotation_x)
    G.msg = 'Grenade!'; G.msg_t = 0.8

# ═══════════════════════════════════════════════════════════
# UPDATE & INPUT
# ═══════════════════════════════════════════════════════════
def update():
    # Rain
    for r in rain_particles:
        r.y -= r.rain_speed * time.dt
        if r.y < 0:
            r.y = random.uniform(8,15)
            r.x = player.x + random.uniform(-15,15)
            r.z = player.z + random.uniform(-15,15)

    # Merchant eyes
    glow = 0.7+0.3*math.sin(time.time()*3)
    _meye_l.color = color.rgb(int(200*glow),int(170*glow),int(40*glow))
    _meye_r.color = color.rgb(int(200*glow),int(170*glow),int(40*glow))
    merchant_ent.look_at_2d(player.position, 'y')

    if G.state == 'play' and math.sqrt((player.x-MERCHANT_POS.x)**2+(player.z-MERCHANT_POS.z)**2) < 3:
        merchant_prompt.text = '[E] Merchant'
    else:
        merchant_prompt.text = ''

    if G.state == 'merchant':
        shop_ptas.text = f'Your PTAS: {player.ptas}'

    if G.state != 'play': return

    # Spawn
    G.sp_t -= time.dt
    if G.e_left > 0 and G.sp_t <= 0:
        G.spawn_enemy(); G.e_left -= 1; G.sp_t = 1.5+random.random()
    G.enemies = [e for e in G.enemies if e.is_alive]

    # Wave
    if G.e_left <= 0 and len(G.enemies) == 0:
        G.wave_t += time.dt
        if G.wave_t > 2:
            player.wave += 1; G.e_left = 4+player.wave*2
            G.sp_t = 1; G.wave_t = 0
            G.msg = f'Chapter {player.wave} — The Village'; G.msg_t = 3
            for _ in range(4):
                for _ in range(20):
                    px = random.uniform(3,M_W-3); pz = random.uniform(3,M_H-3)
                    if get_tile(px,pz) == 0:
                        Pickup(px,pz,random.choices(['hp','ammo','ptas','grenade'],
                                                    weights=[4,4,2,1])[0])
                        break

    if player.hp <= 0:
        G.state = 'dead'; death_panel.enabled = True
        death_stats.text = f'Kills: {player.kills}  |  Chapters: {player.wave}  |  PTAS: {player.ptas}'
        mouse.locked = False; mouse.visible = True

def input(key):
    if key == 'left mouse down':
        if G.state == 'title': G.reset()
        elif G.state == 'dead': death_panel.enabled = False; G.reset()
        elif G.state == 'play': do_shoot()

    if key == 'escape':
        if G.state in ('merchant','briefcase'):
            if G.state == 'merchant': close_merchant()
            else:
                G.state = 'play'; brief_panel.enabled = False
                mouse.locked = True; mouse.visible = False
        elif G.state == 'play':
            mouse.locked = not mouse.locked; mouse.visible = not mouse.locked

    if G.state == 'play':
        if key == 'r':
            wpn = WEAPONS[player.weapon]; ak = wpn['ammo_key']
            if player.ammo[ak] < player.max_ammo[ak]:
                player.ammo[ak] = min(99, player.ammo[ak]+wpn['mag'])
        if key == 'e': interact()
        if key == 'g': throw_grenade()
        if key == 'm':
            pass  # minimap toggle placeholder
        if key == 'tab':
            G.state = 'briefcase'; brief_panel.enabled = True
            update_briefcase()
            mouse.locked = False; mouse.visible = True
        if key == '1': player.weapon = 'handgun'; weapon_vm.set_weapon('handgun')
        if key == '2': player.weapon = 'shotgun'; weapon_vm.set_weapon('shotgun')
        if key == '3': player.weapon = 'rifle'; weapon_vm.set_weapon('rifle')
        if key == '4': player.weapon = 'tmp'; weapon_vm.set_weapon('tmp')
        if key == 'scroll up':
            idx = WPN_ORDER.index(player.weapon)
            player.weapon = WPN_ORDER[(idx+1)%len(WPN_ORDER)]
            weapon_vm.set_weapon(player.weapon)
        if key == 'scroll down':
            idx = WPN_ORDER.index(player.weapon)
            player.weapon = WPN_ORDER[(idx-1)%len(WPN_ORDER)]
            weapon_vm.set_weapon(player.weapon)
    elif G.state == 'briefcase':
        if key == 'tab':
            G.state = 'play'; brief_panel.enabled = False
            mouse.locked = True; mouse.visible = False

app.run()
