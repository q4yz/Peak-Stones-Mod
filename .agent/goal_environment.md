# Goal: Environmental Mechanics (Spawning & Volcano)

## Progress Checklist
### Natural Generation
- [x] Detect when the player successfully loads into a room (`PhotonNetwork.InRoom`).
- [x] Ensure only the Master Client handles world generation (`PhotonNetwork.IsMasterClient`).
- [x] Calculate random map coordinates and use Raycasts to find the floor.
- [x] Spawn permanent stones on the ground exactly once per session.

### The Volcano Event
- [ ] Hook volcanic outbreaks into `WindChillZone.RPCA_ToggleWind` instead of a separate timer or hotkey.
- [ ] Sync outbreak state through the game/network state so every client follows the same storm.
- [ ] Override sky/fog/ambient visuals while the outbreak flag is active.
- [ ] Replace the freezing status with a burn-like volcanic affliction when the game exposes one.
- [ ] Spawn a burst of stones when the outbreak begins, driven by the storm start rather than a coroutine loop.
- [ ] Add cleanup/despawn logic for volcanic stones that are left uncollected.

## Implementation Suggestions
*   **Game-logic hook:** Patch `WindChillZone.RPCA_ToggleWind` with Harmony, roll the outbreak chance only on the master, and broadcast the chosen outbreak state through room properties or a buffered RPC so remotes stay aligned.
*   **Visual override:** Keep a persistent manager alive only to mirror the active outbreak state into shader globals and fog/ambient overrides while the game storm is active.
*   **Despawning Volcano Stones:** Create a custom `VolcanoStoneBehavior : MonoBehaviour` script and attach it to the spawned prefabs. In its `Update()` loop, measure the distance to the nearest player: `Vector3.Distance(transform.position, Player.localPlayer.character.transform.position)`. If the distance exceeds a set threshold (e.g. 100 meters), execute `Photon.Pun.PhotonNetwork.Destroy(gameObject)`.