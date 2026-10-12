> **Note:** written by Claude (AI) as working notes while building this mod.

# FaceCam

A small square "face cam" in the bottom-right corner of your screen, except it's pointed at whatever is hunting you.

- The camera sits where your own camera is, but it automatically turns to face the **closest threat**:
  - **The Mass**, measured straight down to its surface
  - **Faces**, each one tracked and labelled separately ("Face", "Face #2", ...)
  - **The Hunter**
  - **Remains** (only active ones)
  - **The Rat King**
- The feed stays level with the world. It doesn't roll or spin as you look around.
- A name tag in the subtitle font shows what you're looking at. Showing the distance is optional and off by default.
- When the threat is closer than 10 m, the feed starts to shake slightly.
- It's drawn on the HUD layer with the hand icons, so the pause menu covers it. It hides itself when you win, die or the run ends.
- **Off by default.** Press **F7** to turn it on or off.

## Configuration

After you launch the game once with the mod, settings are saved in
`BepInEx/config/com.slopgaming.facecam.cfg`. You can also edit them in the Thunderstore / r2modman config editor.

| Section | Setting | Default | What it does |
|---|---|---|---|
| General | ToggleKey | F7 | Turns the feed on or off |
| General | EnabledOnStart | false | Whether the feed is on when the game launches |
| General | HideWhenNoThreat | true | Hides the feed when nothing is being tracked |
| General | MaxTrackDistance | 250 | Ignores threats further away than this (metres) |
| Threats | TrackMass / TrackFaces / TrackHunter / TrackRemains / TrackRatKing | true | Picks which threats are tracked |
| Viewport | Corner | BottomRight | Which corner the feed sits in |
| Viewport | Size | 0.28 | Side length of the square feed, as a fraction of screen height |
| Viewport | Margin, BorderThickness, BorderColor | 24, 3, light grey | Layout and style (pixels) |
| Viewport | ShowName | true | Shows the name tag |
| Viewport | ShowDistance | false | Adds the distance to the name tag |
| Camera | FieldOfView | 60 | Field of view of the feed |
| Camera | AimSmoothing | 10 | How fast the camera turns (0 = instant) |
| Camera | ResolutionScale | 1 | Set to 2–4 to render the feed at lower resolution for more FPS |
| Shake | ShakeDistance | 10 | The shake starts below this distance (0 turns shake off) |
| Shake | ShakeAngle, ShakePixels, ShakeSpeed | 1.2, 4, 18 | How strong and fast the shake is |

## Notes

- The feed is an extra camera, so it costs some performance while it's on. Raise `ResolutionScale` if you need more FPS.
- Made by SlopGaming with the help of AI (Claude).
