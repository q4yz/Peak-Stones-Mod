# Goal: Configuration & Integration

## Progress Checklist
- [ ] Define required mod variables (MaxStones, EventInterval, DamageThreshold, etc.).
- [ ] Bind variables to BepInEx config file (`Config.Bind`).
- [ ] Attempt native menu injection via Harmony (Investigate UI drawing loop if necessary).
- [ ] **Fallback accepted:** Utilize BepInEx ConfigurationManager for the in-game (F1) UI.
- [ ] Verify settings persist and update dynamically during runtime.

## Implementation Suggestions
*   **Settings Framework:** Continue using `BepInEx.Configuration.ConfigEntry<T>` for all mod variables.
*   **Agent Task (Native UI):** If attempting native integration again, use `ilspycmd` to search for classes handling `RunSettingsUI` or the `CustomRunMenu`. Look for the method that iterates over the `SETTINGTYPE` enum to draw the buttons, and write a `[HarmonyPostfix]` patch for that specific method to instantiate custom UI elements.
*   **Current State:** The backend config generation is fully operational.