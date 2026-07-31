# Stones

The **Stones** mod for PEAK introduces a dynamic, physics-based environmental item and catastrophic weather system to the game. It transforms the world by populating it with interactive stones, adding high-stakes kinetic combat mechanics, and bringing unpredictable danger from the skies.

---

## Features

### 🌋 The Volcano Event
A randomized, catastrophic weather event that completely alters the atmosphere and environment:
* **The Buildup:** Triggers an intense, immersive camera shake accompanied by a dramatic skybox shift to deep red and gray tones.
* **The Climax:** Following a 5-second suspense delay, a fierce barrage of stones rains down across the map.
* **Performance Cleanup:** Volcano-spawned stones automatically despawn when out of player proximity to maintain optimal frame rates.

### 🪨 The Stones
Stones are fully interactable objects behaving like standard world items (similar to the base game's coconut):
* **Velocity & Mass Calculations:** Impact force is calculated dynamically based on the stone's physical traits and speed.
* Striking a player to knocks them out.
* **Shatter Mechanic:** Striking a target with high speed deals damage, completely shatters the stone, and splits it into two smaller, independent flying fragments.
