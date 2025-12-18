# nUtilities - Distribution Guide

This guide explains how to install, distribute, and share the nUtilities package.

## 📦 Package Information

- **Package Name:** com.nutilities.editor
- **Version:** 1.0.0
- **Type:** Unity Editor Tool Package
- **Minimum Unity Version:** 2020.2 or higher

## 🚀 Installation Methods

### Method 1: Local Package (Recommended for Development)

The package is already set up in your `Packages` directory as a local package.

**Location:** `Packages/com.nutilities.editor/`

**Verification:**
1. Open Unity Package Manager (`Window > Package Manager`)
2. Select "Packages: In Project" from the dropdown
3. You should see "nUtilities" in the list

### Method 2: Install from Disk (For Other Projects)

To use nUtilities in another Unity project:

1. Copy the entire `com.nutilities.editor` folder
2. Paste it into the target project's `Packages` directory
3. Unity will automatically detect and import the package

### Method 3: Git URL (For Version Control)

If you host nUtilities on GitHub:

1. Open Package Manager
2. Click the `+` button
3. Select "Add package from git URL..."
4. Enter: `https://github.com/YOUR_USERNAME/nUtilities.git`

### Method 4: Unity Package Manager (Manual)

1. Open Package Manager
2. Click the `+` button
3. Select "Add package from disk..."
4. Navigate to `Packages/com.nutilities.editor/package.json`
5. Click "Open"

## 📤 Sharing the Package

### Option A: Share as Local Package (Easiest)

Simply share the `com.nutilities.editor` folder:

1. Compress `Packages/com.nutilities.editor` to a ZIP file
2. Share the ZIP with others
3. Recipients extract to their `Packages` directory
4. Unity automatically imports it

### Option B: Create Unity Package (.unitypackage)

Unity doesn't support exporting UPM packages as `.unitypackage` directly, but you can:

1. Copy the package contents to `Assets/` temporarily
2. Select all files in Project window
3. Right-click > Export Package...
4. Save as `nUtilities.unitypackage`
5. Delete from Assets after export

**Recipients install via:**
- `Assets > Import Package > Custom Package...`

### Option C: GitHub Repository (Best for Teams)

1. Create a new GitHub repository (e.g., `nUtilities`)
2. Copy `com.nutilities.editor` folder contents to repository root
3. Ensure `package.json` is in the root
4. Push to GitHub
5. Share the repository URL

**Installation via Git URL:**
```
https://github.com/YOUR_USERNAME/nUtilities.git
```

### Option D: Unity Asset Store

To publish on Unity Asset Store:

1. Review [Asset Store Guidelines](https://assetstore.unity.com/publishing)
2. Prepare promotional materials (screenshots, icons, demo)
3. Submit through Unity Publisher Portal
4. Note: May need to restructure as traditional asset

## 🔄 Updating the Package

### For Local Development:

Changes are automatically detected. If not:
1. Modify files in `Packages/com.nutilities.editor/`
2. Force reimport: Right-click package in Package Manager > "Reimport"

### For Distributed Versions:

1. Update `version` in `package.json` (follow [Semantic Versioning](https://semver.org/))
2. Update `CHANGELOG.md` with changes
3. Redistribute using your chosen method

**Version Format:** `MAJOR.MINOR.PATCH`
- MAJOR: Breaking changes
- MINOR: New features (backwards compatible)
- PATCH: Bug fixes

## 🔗 Integration with Version Control

### Recommended Setup:

**Include in Git:**
```
Packages/com.nutilities.editor/
```

**Update `.gitignore` if needed:**
```gitignore
# Include packages
!/Packages/com.nutilities.editor/

# But ignore package cache
/Packages/packages-lock.json
```

### For Teams:

When team members clone the repository:
1. Unity automatically detects the package
2. No additional installation needed
3. Changes sync via Git

## 📋 Data Files Location

nUtilities creates data files in:
```
Assets/Editor/FavoritesData.asset
Assets/Editor/SceneGroupData.asset
Assets/Editor/AssetNotesData.asset
```

**Version Control Recommendations:**
- **Include in VCS:** To share favorites/groups across team
- **Ignore in VCS:** If each user wants personal organization

Add to `.gitignore` if needed:
```gitignore
# Personal nUtilities data
Assets/Editor/FavoritesData.asset
Assets/Editor/FavoritesData.asset.meta
Assets/Editor/SceneGroupData.asset
Assets/Editor/SceneGroupData.asset.meta

# Shared notes (recommended to keep)
# Assets/Editor/AssetNotesData.asset
# Assets/Editor/AssetNotesData.asset.meta
```

## 🛠️ Customization

### Modifying the Package:

All source files are in:
```
Packages/com.nutilities.editor/Editor/nUtils/
```

**Structure:**
```
Editor/nUtils/
├── FavoritesWindow.cs          # Favorites system
├── FavoritesData.cs            # Favorites data structure
├── AssetNotesData.cs           # Shared notes system
├── nUtilsWelcome.cs            # Welcome screen
├── SceneOrganizer/             # Scene management
│   ├── SceneOrganizerWindow.cs
│   ├── SceneGroupData.cs
│   ├── CreateNewSceneWindow.cs
│   ├── SettingsWindow.cs
│   └── SceneOrganizerConstants.cs
└── nSearch/                    # Search utilities
    ├── SpotlightSearch.cs
    ├── HierarchySearcher.cs
    ├── CreateMenuSearcher.cs
    └── (other search components)
```

### Creating a Fork:

1. Rename package in `package.json`:
   ```json
   "name": "com.yourcompany.nutilities",
   "displayName": "Your Company nUtilities",
   ```

2. Update assembly definition:
   - Rename `Editor/com.nutilities.editor.asmdef`
   - Update `name` field inside file

3. Update namespace (optional but recommended):
   - Search/replace in all .cs files
   - Change from `nUtilities` to your namespace

## 📊 Package Dependencies

nUtilities has **zero external dependencies**. It only uses:
- Unity Editor built-in APIs
- Standard Unity modules (included by default)

This makes it easy to:
- Install anywhere
- No conflicts
- No additional downloads

## 🧪 Testing in New Projects

To verify package works correctly:

1. Create a new Unity project (2020.2+)
2. Copy `com.nutilities.editor` to `Packages/` directory
3. Open Unity and wait for import
4. Verify all windows open:
   - `Window > nUtilities > Favorites`
   - `Window > nUtilities > Scene Organizer`
   - `Window > nUtilities > nSearch` (Ctrl/Cmd+K)
   - `Window > nUtilities > Welcome`

## 📝 License Information

nUtilities is released under the MIT License (see `LICENSE.md`).

This means:
- ✅ Commercial use allowed
- ✅ Modification allowed
- ✅ Distribution allowed
- ✅ Private use allowed
- ⚠️ No warranty provided
- ⚠️ License and copyright notice required

## 🆘 Support

### For Users:
- Check `README.md` for full documentation
- See `QUICKSTART.md` for getting started
- Review `CHANGELOG.md` for known issues

### For Distributors:
- Maintain package.json version numbers
- Update CHANGELOG.md for all releases
- Test in clean Unity projects before distribution
- Document any custom modifications

## 🎯 Best Practices for Distribution

1. **Always test** in a clean project before distributing
2. **Update version numbers** according to Semantic Versioning
3. **Document changes** in CHANGELOG.md
4. **Include all files** from the package directory
5. **Don't modify** Unity's Package Manager cache
6. **Keep dependencies minimal** (currently zero)
7. **Test across Unity versions** if supporting multiple versions

## 📦 Package Contents Checklist

Before distributing, verify all files are present:

```
✅ package.json              # Package manifest
✅ README.md                 # Main documentation
✅ CHANGELOG.md              # Version history
✅ QUICKSTART.md             # Quick start guide
✅ LICENSE.md                # License information
✅ DISTRIBUTION.md           # This file
✅ Editor/
   ✅ com.nutilities.editor.asmdef
   ✅ nUtils/
      ✅ All .cs files
      ✅ SceneOrganizer/
      ✅ nSearch/
```

## 🔮 Future Distribution Plans

See `CHANGELOG.md` roadmap section for:
- Planned Unity Asset Store release
- OpenUPM registry support
- Scoped registry support
- UI Toolkit version considerations

---

**For questions or issues with distribution, please open an issue on the GitHub repository.**

**Package maintained by: nUtilities Team**
**Last updated: 2024**