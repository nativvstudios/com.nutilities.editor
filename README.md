# nUtilities

A collection of Unity Editor utilities to boost your productivity.

## Installation

### Option 1: Unity Package Manager (Git URL)

1. Open **Window > Package Manager**
2. Click the **+** button → **Add package from git URL**
3. Enter:
   ```
   https://github.com/nativvstudios/com.nutilities.editor.git
   ```
4. Click **Add**

### Option 2: Clone/Download

1. Clone or download this repository
2. Copy the `com.nutilities.editor` folder into your project's `Packages` folder

## Features

### 🔍 nSearch
Fast fuzzy search for hierarchy, assets, and more.
- **Shortcut:** `Ctrl+Shift+Space`
- **Menu:** `Window > nUtilities > nSearch`

### 🎬 Scene Organizer
Manage scenes with custom groups and quick loading.
- **Menu:** `Window > nUtilities > Scene Organizer`

### ⭐ Favorites
Bookmark frequently used assets with groups and notes.
- **Menu:** `Window > nUtilities > Favorites`
- **Quick Add:** Right-click any asset → `Add to Favorites`

### 📁 Hierarchy Organizer
Create colored separators to organize your scene hierarchy.
- **Menu:** Right-click in Hierarchy → `nUtilities > Hierarchy Organizer`
- **Manager:** `Window > nUtilities > Hierarchy Organizer Manager`

### 🎯 nSelect
Pick anything under the cursor in the Scene view, even what's buried under other objects.
- **Use:** Right-click in the Scene view (a right-drag still pans; empty space still opens Unity's own menu)
- Lists everything there front to back: sprites with their pictures, the tile in each tilemap layer (layer, tile, cell),
  objects that only have a 2D collider or trigger there, UI, and 3D renderers
- Search by name or `t:Type`, filter tabs with counts, hover to outline it in the Scene view
- Click to select, Ctrl/Shift-click to add, double-click to frame, Alt-click for the tile or sprite asset
- **Settings:** `Window > nUtilities > nSelect Settings` (Preferences > nUtilities > nSelect)

## Requirements

- Unity 2020.3 or higher

## Support

- **Website:** [nativvstudios.com](https://nativvstudios.com)
- **GitHub:** [github.com/nativvstudios](https://github.com/nativvstudios)

## License

See [LICENSE.md](LICENSE.md)