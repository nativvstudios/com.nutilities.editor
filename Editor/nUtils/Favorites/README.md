# Favorites

Quick access panel for frequently used assets with organization and notes.

## Quick Start

1. Open **`Window > nUtils > Favorites`**
2. **Drag assets** from Project window into the Favorites panel
3. **Right-click** to add groups and organize

## Features

- **Groups**: Organize favorites into colored, collapsible groups
- **Notes**: Add notes to assets and groups
- **Search**: Filter favorites by name
- **Grid/List View**: Toggle between compact and detailed views
- **Drag & Drop**: Drag assets in, rearrange within groups
- **Context Menu**: Right-click assets or groups for options

## Adding Favorites

- **Drag & Drop**: Drag assets from Project into Favorites window
- **Context Menu**: Right-click asset in Project → `Add to Favorites`

## Groups

| Action | How |
|--------|-----|
| Create Group | Click `+` button or right-click → New Group |
| Rename | Right-click group → Rename |
| Change Color | Right-click group → Change Color |
| Add Note | Right-click group → Edit Note |
| Delete | Right-click group → Delete |

## Color Presets

| Color | RGB |
|-------|-----|
| Blue | (77, 128, 204) |
| Green | (128, 179, 102) |
| Orange | (230, 153, 77) |
| Purple | (179, 102, 179) |
| Red | (204, 77, 102) |
| Cyan | (102, 179, 179) |
| Yellow | (230, 204, 77) |

## Item Actions

Right-click any favorite item to:
- **Open**: Open asset in default editor
- **Ping**: Highlight in Project window
- **Edit Note**: Add/edit notes for the asset
- **Move to Group**: Move between groups
- **Remove**: Remove from favorites

## Data Storage

Favorites data is stored at `Assets/Editor/FavoritesData.asset`. This ScriptableObject can be version controlled to share favorites across a team.

## Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| Delete selected | Delete |
| Search focus | Ctrl+F |