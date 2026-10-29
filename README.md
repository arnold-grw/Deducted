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
-- Techno loop: techno rumble beat 127 bpm.wav by deleted_user_2428730 -- https://freesound.org/s/455025/ -- License: Attribution NonCommercial 4.0
-- open menu: External-storage-enclosure-switch-on-and-hard-drive-spin-up.wav by micropolis -- https://freesound.org/s/637061/ -- License: Attribution 4.0
-- steps: Walking In Socks by IENBA -- https://freesound.org/s/864782/ -- License: Creative Commons 0
-- robot step: Robot walk, looped by Magnesus -- https://freesound.org/s/729437/ -- License: Creative Commons 0
-- heavy robot step: MECH STEPS - 1.wav by jdpstar -- https://freesound.org/s/538402/ -- License: Attribution 4.0
-- lighter: Butane Lighter Flip Open by Alexsani -- https://freesound.org/s/130788/ -- License: Creative Commons 0
-- inhalte exhale: In & Out Breath by Sounds_Fab -- https://freesound.org/s/813664/ -- License: Attribution 4.0
-- ad jingle: Happy bouncey guitar audio logo (artxmpl-al-03) high by artxmp1 -- https://freesound.org/s/661921/ -- License: Attribution 4.0
-- hit: Major punch by janbezouska -- https://freesound.org/s/399183/ -- License: Creative Commons 0
-- death: Cinematic Impact Boom Metalic by EminYILDIRIM -- https://freesound.org/s/535413/ -- License: Creative Commons 0
-- ear ring: Ear Ringing Long (Extended Falloff) by deleted_user_9482149 -- https://freesound.org/s/456698/ -- License: Creative Commons 0
-- notification ping: Retro Inspect Sound, UI, or In-Game Notification.wav by LilMati -- https://freesound.org/s/658266/ -- License: Creative Commons 0

--hanger ambience: Industrial Hangar Ambience – Sci-Fi Factory & Heavy Machinery by TommasoMotteran -- https://freesound.org/s/875395/ -- License: Attribution 4.0
-- eerie ambience: Big Sci-Fi Ambience by IanStarGem -- https://freesound.org/s/269596/ -- License: Creative Commons 0
-- guitar song: Acoustic performance in Ableton Live by ValentinSosnitskiy -- https://freesound.org/s/511819/ -- License: Attribution 4.0

-- ad voice lines: [ElevenLabs]


--models: [Sketchfab]
-- speaker: https://sketchfab.com/3d-models/speakers-low-poly-b2e3d6ecef4e40d994066416c395bf0a is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/)

-- robot: "Lonely bot" (https://skfb.ly/6SIXZ) by Mr Gicu is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/).

## License
