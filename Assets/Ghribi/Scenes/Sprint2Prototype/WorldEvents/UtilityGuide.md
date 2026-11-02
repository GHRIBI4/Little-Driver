# How to understand the scores

Think of utility as a vote for what the scene should do next.
Zero means "cannot happen now". The highest vote wins.

Every action answers two questions:
1. Is this action allowed now?
2. If allowed, how strong is its vote?

## What makes the vote stronger?

| Action | Higher input means | Maximum vote (Importance) |
| --- | --- | --- |
| Police | Player is further above 50 km/h | 1.00 |
| Pedestrian | Player is closer to the crossing | 0.85 |
| Parked vehicle | Player is closer to an unused selected vehicle | 0.65 |

Importance is your Inspector setting. Higher importance helps that action win.
It is not a probability. For example, 0.85 does not mean an 85% chance.

## Police example

50 km/h is the limit. 55 km/h is the maximum speed.
At 52.5 km/h the player is halfway through that 5 km/h speeding range.
Therefore speed urgency is 0.5 (50%).

The script uses a starting priority plus a bonus:
- Starting priority: 0.6.
- Speed bonus: 0.4 x 0.5 = 0.2.
- Combined priority: 0.6 + 0.2 = 0.8.
- Multiply by Police Importance: 1 x 0.8 = 0.8.

If the police cannot see the player, the final score is 0 instead.
If the player is at or below 50 km/h, the final score is also 0.

The 0.6 and 0.4 are design choices: they give an allowed police action a
starting priority of 60%, with up to another 40% for more serious speeding.
They are named StartingPriority and MaximumBonus in PoliceWarningAction.cs.

## Crossing example

Detection range is 45 m. At 22.5 m the player is halfway into that range,
so closeness is 0.5.

- Starting priority: 0.6.
- Closeness bonus: 0.4 x 0.5 = 0.2.
- Multiply by Crossing Importance: 0.85 x (0.6 + 0.2) = 0.68.

The score is only available while speeding, approaching the marked crossing,
outside cooldown, with enough lead time, and with a random crossing request.
The random chance decides whether to request a crossing; it does not change
the score calculation.

## Parked vehicle example

Detection range is 20 m. At 10 m, closeness is 0.5.

- Starting priority: 0.5.
- Closeness bonus: 0.5 x 0.5 = 0.25.
- Multiply by Departure Importance: 0.65 x (0.5 + 0.25) = 0.4875.

The score is 0 if no unused selected vehicle is within range.

## Which action wins?

If all three examples are allowed at the same time:
Police 0.8 > Crossing 0.68 > Departure 0.4875. Police wins.

Once a warning or crossing starts, it gets a temporary score of 1.1 so it can
finish. Other actions wait at 0. Departing vehicles continue moving after the
one-time departure command, allowing the selector to choose later events.

## Where to look

- In Unity: select World Event GameManager and scroll to the three calculation
  panels. They show the live input, starting priority, bonus, importance,
  numeric calculation, final utility, and any condition blocking the action.
- Each action script: CalculateUtility contains its arithmetic at the top.
- SprintWorldEventManager: Score chooses the formula; GetBlockingReason
  explains eligibility; PoliceSpeedUrgency, CrossingCloseness, and
  DepartureCloseness convert the measurements into numbers between 0 and 1.
