> **Note:** written by Claude (AI) as working notes while building this mod.

# FaceCam — MODLOG

## Setup
- Game: White Knuckle, Unity 2022.3.62, Mono, built-in render pipeline (PostProcessing v2). Legacy `Input` works (the game itself uses `Input.GetKeyDown`).
- Loader: BepInEx 5.4.23.5 (BepInExPack 5.4.2305) through Thunderstore Mod Manager, profile `Default`
  (`%APPDATA%\Thunderstore Mod Manager\DataFolder\WhiteKnuckle\profiles\Default`).
- Decompiled code: `%USERPROFILE%\WhiteKnuckle-decomp` (outside the repo, never commit it).
- Route: a BepInEx plugin with no Harmony patches. It only reads public statics.

## Threat sources (Assembly-CSharp)
- Mass: `DEN_DeathFloor.instance`; `DEN_DeathFloor.GetHeight()` returns its surface Y, or -100000 when it is inactive or can't kill. Distance = player Y minus surface Y.
- Faces: `DEN_Face.faceInstances` (a static list). The first face spawned gets the private `isPrimary` flag (it owns the screen effects).
- Hunter: `DEN_Hunter.activeHunters` (a static list), aimed at `headObject`.
- Rat King: `DEN_RatKing.activeRatKings` (a static list).
- Remains: `DEN_Remains_Active` has no list of its own. Scan `GameEntity.entities` (as `UT_Remains_Controller` does) and skip any with `active == false`. `DEN_Remains_Inactive` is a decorative pose and not a threat.
- Player: `ENT_Player.playerObject` (static; `GetPlayer()` calls GameObject.Find when it's null, so don't call that every frame), with `.cam` as the main camera.
- HUD (1.2.0): the overlay is uGUI, parented to the root canvas of `player.hands[i].uiInteract` (the hand icons) and inserted just before the hands' branch, and before `gMan.pauseMenu`'s branch if that shares the canvas.
- Subtitle font: `CL_UIManager.instance.subtitleObject.text` (a TMP_Text) gives `.font` and `.fontSharedMaterial`.
- End of run: `CL_GameManager.runHasEnded` (public static), `dead` (private static) and `loading` (private instance) are read by reflection. Pause: `CL_GameManager.gMan.isPaused`.
- Sky gotcha: the skybox writes alpha 0 into an RGBA render texture. IMGUI `DrawTexture(alphaBlend:false)` hid this (1.0), but a uGUI RawImage alpha-blends, so the sky turned see-through and showed the grey border panel behind it as "fog" (1.2). Fix: an RT format with no alpha (RGB111110Float, RGB565 fallback). A plain `CopyFrom` camera looks right. Cloning the player camera (1.3.0) was a misdiagnosis and has been removed.
- Remains (1.3.2): `DEN_Remains_Active.Update` freezes when `meshRenderer.isVisible` is true, and that counts every camera. A transpiler swaps the call for a helper: isVisible AND inside the player camera's frustum, but only while FaceCam is rendering.
- Name-tag font: `CL_UIManager.subtitleObject.text.font` is NOT the font shown in game; copy it from a live `UI_Subtitle` under `subtitleParent` instead.
- Build: target netstandard2.1. On net472, GeometryUtility pulls in netstandard 2.1 (CS1705). With netstandard2.1, Harmony's `CodeInstruction.labels` (`Label` from mscorlib) won't resolve (CS7069), so edit instructions in place.
- Hardcoded game keys: F1, F4, F5, F12, Shift+F12, Ctrl+;, so the mod uses F7.

## Build / package
- `dotnet build -c Release` builds the DLL and copies it into the profile's `plugins/SlopGaming-FaceCam`.
- `./pack.ps1` writes `dist/SlopGaming-FaceCam-<ver>.zip`.

## Status
- Builds cleanly and is deployed to the Default profile. Not yet verified in game.
