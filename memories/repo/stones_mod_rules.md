# Stones Mod — Hard-Won Rules

These rules came from bugs encountered during development of the Stones BepInEx mod for PEAK (Unity, IL2CPP, .NET Standard 2.1, Photon PUN). Read before touching network sync, pickup/throw, item registration, or the bundle pipeline.

## 0. Architectural bans (read first)

- **RULE (Entity Pipeline) — Skinning is BANNED.** Cloning native prefabs (e.g. `Item_Coconut`) and swapping meshes in place is strictly prohibited. Native code re-enables the original prefab's meshes on every `ItemState` transition, re-binds hardcoded colliders, and replays vanilla sound effects. **All custom items must be loaded from a `.peakbundle` and registered through PEAKLib.**

- **RULE (Item Registration via PEAKLib) — Manual database injection is BANNED.** All items must be authored as **`UnityItemContent` ScriptableObjects** in the Unity Editor dummy project, packed into `stones.peakbundle`, and registered through `PEAKLib.Items`. The loader is forbidden from:
  - Reflecting on `Item.ALL_ACTIVE_ITEMS` / `ItemDatabase` / native dictionaries to insert items.
  - Direct calls to `PhotonNetwork.PrefabPool.RegisterPrefab` or poking Photon's `DefaultPool.ResourceCache`.
  - Runtime `AddComponent<Item>()` on a freshly-built GameObject.

  Only `peakBundle.LoadAsset<UnityItemContent>(name)` + `peakBundle.Mod.RegisterContent()` is allowed. PEAKLib owns the native ItemDatabase insertion and Photon's `CustomPrefabPool` registration. The Cubescape-era `CubePrefabLoader` (which did all three) is **deleted** and must never come back.

- **RULE (UnityItemContent ScriptableObject, NOT raw GameObject).** Each tier in the bundle must be a `PEAKLib.Items.UnityEditor.UnityItemContent` ScriptableObject (`.asset` extension) whose `ItemPrefab` field references a GameObject carrying the vanilla `Item` + `PhotonView` + `Rigidbody` + `Collider` + the tier's custom behavior. The script:
  - `class UnityItemContent : ScriptableObject, IItemContent` with `[field: SerializeField] public GameObject ItemPrefab { get; private set; } = null!;`.
  - Lives in `PEAKLib.Items.UnityEditor` namespace despite the folder name — it is accessible at runtime (NOT `#if UNITY_EDITOR` wrapped).
  - The author's `Name` property: returns `ItemPrefab.GetComponent<Item>()?.name ?? name` — this is what PEAKLib uses to derive itemID hashes and Photon prefab keys, so the GameObject's `name` MUST be stable.

- **RULE (Author names MUST be lowercase).** Tier prefab names in the bundle are `pebble`, `rock`, `boulder` (lowercase). The `Plugin.StoneTiers` PrefabId table must match exactly. PEAKLib's `LoadAsset<UnityItemContent>(name)` relies on Unity's AssetBundle basename lookup and benefits from exact case matches.

- **RULE (Distinct Entities).** The mod ships **three pre-baked tiers**, not one. They are siblings in the same `.peakbundle` and each has a unique Photon prefab key, item ID, and scale / mass range:

  | Tier     | `UnityItemContent.name` | `Bundle path`                | Real itemID (PEAKLib-derived) | Scale range | Mass range (kg) |
  | -------- | ----------------------- | ---------------------------- | ----------------------------- | ----------- | --------------- |
  | Pebble   | `"pebble"`              | `assets/_mod/pebble.asset`   | MD5(`{modId}:pebble`)[0..2]    | 0.30 - 0.50 | 0.5 - 2.0       |
  | Rock     | `"rock"`                | `assets/_mod/rock.asset`     | MD5(`{modId}:rock`)[0..2]     | 0.70 - 1.00 | 2.0 - 5.0       |
  | Boulder  | `"boulder"`             | `assets/_mod/boulder.asset`  | MD5(`{modId}:boulder`)[0..2]  | 1.20 - 1.80 | 5.0 - 15.0      |

  Do **not** collapse them into one entity. Native code, the native item database, and the master client all key off `itemID`, so each tier must have its own unique ID and prefab key.

- **RULE (Photon Key Format).** PEAKLib's `NetworkPrefabManager.RegisterNetworkPrefab(mod, "0_Items/", prefab)` mutates the GameObject to `prefab.name = $"{mod.Id}:{prefab.name}"` and registers it under `"0_Items/{mod.Id}:{prefab.name}"`. Therefore `PhotonNetwork.Instantiate(key, ...)` requires the **full** key — passing just `"pebble"` fails. `ItemSpawnHelper.SpawnStone` reconstructs the full key at runtime as `$"0_Items/{Plugin.ModId}:{prefabId}"`. PEAKLib's `ModId` is `Info.Metadata.GUID` (cached into `Plugin.ModId` at `Awake`).

- **RULE (Harmony Parameter Matching).** When writing Harmony Prefix / Postfix / Transpiler methods, every intercepted method argument **must** be named exactly as it appears in the decompiled target method signature. Renaming a parameter (e.g. `setState` → `state`) is fatal at runtime: HarmonyX throws `System.Exception: Parameter "<x>" not found in method <...>` during the Chainloader phase and the mod never loads. Always cross-check parameter names against the `ilspycmd` decompile of the target method before shipping a new patch.

- **RULE (Inventory Serialization).** Every `Item` on a custom prefab must serialize its identification fields (`itemID`, `itemName`, `prefabName`, `UIData`, `colliders`, `mainRenderer`, `addtlRenderers`) in the Unity Editor **and** have those values preserved by the loader. PEAKLib assigns a deterministic itemID via `PEAKLib.Items.ItemRegistrar.FinishRegisterItem` (MD5 of `mod.Id + item.name`); we do not override it.

## 1. Network sync for randomized properties

- Use **`photonView.RPC(name, RpcTarget.AllBuffered, args)`** as the loopback pattern for randomized / runtime-chosen properties (scale, mass, color, etc).
  - Master fires the RPC, master receives its own call back (loopback), existing remotes receive it immediately, late joiners get it replayed automatically.
  - `InstantiationData` is an *alternative* for one-shot values, but RPC is preferable when the master needs to coordinate the choice (avoids each peer rolling its own random).
- Photon does **not** sync `localScale`, `Rigidbody.mass`, `Renderer.material`, `MeshFilter.sharedMesh`. These must all be replicated manually.

## 2. Verification logging on every spawn

Always log on `IPunInstantiateMagicCallback.OnPhotonInstantiate`:
- `PhotonNetwork.IsMasterClient`
- `photonView.OwnerActorNr`, `photonView.ControllerActorNr`
- `info.Sender.ActorNumber`
- `Item.itemState` — warn if it is `InBackpack` (the (0,-500,0) snap bug fires then).

## 3. Patching policy

- Prefer **Harmony patches** via `AccessTools.Method` over manual controller scripts. Patches are less invasive and easier to roll back.
- Use `new Harmony(id).PatchAll(typeof(Plugin).Assembly)` so all `[HarmonyPatch]` classes in the assembly get applied automatically.
- `Item.SetState(ItemState, Character)` is `internal`. Reach it via reflection from a Postfix.

## 4. The (0, -500, 0) snap bug

- `Item.Update` teleports the GameObject to (0, -500, 0) every frame when `itemState == InBackpack` AND `backpackSlotTransform == null`.
- Fix: call `item.SetKinematicNetworked(false, pos, rot)` immediately after `PhotonNetwork.Instantiate`. It fires `SetKinematicRPC(AllBuffered, false, pos, rot)` and the Postfix on that RPC transitions every peer to `ItemState.Ground` — gravity on, kinematic off, colliders on.
- The Cubescape-era workaround of parking clones at `(0, -9999, 0)` is **gone** — we don't clone at runtime any more, we instantiate pre-baked prefabs.

## 5. PEAKLib Items pipeline (the only allowed approach)

The architecture MUST follow these steps exactly. Skipping step 3 or inlining a manual prefab lookup will produce silent nulls at runtime.

1. **Author** every tier in the Unity Editor dummy project as a `UnityItemContent` ScriptableObject (`Create → PEAKLib → Items → Unity Item Content`). Assign:
   - `ItemPrefab`: a GameObject prefab carrying `Rigidbody`, `BoxCollider`, `PhotonView`, `Item` (with `UIData`, `colliders`, `mainRenderer`, `addtlRenderers` filled), and `StoneBehavior`.
   - The `Item`'s component `name` becomes the Photon prefab ID; pick stable lowercase names (`pebble`, `rock`, `boulder`).
2. **Pack** all `UnityItemContent` ScriptableObjects plus any `UnityModDefinition` into `stones.peakbundle` via the official PEAKLib bundle builder (configurable in the Unity Editor's Build window).
3. **Load** the bundle and register content at runtime via PEAKLib's `LoadBundleWithName` extension:
   ```csharp
   this.LoadBundleWithName("stones.peakbundle", peakBundle =>
   {
       foreach (StoneTier tier in StoneTiers)
       {
           UnityItemContent content = peakBundle.LoadAsset<UnityItemContent>(tier.PrefabId);
           if (content == null) { /* log + continue */ }
           AttachStoneBehavior(content, tier.PrefabId); // attaches to content.ItemPrefab
       }

       peakBundle.Mod.RegisterContent(); // registers ALL IContent in bundle (1 batch call)
   });
   ```
4. **Spawn** over Photon using the FULL key:
   ```csharp
   string photonKey = $"0_Items/{Plugin.ModId}:{tier.PrefabId}";
   var go = PhotonNetwork.Instantiate(photonKey, pos, rot, 0);
   item.SetKinematicNetworked(false, pos, rot);  // transition out of InBackpack
   ```
5. Component order on the Editor prefab: `MeshFilter` + `MeshRenderer` → `Rigidbody` → `BoxCollider` → `PhotonView` → `Item` → `StoneBehavior`.

## 6. SetKinematicNetworked for state transition

- `item.SetKinematicNetworked(false, pos, rot)` fires `SetKinematicRPC(false, pos, rot)` to `RpcTarget.AllBuffered`.
- The Postfix on `SetKinematicRPC` calls `SetState(Ground, null)` via reflection, which flips gravity on, kinematic off, colliders on, and restores interpolation. This works for every peer including late joiners.
- Use this immediately after `PhotonNetwork.Instantiate` to land an item in the world cleanly.

## 7. PUN interface variant

- This PEAK-specific PUN variant exposes `IPunInstantiateMagicCallback` — NOT the standard `IPunInstantiateCallback`. Reflection on `PhotonUnityNetworking.dll` confirmed the spelling.

## 8. Debug spawn hotkeys

- `F2`: spawns Coconut + stones in front of the player.
  - Default mode (`debugTierIndex == -1`): Coconut left, then Pebble / Rock / Boulder spaced 1.5 m apart at chest height.
  - Single-tier mode (`debugTierIndex` in `{0, 1, 2}`): Coconut left, single chosen stone at right.
- `F3`: Volcano event (whatever `VolcanoEvent.Run()` does today).
- `F4`: cycles F2's `debugTierIndex`: `-1 → 0 (Pebble) → 1 (Rock) → 2 (Boulder) → -1 (all)`.

## 9. Where to look in PEAKLib source when debugging registration issues

- `UnityItemContent.cs` (in `src/PEAKLib.Items/UnityEditor/`): `ItemPrefab`, `Name`, `Resolve()`, `Register()`. **Not wrapped in `#if UNITY_EDITOR`**.
- `ItemContent.cs` (runtime wrapper): holds the GameObject-prefab reference and `Register(ModDefinition owner)` call chain.
- `NetworkPrefabManager.cs` (in `src/PEAKLib.Core/`): renames prefab to `{mod.Id}:{name}`, registers under `"0_Items/{...}"` in `PEAKLibPrefabPool`.
- `ModDefinition.cs`: `RegisterContent()` loops over `Content` HashSet, calls each `modContent.Register(this)`. Single batch entry point.
- `BundleLoader.cs`: invokes the user callback after the bundle is opened; provides `LoadAsset<T>` and `GetAllAssetNames` forwarders.
