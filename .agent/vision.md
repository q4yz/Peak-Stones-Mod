# Project Vision: The Stones Mod

## Core Objective
To introduce a dynamic, physics-based "Stone" item into the game world, supported by natural world generation, a catastrophic weather event, and a kinetic-energy-based combat system.

## Configuration & Integration
The mod must be highly configurable by the player, adhering to a strict priority hierarchy for the user interface:
*   **Primary Goal:** Integrate seamlessly into the game's existing, native settings menu (e.g., alongside default hazards and modifiers).
*   **Secondary Goal:** Utilize standard, shared mod-configuration frameworks (e.g., BepInEx ConfigurationManager) if native integration is not viable.
*   **Fallback Goal:** Implement a custom settings menu only if the above options are exhausted.

## Environmental Mechanics
The world must feel dynamic and dangerous through two distinct spawning systems:
*   **Natural Generation:** Upon loading into the world, stones must spawn organically across the map, acting as permanent fixtures of the environment until interacted with.
*   **The Volcano Event:** A randomized, catastrophic weather event that disrupts gameplay. 
    *   **The Buildup:** The event must trigger a severe camera shake and shift the skybox to deep red and gray.
    *   **The Climax:** Following a 5-second suspense delay, a barrage of stones must rain down from the sky.
    *   **Cleanup:** To maintain game performance, stones spawned by the volcano must automatically despawn if no players are within proximity.

## The Stone Entity
The Stone is a new, interactable physics object with highly variable properties.
*   **Interactivity:** Players must be able to pick up and throw stones, mirroring the behavior of the base game's coconut item.
*   **Variable Properties:** Stones must generate with randomized mass and size to create unpredictable physics interactions.
*   **Kinetic Combat System:** Stone impacts must calculate energy based on mass and velocity.
    *   **Threshold 1 (High Energy):** Striking a player with sufficient force knocks them out.
    *   **Threshold 2 (Extreme Energy):** Striking a player with massive force deals additional damage, shatters the stone, and splits it into two smaller, independent stones.