# Kill the Frog: Unity Game (Game Development Assignment 1)

Made with Unity 6 (6000.6.3f1), 2D, using the built-in Input Manager and uGUI.

## How to open and play

1. Open **Unity Hub**, click **Add > Add project from disk**, and choose the `KillTheFrog` folder inside this one.
2. In the Project window, open `Assets/Scenes/KillTheFrog.unity`.
3. Press **Play**. Set the Game view to **16:9** or **1920x1080** so it looks right.

If the scene is missing or you change the generator, use the menu **Kill The Frog > Build Game Scene**. It rebuilds the sprites, sounds, prefabs and the scene.

To make a standalone `.exe`, use **File > Build Profiles > Windows > Build** in the Editor.

## Troubleshooting: "Hold on... Waiting for Unity's code to finish executing"

Unity 6.6 starts background import workers (including Unity Search's indexer) with Direct3D 12 forced. This PC's Intel HD Graphics crashes under D3D12, so the workers die and the Editor waits forever. The project is set up to avoid this:

- `UserSettings/EditorUserSettings.asset`: import worker counts set to 0
- `UserSettings/Search.settings`: `indexOnEditorStartup = false`, and the default `UserSettings/Search.index` is removed
- `Assets/Editor/NoImportWorkers.cs`: re-applies all of the above every time the Editor loads

If it ever freezes again: close Unity in Task Manager, delete `Library/ArtifactDB-lock` and `Library/SourceAssetDB-lock`, and reopen. On another PC with a modern GPU, you can delete `NoImportWorkers.cs`.

## How to play

- Frogs pop up on the pond and hop around. Click or tap a frog to kill it before it escapes.
- **+1** point for each frog you kill.
- **-1** point for clicking where there is no frog (a wrong click).
- **-1 life** when a frog escapes. You start with 3 lives.
- The round lasts 60 seconds. The game ends when time runs out or you have no lives left.

| Level | Reached at | Spawn interval | Frog lifetime | Hop force | Max frogs |
|-------|-----------|----------------|---------------|-----------|-----------|
| 1     | 0 points  | 1.4 s          | 3.0 s         | 2.5       | 3         |
| 2     | 10 points | 1.0 s          | 2.3 s         | 4.0       | 5         |
| 3     | 25 points | 0.7 s          | 1.7 s         | 6.0       | 7         |

You can change these values on the **GameManager** object in the Inspector.

## Where each requirement is met

| Requirement | Where it is done |
|---|---|
| Frogs appear at random positions at timed intervals | `FrogSpawner.SpawnLoop` / `Spawn` (coroutine, random point inside the camera view) |
| Click or tap to kill frogs before they disappear | `GameManager.HandleClick` (mouse click → `Physics2D.OverlapPoint`), `Frog.Escape` |
| A hit raises the score | `GameManager.HandleClick` |
| Missed frogs or wrong clicks reduce score or lives | wrong click: −1 score; escaped frog: −1 life (`GameManager.FrogEscaped`) |
| Unity physics or animation for frog movement | `Rigidbody2D` hops with `AddForce(Impulse)`, linear damping, frog-to-frog collisions, `EdgeCollider2D` walls around the screen; squash and stretch based on velocity |
| UI: scoreboard, timer, start/restart button | HUD (score, level, timer, lives), Start menu, Game Over panel with Restart and Menu buttons |
| Sound effects or animation when a frog is killed | splat sound, `ParticleSystem` splat burst, dead-frog sprite with a squash-and-fade animation, floating "+1" text |
| Extras | 3 levels with a level banner and sound, high score saved with `PlayerPrefs`, blinking warning before a frog escapes, ribbit sound when one escapes |

## Project layout

```
Assets/
  Scripts/GameManager.cs     game rules, input, score, lives, timer, levels, UI, audio
  Scripts/FrogSpawner.cs     timed random spawning and screen-edge walls
  Scripts/Frog.cs            physics hopping, squash/stretch, blink, kill and escape animations
  Scripts/LevelSettings.cs   difficulty settings for each level
  Scripts/BackgroundFit.cs   scales the pond background to fill the screen
  Editor/KillTheFrogBuilder.cs  generates art, sounds, prefabs and the scene
  Art/  Audio/  Prefabs/  Scenes/   generated assets
```

All art and sound is generated in code, so the project has no third-party assets.

## Screenshots for the Word document

If you submit screenshots instead of the project, take these in Play mode (Windows: **Win+Shift+S**):

1. The Start menu
2. Level 1 gameplay, with the HUD and frogs visible
3. A frog being killed (the splat particles and "+1")
4. The "Level 2!" banner, then Level 2 gameplay
5. The "Level 3!" banner, then Level 3 gameplay
6. The Game Over screen showing the final score and Restart button
7. The Unity Editor showing the Hierarchy, the Frog prefab's Rigidbody2D in the Inspector, and one script open
