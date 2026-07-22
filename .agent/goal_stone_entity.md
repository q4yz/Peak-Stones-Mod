# Goal: The Stone Entity (Physics & Combat)

## Progress Checklist
- [x] **Inventory & Network Registration fixed:** Adopted the official `PEAKLib` pipeline. Native `itemID` generation and network `DefaultPool` registration are now handled automatically by `peakBundle.Mod.RegisterContent()`.
- [x] **Prefab Serialization fixed:** Abandoned programmatic `AddComponent<Item>()`. Prefabs are now built entirely in the Unity Editor and wrapped in `UnityItemContent` assets (`Pebble.asset`, `Rock.asset`, `Boulder.asset`) inside `stones.peakbundle`. This successfully bakes native dependencies (`ItemUIData`, `BackpackReference`, `SFX_Settings`) and prevents silent `NullReferenceException` crashes during pickup.
- [x] **Physics & Bounciness fixed:** Replaced default Unity physics with a custom `Physic Material` (Friction: 0.8, Bounciness: 0) directly in the Editor to ensure heavy, dead thuds on impact.
- [x] **Spawn pipeline fixed:** Raw `PhotonNetwork.Instantiate` would teleport items to `(0, -500, 0)` because `Item.Update()` snaps any `InBackpack` item to that kill position. Fixed via `ItemSpawnHelper.SpawnDropped` + `Item_SetKinematicRPC_Patch` Harmony Postfix.
- [x] Implement `OnCollisionEnter` kinetic energy calculations in `StoneBehavior`.
- [x] **Combat (Damage):** Apply Damage to players based strictly on the calculated kinetic energy of the impact.
- [x] **Splitting (Downgrading):** Destroy the original stone and instantiate two prefabs of the next tier down (e.g., a Boulder spawns two Rocks) upon extreme impact.

## PEAKLib Serialized Prefab Pipeline (Current Approach)

We do **not** dynamically inject native `Item` components via code, and we do **not** skin native prefabs. The "Pure AssetBundle Code Injection" approach was abandoned because native item pickup logic iterates over deeply nested, serialized structural data (like `colliders`, `mainRenderer`, `ItemUIData`, and `ItemPhysicsSyncer`). When injected via reflection, these remain `null` and cause silent runtime crashes when transitioning to `ItemState.Held`.

Instead, we use the official **PEAKLib Custom Item Pipeline**:
1. Items are constructed in the Unity Editor with all necessary colliders, renderers, and native `Item` scripts attached.
2. They are wrapped in `UnityItemContent` assets to interface with `PEAKLib`.
3. At runtime, `Plugin.cs` loads the bundle, extracts the prefabs, attaches our custom `StoneBehavior`, and hands the keys to `PEAKLib` to automatically map them to the game's item dictionaries.

## Discrete Tiers vs. Dynamic Scaling
Dynamic scaling and random mass via `[PunRPC]` were **abandoned**. PEAK's inventory system strictly keys backpack slot weight, `throwForceMultiplier`, and UI icons to a static `itemID`. A dynamically scaled 100-pound boulder would incorrectly take up the same backpack space as a 1-pound pebble. 

We now use discrete entities:
*   **Pebble:** Fast throw, 1 inventory slot, low damage, low mass.
*   **Rock:** Slower throw, 2 inventory slots, medium damage, medium mass.
*   **Boulder:** Massive damage, heavy mass, un-storable (forces `ItemState.Held` only).

### Files

| File | Role |
|---|---|
| `Plugin.cs` | PEAKLib entry point. Uses `this.LoadBundleWithName` to load `stones.peakbundle`, extracts the three prefabs, dynamically attaches `StoneBehavior`, and calls `peakBundle.Mod.RegisterContent()`. |
| `StoneHarmonyPatches.cs` | Contains `Item_SetKinematicRPC_Patch` (Postfix, Ground transition) to ensure spawned items drop cleanly instead of teleporting to backpack kill-zones. |
| `StoneBehavior.cs` | Stripped of deprecated network/scaling logic. Purely a hook point for the upcoming `OnCollisionEnter` kinetic energy and damage combat math. |
| `ItemSpawnHelper.cs` | Debug logic to randomly select and instantiate either `"Pebble"`, `"Rock"`, or `"Boulder"`, followed by `item.SetKinematicNetworked(false, pos, rot)` for the Ground transition. |

## Verified Implementation Notes (from `ilspycmd` decompiles)

### Item state machine
- `Item : MonoBehaviourPunCallbacks, IInteractible` - global namespace.
- `public enum ItemState { Ground, Held, InBackpack }` - no "Dropped" state, **Ground IS the world-physics state**.
- `ItemState` is `{ get; set; }` (auto-property, public setter).
- `Item.itemState` defaults to whatever the prefab serialized it as.

### Why raw `PhotonNetwork.Instantiate` fails
- `Item.Update()` runs every frame on every client:
  ```csharp
  if (itemState == ItemState.InBackpack 
      && (backpackSlotTransform == null || !backpackSlotTransform.UnityObjectExists()))
  {
      base.transform.position = new Vector3(0f, -500f, 0f);
  }