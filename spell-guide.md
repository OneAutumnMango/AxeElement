# Guide: Creating a New Spell in MageQuit

This guide covers every step to add a new spell — from code to Unity assets to registration.

---

## Overview: How Spells Work

Every spell consists of two C# classes and a set of Unity assets:

| Component | Base Class | Purpose |
|-----------|------------|---------|
| **Spell definition** | `Spell : MonoBehaviour` | Inspector-configured metadata + `Initialize()` spawns the spell object |
| **Spell object** | `SpellObject : Photon.MonoBehaviour` | Runtime behavior — movement, collision, damage, networking |

The `SpellManager` GameObject holds every `Spell` subclass as a component. On `Awake()`, it scans `GetComponents<Spell>()` and builds a `Dictionary<SpellName, Spell> spell_table`. When a player casts, `SpellHandler` looks up the spell, plays the animation, then calls `spell.Initialize(...)` which instantiates the spell object prefab.

---

## Spell Slots

Each element has 7 spells, one per slot:

| Slot (`SpellButton`) | Index | Role | Example Spells |
|-----------------------|-------|------|----------------|
| `Primary` | 0 | Main projectile | Fireball, Rockshot, Spark, Snowball |
| `Movement` | 1 | Dash / reposition | FlameLeap, BullRush, SomerAssault |
| `Melee` | 2 | Close-range attack | Ignite, Push, Petrify, Sustain |
| `Secondary` | 3 | Utility projectile / trap | PetRock, Boomerang, Scorch, TimeBomb |
| `Defensive` | 4 | Shield / block | PillarOfFire, Deflect, RockBlock |
| `Utility` | 5 | Crowd control / tether | FlameLeash, WaterCannon, Vacuum |
| `Ultimate` | 6 | High-impact finisher | Spitfire, JetStream, Tsunami, Fissure |

---

## Step 1: Add to the `SpellName` Enum

**File:** `Assembly-CSharp/SpellName.cs`

Add your spell at the end of the enum (before any closing brace):

```csharp
public enum SpellName
{
    // ...existing 70 entries...
    ColdFusion,
    MyNewSpell    // <-- ADD HERE
}
```

The integer value is used as a network identifier, so always add at the end to avoid breaking existing mappings.

---

## Step 2: Create the Spell Class (Definition)

This class extends `Spell` and overrides `Initialize()` to spawn your spell object.

### Projectile Spell Template

For a spell that fires a projectile (like Fireball, Rockshot):

**Create file:** `Assembly-CSharp/MyNewSpell.cs`

```csharp
using System;
using UnityEngine;

public class MyNewSpell : Spell
{
    public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
        float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
    {
        // Instantiate the spell object prefab from Resources/Objects/
        GameUtility.Instantiate("Objects/MyNewSpell", position + Spell.skillshotOffset, rotation, 0)
            .GetComponent<MyNewSpellObject>()
            .Init(identity.owner, curve * this.curveMultiplier, this.initialVelocity);
    }
}
```

### Movement Spell Template

For a spell that moves the wizard (like FlameLeap, BullRush):

```csharp
public class MyDash : Spell
{
    public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
        float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
    {
        // Pass the full Identity so the spell object can access the wizard
        GameUtility.Instantiate("Objects/MyDash", position, rotation, 0)
            .GetComponent<MyDashObject>()
            .Init(identity);
    }

    // AI: Don't dash onto lava
    public override bool AvailableOverride(AiController ai, int owner, SpellUses use, int reactivate)
    {
        // Custom AI safety checks
        return base.AvailableOverride(ai, owner, use, reactivate);
    }
}
```

### Melee / Area Spell Template

For a spell that hits around the caster (like Sustain, Ignite):

```csharp
public class MyMelee : Spell
{
    public override void Initialize(Identity identity, Vector3 position, Quaternion rotation,
        float curve, int spellIndex, bool selfCast, SpellName spellNameForCooldown)
    {
        GameUtility.Instantiate("Objects/MyMelee", position, rotation, 0)
            .GetComponent<MyMeleeObject>()
            .Init(identity);
    }
}
```

### Optional Overrides

| Method | When to Override |
|--------|-----------------|
| `GetAiAim(...)` | Custom AI targeting (lead prediction, area denial, etc.) |
| `GetAiRefresh(int owner)` | Change how long AI waits after casting (default: `windUp + windDown + 0.1`) |
| `AvailableOverride(...)` | Conditional availability (e.g., don't dash onto lava) |
| `InitPassive(...)` | One-time setup at round start (for passive effects like Chainmail) |

---

## Step 3: Create the SpellObject Class (Runtime Behavior)

This is where all the actual gameplay logic lives.

### Stat Fields (Set in Constructor)

```csharp
public class MyNewSpellObject : SpellObject
{
    public MyNewSpellObject()
    {
        this.DAMAGE = 10f;      // Hit damage
        this.RADIUS = 4f;       // Explosion/hit detection radius
        this.POWER = 40f;       // Knockback force
        this.Y_POWER = 0f;     // Upward knockback bias (0 = horizontal)
        this.START_TIME = 1f;   // Lifetime in seconds before auto-death
    }
}
```

### Reference Values by Slot

| Slot | Spell | DAMAGE | RADIUS | POWER | Y_POWER | START_TIME |
|------|-------|--------|--------|-------|---------|------------|
| Primary | FireballObject | 10 | 4 | 35 | 0 | 1.0 |
| Primary | RockshotObject | 12 | 4 | 45 | 0 | 1.156 |
| Primary | SparkObject | 13 | 5 | 55 | 0 | 0.185 |
| Primary | SnowballObject | 10 | 4 | 30 | 0 | 1.0 |
| Primary | HinderObject | 8 | 3 | 15 | 0 | 1.2 |
| Movement | FlameLeapObject | 15 | 4 | 50 | 0 | 0.2 |
| Melee | SustainObject | 0 | 3 | 15 | 0 | — |
| Secondary | PetRockObject | 5 | 3 | 25 | 0 | 1.65 |
| Secondary | BoomerangObject | 7 | 3 | 30 | 0 | 20.0 |
| Defensive | PillarOfFireObject | 8 | 4.41 | 0 | 0 | 6.0 |
| Utility | WaterCannonObject | 8 | 2 | 60 | 0 | 0.45 |
| Ultimate | TsunamiObject | 10 | 3 | 20 | 0 | 3.1 |
| Ultimate | JetStreamObject | 25 | 12 | 95 | 0 | 0.86 |

---

### Full Projectile SpellObject Example

```csharp
using System;
using UnityEngine;

public class MyNewSpellObject : SpellObject
{
    // ── Stats ──
    public MyNewSpellObject()
    {
        this.DAMAGE = 10f;
        this.RADIUS = 4f;
        this.POWER = 40f;
        this.Y_POWER = 0f;
        this.START_TIME = 1f;
    }

    // ── Lifecycle ──

    private void Start()
    {
        this.phys = base.GetComponent<PhysicsBody>();
        this.sp.PlaySoundComponentInstantiate("event:/sfx/fire/fireball-start", 5f);
        this.SpellObjectStart(); // sets deathTimer = Time.time + START_TIME
    }

    public void Init(int owner, float curve, float velocity)
    {
        this.id.owner = owner;
        this.curve = curve;
        this.velocity = velocity;
        base.ChangeToSpellLayerDelayed(velocity);

        // Sync to other clients
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

    // ── Movement (every physics tick) ──

    private void FixedUpdate()
    {
        if (base.photonView == null || base.photonView.isMine)
        {
            // Move forward
            this.phys.velocity = base.transform.forward * this.velocity;

            // Apply curve (steering)
            base.transform.Rotate(Vector3.up, this.curve);

            // Check death timer
            if (Time.time > this.deathTimer)
            {
                this.Die();
            }
        }
        else
        {
            // Non-owner: lerp to synced position
            base.BaseClientCorrection();
        }
    }

    // ── Collision ──

    private void OnCollisionEnter(Collision collision)
    {
        if (base.photonView != null && !base.photonView.isMine)
            return;

        GameObject root = collision.gameObject.transform.root.gameObject;
        if (!GameUtility.IdentityCompare(root, UnitType.Unit))
            return;
        if (GameUtility.IdentityCompare(root, this.id.owner))
            return; // don't hit self

        // AoE damage
        Collider[] hits = GameUtility.GetAllInSphere(
            base.transform.position, this.RADIUS, this.id.owner, new UnitType[] { UnitType.Unit });

        foreach (Collider hit in hits)
        {
            GameObject target = hit.transform.root.gameObject;
            UnitStatus us = target.GetComponent<UnitStatus>();
            if (us != null)
            {
                us.TakeDamage(this.DAMAGE, this.id.owner, 0);
                PhysicsBody pb = target.GetComponent<PhysicsBody>();
                if (pb != null)
                {
                    pb.AddForce(GameUtility.GetForceVector(
                        base.transform.position, target.transform.position, this.POWER, this.Y_POWER));
                }
            }
        }

        // Sync impact VFX
        if (Globals.online)
        {
            base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All,
                new object[] { base.transform.position });
        }
        else
        {
            this.rpcCollision(base.transform.position);
        }

        this.Die();
    }

    // ── Death ──

    private void Die()
    {
        if (Globals.online)
        {
            base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, new object[0]);
        }
        else
        {
            this.rpcSpellObjectDeath();
        }
    }

    // ── RPCs (Networking) ──

    [PunRPC]
    public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, float curve, float velocity)
    {
        this.id.owner = owner;
        base.transform.position = pos;
        base.transform.rotation = rot;
        this.curve = curve;
        this.velocity = velocity;
    }

    [PunRPC]
    public void rpcCollision(Vector3 pos)
    {
        UnityEngine.Object.Instantiate(this.impact, pos, Globals.sideways);
        this.sp.PlaySoundInstantiate("event:/sfx/fire/fireball-hit", 5f);
    }

    [PunRPC]
    public void rpcSpellObjectDeath()
    {
        Collider col = base.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        ParticleSystem[] particles = base.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particles)
        {
            ps.Stop();
        }

        UnityEngine.Object.Destroy(base.gameObject, 1f);
    }

    // ── Serialization (position sync) ──

    private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        this.BaseSerialize(stream, info);
    }

    // ── Fields ──
    public UnityEngine.Object impact;   // Assign in Unity Inspector
    private PhysicsBody phys;
    private float decel = 0.9f;
}
```

---

### Movement SpellObject Example (FlameLeap-style)

Key differences from projectile spells:

```csharp
public class MyDashObject : SpellObject
{
    public MyDashObject()
    {
        this.DAMAGE = 15f;
        this.RADIUS = 4f;
        this.POWER = 50f;
        this.START_TIME = 0.2f; // Short — just enough time for the dash arc
    }

    public void Init(Identity identity)
    {
        this.id.owner = identity.owner;

        // Get the WIZARD's physics, not the spell object's
        GameObject wizard = GameUtility.GetWizard(this.id.owner).gameObject;
        this.wizardPhys = wizard.GetComponent<PhysicsBody>();
        this.wizardStatus = wizard.GetComponent<UnitStatus>();

        // Launch the wizard
        this.wizardPhys.abilityVelocity = base.transform.forward * this.velocity;
        this.wizardPhys.abilityVelocity += Vector3.up * 0.4f;
        this.wizardPhys.gravity = -0.015f; // Half gravity for arc
    }

    private void FixedUpdate()
    {
        // Apply curve to wizard movement
        Vector3 vel = this.wizardPhys.abilityVelocity;
        vel = Quaternion.Euler(0f, this.curve, 0f) * vel;
        this.wizardPhys.abilityVelocity = vel;

        if (Time.time > this.deathTimer && this.wizardPhys.onGround)
        {
            // Landing: AoE damage at feet
            Collider[] hits = GameUtility.GetAllInSphere(
                this.wizardPhys.transform.position, this.RADIUS, this.id.owner,
                new UnitType[] { UnitType.Unit });
            // ... apply damage+knockback ...
            UnityEngine.Object.Destroy(base.gameObject);
        }
    }

    private void OnDestroy()
    {
        // CRITICAL: Reset wizard physics
        if (this.wizardPhys != null)
        {
            this.wizardPhys.abilityVelocity = Vector3.zero;
            this.wizardPhys.gravity = Globals.wizard_gravity; // -0.03f
        }
    }

    private PhysicsBody wizardPhys;
    private UnitStatus wizardStatus;
}
```

---

### Defensive SpellObject Example (PillarOfFire-style)

Key differences: stays alive for a long time, scans for enemies each frame, destroys incoming projectiles.

```csharp
public class MyShieldObject : SpellObject
{
    public MyShieldObject()
    {
        this.DAMAGE = 5f;       // DPS to enemies inside
        this.RADIUS = 4f;
        this.POWER = 0f;        // No knockback
        this.START_TIME = 6f;   // Shields last a long time
    }

    private void FixedUpdate()
    {
        // Scan for enemy projectiles and destroy them
        Collider[] projectiles = GameUtility.GetAllInSphere(
            base.transform.position, this.RADIUS, this.id.owner,
            new UnitType[] { UnitType.Projectile });

        foreach (Collider col in projectiles)
        {
            UnityEngine.Object.Destroy(col.transform.root.gameObject);
            // Optionally grow when absorbing
        }

        // Scan for enemy units and apply DPS
        Collider[] enemies = GameUtility.GetAllInSphere(
            base.transform.position, this.RADIUS, this.id.owner,
            new UnitType[] { UnitType.Unit });

        foreach (Collider col in enemies)
        {
            UnitStatus us = col.transform.root.GetComponent<UnitStatus>();
            if (us != null)
            {
                us.TakeDamage(this.DAMAGE * Time.fixedDeltaTime, this.id.owner, 0);
            }
        }

        // Death timer
        if (Time.time > this.deathTimer)
        {
            UnityEngine.Object.Destroy(base.gameObject);
        }
    }
}
```

---

## Step 4: Configure the Spell Inspector Fields

When you attach your `Spell` subclass to the SpellManager GameObject, configure these fields in the Unity Inspector:

| Field | Type | Description |
|-------|------|-------------|
| `spellName` | `SpellName` | Must match your enum entry exactly |
| `element` | `Element` | Which element this spell belongs to |
| `spellButton` | `SpellButton` | Which slot (Primary, Movement, etc.) |
| `description` | `string` | Tooltip text shown during draft |
| `icon` | `Sprite` | Spell icon for HUD and draft screen |
| `cooldown` | `float` | Seconds between casts |
| `windUp` | `float` (0.01–3) | Time from input to spell spawn (animation lead-in) |
| `windDown` | `float` (0.01–3) | Recovery time after spell spawn |
| `animationName` | `string` | Key into `animation_table` (see below) |
| `curveMultiplier` | `float` | How much joystick curve input affects steering |
| `initialVelocity` | `float` | Speed of the projectile |
| `minRange` / `maxRange` | `float` | AI targeting range limits |
| `ignorePath` | `bool` | True for lobbed / AoE (AI ignores obstacles) |
| `spellRadius` | `float` | AI hitbox estimate for the spell |
| `uses` | `SpellUses` (flags) | `Attack`, `Move`, `Defend`, `Custom` — tells AI when to use |
| `video` | `VideoClip` | Preview video in draft screen (optional) |
| `additionalCasts` | `SubSpell[]` | Multi-cast / recast configuration (see below) |
| `reactivate` | `int` | Number of re-casts (0 = single cast) |

### Standard Cooldowns by Slot

| Slot | Typical Cooldown | Typical WindUp | Typical WindDown |
|------|-----------------|----------------|------------------|
| Primary | 1.0–2.0s | 0.3–0.5s | 0.2–0.4s |
| Movement | 4.0–6.0s | 0.2–0.4s | 0.3–0.5s |
| Melee | 3.0–5.0s | 0.3–0.5s | 0.3–0.5s |
| Secondary | 3.0–6.0s | 0.3–0.5s | 0.2–0.4s |
| Defensive | 8.0–12.0s | 0.3–0.6s | 0.3–0.5s |
| Utility | 5.0–8.0s | 0.3–0.5s | 0.3–0.5s |
| Ultimate | 10.0–15.0s | 0.4–0.8s | 0.3–0.5s |

### Animation Names

The `animationName` string maps to an entry in `SpellManager.animation_table`. Existing entries include:

| animationName | Used by |
|---------------|---------|
| `"Attack"` | Most projectile spells (Fireball, Rockshot, etc.) |
| `"Melee"` | Close-range spells (Push, Ignite) |
| `"FlameLeap"` | Movement spells |
| `"Defensive"` | Shield / defensive spells |
| `"SelfCast"` | Self-targeted spells (Rewind, Backup) |
| `"ChannelStart"` | Channeled spells (WaterCannon) |

### Multi-Cast (SubSpell) Configuration

For spells with re-casts (like DoubleStrike), configure `additionalCasts`:

```csharp
[Serializable]
public class SubSpell
{
    public string animationName;       // Re-cast animation
    public float cooldown;             // Delay before full cooldown starts
    public float windUp, windDown;     // Re-cast timing
    public float activationWindow;     // Seconds to press button again (window closes → full CD)
    public bool startsDisabled;        // If true, must be activated
    public float curveMultiplier;
    public float initialVelocity;
    public float minRange, maxRange;
    public bool ignorePath;
    public SpellUses uses;
}
```

---

## Step 5: Create the Unity Prefab

### Spell Object Prefab (the projectile / effect in the world)

Create a prefab at `Resources/Objects/MyNewSpell` with these components:

| Component | Purpose | Notes |
|-----------|---------|-------|
| **MyNewSpellObject** | Your SpellObject script | Primary component |
| **Identity** | Owner tracking | Set `type = UnitType.Projectile` |
| **PhotonView** | Networking | Observed component = MyNewSpellObject |
| **SoundPlayer** | Audio playback | Required by base SpellObject |
| **PhysicsBody** | Custom physics | For projectile movement (set gravity=0 for straight-line) |
| **Collider** (Sphere/Box) | Hit detection | `isTrigger = false` for collision-based, `true` for trigger-based |
| **Rigidbody** | Unity physics | `isKinematic = true` (movement is code-driven via PhysicsBody) |
| **Visual mesh or particles** | Visuals | The actual look of the spell |

#### Prefab hierarchy:

```
MyNewSpell (root)
├── Components: MyNewSpellObject, Identity, PhotonView, SoundPlayer, PhysicsBody,
│               SphereCollider, Rigidbody
├── Visuals (child)
│   └── Mesh or ParticleSystem
└── Trail (child, optional)
    └── TrailRenderer or ParticleSystem
```

#### Identity Configuration

- `type` = `UnitType.Projectile` (for projectile spells)
- `type` = `UnitType.Wall` (for defensive walls / shields)
- `owner` = set at runtime via `Init()`

#### PhysicsBody Configuration (for projectiles)

- `gravity` = `0` (straight-line flight) or `-0.01` (slight arc)
- `groundFriction` = `0.96`
- `airFriction` = `0.99`

#### Collider Notes

- **Spell-to-wizard hits:** Use `OnCollisionEnter` with a non-trigger collider on layer 11 (spell layer)
- `ChangeToSpellLayerDelayed(velocity)` delays the layer change so the caster's own collider doesn't block the spell immediately after casting

### Impact Prefab (explosion / hit VFX)

Create a separate prefab for the impact effect:

```
MyNewSpellImpact (root)
├── ParticleSystem (one-shot burst)
├── PointLight (brief flash, optional)
└── CFX_AutoDestructShuriken (auto-destroys after particles finish)
```

Assign this to the `impact` field on your SpellObject in the Inspector.

---

## Step 6: Register in SpellManager

### 6a. Attach to SpellManager GameObject

The SpellManager discovers spells via `GetComponents<Spell>()`. Attach your `MyNewSpell` MonoBehaviour as a component on the **same GameObject** that has `SpellManager`.

After attaching, configure all the Inspector fields (step 4).

### 6b. Add to AI Draft Priority

In `SpellManager`, add your spell to the `ai_draft_priority` dictionary under the correct `SpellButton`:

```csharp
// In the appropriate SpellButton list:
{
    SpellButton.Primary, new List<SpellName>
    {
        SpellName.Fireball,
        SpellName.Gust,
        // ...
        SpellName.MyNewSpell   // Add at desired AI priority position
    }
}
```

Position in the list determines how much bots prefer this spell. Weights are Fibonacci-based: `[144, 89, 55, 34, 21, 13, 8, 5, 3, 2]`. Position 1 is 72× more likely than position 10.

---

## Step 7: Add Audio Events (FMOD)

MageQuit uses FMOD for all audio. You need at minimum:

| Event Path | When Played |
|------------|-------------|
| `event:/sfx/<element>/<spell>-start` | Spell spawns (`Start()`) |
| `event:/sfx/<element>/<spell>-hit` | Impact / collision (`rpcCollision()`) |

### Playing Sounds in Code

```csharp
// Play attached to object (moves with it)
this.sp.PlaySoundComponentInstantiate("event:/sfx/fire/fireball-start", 5f);

// Play at a position (one-shot, stays in place)
this.sp.PlaySoundInstantiate("event:/sfx/fire/fireball-hit", 5f);

// Play with pitch variation
this.sp.PlaySoundComponentInstantiate("event:/sfx/fire/fireball-start", 5f).setPitch(1.2f);
```

---

## Step 8: Wire Up the Spell to an Element

In your `Spell` component's Inspector configuration:
- Set `element` to the desired `Element` enum value
- Set `spellButton` to the desired slot

The spell draft system (`AvailableElements` + `SpellComponent`) will automatically include your spell in the draft pool for that element and slot.

---

## Step 9: Player Coloring

Most spells should show the owner's color. Call `UpdateColor()` (inherited from SpellObject) in `Start()` or `Init()`:

```csharp
private void Start()
{
    base.UpdateColor(); // Sets materials to the casting wizard's color
    // ...
}
```

This calls `GameUtility.SetWizardColor(id.owner, gameObject, false)` which applies the player color to all renderers on the object.

---

## Step 10: AI Considerations

### `GetAiAim()` — Targeting

The base implementation in `Spell` does lead prediction for projectiles. Override for:
- **Lobbed spells:** Return target position directly (ignore obstacles)
- **Self-cast:** Return own position
- **Melee:** Return direction to nearest enemy

```csharp
public override Vector3? GetAiAim(TargetComponent targetComponent, Vector3 position, 
    Vector3 target, SpellUses use, ref float curve, int owner)
{
    if (use == SpellUses.Attack)
    {
        // Custom targeting logic
        return (target - position).normalized;
    }
    return base.GetAiAim(targetComponent, position, target, use, ref curve, owner);
}
```

### `AvailableOverride()` — When to Use

```csharp
public override bool AvailableOverride(AiController ai, int owner, SpellUses use, int reactivate)
{
    // Don't use this spell in specific situations
    if (/* some condition */) return false;
    return true;
}
```

### `SpellUses` Flags

Set the `uses` field on your Spell component to tell AI when to consider this spell:

| Flag | AI Decision Context |
|------|-------------------|
| `Attack` | When targeting an enemy |
| `Move` | When evading or repositioning |
| `Defend` | When projectiles are incoming |
| `Custom` | Special logic (random chance to use) |

---

## Quick Reference: Networking Boilerplate

Every `SpellObject` needs this networking code:

```csharp
// 1. Init sends state to other clients
public void Init(int owner, ...)
{
    this.id.owner = owner;
    // ... setup ...
    if (base.photonView != null && base.photonView.isMine)
    {
        base.photonView.RPCLocal(this, "rpcSpellObjectStart", PhotonTargets.Others,
            new object[] { owner, base.transform.position, base.transform.rotation, /* ... */ });
    }
}

// 2. Other clients receive the init
[PunRPC]
public void rpcSpellObjectStart(int owner, Vector3 pos, Quaternion rot, /* ... */)
{
    this.id.owner = owner;
    base.transform.position = pos;
    base.transform.rotation = rot;
    // ... restore state ...
}

// 3. Position/rotation sync every frame
private void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
    this.BaseSerialize(stream, info); // handles position + rotation
}

// 4. Non-owner position correction
// In FixedUpdate:
if (!base.photonView.isMine)
{
    base.BaseClientCorrection(); // lerps to synced position
    return;
}

// 5. Game logic only runs on owner/master
if (base.photonView != null && !base.photonView.isMine) return;

// 6. Effects are sent to all
base.photonView.RPCLocal(this, "rpcCollision", PhotonTargets.All, new object[] { pos });

// 7. Death is sent to all
base.photonView.RPCLocal(this, "rpcSpellObjectDeath", PhotonTargets.All, new object[0]);
```

---

## Summary Checklist

- [ ] Add entry to `SpellName` enum
- [ ] Create `MySpell.cs` (extends `Spell`) with `Initialize()` override
- [ ] Create `MySpellObject.cs` (extends `SpellObject`) with stats, movement, collision, death
- [ ] Include all Photon networking RPCs and serialization
- [ ] Create Unity prefab at `Resources/Objects/MySpell`
  - [ ] Add Identity, PhotonView, SoundPlayer, PhysicsBody, Collider components
  - [ ] Add visual mesh / particle systems
  - [ ] Create impact prefab and assign it
- [ ] Attach Spell component to SpellManager GameObject
  - [ ] Configure all Inspector fields (element, slot, cooldown, velocity, etc.)
- [ ] Add to `ai_draft_priority` in SpellManager
- [ ] Create FMOD audio events
- [ ] Test offline (bots), then online
