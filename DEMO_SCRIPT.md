# Live Demo Script

Target length: ~7–9 minutes core demo, +2–3 minutes optional material if time
allows. Practice this at least once end-to-end beforehand — some steps (SLAM
sync, house completion) depend on agents actually wandering into position,
which takes real time.

## Before you present

- [ ] Open **`TerrainTestScene`** (not `SampleScene` — that's the older
      level editor, not the simulation).
- [ ] Confirm the scene has `SlamCoordinator`, `AgentOverlayUI`,
      `SimulationUIBootstrap`, and `SimulationGameplayBridge` all present on
      some GameObject (check the Hierarchy or Inspector before Play).
- [ ] Console window: turn **off** "Error Pause" (the stop-sign icon next to
      the search bar) so a stray warning can't freeze Play mode mid-demo.
- [ ] Do one full dry-run beforehand so you've seen the map layout once and
      aren't surprised by anything.
- [ ] Know where `HouseConstructionSite`'s wood/stone requirements are set
      (Inspector) in case you want to lower them for a faster demo — full
      requirements can take a while to complete live.

---

## 1. Setup & framing (30s)

> "This is a survival simulation where a group of agents — each with a
> different physical impairment — has to gather wood, stone, and food to
> build a house together. The interesting part isn't the survival loop
> itself, it's *how* the agents decide what to do: their planning (GOAP),
> pathfinding (weighted A*), and world knowledge (SLAM) all run in a native
> C++ plugin linked into Unity, not scripted behavior."

Press **Play**.

## 2. Terrain generation (30–45s)

While the terrain builds:

> "The terrain — the river network and the connecting paths — is generated
> with a genuine Prim's-algorithm minimum spanning tree, twice: once to lay
> out the river/lake system, once to connect the camp, build site, and
> resource clusters with a low-cost path network. It's randomized every run
> but guaranteed to be one connected map — a flood-fill pass after
> generation finds and bridges any pocket that would otherwise be cut off."

Point out: the dirt paths connecting key areas (that's the Prim's path
network), and if visible, a bridge crossing the river.

## 3. Impairments — visually obvious (45s)

Zoom/pan to a cluster of agents.

> "Three roles, same action set and goals — no agent is locked out of any
> task. The differences are entirely in cost and constraint:"

- **Blue, flattened, wheel disc underneath** — wheelchair-bound. Point out
  the thin ring around it is *small-to-medium* — normal sight, but it's
  slower overall and physically can't cross rubble tiles near stone
  deposits (point one out if visible).
- **Red, dark blindfold band** — blind. Its sight ring is *tiny* (1 tile) —
  point out how much smaller it is than the others. Relies on hearing food
  from far away instead.
- **Orange, ear-cover spheres** — deaf. Widest sight ring of the three, and
  ignores sound-based path costs (harder to show live, mention it).

> "Those rings aren't decoration — they're each agent's actual native sight
> radius, to scale."

## 4. The overlay — GOAP made visible (45s)

Point at the top-left panel.

> "Every agent's current action here is the live output of a real GOAP
> planner running in C++ — a forward search over a world-state graph, not a
> switch statement. It re-plans every decision tick based on hunger, thirst,
> fatigue, and what that specific agent currently knows."

Scroll the list, point out a couple of different current actions ("Chopping
wood" / "Heading to build site" / "Resting"). Point out the Food/Water/
Fatigue bars filling over time.

## 5. SLAM — click an agent (60–90s)

Click a row in the overlay (or click an agent directly).

> "This selects that agent and locks the camera to it. Watch what happens
> around it —"

Point out:
- **Glowing beacons** appearing/disappearing over resource tiles — "these
  only light up for tiles *this specific agent* has personally discovered.
  Select a different agent —" (click another row) "— and you'll see a
  different set light up. That's SLAM: each agent maps the world for
  itself."
- **The dark overlay across the terrain** (fog-of-war) — "dark means
  undiscovered by this agent; it clears as they explore. This is a direct
  visualization of that agent's actual memory state in the native plugin,
  pulled with one call every quarter-second."
- If two agents happen to be near each other: **the green flash line** —
  "that's the moment their memories merge — SLAM syncing between
  teammates. A blind agent that's never seen the wood cluster can get
  routed there once a teammate who has walks past it."

Click "Free Camera" (or click the same agent again) to release the lock.

## 6. Weighted A* + group coordination (45s)

> "Pathfinding itself is 8-directional A*, but every tile carries a movement
> weight — the path network from Prim's algorithm is weighted cheaper, so
> agents that know about it will detour onto it rather than cut overland
> even if it's not the shortest tile count. And when two agents both know
> about the same tree, only one of them claims it — there's a native
> reservation system so they split up instead of racing each other."

If two agents are visibly headed to different trees in the same cluster,
point that out as the reservation system in action.

## 7. House progress (30s)

Pan to the build site.

> "Wood and stone get delivered here — the site progresses through
> scaffolding, half-built, and finished stages as the combined total
> crosses thresholds." Point at the wood/stone bars (either the world-space
> ones above the site, or the "House Construction" section in the top-left
> panel — the second one is the reliable one to point at).

## 8. Simulation controls (30–45s, optional if short on time)

Open the top-right collapsible panel ("Settings ▾" tab).

> "This panel drives the whole simulation — Random Map regenerates
> everything through the same Prim's pipeline, Restart resets the current
> map, and you can pause, change speed, create or kill an agent, or reassign
> an agent's role on the fly." Demonstrate one (Pause is the safest/fastest
> to show).

## 9. Close (15s)

> "So: real GOAP planning and real SLAM memory both running natively in
> C++, weighted A* tying into a genuine Prim's-algorithm path network, and
> impairments that change *how* an agent solves the problem rather than
> what it's allowed to do."

---

## If you have extra time / get asked to go deeper

- Let an agent's hunger or thirst run out — show the **red X** death marker
  (it stays visible as a corpse rather than disappearing).
- Force-kill an agent via the settings panel's Kill button, same effect.
- Open the Console briefly to show `[Construction]`/`[GridCoordinator]` logs
  confirming things are actually happening, not just visual.
- If asked "show me the code" for something specific, know the map (see
  `README.md`'s "Where things live" section) rather than hunting live.
