# 🐤 Flappy Bird

A classic Flappy Bird clone built from scratch with **Windows Forms** and **C# (.NET Framework 4.7.2)**.

Navigate your bird through an endless stream of pipes, survive surprise **dungeon modes**, and chase your high score!

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)
![License](https://img.shields.io/badge/License-Free%20to%20play-green)

---

## 🎮 Gameplay

- Press **Space** to flap and keep the bird airborne
- Dodge the top and bottom pipes scrolling towards you
- Every pipe you pass earns **+1 point**
- The game **speeds up** as your score increases
- Every few seconds a **Dungeon Mode** triggers — a cluster of tightly spaced pipes that tests your reflexes
- Hit a pipe or fly out of bounds and it's **Game Over**

---

## ✨ Features

| Feature | Description |
|---|---|
| **Main Menu** | Clean menu with New Game, Options, About, and Exit |
| **Dungeon Mode** | Periodic bursts of tightly-clustered pipes for extra challenge |
| **Options Screen** | Adjust pipe gap, base speed, and dungeon interval with sliders |
| **High Score Tracking** | Best score is tracked across games within a session |
| **Custom Sprites** | Supports custom bird, pipe, and background images |
| **Pause / Resume** | Press **P** or click the Pause button anytime |
| **Smooth Difficulty Curve** | Speed ramps up gradually using linear interpolation |

---

## 🕹️ Controls

| Key | Action |
|---|---|
| **Space** | Flap (fly upward) |
| **P** | Pause / Resume |
| **R** | Restart (after Game Over) |

---

## 📥 Download & Play

1. Go to the [**Releases**](https://github.com/BoykoNikolov99/FlappyBird/releases) page
2. Download the latest **FlappyBird-v1.0.1.zip**
3. Extract the zip to any folder
4. Run **FlappyBird.exe**
5. Flap away! 🐦

### Requirements

- **Windows 10 / 11** (or any Windows with .NET Framework 4.7.2 installed — it comes pre-installed on Windows 10 April 2018 Update and later)

---

## 🛠️ Build from Source

1. Clone the repository:
   ```
   git clone https://github.com/BoykoNikolov99/FlappyBird.git
   ```
2. Open **WindowsFormsApp1.slnx** in Visual Studio
3. Set the configuration to **Release**
4. Build and run (**F5**)

---

## ⚙️ Options

You can customise the difficulty from the **Options** menu before starting a game:

- **Pipe Gap** (80–200) — the vertical space between top and bottom pipes. Lower = harder.
- **Base Pipe Speed** (3–12) — how fast the pipes scroll. Higher = harder.
- **Dungeon Interval** (5–30 seconds) — how often a dungeon cluster appears. Lower = more frequent.

---

## 📁 Project Structure

```
├── Program.cs              # Application entry point
├── MainMenuForm.cs         # Main menu with navigation
├── Form1.cs / .Designer.cs # Core game logic and UI
├── OptionsForm.cs          # Difficulty settings screen
├── AboutForm.cs            # About / credits screen
├── IconHelper.cs           # Shared window icon loader
├── flappy-bird.png         # Bird sprite (also used as window icon)
├── pipe.png                # Pipe sprite
└── background.png          # Background image
```

---

## 👤 Author

**Created by Boyko Nikolov**

---

## 📄 License

This project is free to use for personal and educational purposes.
