"""
main.py - Entry point for EntropyTag Prototype.

Controls
--------
  Player 1 (Ice)   – WASD to move, Left Shift to spray
  Player 2 (Water) – IJKL to move, Right Shift to spray
  Player 3 (Fire)  – Xbox controller: Left stick move, A / RT to spray

  R      Restart
  ESC    Quit
  F      Force end game (debug)
"""

import sys
import pygame

from game.constants import (
    SCREEN_WIDTH, SCREEN_HEIGHT, FPS, HUD_HEIGHT,
    TILE_SIZE, GRID_COLS, GRID_ROWS,
    Element,
)
from game.player import Player
from game.engine import GameEngine
from game.renderer import Renderer
from game.ai import AIController

# ─── Controller constants ────────────────────────────────────────
STICK_DEADZONE = 0.15        # ignore tiny stick drift
TRIGGER_THRESHOLD = 0.3      # RT press threshold


# ─── helpers ──────────────────────────────────────────────────────

def create_players() -> tuple[list[Player], list[Player]]:
    """Return (all_players, [human_ice, human_water, human_fire]).
    3 teams × 2 players each.  First player of each team is human."""
    players: list[Player] = []
    humans: list[Player] = []
    pid = 0

    # Team spawn positions (col, row)
    spawns = {
        Element.ICE:   [(5,  5),  (10, 8)],
        Element.WATER: [(GRID_COLS - 6, GRID_ROWS - 6),
                        (GRID_COLS - 11, GRID_ROWS - 9)],
        Element.FIRE:  [(GRID_COLS // 2, 5),
                        (GRID_COLS // 2 + 4, 8)],
    }

    for elem, positions in spawns.items():
        for i, (c, r) in enumerate(positions):
            is_human = (i == 0)   # first player of every team is human
            p = Player(elem, c, r, is_human=is_human, player_id=pid)
            players.append(p)
            if is_human:
                humans.append(p)
            pid += 1

    # humans order: Ice, Water, Fire (matches dict insertion order)
    return players, humans


def build_game() -> tuple[GameEngine, list[AIController], list[Player]]:
    players, humans = create_players()
    engine = GameEngine(players)
    ai_controllers = [
        AIController(p) for p in players if not p.is_human
    ]
    return engine, ai_controllers, humans


# ─── main loop ────────────────────────────────────────────────────

def main() -> None:
    pygame.init()
    pygame.joystick.init()
    screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT + HUD_HEIGHT))
    pygame.display.set_caption("EntropyTag – Elemental Territory Control")
    clock = pygame.time.Clock()
    renderer = Renderer(screen)

    engine, ai_controllers, humans = build_game()
    # humans[0] = Ice (WASD), humans[1] = Water (IJKL), humans[2] = Fire (Xbox)

    # Try to grab the first connected joystick for Player 3
    joystick: pygame.joystick.JoystickType | None = None
    if pygame.joystick.get_count() > 0:
        joystick = pygame.joystick.Joystick(0)
        joystick.init()
        print(f"[Controller] Connected: {joystick.get_name()}")
    else:
        print("[Controller] No gamepad found – Player 3 (Fire) will use arrow keys + Enter as fallback")

    running = True
    while running:
        # ── events ────────────────────────────────────────────────
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False
            elif event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    running = False
                elif event.key == pygame.K_r:
                    engine, ai_controllers, humans = build_game()
                elif event.key == pygame.K_f:
                    engine.force_end()
            # Hot-plug controller
            elif event.type == pygame.JOYDEVICEADDED:
                if joystick is None:
                    joystick = pygame.joystick.Joystick(event.device_index)
                    joystick.init()
                    print(f"[Controller] Connected: {joystick.get_name()}")
            elif event.type == pygame.JOYDEVICEREMOVED:
                if joystick is not None and event.instance_id == joystick.get_instance_id():
                    joystick = None
                    print("[Controller] Disconnected")

        keys = pygame.key.get_pressed()

        # ── Player 1: Ice – WASD + Left Shift ────────────────────
        p1 = humans[0]
        p1.dx = 0.0
        p1.dy = 0.0
        if keys[pygame.K_w]:
            p1.dy -= 1.0
        if keys[pygame.K_s]:
            p1.dy += 1.0
        if keys[pygame.K_a]:
            p1.dx -= 1.0
        if keys[pygame.K_d]:
            p1.dx += 1.0
        p1.spraying = keys[pygame.K_LSHIFT] or keys[pygame.K_SPACE]

        # ── Player 2: Water – IJKL + Right Shift ─────────────────
        p2 = humans[1]
        p2.dx = 0.0
        p2.dy = 0.0
        if keys[pygame.K_i]:
            p2.dy -= 1.0
        if keys[pygame.K_k]:
            p2.dy += 1.0
        if keys[pygame.K_j]:
            p2.dx -= 1.0
        if keys[pygame.K_l]:
            p2.dx += 1.0
        p2.spraying = keys[pygame.K_RSHIFT]

        # ── Player 3: Fire – Xbox controller (or arrow keys fallback)
        p3 = humans[2]
        p3.dx = 0.0
        p3.dy = 0.0

        if joystick is not None:
            # Left stick (axis 0 = X, axis 1 = Y)
            lx = joystick.get_axis(0)
            ly = joystick.get_axis(1)
            if abs(lx) < STICK_DEADZONE:
                lx = 0.0
            if abs(ly) < STICK_DEADZONE:
                ly = 0.0
            p3.dx = lx
            p3.dy = ly

            # Spray: A button (button 0) or Right Trigger (axis 5 on Xbox)
            a_pressed = joystick.get_button(0)
            rt_pressed = False
            if joystick.get_numaxes() > 5:
                rt_pressed = joystick.get_axis(5) > TRIGGER_THRESHOLD
            elif joystick.get_numaxes() > 4:
                rt_pressed = joystick.get_axis(4) > TRIGGER_THRESHOLD
            p3.spraying = a_pressed or rt_pressed
        else:
            # Fallback: Arrow keys + Return/Enter
            if keys[pygame.K_UP]:
                p3.dy -= 1.0
            if keys[pygame.K_DOWN]:
                p3.dy += 1.0
            if keys[pygame.K_LEFT]:
                p3.dx -= 1.0
            if keys[pygame.K_RIGHT]:
                p3.dx += 1.0
            p3.spraying = keys[pygame.K_RETURN] or keys[pygame.K_KP_ENTER]

        # ── AI ────────────────────────────────────────────────────
        for ai in ai_controllers:
            ai.update(engine.game_map, engine.players)

        # ── simulation ────────────────────────────────────────────
        engine.tick()

        # ── render ────────────────────────────────────────────────
        renderer.draw(engine)
        clock.tick(FPS)

    pygame.quit()
    sys.exit()


if __name__ == "__main__":
    main()
