# unity-armor

A third-person action-combat prototype in Unity 6 (URP). You wear armor, swing a weapon, block, and roll — the usual souls-like vocabulary, but the interesting part is the plumbing underneath.

## How it's put together

**Equipment** splits three ways: a ScriptableObject holds the config, a State object is the single source of truth for what you're wearing, and a Renderer just listens for changes and rebuilds the mesh — it never owns state. Armor, weapons, and appearance share one generic base with the slot enum as a type parameter, so wiring the wrong pieces together won't compile.

**Damage** runs through a pipeline. `Health` only knows about HP; anything that reduces damage registers itself as a modifier and gets sorted by order:

```
DamageInfo → Dodge → Block → Armor → Stagger → HP
```

So players and enemies can share the exact same `Health`. The only difference is who registered what.

**Actions coordinate by polling, not flags.** Before starting, an action asks around: is anyone else using the body? Who drives movement right now? Can I swap gear? Nobody flips a bool on anyone else, so `PlayerController` genuinely has no idea that attacking or dodging exist.

Debug-only stuff — gizmos, testers — lives in a `Debug/` folder wrapped in `#if UNITY_EDITOR`.

## Working now

| System | What it does |
|---|---|
| **Damage foundation** | `DamageInfo`, `Health`, and the ordered modifier pipeline everything else plugs into |
| **Armor mitigation** | Two-phase curve — stacking armor gives diminishing returns rather than immunity |
| **Attack & hit detection** | Animation-driven hitbox windows, swept sampling so fast swings don't tunnel, allocation-free |
| **Stamina, block, dodge** | Parries, guard breaks, i-frames in the middle of a roll, and stamina that goes negative as its own punishment timer |

## Coming up

| System | What it'll do |
|---|---|
| **HUD** | Health and stamina bars, damage numbers, some feedback when a block actually lands |
| **Enemy AI** | Something that chases and hits back, reusing the same combat spine the player uses |
| **Carry & throw** | Pick up a crate, haul it around, throw it |

## Layout

`Character/` for movement and stamina, `Combat/` for the damage pipeline and action components, `Equipment/` for the armor/weapon/appearance trio, plus a Garden test scene.