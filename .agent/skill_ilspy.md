# Agent Skill: ILSpy Assembly Decompiler

## Purpose
Enables the agent to reverse-engineer and inspect the compiled C# assemblies of the game (`Assembly-CSharp.dll`) using the command-line interface tool `ilspycmd`. This allows the agent to find internal class names, variables, methods, and structures required for writing accurate Harmony patches and custom components.

## Prerequisites
The host machine must have the .NET SDK installed and the global ILSpy tool configured:
`dotnet tool install -g ilspycmd`

## Target Paths
The agent must verify the existence of the primary assembly path. If it does not exist, the agent must seamlessly transition to using the local fallback path relative to the project root.
*   **Primary Assembly:** `D:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\Managed\Assembly-CSharp.dll`
*   **Fallback Assembly:** `game_files\Assembly-CSharp.dll`
*   **Output Workspace:** `DecompiledPEAK`

## Helper Script Execution
The project includes a unified batch script located at `scripts\ilspy_helper.bat` to handle path resolution automatically. The agent should execute this script from the project root workspace.

### Protocol 1: Inspect a Specific Class Name
Use this when you know the class name (e.g., `Player`, `Item`, `DamageHandler`) and need to see its internal methods and structure.
*   **Command:** 
    `.\scripts\ilspy_helper.bat type [ClassName]`
*   **Agent Rule:** Read the raw standard output text directly into your context window. Do not attempt to rewrite it.

### Protocol 2: Search for Keywords Across All Type Names
Use this when looking for generic terms like "Health", "Damage", "Ragdoll", or "Spawning" to identify what classes the game developers created.
*   **Command:** 
    `.\scripts\ilspy_helper.bat search "[Keyword]"`
*   **Agent Rule:** Parse the returned list of matching namespaces and type names to isolate candidates for deeper inspection using Protocol 1.

### Protocol 3: Full Assembly Decompilation
Use this only if you need a comprehensive, text-searchable repository of the entire codebase to trace complex logic or references across multiple systems.
*   **Command:** 
    `ilspycmd -p "game_files\Assembly-CSharp.dll" -o "DecompiledPEAK"`
*   **Agent Rule:** Once executed, utilize local directory search tools (e.g., `grep` or file reading) to investigate the generated `.cs` source files.

## Immediate Agent Tasks
1. Verify which assembly path is currently valid.
2. Execute **Protocol 2** (`.\scripts\ilspy_helper.bat search Player`) to locate the components managing player interactions and status effects.
3. Execute **Protocol 2** (`.\scripts\ilspy_helper.bat search Damage`) to find the exact class and method signatures responsible for reducing player health.
4. Update the `goal_stone_entity.md` checklist with the exact method names found (e.g., `TakeDamage`, `ApplyDamage`, `Knockout`).