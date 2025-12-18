# nUtilities Quick Start Guide

Get up and running with nUtilities in 5 minutes!

## 🚀 First Launch

After installing the package, the Welcome screen will automatically appear. If not, open it via:
`Window > nUtilities > Welcome`

## 📚 Essential Workflows

### 1. Setting Up Favorites (2 minutes)

**Add Your First Favorite:**
1. Right-click any asset in the Project window
2. Select `Add to Favorites`
3. Open Favorites: `Window > Favorites ⭐`

**Create Your First Group:**
1. In Favorites window, click the `+ Create Group` button
2. Name it (e.g., "Scripts", "Prefabs", "Materials")
3. Choose a color
4. Drag items from Ungrouped into your new group

**Pro Tip:** Drag an entire folder onto the Favorites window to create a nested group hierarchy instantly!

### 2. Organizing Scenes (3 minutes)

**Open Scene Organizer:**
`Window > nUtilities > Scene Organizer`

**Create Scene Groups:**
1. Click `+ New Group` button (or press `Ctrl/Cmd + N`)
2. Name it (e.g., "Game Levels", "Menus", "Test Scenes")
3. Drag scenes from the left panel into your groups

**Quick Scene Navigation:**
- Double-click a scene to open it
- Right-click a group > `Load All in Group` to load multiple scenes
- Press `F2` to rename a scene

**Pro Tip:** Use the search bar (`Ctrl/Cmd + F`) to quickly find scenes in large projects!

### 3. Using nSearch (1 minute)

**Open Search:**
Press `Ctrl/Cmd + K` anywhere in Unity

**Try These Searches:**
- Type any GameObject name to find it in your hierarchy
- Type `cube` and press Enter to create a cube
- Type `2 + 2` to use it as a calculator
- Type `sin(45)` for advanced math

**Pro Tip:** Search is fuzzy-matched, so "trnsfm" will find "Transform"!

### 4. Adding Notes (1 minute)

**Add a Note to Any Asset:**
1. In Favorites or Scene Organizer, right-click an item
2. Select `Add/Edit Note...`
3. Type your note (e.g., "Main character prefab - updated 12/15")
4. Click Save

**Visual Feedback:**
- Assets with notes show a 📝 icon
- Hover over the icon to see the note preview
- Notes sync between Favorites and Scene Organizer automatically!

## ⌨️ Essential Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl/Cmd + K` | Open nSearch |
| `Ctrl/Cmd + O` | Open selected scene |
| `Ctrl/Cmd + N` | Create new group |
| `Ctrl/Cmd + F` | Focus search field |
| `F2` | Rename scene |
| `Esc` | Close dialogs |

## 💡 Pro Tips for Maximum Productivity

### Favorites Power User Tips:
- **Toggle Views:** Click the grid/list icon to switch between visual grid and detailed list view
- **Bulk Add:** Select multiple assets in Project window, right-click > Add to Favorites
- **Quick Access:** Use search to find favorites instantly instead of scrolling through groups
- **Color Coding:** Use similar colors for related groups (e.g., all blue for code, all green for art)

### Scene Organizer Power User Tips:
- **Split View:** Adjust the divider between scenes and groups to your preference
- **Recent Scenes:** Enable "Show Recent Scenes" in options to see your working history
- **Backups:** Enable automatic backups in settings (`⚙️ > Settings`) to protect your organization
- **Sort Modes:** Switch between Name/Path/Recently Used sorting in the options menu

### nSearch Power User Tips:
- **Calculator Mode:** Use it for quick math without leaving Unity
  - Basic: `5 * 3.14`
  - Advanced: `sqrt(144)`, `cos(90)`, `log(100)`
- **Quick Create:** Type object name + Enter to create without opening menus
- **Asset Finding:** Start typing any asset name to locate it instantly

### Notes System Power User Tips:
- **Documentation:** Use notes to document why assets exist or how to use them
- **Task Tracking:** Add TODO notes to scenes or assets
- **Team Communication:** Leave notes for other team members about asset changes
- **Status Tracking:** Note the status of scenes (WIP, Review Needed, Complete, etc.)

## 🎯 Common Workflows

### Setting Up a New Project:
1. Create Favorites groups for your main asset categories
2. Create Scene groups (Levels, Menus, Systems, Testing)
3. Add notes to important assets explaining their purpose
4. Set up keyboard shortcuts to your preference

### Daily Development:
1. Use `Ctrl/Cmd + K` to quickly find and create objects
2. Use Favorites to access frequently used assets
3. Use Scene Organizer to quickly switch between working contexts
4. Add notes as you work to document decisions

### Team Collaboration:
1. Share `FavoritesData.asset` and `SceneGroupData.asset` in version control
2. Use notes to communicate about assets and scenes
3. Color-code groups consistently across the team
4. Document keyboard shortcuts in your team wiki

## 🔧 Customization

### Favorites Settings:
- Toggle between grid and list view anytime
- Search is always available - just start typing
- Groups can be nested by dragging folders

### Scene Organizer Settings:
Access via the `⚙️` button:
- Enable/disable backups
- Set backup directory
- Configure backup frequency
- Toggle scene path display
- Choose sort mode

### nSearch:
- Shortcut can be changed in `Edit > Shortcuts`
- Search across Hierarchy, Assets, or Create menu
- Calculator mode activates automatically for math expressions

## ❓ Need Help?

### Quick Troubleshooting:
- **Nothing showing up?** Check if items are in collapsed groups
- **Notes not syncing?** Ensure both windows are open and using the same project
- **Search not working?** Try `Edit > Shortcuts` to check for conflicts

### Full Documentation:
See [README.md](README.md) for complete documentation and troubleshooting.

### Missing Features?
Check [CHANGELOG.md](CHANGELOG.md) for the roadmap and upcoming features!

---

**Now you're ready to supercharge your Unity workflow! Happy developing! 🚀**