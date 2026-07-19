# Goal: The Stone Entity (Physics & Combat)

## Progress Checklist
- [ ] Create the Stone prefab in the Unity Editor with `PhotonView`, `Rigidbody`, and `Collider`.
- [ ] Export the Stone prefab as an AssetBundle.
- [ ] Load the AssetBundle via BepInEx and register it to the Photon Network pool.
- [ ] Make the Stone pickup-able and throwable (inherit logic from the Coconut item).
- [ ] Randomize the scale/size of the Stone upon spawning.
- [ ] Randomize the physical mass of the Stone's `Rigidbody` upon spawning.
- [ ] Implement `OnCollisionEnter` kinetic energy calculations.
- [ ] **Threshold 1:** Apply Knockout status to players hit with high energy.
- [ ] **Threshold 2:** Apply Damage to players hit with extreme energy.
- [ ] **Threshold 2:** Destroy the original stone and instantiate two smaller stone prefabs upon extreme impact.

## Implementation Suggestions
*   **AssetBundle Loading:** Use `AssetBundle.LoadFromFile(Path.Combine(Paths.PluginPath, "stonebundle"))` to load the custom model.
*   **Randomization:** In the Stone's initialization method (e.g., `Start()`), use `transform.localScale = Vector3.one * Random.Range(0.5f, 2.0f);` and `GetComponent<Rigidbody>().mass = Random.Range(1f, 10f);` to give each stone unique physical properties.
*   **Kinetic Combat Math:** In the `OnCollisionEnter(Collision col)` method, calculate the kinetic energy using standard physics: 
    $E = \frac{1}{2}mv^2$ 
    *(In C#: `float kineticEnergy = 0.5f * rb.mass * Mathf.Pow(rb.velocity.magnitude, 2);`)*
*   **Applying Damage/Knockout:** Use `ilspycmd` to inspect the player collision/health scripts (e.g., search for `PlayerHealth`, `TakeDamage`, or `RagdollController`). Cast the `col.gameObject` to the player's health component and call its damage method if `kineticEnergy > StoneDamageThreshold.Value`.
*   **Splitting:** When Threshold 2 is reached, call `PhotonNetwork.Instantiate` twice using a smaller scale, apply an explosive outward force to their Rigidbodies via `rb.AddExplosionForce()`, and finally `PhotonNetwork.Destroy` the original large stone.