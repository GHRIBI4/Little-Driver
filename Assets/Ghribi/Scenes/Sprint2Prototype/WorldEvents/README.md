# Sprint2 world events

For a beginner-friendly scoring explanation, read UtilityGuide.md. Each
action script now owns a CalculateUtility method with named steps. The
GameManager Inspector shows the full arithmetic and why an action is blocked.

Select **World Event GameManager** in Sprint2Prototype. All actor references,
thresholds, probabilities, distances, cooldowns, importance weights, and live
utility scores are visible in its Inspector. Select it with Gizmos enabled to
see police vision (cyan), pedestrian crossing paths (yellow), and vehicle
destinations (green).

The saved Behavior Designer tree is Entry > Repeat Forever > Utility Selector:
PoliceWarningAction, PedestrianCrossingAction, ParkedVehicleDepartureAction,
and WaitForWorldEvent. Each event has its own script. The highest positive
utility wins; idle has utility 0.001. Police and crossings finish their selected
action before another begins (committed utility 1.1). A vehicle departure is a
one-time command: the vehicle continues moving while the selector considers
later events. Ties prefer police, then crossing, then departure.

- Player maximum speed: 55 km/h. Speeding threshold: strictly above 50 km/h.
- Police: parked visual/audio warning, forward cone, configurable distance
  and angle, optional line-of-sight blocking, alternating roof beacons, and a
  temporary generated siren. Replace Siren Clip when final audio is ready.
  The default warning lasts 1.5 seconds so the selector can consider the
  nearby crossing before a speeding player passes it.
- Pedestrians: existing Crossing Adult and Crossing Child. Random decisions
  happen at intervals while a speeding player approaches the marked crossing.
  Once selected, a pedestrian finishes walking to the opposite sidewalk even
  if the player slows down. Subsequent eligible events allow a return trip.
  The existing idle clips and included walking clips are used by new local
  controllers; original character controller assets are preserved.
- Parked vehicles: only entries you put in Parked Vehicles can activate.
  ParkedBus is configured. It moves forward at 18 km/h to its Road End marker
  and disappears there. Its Activated flag prevents a second departure.
- Player Restart restores pedestrian sides, bus position and visibility,
  activation flags, cooldowns, and police effects.

To add a parked vehicle, add an Inspector entry, assign its root Transform,
and create/assign a Road End Transform straight ahead at the road boundary.
Add a kinematic Rigidbody to moving vehicles for physics contacts. Move the
crossing endpoint markers only along marked crossings. Changing Inspector
values during Play mode is temporary; edit outside Play mode to save tuning.

Police utility = importance * (0.6 + 0.4 * speeding severity), gated by vision,
speeding, and cooldown. Crossing utility = importance * (0.6 + 0.4 * proximity),
gated by a random request, speeding, approaching, cooldown and minimum lead
time. Departure utility = importance * (0.5 + 0.5 * proximity), gated by an
unactivated selected vehicle within range. Inputs are normalized to 0..1.

Editor setup is available at Little Driver > World Events > Set Up Sprint2
Events. This rebuilds references/tree from the original named actors and
replaces the manager's pedestrian/vehicle lists, so use it for initial setup,
not after manually customizing those lists.

For a repeatable controlled check, enter Play mode and temporarily add
WorldEventPlayChecks to the manager. It logs PASS/FAIL after exercising police
vision and warning, utility priority, crossing/return, departure, and restart.
The check component is Editor-only and must not be saved on a scene object.
