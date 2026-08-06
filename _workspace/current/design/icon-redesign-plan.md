# HongT Icon Redesign & Shader Enhancement Plan

## Goal
All 29 icons in Cinder Court redesigned with unified "Abyssal Lantern" aesthetic (dark, fire/oil theme) + URP shader effects (shadow, glow, depth).

## Icon Categories

### UI Control Icons (7 icons) ✅ GENERATED
- `ui-pause.png` — pause button
- `ui-play.png` — play button  
- `ui-restart.png` — restart/reset
- `ui-settings.png` — settings gear
- `skill-cooldown-ring.png` — circular progress
- `skill-highlight.png` — selection frame
- `stat-oil-energy.png` — oil/energy drop

### Skill Icons (7 icons) — TO REGENERATE
1. **skill-nova.png** — Ember Nova (radial explosion, orange/fire)
2. **skill-ward.png** — Lantern Ward (protective shield, blue/ethereal)
3. **skill-bolt.png** — Rift Bolt (lightning strike, purple)
4. **skill-pulse.png** — Grave Pulse (wave emanation, dark blue)
5. **skill-dash.png** — Dash/Rekindle (swift motion, gold/orange)
6. **skill-strike.png** — Strike/Attack (melee, red/orange)
7. **skill-aegis.png** — Void Aegis (defense, dark purple)

### Equipment Icons (3 icons) — TO REGENERATE
1. **equip-weapon.png** — Warden's weapon (sword/staff, metallic)
2. **equip-cloak.png** — Dark cloak (robe, cloth texture)
3. **equip-lantern.png** — Lantern (light source, warm glow)

### Pickup/Drop Icons (3 icons) — TO REGENERATE
1. **pickup-ember.png** — Ember shard (+HP, red/orange)
2. **pickup-flask.png** — Oil flask (+energy, amber/gold)
3. **pickup-relic.png** — Relic mote (+score, mystical glow)

### Stat Icons (3 icons) — TO REGENERATE
1. **stat-vitality.png** — Vitality/HP (heart, red)
2. **stat-swiftness.png** — Movement speed (wings, blue)
3. **stat-attack.png** — Attack power (sword, orange)

### UI Interaction Icons (4 icons) — TO REGENERATE  
1. **ui-button.png** — Default button state
2. **ui-button-active.png** — Pressed/hover state
3. **ui-button-disabled.png** — Disabled state
4. **ui-joystick-base.png** — Joystick background
5. **ui-joystick-nub.png** — Joystick thumb

### App Icons (1 icon) — TO REGENERATE
1. **app-lantern.png** — App icon (lantern symbol)

---

## Visual Style Guide

### Color Palette
| Category | Primary | Secondary | Accent |
|----------|---------|-----------|--------|
| Warmth (Fire/Oil) | #FF6B35 (Orange) | #FF4500 (Dark Orange) | #FFD700 (Gold) |
| Cold (Void/Magic) | #4B0082 (Indigo) | #6A0572 (Deep Purple) | #1E90FF (Dodger Blue) |
| Neutral (UI) | #1F1F1F (Dark Gray) | #404040 (Medium Gray) | #CCCCCC (Light Gray) |
| Glow | #FFB347 (Peach) | #87CEEB (Sky Blue) | #00FF7F (Spring Green) |

### Visual Elements
1. **Background**: Dark (nearly black) with subtle vignette
2. **Main shape**: Clean, bold silhouette (256×256)
3. **Glow ring**: 1-2px outer glow using warm/cold accent color
4. **Depth**: Subtle shadow (1px drop, 30% opacity)
5. **Texture**: Minimal — no noise, max clarity

### Prompts Template

"<icon_name> icon for fantasy game, <description>, 
dark background with subtle glow effect, minimalist style, 
256x256 square, professional gaming ui"


---

## Shader Enhancements (URP)

### Shader: `UI-Icon-Glow.shader` (NEW)
Features:
- **Base color** from texture
- **Glow intensity** slider (0-2)
- **Glow color** multiplier (separate from base)
- **Shadow depth** (soft drop shadow, 1-2px offset)
- **Outer rim glow** (Fresnel-based)

### Material Setup
- Create `Assets/Materials/UIIcon-GlowMaterial.mat`
- Apply to all icon Image components
- Tweak glow intensity per category

---

## Timeline

1. **Phase 1**: Generate 22 new icons (skills, equipment, pickups, stats, UI)
   - Batch 1: Skills (7 icons)
   - Batch 2: Equipment + Pickups (6 icons)  
   - Batch 3: Stats + UI (9 icons)
   - Tool: `gti --provider codex-cli`
   - Time: ~2h (rate-limited)

2. **Phase 2**: Create URP shader + material
   - Write `UI-Icon-Glow.shader`
   - Create material + prefab
   - Test on 3 sample icons
   - Time: ~30m

3. **Phase 3**: Apply shader to all icons
   - Batch import to Resources/Icons/
   - Apply material to HUD Image components
   - A/B test glow intensity
   - Time: ~30m

4. **Phase 4**: Provenance + WebGL verification
   - Document all prompts + sessions
   - Build WebGL test
   - Verify texture performance (<1024 constraint)
   - Time: ~20m

---

## Risk & Mitigation

| Risk | Mitigation |
|------|------------|
| Rate limit hit | Use `--provider codex-cli`, batch in 3 phases |
| Inconsistent style | Create detailed prompt + reference image |
| Shader performance | Bake glow into texture if needed, test on WebGL |
| Icon color clash | Preview in-game with actual backgrounds |

---

## Next Step
Generate **Batch 1 (Skills)** using gti with provided prompts.
