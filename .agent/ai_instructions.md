# AI Agent Instructions (Project Stones)

## Introduction
Welcome, Agent. You are assisting in the development of the "Stones" mod for the game PEAK. This folder (`.agent/`) is your primary workspace and memory bank. Do not make assumptions about the game's architecture; rely on the provided tools to inspect the compiled game assembly.

Your primary directive is to read these files, understand the project boundaries, use your provided skills to interact with the codebase, and update the goal files as you make progress.

## Directory Overview & File Purposes

### 1. The Core Vision
*   **`vision.md`**
    *   **What it is:** The strict, high-level design document for the mod.
    *   **Agent Action:** Read this first to understand *what* we are building. It contains the rules, mechanics, and design philosophy. It does not contain code. Do not violate the rules set in this document.

### 2. The Goal Checklists (The Roadmap)
These files break the vision down into actionable development steps. 
*   **`goal_configuration.md`**
    *   **What it is:** The checklist and implementation notes for the mod's settings and UI (BepInEx Configuration).
*   **`goal_environment.md`**
    *   **What it is:** The checklist and implementation notes for world generation, the Volcano event, screen shakes, and despawning logic.
*   **`goal_stone_entity.md`**
    *   **What it is:** The checklist and implementation notes for the custom Stone prefab, its physics, and the kinetic-energy-based combat system.
    *   **Agent Action (For all goal files):** Read these to determine the current state of the project. When you complete a task, you must update the Markdown checklist (`[ ]` to `[x]`) in the respective file to maintain an accurate state of progress.

### 3. Agent Skills (Tools)
*   **`skill_ilspy.md`**
    *   **What it is:** The instruction manual for your local decompiler tool.
    *   **Agent Action:** Read this to understand how to invoke `ilspycmd` via the terminal. You must use this skill to look up native game classes, methods, and variables before writing Harmony patches or attempting to hook into the game's physics/health systems. 
    *   **Note:** This skill relies on an external batch script located at `scripts\ilspy_helper.bat` in the project root.

## Execution Flow
When you are initialized or asked to perform a task, follow this loop:
1.  **Assess:** Read `vision.md` and the relevant `goal_*.md` file to understand the current objective.
2.  **Investigate:** If the task requires interacting with the game's native code (e.g., finding the Player Health script), execute the commands outlined in `skill_ilspy.md` to extract the raw C# from the game assembly.
3.  **Implement:** Write the necessary C# code (e.g., Unity MonoBehaviours, Harmony Patches, BepInEx configurations).
4.  **Record:** Update the relevant `goal_*.md` file to check off completed items and add any new implementation notes or discovered method names.