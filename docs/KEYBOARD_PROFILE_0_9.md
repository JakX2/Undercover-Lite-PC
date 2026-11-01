# Undercover Lite PC 0.9: Driving-first keyboard input profile

Version 0.9 adds a **real, opt-in PCSX2 Qt input profile** for the first controller port. It is designed around the in-game Undercover PS2 Controls screen and our already-verified **Cross → W** sprite. Unlike the older PC Input Lab's editable *labels*, the generated profile actually encodes `[Pad1]` button-to-key bindings.

**Status:** source and synthetic integration tests, pending PCSX2 gameplay validation. This is **not** automatic context-dependent input remapping, and the installer does not activate the profile without the user selecting it in PCSX2.

## Proposed default layout

| Keyboard | PS2 input | Undercover driving action | Menu limitation |
|---|---|---|---|
| W | Cross | Accelerate | Also Accept in menus |
| S | Circle | Brake / reverse | Also the Circle action in menus |
| A / D | Left stick left / right | Steering | Not D-pad navigation |
| Arrow keys | D-pad | Map, navigation and other D-pad actions | Menu direction |
| Space | Triangle | Handbrake | Also Back in menus |
| X | Square | Speedbreaker | Also Delete in name entry |
| Q / E | L1 / R1 | Shift down / up | Also previous/next tab |
| C | L2 | Alternative handbrake | Game-dependent |
| Shift | R2 | Nitrous | Same as R2 in menus |
| F | Start | Pause / finish name entry | Start in all contexts |
| R | Select | Reset car | Select in menus |
| V | R3 | Change view | Right stick click |
| Z | L3 | Left stick click | Context dependent |
| I / J / K / L | Right stick | Freelook | Right analog camera |

The profile intentionally leaves the **left stick's vertical axis unbound**. The PS2 game uses steering on the horizontal axis. Test this in actual gameplay before considering mouse or additional analog bindings. Digital keyboard steering may feel different from analog stick movement; the profile starts with PCSX2's normal digital-to-axis behavior rather than unverified custom steering physics.

## Install and activate

1. Exit the game and close PCSX2.
2. Run Undercover Lite PC and keep **Separate PCSX2 settings and saves** enabled.
3. Open **PC input / name-entry lab**.
4. Click **Install driving profile**, review the warning, and approve.
5. The launcher creates `UndercoverLite-DrivingFirst.ini` in PCSX2's isolated `inputprofiles` directory. It does not edit `PCSX2.ini`, shared input bindings, a game settings override, BIOS or the game image.
6. Open **Configure PCSX2**. Right-click Undercover in the game list, select **Properties**, and on the **Summary** tab choose **UndercoverLite-DrivingFirst** under **Input Profile**.
7. Test keyboard input and steering in game; visit the menu and name-entry screen as well. Revert to **Shared** in the same dropdown if the control scheme is unsuitable.

PCSX2's official documentation confirms that input profiles must be assigned to the game in Game Properties and **cannot dynamically switch while playing**: https://pcsx2.net/docs/configuration/controllers/#input-profiles

The isolated data directory layout observed with the project's PCSX2 2.8.2 package is typically:

```text
%LOCALAPPDATA%/UndercoverLitePC/pcsx2-data/PCSX2/inputprofiles/UndercoverLite-DrivingFirst.ini
```

Older installations may use a direct `pcsx2-data/inputprofiles` directory. The installer detects existing layouts. If you've customized PCSX2's **Input Profiles** folder in its own settings, verify the actual location manually. The installer only targets an isolated Undercover Lite data directory.

## Safety guarantees

- Preview/inspection performs no writes.
- Installation is opt-in, never overwrites an existing profile with different contents, and uses a staged file move.
- A second install with identical contents is a no-op.
- Installation refuses non-isolated PCSX2 configurations and asks the user to configure an external profile manually.
- **The active profile is never changed automatically.** The user must choose it under game Properties.
- No ISO, BIOS, installed PCSX2 binary, or game texture files are copied into this repository.

## Important remaining work

Our 0.5b glyph themes encode image labels, **not** input behavior. This 0.9 profile matches the **driving-first** glyph theme for the main captured symbols, but menus still inherit the same PS2 buttons. That means you will see and use **W for Accept** unless a future game-side context-aware mapping is implemented.

The first actual in-game validation should confirm that W accelerates and confirms menus, A/D steer correctly, F pauses, Q/E shift, and Shift triggers nitrous. Visual inspection of the controller diagram introduced in 0.7 is still required. A genuine PC-native name-entry field remains a separate longer-term research milestone.
