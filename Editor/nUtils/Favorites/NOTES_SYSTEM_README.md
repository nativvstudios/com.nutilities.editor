# nUtilities Shared Notes System

## Overview

The nUtilities tools now feature a **unified notes system** that allows you to add notes to assets across multiple tools. Notes are stored centrally in `AssetNotesData.asset` and are automatically synchronized between the Scene Organizer and Favorites windows.

## Key Features

- ✅ **Shared Across Tools**: Notes added in Scene Organizer appear in Favorites and vice versa
- ✅ **Asset Path Based**: Notes are tied to asset paths, not specific tools
- ✅ **Hover to View**: See notes by hovering over the 📝 icon
- ✅ **Easy Editing**: Right-click context menu provides "Edit Note" option
- ✅ **Automatic Migration**: Old Favorites notes are automatically migrated to the shared system

## How It Works

### Data Storage

All notes are stored in:
```
Assets/Editor/AssetNotesData.asset
```

This ScriptableObject contains a list of asset paths with their associated notes. The file is automatically created when you first use either tool.

### Note Indicators

When an asset has a note, a **📝 icon** appears next to it:
- **Scene Organizer**: Icon appears next to scene names in both the scene list and groups
- **Favorites**: Icon appears on favorite items in both list and grid view

**Hover over the 📝 icon** to see the note content in a tooltip.

## Using Notes in Scene Organizer

### Adding/Editing Notes

1. **Right-click** on any scene (in the scene list or in a group)
2. Select **"Edit Note"** from the context menu
3. Type or edit your note in the popup window
4. Click **"Save"** to save changes

### Where Notes Appear

- In the **left panel** (All Scenes list)
- In the **Recent Scenes** list
- Inside **scene groups** on the right panel

### Use Cases

- Document scene purposes: "Main menu - handles player login"
- Track scene status: "WIP - needs lighting pass"
- Add reminders: "TODO: Optimize navmesh before shipping"
- Note dependencies: "Requires GameManager scene loaded first"

## Using Notes in Favorites

### Adding/Editing Notes

1. **Right-click** on any favorite item
2. Select **"Edit Note"** from the context menu
3. Type or edit your note in the popup window
4. Click **"Save"** to save changes

### Where Notes Appear

- On items in **list view** (right side of item)
- On items in **grid view** (top-right corner of preview)
- In all groups and ungrouped items

### Use Cases

- Document prefab usage: "Player controller - use for all character types"
- Track asset versions: "v2 - fixed collision issues"
- Add configuration notes: "Set speed to 5.0 for normal enemies"
- Note asset dependencies: "Requires PlayerInputSystem package"

## Note Sharing Example

**Scenario**: You have a scene called "MainMenu.unity"

1. In **Scene Organizer**, you add a note: "Entry point - handles authentication"
2. You add the same scene to **Favorites** by dragging it from the Project window
3. The note **automatically appears** on the scene in Favorites (no need to re-type it!)
4. If you edit the note in Favorites, the change **syncs back** to Scene Organizer

This works for **any asset** - scenes, prefabs, materials, scripts, etc.

## Technical Details

### AssetNotesData Class

The shared notes system is implemented via the `AssetNotesData` ScriptableObject:

```csharp
// Get a note
string note = assetNotesData.GetNote(assetPath);

// Set/Update a note
assetNotesData.SetNote(assetPath, "Your note here");

// Check if note exists
bool hasNote = assetNotesData.HasNote(assetPath);

// Remove a note
assetNotesData.RemoveNote(assetPath);
```

### Asset Path Format

Notes use Unity's standard asset paths:
```
Assets/Scenes/MainMenu.unity
Assets/Prefabs/Player.prefab
Assets/Scripts/GameManager.cs
```

### Automatic Migration

When you first open Favorites after the update, any existing notes stored in `FavoriteItem.note` are automatically migrated to the shared system. The old note fields are cleared to avoid duplication.

## Group Notes

In addition to asset notes, **Favorite Groups** also support notes (these are NOT shared since they're specific to the Favorites organization):

1. Click the ⚙ (settings) button on a group header
2. Select **"Edit Note"**
3. Add documentation about what the group contains
4. The 📝 icon appears in the group header

## Best Practices

### Scene Notes

- Document the scene's role in your game flow
- Note any required scene loading order
- Track optimization status
- List known issues or TODOs

### Asset Notes

- Explain non-obvious configuration
- Document prefab variants and their differences
- Track asset versions or change history
- Note dependencies or required packages

### Group Notes

- Describe the purpose of the group
- Document when to use assets in this group
- Add workflow tips

## Keyboard Shortcuts

- **Ctrl+Shift+Space**: Open nSearch (can search scenes/assets, then add notes via Favorites)

## Troubleshooting

### Notes Not Appearing

1. Ensure `AssetNotesData.asset` exists in `Assets/Editor/`
2. Try refreshing the tool window (close and reopen)
3. Check the Console for any error messages

### Notes Not Syncing

1. Make sure both tools are pointing to the same `AssetNotesData.asset`
2. Close and reopen the tool windows to reload data
3. Check that the asset paths match exactly

### Lost Notes After Asset Move

If you move/rename an asset in Unity, the asset path changes and notes won't automatically follow. You'll need to re-add the note to the new path.

## Credits

Part of the **nUtilities** collection by Nativv Studios
- Website: https://nativvstudios.com
- GitHub: https://github.com/nativvstudios

---

*For more information about nUtilities tools, see Window > nUtilities > Welcome*