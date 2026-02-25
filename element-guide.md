# Guide: Adding a New Element to MageQuit

This guide walks through every step required to add a new element (e.g., "Shadow") to the game.

---

## 1. Add the Element to the `Element` Enum

**File:** [Assembly-CSharp/Element.cs](Assembly-CSharp/Element.cs)

The [`Element`](Assembly-CSharp/Element.cs) enum defines all elements. Add your new element before `Tutorial`:

```csharp
public enum Element
{
    None,
    Fire,
    Water,
    Air,
    Earth,
    Sand,
    Nature,
    Electric,
    Steam,
    Metal,
    Ice,
    Shadow,    // <-- NEW
    Tutorial
}
```

> **Important:** The integer value matters. Many arrays throughout the codebase are indexed by `(int)element`. Adding it before `Tutorial` means `Shadow = 11` and `Tutorial = 12`. You must update all fixed-size arrays that were sized for 11 elements (indices 0–10) to now accommodate index 11.

---

## 2. Add Spell Names to the `SpellName` Enum

**File:** [Assembly-CSharp/SpellName.cs](Assembly-CSharp/SpellName.cs)

Each spell has an entry in [`SpellName`](Assembly-CSharp/SpellName.cs). You need one spell per slot. The spell slots (from [`SpellButton`](Assembly-CSharp/InputBase.cs)) are:

| Slot | Example (Fire) |
|------|----------------|
| Primary | Fireball |
| Movement | FlameLeap |
| Melee | Ignite |
| Secondary | Scorch |
| Defensive | PillarOfFire |
| Utility | FlameLeash |
| Ultimate | Spitfire |

Add 7 new entries:

```csharp
public enum SpellName
{
    // ...existing entries...
    ShadowBolt,       // Primary
    ShadowStep,       // Movement
    ShadowStrike,     // Melee
    ShadowOrb,        // Secondary
    ShadowShield,     // Defensive
    ShadowBind,       // Utility
    ShadowStorm,      // Ultimate
}
```

---

## 3. Create Spell Classes (one per spell)

Each spell needs two classes:

### 3a. The `Spell` Subclass (spell definition / initializer)

This is analogous to [`Fireball`](Assembly-CSharp/Fireball.cs), [`Rockshot`](Assembly-CSharp/Rockshot.cs), [`PetRock`](Assembly-CSharp/PetRock.cs), etc.

**Create file:** `Assembly-CSharp/ShadowBolt.cs`

```csharp
using System;
using UnityEngine;

public class ShadowBolt : Spell
{
    public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
        float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
    {
        GameUtility.Instantiate("Objects/ShadowBolt", position + Spell.skillshotOffset, rotation, 0)
            .GetComponent<ShadowBoltObject>()
            .Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
    }

    public override Vector3? GetAiAim(TargetComponent targetComponent, Vector3 position,
        Vector3 target, SpellUses use, ref float curve, int owner)
    {
        return base.GetAiAim(targetComponent, position, target, use, ref curve, owner);
    }

    public override float GetAiRefresh(int owner)
    {
        return base.GetAiRefresh(owner);
    }
}
```

### 3b. The `SpellObject` Subclass (runtime behavior)

This is analogous to [`FireballObject`](Assembly-CSharp/FireballObject.cs), [`RockshotObject`](Assembly-CSharp/RockshotObject.cs), [`SparkObject`](Assembly-CSharp/SparkObject.cs), etc.

**Create file:** `Assembly-CSharp/ShadowBoltObject.cs`

```csharp
using System;
using UnityEngine;

public class ShadowBoltObject : SpellObject
{
    public ShadowBoltObject()
    {
        // Tune these values for balance
        this.DAMAGE = 10f;    // See FireballObject (10), RockshotObject (12), SparkObject (13)
        this.RADIUS = 4f;     // See FireballObject (4), RockshotObject (4), SparkObject (5)
        this.POWER = 40f;     // Knockback force. See FireballObject (40), RockshotObject (45)
        this.Y_POWER = 0f;    // Upward knockback bias
        this.START_TIME = 1f; // Lifetime in seconds
    }

    private void Start()
    {
        this.phys = base.GetComponent<PhysicsBody>();
        this.sp.PlaySoundComponentInstantiate("event:/sfx/shadow/shadowbolt-start", 5f);
        this.SpellObjectStart();
    }

    public void Init(int owner, float curve, float velocity)
    {
        this.id.owner = owner;
        this.curve = curve;
        this.velocity = velocity;
        base.ChangeToSpellLayerDelayed(velocity);
        if (base.photonView != null && base.photonView.isMine)
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.Others, new object[]
            {
                owner,
                base.transform.position,
                base.transform.rotation,
                curve,
                velocity
            });
        }
    }

    [PunRPC]
    public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, float curve, float velocity)
    {
        this.id.owner = owner;
        base.transform.position = pos;
        base.transform.rotation = rot;
        this.curve = curve;
        this.velocity = velocity;
    }

    private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        this.BaseSerialize(stream, info);
    }

    [PunRPC]
    public void rpcCollision(Vector3 pos)
    {
        UnityEngine.Object.Instantiate(this.impact, pos, Globals.sideways);
        this.sp.PlaySoundInstantiate("event:/sfx/shadow/shadowbolt-hit", 5f);
    }

    public UnityEngine.Object impact;
    private PhysicsBody phys;
}
```

> **Repeat** this pattern for all 7 spells (ShadowStep, ShadowStrike, ShadowOrb, ShadowShield, ShadowBind, ShadowStorm). Reference existing spells of the same slot for structure:
> - Movement: [`FlameLeapObject`](Assembly-CSharp/FlameLeapObject.cs)
> - Melee: [`CompoundObject`](Assembly-CSharp/CompoundObject.cs) / [`SustainObject`](Assembly-CSharp/SustainObject.cs)
> - Secondary: [`PetRockObject`](Assembly-CSharp/PetRockObject.cs) / [`BoomerangObject`](Assembly-CSharp/BoomerangObject.cs)
> - Defensive: [`PillarOfFireObject`](Assembly-CSharp/PillarOfFireObject.cs)
> - Utility: [`WaterCannonObject`](Assembly-CSharp/WaterCannonObject.cs) / [`IceHookObject`](Assembly-CSharp/IceHookObject.cs)
> - Ultimate: [`TsunamiObject`](Assembly-CSharp/TsunamiObject.cs) / [`JetStreamObject`](Assembly-CSharp/JetStreamObject.cs)

---

## 4. Register Spells in the Spell Manager

**File:** [Assembly-CSharp/SpellManager.cs](Assembly-CSharp/SpellManager.cs)

### 4a. Add to `spell_table`

The `spell_table` dictionary maps `SpellName` → `Spell`. Each spell prefab must be registered. This is typically done via Unity's serialized fields or in code. Ensure all 7 new spells are added.

### 4b. Add to AI Draft Priority

The [`ai_draft_priority`](Assembly-CSharp/SpellManager.cs) dictionary tells bots which spells to prefer per slot. Add your new spells:

```csharp
// In SpellButton.Primary list:
SpellName.ShadowBolt,

// In SpellButton.Movement list:
SpellName.ShadowStep,

// In SpellButton.Melee list:
SpellName.ShadowStrike,

// In SpellButton.Secondary list:
SpellName.ShadowOrb,

// In SpellButton.Defensive list:
SpellName.ShadowShield,

// In SpellButton.Utility list:
SpellName.ShadowBind,

// In SpellButton.Ultimate list:
SpellName.ShadowStorm,
```

---

## 5. Update the Element Unlock Order

**File:** [Assembly-CSharp/AvailableElements.cs](Assembly-CSharp/AvailableElements.cs)

The [`unlockOrder`](Assembly-CSharp/AvailableElements.cs) array determines which elements are available and in what order they unlock:

```csharp
public static Element[] unlockOrder = new Element[]
{
    Element.Fire,
    Element.Water,
    Element.Air,
    Element.Earth,
    Element.Nature,
    Element.Electric,
    Element.Sand,
    Element.Steam,
    Element.Metal,
    Element.Ice,
    Element.Shadow   // <-- NEW
};
```

---

## 6. Update Game Settings

**File:** [Assembly-CSharp/GameSettings.cs](Assembly-CSharp/GameSettings.cs)

The [`elements`](Assembly-CSharp/GameSettings.cs) array is sized to the number of elements. Increase its size:

```csharp
public ElementInclusionMode[] elements = new ElementInclusionMode[11]; // was 10
```

---

## 7. Update Element Color / Audio Mappings

### 7a. Element Colors

**File:** [Assembly-CSharp/ElementColorMapping.cs](Assembly-CSharp/ElementColorMapping.cs)

Add entries for your element in all color arrays (vignette colors, icon emission colors, etc.):

```csharp
// Add a new entry at index 11 in every Color[] array in this class
// Example for vignetteColors:
new Color(0.2f, 0.0f, 0.3f),  // Shadow purple
```

### 7b. Ambient Sound

In the same file, add the ambient sound path:

```csharp
"event:/sfx/ambience/shadow"
```

### 7c. Cast Sounds

**File:** [Assembly-CSharp/SpellHandler.cs](Assembly-CSharp/SpellHandler.cs)

Add to the [`castSounds`](Assembly-CSharp/SpellHandler.cs) dictionary:

```csharp
{
    Element.Shadow,
    "event:/sfx/wizard/spell-attack"
},
```

### 7d. Icon Emission Colors

**File:** Check `Globals.iconEmissionColors` — it's an array indexed by `(int)Element`. Add an entry at index 11.

---

## 8. Update AI / Bot Logic

**File:** [Assembly-CSharp/SpellComponent.cs](Assembly-CSharp/SpellComponent.cs)

The [`CheckForBadSportsSpells`](Assembly-CSharp/SpellComponent.cs) method and other AI methods reference specific spell names. If any of your shadow spells should be restricted in certain game modes (like Sports), add them here.

**File:** [Assembly-CSharp/AiController.cs](Assembly-CSharp/AiController.cs)

Ensure bots can properly aim/use your new spells. Override `GetAiAim` and `GetAiRefresh` in each Spell subclass as needed.

---

## 9. Create Unity Prefabs

For each spell object, you need a Unity prefab in the Resources folder:

| Prefab Path | Component |
|-------------|-----------|
| `Resources/Objects/ShadowBolt` | `ShadowBoltObject` |
| `Resources/Objects/ShadowStep` | `ShadowStepObject` |
| `Resources/Objects/ShadowStrike` | `ShadowStrikeObject` |
| `Resources/Objects/ShadowOrb` | `ShadowOrbObject` |
| `Resources/Objects/ShadowShield` | `ShadowShieldObject` |
| `Resources/Objects/ShadowBind` | `ShadowBindObject` |
| `Resources/Objects/ShadowStorm` | `ShadowStormObject` |

Each prefab needs:
- The SpellObject script component
- A `PhotonView` component (for networking)
- An `Identity` component
- A `SoundPlayer` component
- A `PhysicsBody` component (if it moves)
- Visual mesh/particle effects
- Colliders for hit detection
- Impact prefab reference (explosion/hit effect)

You also need a **Spell definition prefab** for each spell with the `Spell` subclass attached, configured with:
- `spellName` — matching [`SpellName`](Assembly-CSharp/SpellName.cs) enum value
- `element` — set to `Element.Shadow`
- `spellButton` — the correct [`SpellButton`](Assembly-CSharp/InputBase.cs) slot
- `icon` — a `Sprite` for the UI
- `cooldown`, `windUp`, `windDown` — timing values
- `curveMultiplier`, `initialVelocity` — projectile tuning
- `description` — tooltip text
- `video` — (optional) preview video clip

---

## 10. Create Audio Events (FMOD)

The game uses FMOD for audio. Create these events in your FMOD project:

- `event:/sfx/shadow/shadowbolt-start`
- `event:/sfx/shadow/shadowbolt-hit`
- `event:/sfx/ambience/shadow`
- (one start + hit sound per spell)

---

## 11. Create UI Assets

- **Spell Icons:** One `Sprite` per spell (7 total) for the HUD
- **Element Icon:** One icon for the element selection screen (used in [`AvailableElements`](Assembly-CSharp/AvailableElements.cs))
- **Element Lock Icon:** Shown when the element is locked

---

## 12. Update Tutorials and Practice Mode

**File:** [Assembly-CSharp/TutorialManager.cs](Assembly-CSharp/TutorialManager.cs)

The [`Globals.selected_elements`](Assembly-CSharp/TutorialManager.cs) array hard-codes elements for tutorial. Update if Shadow should appear.

**File:** [Assembly-CSharp/PracticeRangeManager.cs](Assembly-CSharp/PracticeRangeManager.cs)

The [`LearnAbilities`](Assembly-CSharp/PracticeRangeManager.cs) method uses `AvailableElements.unlockOrder` to assign spells — this should work automatically once step 5 is done.

---

## 13. Update Default Player Selection (Late Join)

**File:** [Assembly-CSharp/DefaultPlayerSelection.cs](Assembly-CSharp/DefaultPlayerSelection.cs)

The random element assignment in [`Start`](Assembly-CSharp/DefaultPlayerSelection.cs) uses `Random.Range(1, 11)`. Update the upper bound:

```csharp
Element element = (Element)UnityEngine.Random.Range(1, 12); // was 11
```

**File:** [Assembly-CSharp/PlayerDropIn.cs](Assembly-CSharp/PlayerDropIn.cs)

Similar random element selection in [`AddPlayer`](Assembly-CSharp/PlayerDropIn.cs) — ensure the range includes your new element.

---

## 14. Update Video Spell Player

**File:** [Assembly-CSharp/VideoSpellPlayer.cs](Assembly-CSharp/VideoSpellPlayer.cs)

The [`DraftSpells`](Assembly-CSharp/VideoSpellPlayer.cs) method handles spell drafting for video/preview. Ensure `VideoSpellPlayer.GetMappedElement` maps your new element correctly.

---

## 15. Balance Reference (DAMAGE / RADIUS / POWER per slot)

Use these existing values as a baseline:

| Slot | Spell | DAMAGE | RADIUS | POWER | START_TIME |
|------|-------|--------|--------|-------|------------|
| Primary | [`FireballObject`](Assembly-CSharp/FireballObject.cs) | 10 | 4 | 40 | 1.0 |
| Primary | [`RockshotObject`](Assembly-CSharp/RockshotObject.cs) | 12 | 4 | 45 | 1.156 |
| Primary | [`SparkObject`](Assembly-CSharp/SparkObject.cs) | 13 | 5 | 55 | 0.185 |
| Primary | [`HinderObject`](Assembly-CSharp/HinderObject.cs) | 8 | 3 | 15 | 1.2 |
| Secondary | [`PetRockObject`](Assembly-CSharp/PetRockObject.cs) | 5 | 3 | 25 | 1.65 |
| Secondary | [`BoomerangObject`](Assembly-CSharp/BoomerangObject.cs) | 7 | 3 | 30 | 20.0 |
| Defensive | [`PillarOfFireObject`](Assembly-CSharp/PillarOfFireObject.cs) | 8 | 4.41 | 0 | 6.0 |
| Melee | [`SustainObject`](Assembly-CSharp/SustainObject.cs) | 0 | 3 | 15 | — |
| Utility | [`WaterCannonObject`](Assembly-CSharp/WaterCannonObject.cs) | 8 | 2 | 60 | 0.45 |
| Ultimate | [`JetStreamObject`](Assembly-CSharp/JetStreamObject.cs) | 25 | 12 | 95 | 0.86 |

---

## Summary Checklist

- [ ] Add to [`Element`](Assembly-CSharp/Element.cs) enum
- [ ] Add 7 entries to [`SpellName`](Assembly-CSharp/SpellName.cs) enum
- [ ] Create 7 `Spell` subclasses (initializers)
- [ ] Create 7 `SpellObject` subclasses (runtime logic)
- [ ] Register in [`SpellManager`](Assembly-CSharp/SpellManager.cs) spell_table + AI priority
- [ ] Update [`AvailableElements.unlockOrder`](Assembly-CSharp/AvailableElements.cs)
- [ ] Update [`GameSettings.elements`](Assembly-CSharp/GameSettings.cs) array size
- [ ] Update [`ElementColorMapping`](Assembly-CSharp/ElementColorMapping.cs) (colors + sounds)
- [ ] Update [`SpellHandler.castSounds`](Assembly-CSharp/SpellHandler.cs)
- [ ] Update `Globals.iconEmissionColors` array
- [ ] Update [`DefaultPlayerSelection`](Assembly-CSharp/DefaultPlayerSelection.cs) random range
- [ ] Update [`PlayerDropIn`](Assembly-CSharp/PlayerDropIn.cs) random range
- [ ] Update [`VideoSpellPlayer`](Assembly-CSharp/VideoSpellPlayer.cs) element mapping
- [ ] Create 7 spell object Unity prefabs in Resources
- [ ] Create 7 spell definition prefabs with Spell components
- [ ] Create FMOD audio events
- [ ] Create UI sprites (7 spell icons + 1 element icon)
- [ ] Test in tutorial, practice, online, and bot modes