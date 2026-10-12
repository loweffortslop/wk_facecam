Content warning: the mod is pretty much entirely vibe-coded with Claude and Universal Modder, but had extensive playtesting, so hopefully no bugs were missed.

# FaceCam

A small streamer camera in the corner that tracks whatever is currently trying to kill you and is the closest. Tracks the mass, faces, hunter, the rat king and the remains. Priority by distance. 
The sneaky bastards in the endless abyss sniped me one too many times, and I just wanted to know where they are at without turning around.

![FaceCam tracking a Face in the Endless Abyss](https://raw.githubusercontent.com/loweffortslop/wk_facecam/main/docs/screenshots/face.jpg)
![FaceCam tracking the Hunter](https://raw.githubusercontent.com/loweffortslop/wk_facecam/main/docs/screenshots/hunter.jpg)
![FaceCam tracking Remains](https://raw.githubusercontent.com/loweffortslop/wk_facecam/main/docs/screenshots/remains.jpg)

Configurable, comes with the following settings

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


Possible untested mod conflicts:
-Remains had to be tweaked to allow for the mod camera to look at them, so this may cause issues with the multiplayer mod