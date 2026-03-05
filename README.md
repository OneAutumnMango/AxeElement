# Blood — MageQuit Element Mod

Adds the **Blood** element to MageQuit, playable in all standard game modes.

## Spells

| Slot | Name | Description |
|------|------|-------------|
| Primary | **Rend** | Hurl a spinning blade forward. |
| Movement | **Lunge** | Step back and surge forward, striking all enemies in your path. Press again to chain up to 3 lunges. |
| Melee | **Bleed** | Slash enemies to open deep wounds. Bleeding targets take increased spell damage. Hitting spells refreshes the duration. |
| Secondary | **Wild Axes** | Unleash two axes that arc outward and converge back, piercing through all enemies in their path. |
| Defensive | **Riposte** | Brace for 1 second; if struck, vanish and reappear at your attacker, dealing 5 damage. |
| Utility | **Blade Storm** | Summon two spinning glaives to orbit you for 5 seconds, shredding nearby enemies. |
| Ultimate | **Sanguine Aura** | Drive your weapon into the ground, saturating the area with a blood field. Enemies inside bleed and slow; every wound you deal to bleeding foes restores your health. Spell persists if enemies remain inside. |

## Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx) for MageQuit if you haven't already.
2. Install the [MageQuit Mod Framework](https://github.com/magequit/MageQuitModFramework) dependency.
3. Drop the compiled `AxeElement.dll` into your `BepInEx/plugins` folder.

## Known Limitations

- **Bots do not work with the Blood element in the 2 Round Snake mode.**
They cannot pick spells if a blood spell is available, causing the game to hang.

## License

See [LICENSE](LICENSE).
