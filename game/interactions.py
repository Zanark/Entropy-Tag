"""
interactions.py - Element interaction rules.

Handles:
  1. What terrain state results when an element is sprayed on a tile.
  2. What debuff a player receives when hit by a countering element.
  3. Ownership resolution.
"""

from __future__ import annotations
from game.constants import Element, TerrainState
from game.player import DebuffType


# ─── Terrain Conversion Table ────────────────────────────────────
# Key:   (spray_element, current_terrain_state)
# Value: (new_terrain_state, new_owner_element)
#
# If a combo is not listed the terrain simply becomes the
# sprayed element's primary state.

TERRAIN_CONVERSION: dict[
    tuple[Element, TerrainState],
    tuple[TerrainState, Element],
] = {
    # Ice sprayed onto …
    (Element.ICE, TerrainState.WATER):   (TerrainState.FROZEN,  Element.ICE),
    (Element.ICE, TerrainState.PUDDLE):  (TerrainState.FROZEN,  Element.ICE),
    (Element.ICE, TerrainState.FIRE):    (TerrainState.MIST,    Element.NONE),
    (Element.ICE, TerrainState.MIST):    (TerrainState.ICE,     Element.ICE),

    # Water sprayed onto …
    (Element.WATER, TerrainState.FIRE):  (TerrainState.PUDDLE,  Element.WATER),
    (Element.WATER, TerrainState.ICE):   (TerrainState.PUDDLE,  Element.WATER),
    (Element.WATER, TerrainState.FROZEN):(TerrainState.WATER,   Element.WATER),
    (Element.WATER, TerrainState.MIST):  (TerrainState.WATER,   Element.WATER),

    # Fire sprayed onto …
    (Element.FIRE, TerrainState.ICE):    (TerrainState.MIST,    Element.FIRE),
    (Element.FIRE, TerrainState.FROZEN): (TerrainState.MIST,    Element.FIRE),
    (Element.FIRE, TerrainState.WATER):  (TerrainState.MIST,    Element.FIRE),
    (Element.FIRE, TerrainState.PUDDLE): (TerrainState.FIRE,    Element.FIRE),
    (Element.FIRE, TerrainState.MIST):   (TerrainState.FIRE,    Element.FIRE),
}

# Default terrain for each element when sprayed on neutral or own tiles
ELEMENT_PRIMARY_TERRAIN: dict[Element, TerrainState] = {
    Element.ICE:   TerrainState.ICE,
    Element.WATER: TerrainState.WATER,
    Element.FIRE:  TerrainState.FIRE,
}


def resolve_spray(spray_element: Element, current_state: TerrainState) -> tuple[TerrainState, Element]:
    """Return (new_state, new_owner) when *spray_element* hits a tile."""
    if current_state == TerrainState.DEAD:
        return (TerrainState.DEAD, Element.NONE)

    key = (spray_element, current_state)
    if key in TERRAIN_CONVERSION:
        return TERRAIN_CONVERSION[key]

    # Default: overwrite with own terrain
    return (ELEMENT_PRIMARY_TERRAIN[spray_element], spray_element)


# ─── Player-vs-Player Counter Logic ──────────────────────────────
# Key:   attacking_element
# Value: (victim_element, debuff_applied)
COUNTER_TABLE: dict[Element, tuple[Element, DebuffType]] = {
    Element.ICE:   (Element.WATER, DebuffType.FROZEN),   # Ice counters Water
    Element.WATER: (Element.FIRE,  DebuffType.SHRUNK),   # Water counters Fire
    Element.FIRE:  (Element.ICE,   DebuffType.SLUSHY),   # Fire counters Ice
}


def get_counter_debuff(attacker: Element, victim: Element) -> DebuffType | None:
    """If *attacker* element counters *victim* element, return the debuff.
    Otherwise return None."""
    entry = COUNTER_TABLE.get(attacker)
    if entry and entry[0] == victim:
        return entry[1]
    return None
