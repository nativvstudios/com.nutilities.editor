# Changelog

All notable changes to nUtilities will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-01-01

### Added
- **Favorites Window**: Complete favorites management system
  - Add assets via context menu or drag-and-drop
  - Custom groups with color coding
  - Nested group hierarchies from folders
  - Grid and list view modes
  - Search functionality
  - Drag items between groups
  - Quick ping and selection
  
- **Scene Organizer**: Advanced scene management system
  - Custom scene groups with color coding
  - Split-view interface
  - Recent scenes tracking
  - Load single scenes or entire groups
  - Rename scenes directly in the window
  - Multiple sort modes (Name, Path, Recently Used)
  - Backup system for scene groups
  - Keyboard shortcuts for navigation
  - Search functionality
  
- **nSearch**: Fast search system inspired by macOS Spotlight
  - Quick search across hierarchy, assets, and create menu
  - Calculator functionality (basic and advanced math)
  - Smart search with fuzzy matching
  - Keyboard-driven interface (`Ctrl/Cmd + K`)
  - Multiple search modes
  
- **Shared Notes System**: Universal asset annotation system
  - Notes stored in central `AssetNotesData.asset`
  - Notes sync between Favorites and Scene Organizer
  - Visual indicators (📝) for assets with notes
  - Tooltip preview on hover
  - Rich text editing window
  
- **Welcome Screen**: First-time setup and quick access
  - Overview of all utilities
  - Quick launch buttons
  - Keyboard shortcut reference
  - Auto-shows on first use

### Technical Features
- Complete Unity Package Manager support
- Modular architecture
- EditorPrefs integration for user settings
- ScriptableObject-based data persistence
- IMGUI implementation for maximum compatibility
- Comprehensive error handling and logging

### Fixed
- Internal drag detection in Favorites window to prevent false duplicate warnings
- Event handling order to allow group handlers to process drags before window-level handlers
- Shared notes integration between Favorites and Scene Organizer
- Note indicators properly display across all views (list and grid)

### Known Issues
- None at release

### Under Consideration
- Git integration for scene status indicators
- Asset dependency viewer
- Custom asset templates
- Team collaboration features (shared notes sync)
- Dark/Light theme customization
- Asset preview improvements

---

## Version History

- **1.0.0** - Initial release with complete feature set

---

For detailed documentation, see [README.md](README.md)