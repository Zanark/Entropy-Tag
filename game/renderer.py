"""
renderer.py - Pygame rendering layer.

Draws:
  • Terrain grid
  • Players (circles with element colour)
  • Spray direction indicators
  • HUD bar (territory %, bank scores, debuff icons)
  • Ring boundary outline
  • Game-over overlay
"""

from __future__ import annotations
import math
import pygame
from game.constants import (
    SCREEN_WIDTH, SCREEN_HEIGHT, TILE_SIZE, HUD_HEIGHT,
    TERRAIN_COLORS, ELEMENT_COLORS, TEAM_NAMES,
    Element, TerrainState,
    PLAYER_RADIUS,
)
from game.terrain import GameMap
from game.player import Player, DebuffType
from game.engine import GameEngine


class Renderer:
    """All drawing lives here; the engine knows nothing about pygame."""

    def __init__(self, screen: pygame.Surface) -> None:
        self.screen = screen
        self.font = pygame.font.SysFont("consolas", 14)
        self.big_font = pygame.font.SysFont("consolas", 32, bold=True)

    # ── full frame ────────────────────────────────────────────────
    def draw(self, engine: GameEngine) -> None:
        self.screen.fill((0, 0, 0))
        self._draw_terrain(engine.game_map)
        self._draw_ring_border(engine.game_map)
        self._draw_players(engine.players)
        self._draw_hud(engine)
        if engine.game_over:
            self._draw_game_over(engine)
        pygame.display.flip()

    # ── terrain ───────────────────────────────────────────────────
    def _draw_terrain(self, gm: GameMap) -> None:
        for r in range(gm.rows):
            row = gm.grid[r]
            y = r * TILE_SIZE
            for c in range(gm.cols):
                tile = row[c]
                colour = TERRAIN_COLORS.get(tile.state, (100, 100, 100))
                pygame.draw.rect(
                    self.screen, colour,
                    (c * TILE_SIZE, y, TILE_SIZE, TILE_SIZE),
                )

    # ── ring border ───────────────────────────────────────────────
    def _draw_ring_border(self, gm: GameMap) -> None:
        rect = pygame.Rect(
            gm.ring_left * TILE_SIZE,
            gm.ring_top * TILE_SIZE,
            (gm.ring_right - gm.ring_left + 1) * TILE_SIZE,
            (gm.ring_bottom - gm.ring_top + 1) * TILE_SIZE,
        )
        pygame.draw.rect(self.screen, (255, 60, 60), rect, 2)

    # ── players ───────────────────────────────────────────────────
    def _draw_players(self, players: list[Player]) -> None:
        for p in players:
            if not p.alive:
                continue
            colour = ELEMENT_COLORS.get(p.element, (255, 255, 255))
            radius = p.radius()

            # Debuff outline
            outline_colour = None
            if p.debuff == DebuffType.FROZEN:
                outline_colour = (200, 240, 255)
            elif p.debuff == DebuffType.SHRUNK:
                outline_colour = (255, 200, 50)
            elif p.debuff == DebuffType.SLUSHY:
                outline_colour = (180, 130, 80)

            cx, cy = int(p.x), int(p.y)

            if outline_colour:
                pygame.draw.circle(self.screen, outline_colour, (cx, cy), radius + 3)

            pygame.draw.circle(self.screen, colour, (cx, cy), radius)

            # Facing-direction indicator
            end_x = cx + int(math.cos(p.angle) * (radius + 6))
            end_y = cy + int(math.sin(p.angle) * (radius + 6))
            pygame.draw.line(self.screen, (255, 255, 255), (cx, cy), (end_x, end_y), 2)

            # Label for human players
            if p.is_human:
                labels = {0: "P1", 1: "P2", 2: "P3"}
                # Determine label by element (Ice=P1, Water=P2, Fire=P3)
                elem_order = {Element.ICE: 0, Element.WATER: 1, Element.FIRE: 2}
                label_idx = elem_order.get(p.element, p.player_id)
                label_text = labels.get(label_idx, "P?")
                tag = self.font.render(label_text, True, (255, 255, 255))
                self.screen.blit(tag, (cx - tag.get_width() // 2, cy - radius - 16))

    # ── HUD ───────────────────────────────────────────────────────
    def _draw_hud(self, engine: GameEngine) -> None:
        hud_y = SCREEN_HEIGHT - HUD_HEIGHT
        pygame.draw.rect(self.screen, (30, 30, 30), (0, hud_y, SCREEN_WIDTH, HUD_HEIGHT))

        pcts = engine.game_map.territory_percentages()

        # Territory bars
        bar_x = 10
        bar_w = 200
        bar_h = 12
        for elem in (Element.ICE, Element.WATER, Element.FIRE):
            pct = pcts.get(elem, 0.0)
            colour = ELEMENT_COLORS[elem]
            label = f"{TEAM_NAMES[elem]}: {pct:.1f}%  Bank:{engine.bank.get(elem, 0)}"
            txt = self.font.render(label, True, (220, 220, 220))
            self.screen.blit(txt, (bar_x, hud_y + 4))

            # Small bar
            inner_w = int(bar_w * pct / 100.0)
            pygame.draw.rect(self.screen, (60, 60, 60), (bar_x, hud_y + 22, bar_w, bar_h))
            pygame.draw.rect(self.screen, colour, (bar_x, hud_y + 22, inner_w, bar_h))

            bar_x += bar_w + 100

        # Frame / timer
        timer_txt = self.font.render(f"Frame: {engine.frame}  Ring in: {engine.ring_timer}", True, (180, 180, 180))
        self.screen.blit(timer_txt, (SCREEN_WIDTH - timer_txt.get_width() - 10, hud_y + 4))

    # ── game over overlay ─────────────────────────────────────────
    def _draw_game_over(self, engine: GameEngine) -> None:
        overlay = pygame.Surface((SCREEN_WIDTH, SCREEN_HEIGHT), pygame.SRCALPHA)
        overlay.fill((0, 0, 0, 160))
        self.screen.blit(overlay, (0, 0))

        if engine.winner:
            text = f"{TEAM_NAMES[engine.winner]} WINS!"
            colour = ELEMENT_COLORS[engine.winner]
        else:
            text = "DRAW!"
            colour = (255, 255, 255)

        rendered = self.big_font.render(text, True, colour)
        x = (SCREEN_WIDTH - rendered.get_width()) // 2
        y = (SCREEN_HEIGHT - rendered.get_height()) // 2 - 20
        self.screen.blit(rendered, (x, y))

        sub = self.font.render("Press R to restart  |  ESC to quit", True, (200, 200, 200))
        self.screen.blit(sub, ((SCREEN_WIDTH - sub.get_width()) // 2, y + 50))
