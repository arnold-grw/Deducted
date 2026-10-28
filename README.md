# DEDUCTED

A 1-bit, third-person descent through a dystopian megastructure, where every passage has a price and the only thing you have left to sell is yourself.

Made in one week for the 15th 1-BIT game jam. https://itch.io/jam/1-bit-jam-15
**Theme:** *Everything has a price* · **Limitation:** *1-bit (black and white only)*

## Concept

You are released from a facility with no memory and an almost empty account. To move through the city, you pay tolls at gates. To pay tolls, you sell your body to corporate terminals, or swap parts for synthetic replacements that charge you by the second.

- **Sell** a part to VITALIS Reclamation: more money, lose the ability for good
- **Swap / ** a part at CONTINUUM Prosthetics: keep the ability, pay a subscription
- **Pay** the Passage Authority to open gates

Fall into debt and the collectors come for what's left of you. There are no revives. Learn the city, learn the deals, and find the route up.

## Controls

| Input | Action |
|---|---|
| WASD | Move |
| Hold F | Confirm transaction |
| Tab / E | Open status menu (body, balance, subscriptions) |

## Tech

- Unity 6, Universal Render Pipeline
- Low-resolution render target with ordered (Bayer) dithering to 1-bit
- Fixed security-camera perspective
- Target platform: Web (itch.io), Windows as fallback

## Project structure

```
Assets/
  _Project/
    Scenes/       Levels and test scenes
    Scripts/      Gameplay code (economy, interaction, player, collectors)
    Shaders/      1-bit dither and outline shaders
    Materials/
    Models/       Player, clerks, environment
    Audio/
    UI/
  ThirdParty/     Imported assets (see Credits)
```

## Running the project

1. Open the project with Unity 6 (URP).
2. Open `Assets/_Project/Scenes/Main.unity`.
3. Press Play.

To build for the web: **File → Build Profiles → Web**, then build and upload the zipped build folder to itch.io.

## Credits

--animations: [Mixamo](https://www.mixamo.com/)


--sounds: [Freesound.org]
-- Music loop: https://freesound.org/people/deleted_user_2428730/sounds/455025/
-- open menu: https://freesound.org/people/micropolis/sounds/637061/
-- steps: https://freesound.org/people/IENBA/sounds/864782/
-- robot step: https://freesound.org/people/Magnesus/sounds/729437/?
-- lighter: https://freesound.org/people/Alexsani/sounds/130788/
-- inhalte exhale: https://freesound.org/people/Sounds_Fab/sounds/813664/
-- ad jingle: Happy bouncey guitar audio logo (artxmpl-al-03) high by artxmp1 -- https://freesound.org/s/661921/ -- License: Attribution 4.0

-- ad voice lines: [Mixamo]


--models: [Sketchfab]
-- speaker: https://sketchfab.com/3d-models/speakers-low-poly-b2e3d6ecef4e40d994066416c395bf0a

## License
