# Little Driver - Sprint 1 Prototype

## Sprint goal

Build a playable gray-box prototype of US 1.1-1.5: acceleration and braking, two-lane movement, seat belt safety, horn usage, and collision detection. The priority is validating the mechanics before adding final visuals and educational content.

The player follows a fixed road with two forward lanes and premade obstacles. Rayen's main tasks are the seat belt (1.3) and horn (1.4).

Open `Assets/Ghribi/Scenes/Sprint1Prototype.unity` and press Play.

## Main scripts

| Script | What it does |
| --- | --- |
| `SprintPrototypeCar` | Controls the car's state and Rigidbody movement. Holding GO accelerates, releasing slows down, and STOP brakes. LEFT/RIGHT select a lane. It also handles the horn, collisions, finish line, and restart. |
| `SprintSeatBeltDrag` | Lets the player drag the belt handle to the buckle. Releasing near the target snaps it into place and unlocks driving; missing returns it to the start. |
| `SprintHoldControl` | Detects pressing and releasing GO, STOP, and HORN. It tells the car which action is being held and tracks the initiating finger. |
| `SprintPrototypeHUD` | Shows the seat belt panel before driving, then the driving controls. Updates speed, belt status, counters, and feedback messages. |

The car reads keyboard input in `Update`, applies physics movement in `FixedUpdate`, and follows the car with the camera in `LateUpdate`.

## How everything connects

```
Player input
    |
    +-- Drag belt --> SprintSeatBeltDrag --> car.Buckle()
    |
    +-- Hold buttons --> SprintHoldControl --> car input flags
    |
    +-- Lane buttons / keyboard --> car actions
                                      |
                              SprintPrototypeCar
                                |           |
                            Rigidbody   AudioSource
                                |
                       State read by the HUD
```

- **Seat belt:** the car stays stopped until `IsBuckled` becomes true. The HUD then hides the belt panel and shows driving controls.
- **Movement:** speed changes gradually, while lane selection moves the car smoothly between two fixed X positions. Braking takes priority over acceleration.
- **Horn:** an AudioSource loops a temporary beep while HORN or H is held, and stops when released.
- **Collisions:** the player's Rigidbody and BoxCollider contact an obstacle collider. `OnCollisionEnter` updates the counter and prints `Player collided with 'Obstacle ...' (collision #1).` in the Console.
- **Restart:** the car resets its position and state, then raises a `Restarted` event so the HUD and belt interaction reset too.

The Canvas uses Unity's Input System UI module for pointer input and TextMeshPro for labels. Scene references and button actions are already wired.

## Controls and checking

- Drag the belt into the buckle before driving.
- Hold GO or W/Up Arrow to accelerate; release to coast.
- Hold STOP or Space/Down Arrow to brake.
- Use LEFT/RIGHT or A/D/arrow keys to select a lane.
- Hold HORN or H to sound the horn.
- Press RESTART or R to reset.

`Editor/SprintPrototypeSceneBuilder` created the scene and connected its components. It does not run during gameplay.

`Editor/SprintPrototypeChecks` provides **Little Driver \> Check Sprint 1 (while playing)**. It checks the belt, movement, lanes, held horn, collisions, finish, and restart. The Editor gameplay checks passed; physical Android testing is still pending.

## Prototype limits

The scene uses gray boxes, temporary English labels, and a generated horn sound. Final art, Arabic guidance, cutscenes, and collision penalties/recovery are later work. US 1.5 currently covers collision detection and feedback only.
