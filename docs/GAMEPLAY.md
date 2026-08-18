# Gameplay and Rules

## Match setup

EntropyTag creates six players in a fixed order:

| ID | Team | Control | Spawn tile |
|---:|---|---|---|
| 0 | Ice | Human | `(5, 5)` |
| 1 | Ice | Bot | `(10, 8)` |
| 2 | Water | Human | `(58, 42)` |
| 3 | Water | Bot | `(53, 39)` |
| 4 | Fire | Human | `(32, 5)` |
| 5 | Fire | Bot | `(36, 8)` |

All 3,072 arena tiles begin neutral. Players begin at tile centers and initially face right.

```mermaid
flowchart TB
    accTitle: Team and player composition
    accDescr: Each of the Ice, Water, and Fire teams contains one human player and one bot player.

    match[EntropyTag match]
    ice[Ice team]
    water[Water team]
    fire[Fire team]
    iceHuman[Human P1]
    iceBot[Bot ID 1]
    waterHuman[Human P2]
    waterBot[Bot ID 3]
    fireHuman[Human P3]
    fireBot[Bot ID 5]

    match --> ice
    match --> water
    match --> fire
    ice --> iceHuman
    ice --> iceBot
    water --> waterHuman
    water --> waterBot
    fire --> fireHuman
    fire --> fireBot
```

## Movement

Base movement speed is 3 pixels per frame. Direction input is normalized, so diagonal movement does not provide
additional speed.

### Terrain speed modifiers

| Player | Terrain | Multiplier |
|---|---|---:|
| Ice | Ice | 1.40x |
| Ice | Fire | 0.70x |
| Ice | Puddle | 0.85x |
| Ice | Frozen | 1.20x |
| Water | Water | 1.40x |
| Water | Ice | 0.70x |
| Water | Frozen | 0.60x |
| Water | Puddle | 1.20x |
| Fire | Fire | 1.40x |
| Fire | Water | 0.70x |
| Fire | Mist | 1.00x |

All unspecified combinations use a 1.00x multiplier.

> Fire on Mist is currently defined twice in the source. Python keeps the later 1.00x value and discards the
> earlier 0.85x value.

## Spraying

- Base range: four tiles.
- Nominal width: five tiles.
- Cooldown: five frames between spray updates.
- Direction: the player's current facing angle.
- A stationary player continues spraying in its last facing direction.
- Dead tiles outside the ring cannot be converted.

When a spray changes a tile from another owner or neutral ownership to a team owner, that team receives one
bank point.

```mermaid
flowchart TD
    accTitle: Spray processing flow
    accDescr: The engine checks spray readiness, projects tiles from player facing, skips invalid tiles, resolves elemental conversion, and awards bank points for ownership changes.

    intent{Spray held and cooldown ready?}
    project[Project range and width from facing]
    valid{Tile inside ring and alive?}
    resolve[Resolve elemental conversion]
    changed{New team owner differs?}
    bank[Award one bank point]
    cooldown[Reset five-frame cooldown]
    skip[Skip tile or player]

    intent -->|No| skip
    intent -->|Yes| project --> valid
    valid -->|No| skip
    valid -->|Yes| resolve --> changed
    changed -->|Yes| bank --> cooldown
    changed -->|No| cooldown
```

## Terrain conversion table

Unlisted combinations become the spraying team's primary terrain and ownership.

| Spray | Existing terrain | Result | Owner |
|---|---|---|---|
| Ice | Water | Frozen | Ice |
| Ice | Puddle | Frozen | Ice |
| Ice | Fire | Mist | None |
| Ice | Mist | Ice | Ice |
| Water | Fire | Puddle | Water |
| Water | Ice | Puddle | Water |
| Water | Frozen | Water | Water |
| Water | Mist | Water | Water |
| Fire | Ice | Mist | Fire |
| Fire | Frozen | Mist | Fire |
| Fire | Water | Mist | Fire |
| Fire | Puddle | Fire | Fire |
| Fire | Mist | Fire | Fire |

Primary terrain defaults:

- Ice spray creates Ice.
- Water spray creates Water.
- Fire spray creates Fire.

```mermaid
stateDiagram-v2
    accTitle: Elemental terrain reaction network
    accDescr: State transitions show the explicit non-default reactions caused by Ice, Water, and Fire sprays.

    Water --> Frozen: Ice spray
    Puddle --> Frozen: Ice spray
    Fire --> Mist: Ice spray
    Mist --> Ice: Ice spray

    Fire --> Puddle: Water spray
    Ice --> Puddle: Water spray
    Frozen --> Water: Water spray
    Mist --> Water: Water spray

    Ice --> Mist: Fire spray
    Frozen --> Mist: Fire spray
    Water --> Mist: Fire spray
    Puddle --> Fire: Fire spray
    Mist --> Fire: Fire spray
```

```mermaid
flowchart LR
    accTitle: Default terrain ownership rule
    accDescr: Any terrain combination not in the explicit reaction table becomes the spraying team's primary terrain and ownership.

    unknown[Combination not listed]
    element{Spraying element}
    ice[Ice terrain owned by Ice]
    water[Water terrain owned by Water]
    fire[Fire terrain owned by Fire]

    unknown --> element
    element -->|Ice| ice
    element -->|Water| water
    element -->|Fire| fire
```

## Player counters and debuffs

Opposing players within 20 pixels can receive a debuff when the countering player is spraying.

| Attacker | Victim | Debuff | Duration | Effect |
|---|---|---|---:|---|
| Ice | Water | Frozen | 120 frames | Movement stops |
| Water | Fire | Shrunk | 180 frames | 0.80x speed, half radius, half spray range |
| Fire | Ice | Slushy | 150 frames | 0.55x speed |

An active debuff is not refreshed or replaced. The proximity check currently does not consider spray direction
or whether the visual spray geometry reaches the victim.

```mermaid
flowchart LR
    accTitle: Counter debuffs and effects
    accDescr: The elemental counter cycle maps each attacker and victim pairing to its temporary gameplay effect.

    ice[Ice sprays Water] --> frozen[Frozen<br/>120 frames]
    frozen --> frozenEffect[Movement disabled]

    water[Water sprays Fire] --> shrunk[Shrunk<br/>180 frames]
    shrunk --> shrunkEffect[Smaller, slower, shorter range]

    fire[Fire sprays Ice] --> slushy[Slushy<br/>150 frames]
    slushy --> slushyEffect[Speed reduced to 55 percent]
```

## Bots

Each team has one bot. Bots reevaluate their objective every 30 frames and always spray.

Decision probabilities:

- 30% wander to a random point inside the ring.
- 42% seek a sampled tile not owned by their team.
- 28% chase the nearest opposing player.

AI randomness is not seeded, so matches are nondeterministic.

```mermaid
flowchart TD
    accTitle: Bot decision process
    accDescr: Every thirty frames a bot may wander, seek territory, or chase the nearest enemy, then steers toward its target while spraying.

    update[AI update]
    ready{Decision timer expired?}
    wanderRoll{Thirty percent wander?}
    actionRoll{Territory roll succeeds?}
    wander[Choose random in-ring target]
    territory[Choose nearest sampled non-owned tile]
    chase[Choose nearest living enemy]
    steer[Steer toward target]
    spray[Always spray]

    update --> ready
    ready -->|No| steer
    ready -->|Yes| wanderRoll
    wanderRoll -->|Yes| wander --> steer
    wanderRoll -->|No| actionRoll
    actionRoll -->|Yes| territory --> steer
    actionRoll -->|No| chase --> steer
    steer --> spray
```

## Shrinking ring

- The ring shrinks every 600 frames, approximately ten seconds at 60 FPS.
- Each successful shrink removes two tile rows or columns from every edge.
- Removed tiles become Dead and lose ownership.
- Players outside the new boundary are moved inside rather than eliminated.
- Eleven successful shrinks leave a 20x4 live arena.
- The next shrink attempt fails at frame 7,200 and ends the match.

The resulting intended match duration is approximately 120 seconds.

```mermaid
flowchart LR
    accTitle: Ring shrink timeline
    accDescr: The arena begins at 64 by 48 tiles, shrinks every six hundred frames, reaches 20 by 4 after eleven successful shrinks, and ends on the next failed shrink.

    start[Frame 0<br/>64 by 48]
    first[Frame 600<br/>60 by 44]
    middle[Repeated ten-second shrinks]
    last[Frame 6600<br/>20 by 4]
    finish[Frame 7200<br/>No smaller ring fits]
    resolve([Resolve winner])

    start --> first --> middle --> last --> finish --> resolve
```

## Winner and bank scoring

At game end:

1. Count each team's owned tiles within the final live ring.
2. Convert those counts to percentages of all living tiles, including neutral tiles.
3. Select the team with the highest percentage.
4. If teams are within less than one percentage point, select the tied team with the highest bank.

The HUD displays bank points, but the intended bank-based spread bonus is not currently connected to gameplay.

```mermaid
flowchart TD
    accTitle: Winner resolution
    accDescr: Final living tiles produce team percentages, the highest percentage wins unless teams are within one point, in which case bank score breaks the tie.

    stop([Ring cannot shrink])
    count[Count owned living tiles]
    pct[Calculate percentages including neutral]
    top[Find highest team percentage]
    tied{Other team within one point?}
    direct[Select territory leader]
    bank[Select tied team with highest bank]
    result([Set winner and show overlay])

    stop --> count --> pct --> top --> tied
    tied -->|No| direct --> result
    tied -->|Yes| bank --> result
```

## Current gameplay limitations

- Players cannot die or respawn.
- There is no active early-win or dominance condition.
- A forced end on a completely neutral board incorrectly declares Ice the winner.
- Player debuff collision handling depends on player list order.
- Tile coating strength exists in data but has no gameplay effect.
