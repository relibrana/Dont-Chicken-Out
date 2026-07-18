# DON'T CHICKEN OUT! — Production Roadmap

> *Where hesitation means defeat.*

**Developer:** Raymi Games  **·**  **Target Release:** July 2027  **·**  **Platforms:** PC & Console  **·**  **Players:** 2–4 (Local & Online)  **·**  **Genre:** Party · Action · Casual  **·**  **Audience:** 13–21

**Current milestone:** Vertical Slice (May 2026) → wrapping up · Alpha next.

*All content and features are subject to publisher strategy and guidance.*

---

## 1. At a Glance

A chaotic 2–4 player party game where Tetris-inspired blocks become the battlefield. Players climb, sabotage, and shove each other as the camera scrolls relentlessly upward. Quick reflexes, hilarious physics, and high streamability built in from day one.

This roadmap is **version-driven**: every milestone is a build the team — and the publisher — can play, demo, and measure. Each version is a stake in the ground, not a moving target.

| # | Milestone | Status | Target | What ships |
|---|-----------|--------|--------|------------|
| M1 | **Prototype** | ✅ Done | Oct 2025 – Apr 2026 | Core falling-block climb loop, jump/glide platformer, first sabotage interactions |
| M2 | **Vertical Slice** | 🟢 In progress | May 2026 | Polished single-map experience, 2–4 local players, item rotation, full feedback loop |
| M3 | **Alpha** | ⏳ Next | Aug – Sep 2026 | Feature-complete local build, 4P stable, demo-ready for publishers & showcases |
| M4 | **Beta** *(Closed Multiplayer Beta)* | 🔜 Planned | Feb – Mar 2027 | Online + Local 4-player matches (First-to-5), Skins, Skin Shop — closed beta build |
| M5 | **Launch** | 🎯 Target | **July 2027** | Custom Gameplay Settings, Multiple Maps, 3 Game Modes — PC & Console release |
| M6 | **Live Ops** | 🔁 Post-launch | Aug 2027 → | Additional modes, IP-collab cosmetics, cross-play expansion, seasonal content |

---

## 2. Milestone Breakdown

### M1 — Prototype  ·  ✅ Completed (Oct 2025 – Apr 2026)

**Vision check.** Prove the falling-block-meets-platformer loop actually plays like a party game and produces the "just one more round" feeling.

**What shipped**
- Core climb loop with auto-scrolling camera
- 2D platformer feel: jump buffer, coyote time, glide-fall, head-stomp
- First sabotage primitives (pushable blocks, basic kicks)
- Local 2-player input proof-of-concept

**Outcome.** The loop holds up. Greenlit Vertical Slice scope.

---

### M2 — Vertical Slice  ·  🟢 In progress (May 2026)

**The publisher-facing demo.** A short, polished, hilarious slice that *sells* the game in 5 minutes of hands-on play.

**What ships**
- One curated map with hand-tuned block pacing
- 2–4 local players (keyboard split + gamepads)
- Round-based match flow with live ranking (Winning / Neutral / Losing)
- Item rotation: **Bomb** (radius explosion), **Spring Disc**, **Item Capsules**, horizontal item spawners
- Camera auto-rise with acceleration curve
- Feather VFX on death · Cluck system · audio bed (music + SFX) · screen-shake feedback
- Functional main menu, scene transitions, pause flow

**Deliverables for the publisher**
- Playable build (Windows, controller-ready)
- 60-second sizzle trailer
- One-page fact sheet (already drafted in Follow-up Pitch)

**Success criteria**
- ≥ 80% of first-time playtesters request a rematch
- Average session ≥ 8 minutes unattended
- Build runs at locked 60 FPS on mid-range laptops

---

### M3 — Alpha  ·  ⏳ Aug – Sep 2026

**Feature-complete for the *local* experience.** Everything we want in the launch game except online and content volume. This is the build we tour with at showcases.

**What ships**
- Full local 4-player support, stable across keyboard + gamepad combinations
- Round-based match: **First-to-3 wins** (placeholder pacing for tuning before locking First-to-5 at Beta)
- Polished item roster (≥ 4 items, balanced sabotage vs. defense)
- Difficulty-scaled block pooling refined per-rank (winning players get harder paths)
- Main menu, pause, settings (audio, video, controls), credits
- First-pass localization scaffolding (EN base, ES-LATAM)
- Closed demo build distributable to publishers and selected creators

**Deliverables**
- Demo build (PC, signed, with telemetry hooks)
- Updated trailer with real footage
- Press kit v1

**Success criteria**
- Publisher demos require zero developer hand-holding
- Crash rate < 1 per 30 sessions in internal QA
- Streamer-ready: build can be live-streamed without spoilers or debug overlays

---

### M4 — Beta — Closed Multiplayer Beta  ·  🔜 Feb – Mar 2027

**The first time the world plays online.** Scoped to validate netcode, monetization plumbing, and player retention before we commit to platform certification.

**What ships**
- **Online Multiplayer — up to 4 players.** Match format: **First-to-5 rounds wins.**
- **Local Multiplayer — up to 4 players.** Same First-to-5 format.
- **Customizable Skins.** Cosmetic-only, no gameplay impact. Built on a modular character rig so future IP-collab drops can plug into the same system.
- **Skin Shop.** In-game store with cosmetic catalog, themed sets, and the rails for future content drops and collaborations.
- Account / profile system for cosmetic persistence
- Backend matchmaking + lobby
- Steam closed-beta deployment
- Telemetry: session length, churn point, item usage, win conditions

**Deliverables**
- Steam closed-beta access (keys for publisher + creator partners)
- Targeted Steam Next Fest presence (Feb 2027 window)
- Backend monitoring dashboards

**Success criteria**
- < 150 ms median input-to-display in 4-player online matches
- D1 retention ≥ 40% in closed beta cohort
- Shop conversion telemetry validates pricing assumptions

**Risk & dependency**
- Online implementation is the single biggest technical bet in production. Tech selection (Netcode for GameObjects / Mirror / Photon Fusion) locks in Q3 2026 to leave a 6-month integration runway.

---

### M5 — Launch  ·  🎯 July 2027

**Full commercial release on PC & Console.** Everything from Beta, plus the content volume needed to support a long-tail party game.

**What ships**
- **Custom Gameplay Settings.** Player-driven match modifiers: team counts, item pool, round count, special rule toggles, map rotations. The "House Rules" tooling teased in the original pitch.
- **Multiple Game Maps.** Several arenas, each with distinct block geometry and gameplay dynamics — *no two matches are ever the same.*
- **3 Additional Game Modes.** Beyond core climb-to-survive. Each mode reuses the same character and physics systems but reshapes the win condition. **Designed as a foundation — additional modes ship post-Launch.**
- Full localization (initial language set per publisher strategy)
- Console platform adaptation & certification (target platforms locked with publisher)
- Cosmetic catalog at launch scale
- Marketing & event participation (Steam Next Fest pre-launch window, influencer demo drops)

**Deliverables**
- Gold master (PC + console SKUs per publisher plan)
- Launch trailer
- Storefront pages live across platforms

**Success criteria**
- Concurrent 4-player online stable across all supported platforms
- Cert pass on console SKUs on first or second submission
- Day-1 critical issues: zero P0, < 5 P1

---

### M6 — Live Ops  ·  🔁 Aug 2027 onwards

**The party doesn't stop.** Built into the Beta architecture from day one so we ship updates on cadence, not by retrofitting.

**Planned content streams**
- **Additional game modes** beyond the launch three, on a recurring cadence
- **IP collaborations & crossovers** — cosmetic-only sets via the Skin Shop, hooks already in place from Beta
- **Seasonal events** tied to streamer-friendly moments
- **Cross-play expansion** across supported platforms
- **Community tournament tooling** — match presets, observer mode, replays
- DLCs, updates, and continued press & influencer coverage

---

## 3. Mapping to the Production Phases

For continuity with the Follow-up Pitch, here's how the version milestones land against the production phases shown in the original pitch deck:

| Production Phase (from Pitch) | Version Milestone(s) | Window |
|-------------------------------|-----------------------|--------|
| **Concept** — Vision, market research, GDD, supporting docs | (pre-Prototype) | H1 – Q3 2025 |
| **Pre-Production** — Playable prototype, vertical slice, core systems, internal testing, marketing setup, public showcases | M1 → M2 → Alpha feed | Oct 2025 → Sep 2026 |
| **Production** — Full content development, balancing, **online implementation**, QA, localization, console adaptation | M3 Alpha → M4 Beta → M5 Launch | Sep 2026 → Jun 2027 |
| **Pre-Launch** — Optimization, QA, localization, Steam Next Fest, influencer & demo releases | M4 Beta tail | Mar – Jun 2027 |
| **Launch** — PC & Console release, press coverage, storefront publishing, launch bundles | M5 Launch | July 2027 |
| **Support** — DLCs, updates, technical support, IP collaborations, cross-play | M6 Live Ops | Aug 2027 → |

---

## 4. Critical Path

The dependencies that gate the July 2027 release window, in order:

1. **Online netcode tech selection** (locks Q3 2026) — single largest technical decision; drives architecture for M4.
2. **Modular character rig for cosmetics** (during M3) — required so the Skin system at M4 and the IP-collab pipeline at M6 share one rig.
3. **Backend / account services** (M3 → M4) — needed before Beta launch; sets pricing rails for the Shop.
4. **Console kit access & first cert submission window** (mid M5) — publisher-driven; defines submission buffer before July 2027 gold.
5. **Localization pipeline** (active from M3) — accumulates content, ships at M5.
6. **Steam Next Fest slot** — target Feb 2027 (Beta reveal) and/or June 2027 (pre-Launch).

---

## 5. What We Need From the Publisher

Calibrated by milestone, so engagement scales with the work:

| Milestone | Publisher contribution |
|-----------|------------------------|
| M2 Vertical Slice | Build feedback, scope validation, fit assessment |
| M3 Alpha | Showcase placement guidance, demo distribution channels, early QA loop |
| M4 Beta | Backend infra recommendations, monetization strategy, closed-beta marketing, Steam Next Fest slot |
| M5 Launch | Console porting / cert support, localization (extended languages), full launch marketing, storefront management, influencer & press campaign, launch bundles |
| M6 Live Ops | IP collaboration brokering, sustained marketing, seasonal campaign support, cross-play certification |

---

## 6. Budget Anchor

Production scope tied to the **$125,000 USD** budget shown in the Follow-up Pitch (76.9% team, 19.2% contingency, 3.8% tools & licenses). Online infrastructure, console certification fees, and extended localization are scoped against publisher strategy and not bundled into this figure.

---

## Contact

**Raymi Games**
📧 raymigames.studio@gmail.com
📱 @raymi.games

*The chaos makers of Don't Chicken Out!*
