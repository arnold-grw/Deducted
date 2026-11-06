# DEDUCTED

![DEDUCTED in-game screenshot](Docs/screenshot_1.png)

A 1-bit, third-person descent through a dystopian city block. Your account balance is your health: sell your body to VITALIS, rent it back from CONTINUUM by the second, and keep the balance above zero or the collectors take what is left of you.

Made in 7 days for the [15th 1-Bit Game Jam](https://itch.io/jam/1-bit-jam-15) · Theme: *Everything has a price*

> **Note:** For now, this repository contains an **outdated, incomplete snapshot** of the project, **not the released version**. It only includes the test scene and the basic components: the player with the different body states, NPCs and the collector enemies. Finished levels and the main menu are not part of it.

## Tech stack

- Unity **6000.3.15f1** (Unity 6), URP **17.3.0**
- Input System **1.19.0** (new Input System only), AI Navigation **2.0.12**, uGUI + TextMeshPro
- Targets: Web (itch.io) and Windows

## What's in here

All code lives in `Assets/_Project/Scripts/<Area>/` (namespace `Deducted.<Area>`); values are serialized fields with tooltips.

- **Economy:** `BalanceManager` is the single transaction rule (every deal becomes a receipt line); `SubscriptionManager` bills synthetic parts per second.
- **Body:** `PlayerBody` with six parts (eyes, ears, legs), each *Organic*, *Synthetic* or *Missing*. Missing eyes black out half the screen, missing ears mute a side, legs change the movement form.
- **NPCs and shops:** page-based dialogues with shop pages (sell parts, rent synthetic ones, buy cigarettes, sell your name) and hold-to-buy.
- **Collectors:** NavMesh enemies that hunt the player once the balance is negative and reclaim organic parts on contact.
- **Rendering:** a full-screen 1-bit pass (`Shaders/OneBit.shader`, pixelation + Bayer dithering, two colours) as a URP Full Screen Pass Renderer Feature. UI canvases are rendered through the camera so they are dithered too.
- **Editor tools:** menu *Deducted/* builds most of the UI from code.

## Running

1. Open the `Deducted/` folder with Unity **6000.3.15f1**.
2. Open `Assets/_Project/Scenes/Test.unity` and press Play.

Binary assets are stored with Git LFS.

## Credits

The game uses third-party models, animations, sounds and fonts. Full credits and licenses are listed in the **in-game credits** of the released version on itch.io.
