# Story Mode opening prototype

This is a small, optional opening-story layer on top of the existing ecology loop. It does not replace Sandbox Mode or add a second ecology counter.

## Try it

- Press **F5** to switch between Story Mode and Sandbox Mode.
- While Story Mode is on, the father marker follows behind the player.
- Each existing damage event lowers the current ecology balance. At `StoryCaptureBalanceThreshold` (default `-10`), the father leaves to retrieve the lost axe; after a short beat he disappears and the `FatherCaptured` event fires.
- Other story code can subscribe to `ObliqueForest.FatherCaptured` to begin the rescue section. No rescue puzzle or scene transition is included here.

## Content and limits

- The father sprite is intentionally a clearly tagged temporary marker. Assign a verified sprite to `StoryFatherGlyph` after the user supplies it; the marker is not a Dongba pictograph.
- The lost-axe departure and capture are the game's adaptation of the source story. The current trigger threshold, follow behavior, timed capture beat, and event hook are prototype mechanics.
- Story Mode starts disabled so the existing sandbox loop is unchanged. It can also be enabled in the component Inspector or with `--story-mode-preview` for a preview build.
- The father uses the existing forest walk map and clearance checks. He does not cross the river or tree obstacles intentionally; if no route can be found, he waits and retries.
