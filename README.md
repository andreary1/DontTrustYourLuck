# Don't Trust Your Luck!

<img width="627" height="627" alt="Don't Trust Your Luck" src="https://github.com/user-attachments/assets/4378d997-809a-4b7b-8ae9-7dafa4474c3e" />

A competitive party board game made in Unity for **Unity Game Jam 2026**.

Up to four animal lottery cards race around a city-diorama board. One golden **lucky number** wins the match — but you must land on it *exactly*. Every round a new **unlucky number** is called, and ending your move on it (or right next to it) knocks you out. Roll two dice, pick one, play your cards, and push your rivals into bad luck.

Playable in the browser (Unity Web / WebGL).

## The Story

An old, enchanted deck of printed animal cards travels from fair to fair across Latin America. Every night, in a different town square, the CANTADOR shuffles the deck and sings fortunes in rhyming verse.

The Jaguar, the Capybara, the Monkey and the Toucan are figures from that deck who came to life. Each of them is sure they were born lucky. Tonight, the Cantador draws a number, and the first beast to stop exactly on it becomes the fair's lucky card.

<img width="953" height="533" alt="AdobeExpressPhotos_175e15bdd8fb456cac9c6e8626678b39_Edited" src="https://github.com/user-attachments/assets/2d293e1b-eefb-4832-9bc1-2252ddf2fcce" />

But above the square flies AZAR, a woodcut vulture. Every round he picks a new tile to perch on, and the shadow of his wings darkens it and the tiles beside it. Step into his shadow and your card tears and goes back into the deck.

So roll your dice — you'll get a pair,

but only one will take you through.

Your luck can lie, your friends will cheat;

the choosing is the part that's true.

The deck is shuffled, the candle's lit:

don't trust your luck. Trust what you do.

That's the heart of the game: you always roll two dice, but you choose only one. Luck deals the options; you make the call.

## Inspirations

- Mexican Lotería: the "cantor" who calls out illustrated cards, and the look of aged printed game cards.
- Brazilian cordel literature: the sextilha rhyme scheme of the opening verses, the repentista singers, and the bold lines of woodcut prints (xilogravura).
- Northeastern Brazilian popular prints and lottery tickets, reimagined as a visual language (no real betting involved).
- Brazilian samba from the 1920s–30s (the era of Noel Rosa and Sinhô) and baião: the soundtrack is ORIGINAL music composed for this game in that spirit. No recordings or melodies were used.
- The vibrant line and northeastern fauna of Brazilian modernist painting, especially the spirit of Aldemir Martins' work, as a general mood for colour and line. No artwork was copied, and there is no affiliation or endorsement.
- The city squares of Oaxaca and the beachfront of Fortaleza, Ceará.

<img width="953" height="533" alt="Fortaleza" src="https://github.com/user-attachments/assets/6d44e330-26ae-40cb-8dec-1b55c44c78e9" />

## How to play

- **Lucky number:** drawn once per match (15–30). Stop exactly on it to win. The board is a 30-tile circuit; after tile 30 it continues from tile 1.
- **Danger zone:** the unlucky number changes every round. End a move on it or on a neighbouring tile and you are out, unless a Shield saves you.
- **Your turn:** roll two dice and move by **one** of them. Landing on a rival pushes them 3 tiles back, and their new tile counts, good or bad.
- **Cards:** play one per turn. You start with 2 and draw one more every two rounds (max 3).
  - **Lucky Roll**, **Shield**, **Retreat**, **Swap** (trade tiles with your nearest rival).
  - Debuffs for rivals: **Freeze**, **Heavy Feet**, **Cracked Die**, **Sticky Bad Luck**. They hit the target next round, for one round.
- **Modes:**
  - **Quick Match** ends with the last animal standing or at round 15 (closest to the lucky number wins).
  - **Series** awards a point per round won; first to 3, 5 or 7.
- 2–4 players: you against bots, or hot-seat on one computer.

## Controls

| Action | Input |
|---|---|
| Select dice, cards, menu buttons | Mouse click |
| Back / close panel | `Esc` |
| Skip intro text | Click or any key |

## Running the game

### In the browser (built version)

Unity Web builds do not run from `file://`. Serve the build folder over HTTP:

```bash
python -m http.server 8081 --bind 127.0.0.1 --directory Builds/WebRelease
```

Then open <http://127.0.0.1:8081>.

### In the Unity Editor

1. Install **Unity 6000.6.8f1** (see `ProjectSettings/ProjectVersion.txt`) with the **Web Build Support** module.
2. Open the project folder in Unity Hub.
3. Open `Assets/_Game/Scenes/GameBoard.unity` and press Play.

### Building for the web

1. **File > Build Profiles**, select **Web - Desktop - Release** and make it active.
2. Build to `Builds/WebRelease`. The profile uses Gzip compression with decompression fallback, so the build works on itch.io and simple static hosts.
3. For itch.io, zip the *contents* of the build folder (`index.html` at the zip root) and mark the upload as playable in the browser.

## Tests

The rules live in a pure C# domain layer covered by EditMode tests; Unity integration has PlayMode tests.

In Unity: **Window > General > Test Runner**, then run **EditMode** and **PlayMode**.

## Project structure

```text
Assets/_Game/
  Scripts/
    Domain/          Pure game rules: board, dice, cards, turns, victory (no Unity dependencies)
    Gameplay/        Match flow and bots on top of the domain
    Infrastructure/  RNG and other services
    Presentation/    MonoBehaviours: board, pieces, HUD, menus, audio, narration
    Editor/          Scene/asset builders and procedural art and music generators
  Tests/             EditMode and PlayMode tests
  Data/              ScriptableObjects: match config, cards, characters, audio library
  Art/ Audio/        Generated textures, sprites, materials, music and SFX
  Scenes/            GameBoard (main scene)
docs/                Game design, technical design, art direction, narrative, QA notes
```

Gameplay rules are deterministic and separate from presentation, with random numbers behind a seedable service.

## Credits

**Team**
- andreary1 (André Ary Castelo): design and development
- assefjl-gif (Jorge Assef Lutif Filho): design and development

**Assets**
- Art, music and sound effects: original, made for this project (procedurally generated by the project's own editor scripts).
- Visual inspiration: Oaxaca (Mexico) and Fortaleza, Ceará (Brazil).
- Font: [Jersey 25](https://github.com/scfried/soft-type-jersey) by The Soft Type Project Authors, licensed under the SIL Open Font License 1.1 (`Assets/_Game/Art/Fonts/Jersey25/OFL.txt`).
- Engine: Unity 6, Universal Render Pipeline.
