"""
engine.py - GameEngine: the main simulation loop (decoupled from rendering).

Responsibilities:
  • Process input (set player intents).
  • Move players (respecting terrain speed modifiers and debuffs).
  • Execute sprays (convert tiles, apply counter debuffs).
  • Manage ring shrinking.
  • Track territory-banking scores.
  • Determine win condition.
"""

from __future__ import annotations
import math
from typing import Optional

from game.constants import (
    Element, TerrainState,
    TILE_SIZE, GRID_COLS, GRID_ROWS,
    SPRAY_WIDTH, PLAYER_RADIUS,
    RING_SHRINK_INTERVAL, RING_SHRINK_AMOUNT,
    BANK_PER_TILE, BANK_SPREAD_BONUS,
)
from game.terrain import GameMap
from game.player import Player, DebuffType
from game.interactions import resolve_spray, get_counter_debuff


class GameEngine:
    """Tick-based game simulation."""

    def __init__(self, players: list[Player]) -> None:
        self.game_map = GameMap(GRID_COLS, GRID_ROWS)
        self.players = players
        self.frame: int = 0

        # Territory-banking scores
        self.bank: dict[Element, int] = {
            Element.ICE: 0,
            Element.WATER: 0,
            Element.FIRE: 0,
        }

        # Game state
        self.game_over: bool = False
        self.winner: Optional[Element] = None
        self.ring_timer: int = RING_SHRINK_INTERVAL

    # ── main tick ─────────────────────────────────────────────────
    def tick(self) -> None:
        if self.game_over:
            return
        self.frame += 1

        # 1. Debuff timers
        for p in self.players:
            p.tick_debuff()

        # 2. Movement
        self._move_players()

        # 3. Spraying
        self._process_sprays()

        # 4. Player-vs-player proximity debuffs
        self._check_player_collisions()

        # 5. Ring shrink
        self.ring_timer -= 1
        if self.ring_timer <= 0:
            self.ring_timer = RING_SHRINK_INTERVAL
            alive = self.game_map.shrink_ring(RING_SHRINK_AMOUNT)
            if not alive:
                self._resolve_winner()
                return
            # Push players inside new ring
            self._clamp_players_to_ring()

        # 6. Check win (optional mid-game dominance win)
        # (could be enabled: if any team >80% of living tiles → instant win)

    # ── movement ──────────────────────────────────────────────────
    def _move_players(self) -> None:
        for p in self.players:
            if not p.alive:
                continue
            tile = self.game_map.tile_at(p.col, p.row)
            terrain = tile.state if tile else TerrainState.NEUTRAL
            speed = p.effective_speed(terrain)
            p.move(speed)
            p.tick_spray()
        # Keep every player inside the current ring
        self._clamp_players_to_ring()

    # ── spraying ──────────────────────────────────────────────────
    def _process_sprays(self) -> None:
        for p in self.players:
            if not p.alive or not p.can_spray():
                continue
            p.reset_spray_cooldown()

            # Determine tiles in spray cone (simple rectangle in facing dir)
            cos_a = math.cos(p.angle)
            sin_a = math.sin(p.angle)
            rng = p.spray_range()

            for dist in range(1, rng + 1):
                for offset in range(-SPRAY_WIDTH, SPRAY_WIDTH + 1):
                    # Tile centre along the facing direction
                    tc = p.col + int(round(cos_a * dist - sin_a * offset))
                    tr = p.row + int(round(sin_a * dist + cos_a * offset))

                    if not self.game_map.in_ring(tc, tr):
                        continue

                    tile = self.game_map.tile_at(tc, tr)
                    if tile is None or tile.state == TerrainState.DEAD:
                        continue

                    old_owner = tile.owner
                    new_state, new_owner = resolve_spray(p.element, tile.state)
                    tile.set(new_state, new_owner)

                    # Banking: score when converting enemy / neutral tiles
                    if new_owner != Element.NONE and new_owner != old_owner:
                        self.bank[new_owner] = self.bank.get(new_owner, 0) + BANK_PER_TILE

    # ── player collisions ─────────────────────────────────────────
    def _check_player_collisions(self) -> None:
        """If two opposing players overlap while one is spraying,
        apply counter debuff if applicable."""
        radius = PLAYER_RADIUS * 2.5  # proximity threshold in pixels
        for i, a in enumerate(self.players):
            if not a.alive or not a.spraying:
                continue
            for b in self.players[i + 1:]:
                if not b.alive or a.element == b.element:
                    continue
                dist = math.hypot(a.x - b.x, a.y - b.y)
                if dist > radius:
                    continue

                # a is spraying → check if a counters b
                debuff = get_counter_debuff(a.element, b.element)
                if debuff and b.debuff == DebuffType.NONE:
                    b.apply_debuff(debuff)

                # b might also be spraying → check reverse
                if b.spraying:
                    debuff2 = get_counter_debuff(b.element, a.element)
                    if debuff2 and a.debuff == DebuffType.NONE:
                        a.apply_debuff(debuff2)

    # ── ring clamping ─────────────────────────────────────────────
    def _clamp_players_to_ring(self) -> None:
        gm = self.game_map
        left_px = gm.ring_left * TILE_SIZE + PLAYER_RADIUS
        right_px = (gm.ring_right + 1) * TILE_SIZE - PLAYER_RADIUS
        top_px = gm.ring_top * TILE_SIZE + PLAYER_RADIUS
        bot_px = (gm.ring_bottom + 1) * TILE_SIZE - PLAYER_RADIUS
        for p in self.players:
            p.x = max(left_px, min(p.x, right_px))
            p.y = max(top_px, min(p.y, bot_px))

    # ── win condition ─────────────────────────────────────────────
    def _resolve_winner(self) -> None:
        self.game_over = True
        pcts = self.game_map.territory_percentages()
        # Remove NONE
        team_pcts = {e: v for e, v in pcts.items() if e != Element.NONE}
        if not team_pcts:
            self.winner = None
            return

        best = max(team_pcts, key=lambda e: team_pcts[e])
        # Check tie (two teams within 1%)
        top_val = team_pcts[best]
        tied = [e for e, v in team_pcts.items() if abs(v - top_val) < 1.0]
        if len(tied) > 1:
            # Tiebreaker: highest bank score
            best = max(tied, key=lambda e: self.bank.get(e, 0))
        self.winner = best

    def force_end(self) -> None:
        """Force game over now (for debug)."""
        self._resolve_winner()

    # ── convenience ───────────────────────────────────────────────
    def spread_bonus(self, element: Element) -> float:
        """Extra spread-rate multiplier from banked score."""
        return 1.0 + self.bank.get(element, 0) * BANK_SPREAD_BONUS
