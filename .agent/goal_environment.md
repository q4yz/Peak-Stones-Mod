# Goal: Environmental Mechanics (Spawning & Volcano)

## Progress Checklist
### Natural Generation
- [ ] Detect when the player successfully loads into a room (`PhotonNetwork.InRoom`).
- [ ] Ensure only the Master Client handles world generation (`PhotonNetwork.IsMasterClient`).
- [ ] Calculate random map coordinates and use Raycasts to find the floor.
- [ ] Spawn permanent stones on the ground exactly once per session.

### The Volcano Event
- [ ] Implement test trigger (F3 key).
- [ ] Tie event trigger to a randomized timer using the `VolcanoEventInterval` config.
- [ ] Implement 2-second screen shake via `Camera.main.transform.localPosition` manipulation.
- [ ] Implement atmospheric lighting shift (Red/Gray skybox or ambient light).
- [ ] Implement the 5-second suspense delay.
- [ ] Spawn stones high above the player on a timed loop.
- [ ] Create despawn logic for un-interacted volcano stones.

## Implementation Suggestions
*   **Event Timer:** Replace the F3 trigger with a background Coroutine or an `Update()` timer that resets to `VolcanoEventInterval.Value` after every eruption.
*   **Despawning Volcano Stones:** Create a custom `VolcanoStoneBehavior : MonoBehaviour` script and attach it to the spawned prefabs. In its `Update()` loop, measure the distance to the nearest player: `Vector3.Distance(transform.position, Player.localPlayer.character.transform.position)`. If the distance exceeds a set threshold (e.g., 100 meters), execute `Photon.Pun.PhotonNetwork.Destroy(gameObject)`.