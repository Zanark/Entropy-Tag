"""
ai.py - Simple AI behaviours for bot players.

Three modes:
  1. SPRAY_NEUTRAL  – seek nearest neutral / enemy tile and spray.
  2. CHASE_ENEMY    – find nearest enemy player and approach while spraying.
  3. WANDER         – random movement.

The bot re-evaluates every AI_DECISION_INTERVAL frames.
"""

from __future__ import annotations
import math
import random
from game.constants import (
    Element, TerrainState,
    AI_DECISION_INTERVAL, AI_WANDER_CHANCE,
    TILE_SIZE, GRID_COLS, GRID_ROWS,
)
from game.player import Player, DebuffType
from game.terrain import GameMap


class AIController:
    """Drives a single bot Player."""

    def __init__(self, player: Player) -> None:
        self.player = player
        self.timer: int = 0
        self.target_x: float = player.x
        self.target_y: float = player.y

    def update(self, game_map: GameMap, all_players: list[Player]) -> None:
        p = self.player
        if not p.alive:
            return

        self.timer -= 1
        if self.timer <= 0:
            self.timer = AI_DECISION_INTERVAL
            self._decide(game_map, all_players)

        # Steer towards target
        dx = self.target_x - p.x
        dy = self.target_y - p.y
        dist = math.hypot(dx, dy)

        if dist < TILE_SIZE:
            # Arrived – pick a new decision next tick
            p.dx = 0.0
            p.dy = 0.0
            self.timer = 0  # force re-decide
        else:
            p.dx = dx
            p.dy = dy

        # Always spray when moving (bots are aggressive)
        p.spraying = True

    # ── decision logic ────────────────────────────────────────────
    def _decide(self, gm: GameMap, all_players: list[Player]) -> None:
        if random.random() < AI_WANDER_CHANCE:
            self._wander(gm)
            return

        # 60% chance to claim neutral/enemy territory, 40% to chase
        if random.random() < 0.6:
            self._seek_territory(gm)
        else:
            self._chase_enemy(all_players)

    def _wander(self, gm: GameMap) -> None:
        """Pick a random location inside the ring."""
        c = random.randint(gm.ring_left + 1, max(gm.ring_left + 1, gm.ring_right - 1))
        r = random.randint(gm.ring_top + 1, max(gm.ring_top + 1, gm.ring_bottom - 1))
        self.target_x = (c + 0.5) * TILE_SIZE
        self.target_y = (r + 0.5) * TILE_SIZE

    def _seek_territory(self, gm: GameMap) -> None:
        """Find nearest non-owned tile and go there."""
        p = self.player
        best_dist = float("inf")
        best_c, best_r = p.col, p.row

        # Sample a subset for performance
        sample_step = max(1, (gm.ring_right - gm.ring_left) // 12)
        for r in range(gm.ring_top, gm.ring_bottom + 1, sample_step):
            for c in range(gm.ring_left, gm.ring_right + 1, sample_step):
                tile = gm.grid[r][c]
                if tile.state == TerrainState.DEAD:
                    continue
                if tile.owner == p.element:
                    continue  # skip own territory
                d = abs(c - p.col) + abs(r - p.row)
                if d < best_dist:
                    best_dist = d
                    best_c, best_r = c, r

        self.target_x = (best_c + 0.5) * TILE_SIZE
        self.target_y = (best_r + 0.5) * TILE_SIZE

    def _chase_enemy(self, all_players: list[Player]) -> None:
        """Move towards the nearest enemy player."""
        p = self.player
        nearest = None
        nearest_dist = float("inf")

        for other in all_players:
            if other is p or not other.alive:
                continue
            if other.element == p.element:
                continue
            d = math.hypot(other.x - p.x, other.y - p.y)
            if d < nearest_dist:
                nearest_dist = d
                nearest = other

        if nearest:
            self.target_x = nearest.x
            self.target_y = nearest.y
        else:
            # No enemies found – wander randomly
            c = random.randint(2, GRID_COLS - 3)
            r = random.randint(2, GRID_ROWS - 3)
            self.target_x = (c + 0.5) * TILE_SIZE
            self.target_y = (r + 0.5) * TILE_SIZE
