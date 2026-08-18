"""
player.py - Player class with movement, spraying, and debuffs.
"""

from __future__ import annotations
import math
from enum import Enum, auto
from game.constants import (
    Element,
    PLAYER_SPEED, PLAYER_RADIUS, TILE_SIZE,
    SPRAY_RANGE, SPRAY_WIDTH, SPRAY_COOLDOWN,
    FREEZE_DURATION, SHRINK_DURATION, SLUSH_DURATION,
    SPEED_MODIFIERS, DEFAULT_SPEED_MODIFIER,
    GRID_COLS, GRID_ROWS,
)


class DebuffType(Enum):
    NONE = auto()
    FROZEN = auto()       # Water player hit by Ice  → immobilised
    SHRUNK = auto()       # Fire player hit by Water → reduced size & range
    SLUSHY = auto()       # Ice player hit by Fire   → reduced speed


class Player:
    """Represents one player on the field."""

    def __init__(
        self,
        element: Element,
        start_col: int,
        start_row: int,
        is_human: bool = False,
        player_id: int = 0,
    ) -> None:
        self.element = element
        self.player_id = player_id
        self.is_human = is_human

        # Position in *pixel* space (float for smooth movement)
        self.x: float = (start_col + 0.5) * TILE_SIZE
        self.y: float = (start_row + 0.5) * TILE_SIZE

        # Direction the player faces (radians, 0 = right)
        self.angle: float = 0.0

        # Movement intent set each frame (normalised direction)
        self.dx: float = 0.0
        self.dy: float = 0.0

        # Spraying
        self.spraying: bool = False
        self.spray_timer: int = 0

        # Debuff
        self.debuff: DebuffType = DebuffType.NONE
        self.debuff_timer: int = 0

        # Alive flag (for future respawn logic)
        self.alive: bool = True

    # ── grid helpers ──────────────────────────────────────────────
    @property
    def col(self) -> int:
        return int(self.x // TILE_SIZE)

    @property
    def row(self) -> int:
        return int(self.y // TILE_SIZE)

    # ── debuff application ────────────────────────────────────────
    def apply_debuff(self, dtype: DebuffType) -> None:
        durations = {
            DebuffType.FROZEN: FREEZE_DURATION,
            DebuffType.SHRUNK: SHRINK_DURATION,
            DebuffType.SLUSHY: SLUSH_DURATION,
        }
        self.debuff = dtype
        self.debuff_timer = durations.get(dtype, 0)

    def tick_debuff(self) -> None:
        if self.debuff_timer > 0:
            self.debuff_timer -= 1
            if self.debuff_timer <= 0:
                self.debuff = DebuffType.NONE

    # ── movement ──────────────────────────────────────────────────
    def effective_speed(self, terrain_state) -> float:
        """Return pixel-speed considering terrain + debuffs."""
        base = PLAYER_SPEED
        # Terrain modifier
        key = (self.element, terrain_state)
        modifier = SPEED_MODIFIERS.get(key, DEFAULT_SPEED_MODIFIER)
        speed = base * modifier

        # Debuff penalties
        if self.debuff == DebuffType.FROZEN:
            speed = 0.0
        elif self.debuff == DebuffType.SLUSHY:
            speed *= 0.55
        elif self.debuff == DebuffType.SHRUNK:
            speed *= 0.80

        return speed

    def move(self, speed: float) -> None:
        """Apply dx/dy to position. Clamp to grid."""
        if self.debuff == DebuffType.FROZEN:
            return

        mag = math.hypot(self.dx, self.dy)
        if mag < 0.001:
            return

        # Normalise
        nx = self.dx / mag
        ny = self.dy / mag

        self.x += nx * speed
        self.y += ny * speed

        # Update facing angle
        self.angle = math.atan2(ny, nx)

        # Clamp to map
        half = PLAYER_RADIUS
        self.x = max(half, min(self.x, GRID_COLS * TILE_SIZE - half))
        self.y = max(half, min(self.y, GRID_ROWS * TILE_SIZE - half))

    def radius(self) -> int:
        """Visual radius (shrinks when debuffed SHRUNK)."""
        if self.debuff == DebuffType.SHRUNK:
            return max(4, PLAYER_RADIUS // 2)
        return PLAYER_RADIUS

    def spray_range(self) -> int:
        """Effective spray range in tiles."""
        if self.debuff == DebuffType.SHRUNK:
            return max(1, SPRAY_RANGE // 2)
        return SPRAY_RANGE

    # ── spray cooldown ────────────────────────────────────────────
    def can_spray(self) -> bool:
        return self.spraying and self.spray_timer <= 0

    def tick_spray(self) -> None:
        if self.spray_timer > 0:
            self.spray_timer -= 1

    def reset_spray_cooldown(self) -> None:
        self.spray_timer = SPRAY_COOLDOWN
