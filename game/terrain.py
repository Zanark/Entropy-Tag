"""
terrain.py - Tile and GameMap classes.

The map is a 2-D grid of Tiles.  Each tile tracks its current terrain
state, which team "owns" it, and an optional strength value.
"""

from __future__ import annotations
from typing import Optional
from game.constants import (
    TerrainState, Element,
    GRID_COLS, GRID_ROWS,
    RING_SHRINK_AMOUNT,
)


class Tile:
    """Single grid cell."""

    __slots__ = ("state", "owner", "strength")

    def __init__(self) -> None:
        self.state: TerrainState = TerrainState.NEUTRAL
        self.owner: Element = Element.NONE
        self.strength: float = 0.0        # 0..1 – how "strong" the coating is

    def set(self, state: TerrainState, owner: Element, strength: float = 1.0) -> None:
        self.state = state
        self.owner = owner
        self.strength = min(max(strength, 0.0), 1.0)

    def reset(self) -> None:
        self.state = TerrainState.NEUTRAL
        self.owner = Element.NONE
        self.strength = 0.0

    def kill(self) -> None:
        """Mark tile as outside the ring."""
        self.state = TerrainState.DEAD
        self.owner = Element.NONE
        self.strength = 0.0


class GameMap:
    """Grid of tiles with ring-boundary tracking."""

    def __init__(self, cols: int = GRID_COLS, rows: int = GRID_ROWS) -> None:
        self.cols = cols
        self.rows = rows
        self.grid: list[list[Tile]] = [
            [Tile() for _ in range(cols)] for _ in range(rows)
        ]
        # Ring boundary (inclusive)
        self.ring_left = 0
        self.ring_top = 0
        self.ring_right = cols - 1
        self.ring_bottom = rows - 1

    # ── queries ───────────────────────────────────────────────────
    def in_bounds(self, col: int, row: int) -> bool:
        return 0 <= col < self.cols and 0 <= row < self.rows

    def in_ring(self, col: int, row: int) -> bool:
        return (self.ring_left <= col <= self.ring_right
                and self.ring_top <= row <= self.ring_bottom)

    def tile_at(self, col: int, row: int) -> Optional[Tile]:
        if self.in_bounds(col, row):
            return self.grid[row][col]
        return None

    # ── ring shrinking ────────────────────────────────────────────
    def shrink_ring(self, amount: int = RING_SHRINK_AMOUNT) -> bool:
        """Shrink the ring inward.  Returns False if arena is gone."""
        new_l = self.ring_left + amount
        new_r = self.ring_right - amount
        new_t = self.ring_top + amount
        new_b = self.ring_bottom - amount

        if new_l > new_r or new_t > new_b:
            return False  # no more room

        # Kill tiles that fall outside the new ring
        for r in range(self.rows):
            for c in range(self.cols):
                if not (new_l <= c <= new_r and new_t <= r <= new_b):
                    if self.grid[r][c].state != TerrainState.DEAD:
                        self.grid[r][c].kill()

        self.ring_left = new_l
        self.ring_right = new_r
        self.ring_top = new_t
        self.ring_bottom = new_b
        return True

    # ── territory counting ────────────────────────────────────────
    def count_territory(self) -> dict[Element, int]:
        """Return {element: tile_count} for living tiles only."""
        counts: dict[Element, int] = {
            Element.ICE: 0,
            Element.WATER: 0,
            Element.FIRE: 0,
            Element.NONE: 0,
        }
        for r in range(self.ring_top, self.ring_bottom + 1):
            for c in range(self.ring_left, self.ring_right + 1):
                t = self.grid[r][c]
                if t.state != TerrainState.DEAD:
                    counts[t.owner] += 1
        return counts

    def total_living_tiles(self) -> int:
        return sum(self.count_territory().values())

    def territory_percentages(self) -> dict[Element, float]:
        counts = self.count_territory()
        total = sum(counts.values())
        if total == 0:
            return {e: 0.0 for e in counts}
        return {e: (c / total) * 100.0 for e, c in counts.items()}
