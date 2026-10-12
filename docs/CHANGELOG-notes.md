> **Note:** written by Claude (AI) as working notes while building this mod.

# Changelog

## 1.3.1
- Fixed the sky showing as flat fog in the feed (broken since 1.2.0). The sky renders with zero alpha, which made it see-through on the HUD. The feed is now always opaque.
- Back to the plain camera setup from 1.0–1.1. The camera cloning added in 1.3.0 is removed.

## 1.3.0
- The shake is twice as strong by default (`ShakeAngle` 1.2, `ShakePixels` 4).

## 1.2.0
- The feed is now square and sits in the bottom-right corner by default (`Viewport.Size` replaces `Width`).
- The camera stays level with the world instead of rolling with the player's view.
- The name tag uses the game's subtitle font. Distance is now optional and off by default (`ShowName` and `ShowDistance` replace `ShowLabel`).
- The overlay is drawn on the HUD canvas next to the hand icons, so the pause menu draws over it. It hides when the run ends (win or death) and during loading.

## 1.1.0
- Tracks Remains and the Rat King as well. Each can be turned off in the config (`TrackRemains`, `TrackRatKing`).

## 1.0.0
- Initial release: a corner camera that auto-aims at the closest threat (the Mass, Faces or the Hunter), shakes when the threat is close, and toggles with F7.
