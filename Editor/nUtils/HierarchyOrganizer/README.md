# Hierarchy Organizer

Organize your Unity scene hierarchy with colored separators.

## Quick Start

1. **Right-click** in Hierarchy → `GameObject > nUtils > Hierarchy Organizer`
2. Choose a **name preset** or type your own
3. Pick a **color**
4. Click **Create** (or press Enter)

## Features

- **Creation Window**: Simple UI with name presets and color swatches
- **8 Colors**: Blue, Green, Orange, Purple, Red, Cyan, Yellow, Custom
- **Live Preview**: See your separator before creating
- **Manager Window**: View and manage all separators (`Window > nUtils > Hierarchy Organizer Manager`)

## Color Presets

| Color | Suggested Use |
|-------|---------------|
| Blue | Gameplay |
| Green | Environment |
| Orange | Managers |
| Purple | UI |
| Red | Debug |
| Cyan | Audio |
| Yellow | Lighting |

## Example Hierarchy

```
--- MANAGERS --- (Orange)
  GameManager
  AudioManager

--- GAMEPLAY --- (Blue)
  Player
  Enemies

--- ENVIRONMENT --- (Green)
  Terrain
  Props

--- UI --- (Purple)
  Canvas
```

## Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| Create | Enter |
| Cancel | Escape |
| Rename | F2 |

## Other Options

- **Convert Existing**: Right-click any GameObject → `Convert to Separator`
- **Edit**: Select separator → modify in Inspector