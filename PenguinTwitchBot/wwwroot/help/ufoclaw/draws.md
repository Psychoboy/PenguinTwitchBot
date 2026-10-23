# UFO Claw Draws

Create independent named prize sets in **Minigames > UFO Claw**, add entries, and save settings. Labels identify the prize on stream; values are optional data for actions, not automatic payouts. Relative weights control odds: weights 1 and 3 give 25% and 75%. Zero-weight or disabled entries cannot win. Equal-sized objects do not imply equal prize odds.

Select Prizes and a saved set, then Show and Grab. The server selects the winner before animating the mechanical claw. Teasing, overlapping objects and horizontal motion never change the selected result. Every accepted grab wins. Another grab is rejected while busy.

For viewers, Open Fresh Entries clears the previous viewer roster and removed-winner list. Viewers enter with `!joinclaw` (or its configured alias). Each identified, non-bot viewer gets one equal entry. Close Entries stops new joins without hiding the overlay. Joins during a grab affect the next grab only. Wheel viewer entries are independent.

Remove Winners excludes each selected entry until Reset Winners or a fresh viewer enrollment. Duplicate labels remain separate entries. A removed viewer cannot regain eligibility by joining again. Exhaustion requires an explicit reset; there is no implicit refill or losing grab.

Save applies editing changes to future grabs, not one already accepted. Hide is presentation-only: the draw still completes, including its trigger. Sound, travel speed, grab duration, result hold, teasing, auto-hide, UFO artwork and per-prize images are configurable. Changes in the Overlay Editor affect the same saved claw settings.