"""
constants.py - Enums, colours, and tuning knobs for the prototype.
"""

from enum import Enum, auto

# ─── Window / Grid ────────────────────────────────────────────────
SCREEN_WIDTH = 1024
SCREEN_HEIGHT = 768
TILE_SIZE = 16          # pixels per grid cell
FPS = 60

# Derived grid dimensions
GRID_COLS = SCREEN_WIDTH // TILE_SIZE    # 64
GRID_ROWS = SCREEN_HEIGHT // TILE_SIZE   # 48

# ─── HUD ──────────────────────────────────────────────────────────
HUD_HEIGHT = 40         # pixels reserved at bottom for HUD

# ─── Elements ─────────────────────────────────────────────────────
class Element(Enum):
    NONE = auto()
    ICE = auto()
    WATER = auto()
    FIRE = auto()


# ─── Terrain States ───────────────────────────────────────────────
class TerrainState(Enum):
    NEUTRAL = auto()
    ICE = auto()
    WATER = auto()
    FIRE = auto()
    FROZEN = auto()      # ice freezes water
    PUDDLE = auto()      # water douses fire
    MIST = auto()        # fire melts ice / evaporates water
    DEAD = auto()        # outside shrinking ring


# ─── Colours (R, G, B) ───────────────────────────────────────────
TERRAIN_COLORS = {
    TerrainState.NEUTRAL: (180, 180, 180),
    TerrainState.ICE:     (160, 220, 255),
    TerrainState.WATER:   (50,  120, 220),
    TerrainState.FIRE:    (230, 80,  30),
    TerrainState.FROZEN:  (200, 240, 255),
    TerrainState.PUDDLE:  (100, 160, 200),
    TerrainState.MIST:    (220, 220, 230),
    TerrainState.DEAD:    (40,  40,  40),
}

ELEMENT_COLORS = {
    Element.NONE:  (200, 200, 200),
    Element.ICE:   (100, 200, 255),
    Element.WATER: (30,  90,  200),
    Element.FIRE:  (255, 100, 20),
}

TEAM_NAMES = {
    Element.ICE:   "Ice Team",
    Element.WATER: "Water Team",
    Element.FIRE:  "Fire Team",
}

# ─── Player tuning ───────────────────────────────────────────────
PLAYER_RADIUS = 8            # pixels (drawn as circle)
PLAYER_SPEED = 3.0           # pixels per frame
SPRAY_RANGE = 4              # tiles ahead converted per spray tick
SPRAY_WIDTH = 2              # half-width in tiles
SPRAY_COOLDOWN = 5           # frames between spray ticks

# ─── Debuff durations (frames) ───────────────────────────────────
FREEZE_DURATION = 120        # ~2 s at 60 fps
SHRINK_DURATION = 180        # ~3 s
SLUSH_DURATION  = 150        # ~2.5 s

# ─── Mobility modifiers (multiplied with base speed) ─────────────
SPEED_MODIFIERS = {
    # (player_element, terrain_state) → speed multiplier
    # Friendly terrain gives a boost
    (Element.ICE,   TerrainState.ICE):     1.40,
    (Element.WATER, TerrainState.WATER):   1.40,
    (Element.FIRE,  TerrainState.FIRE):    1.40,
    # Hostile terrain slows
    (Element.ICE,   TerrainState.FIRE):    0.70,
    (Element.ICE,   TerrainState.PUDDLE):  0.85,
    (Element.WATER, TerrainState.ICE):     0.70,
    (Element.WATER, TerrainState.FROZEN):  0.60,
    (Element.FIRE,  TerrainState.WATER):   0.70,
    (Element.FIRE,  TerrainState.MIST):    0.85,
    # Neutral & reaction states
    (Element.ICE,   TerrainState.FROZEN):  1.20,
    (Element.WATER, TerrainState.PUDDLE):  1.20,
    (Element.FIRE,  TerrainState.MIST):    1.00,
}
DEFAULT_SPEED_MODIFIER = 1.0

# ─── Ring / Shrink ────────────────────────────────────────────────
RING_SHRINK_INTERVAL = 600   # frames between shrink steps (~10 s)
RING_SHRINK_AMOUNT = 2       # tiles removed per step from each edge

# ─── Territory Banking ───────────────────────────────────────────
BANK_PER_TILE = 1            # score gained per tile converted
BANK_SPREAD_BONUS = 0.002    # extra spread rate per 100 banked points

# ─── AI ───────────────────────────────────────────────────────────
AI_DECISION_INTERVAL = 30    # frames between AI re-evaluations
AI_WANDER_CHANCE = 0.3       # probability of random wander vs objective
