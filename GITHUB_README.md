# nUtilities - Unity Editor Productivity Suite

[![Unity Version](https://img.shields.io/badge/Unity-2020.2%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)
[![Version](https://img.shields.io/badge/Version-1.0.0-orange.svg)](CHANGELOG.md)

A comprehensive collection of Unity Editor tools designed to supercharge your development workflow with better organization, faster navigation, and improved productivity.

![nUtilities Banner](https://via.placeholder.com/800x200/2C3E50/FFFFFF?text=nUtilities+-+Unity+Editor+Productivity+Suite)

## ✨ Features at a Glance

| Tool | Description | Quick Access |
|------|-------------|--------------|
| ⭐ **Favorites** | Bookmark and organize your most-used assets | `Window > Favorites ⭐` |
| 🎬 **Scene Organizer** | Professional scene management with groups | `Window > nUtilities > Scene Organizer` |
| 🔍 **nSearch** | Fast search + calculator (like macOS Spotlight) | `Ctrl/Cmd + K` |
| 📝 **Notes System** | Shared asset annotations across all tools | Right-click > Add/Edit Note |

## 🎯 Why nUtilities?

**Stop wasting time searching.** Start creating faster.

- 📁 **Organize Once, Access Forever** - Bookmark assets and scenes into color-coded groups
- ⚡ **Lightning Fast Search** - Find anything with nSearch (`Ctrl/Cmd + K`)
- 🔄 **Shared Notes** - Document assets with notes that sync across all windows
- 🎨 **Beautiful UI** - Intuitive, keyboard-friendly interface
- 🚀 **Zero Dependencies** - Works out of the box in any Unity project
- 🤝 **Team Friendly** - Share your organization via version control

## 🚀 Quick Start (5 Minutes)

### Installation

**Option 1: Via Git URL (Recommended)**
```
1. Open Unity Package Manager (Window > Package Manager)
2. Click the '+' button
3. Select "Add package from git URL..."
4. Enter: https://github.com/YOUR_USERNAME/nUtilities.git
```

**Option 2: Local Installation**
```
1. Download/clone this repository
2. Copy the entire folder to your project's Packages/ directory
3. Unity will automatically import it
```

**Option 3: Unity Package Manager**
```
1. Window > Package Manager
2. '+' button > "Add package from disk..."
3. Select package.json from this repository
```

### First Steps

1. **Open Favorites**: `Window > Favorites ⭐`
   - Right-click any asset in Project window > "Add to Favorites"
   - Create groups with the "+ Create Group" button
   - Drag items between groups

2. **Try nSearch**: Press `Ctrl/Cmd + K`
   - Search for any GameObject or asset
   - Try calculator: `45 * 3.14` or `sqrt(144)`
   - Create objects: Type "Cube" and press Enter

3. **Organize Scenes**: `Window > nUtilities > Scene Organizer`
   - Click "+ New Group" to create scene groups
   - Drag scenes into groups
   - Double-click to open scenes
   - Right-click group > "Load All in Group"

4. **Add Notes**: Right-click any asset or scene > "Add/Edit Note..."
   - Notes sync between Favorites and Scene Organizer automatically!

📖 **Full guide:** See [QUICKSTART.md](QUICKSTART.md)

## 🌟 Feature Highlights

### ⭐ Favorites Window

**Never lose track of important assets again.**

- **Quick Bookmarking**: Right-click any asset > "Add to Favorites"
- **Custom Groups**: Color-coded organization with unlimited nesting
- **Folder Import**: Drag folders to create instant hierarchies
- **Grid/List Views**: Switch between visual and detailed modes
- **Fast Search**: Find favorites instantly
- **Drag & Drop**: Reorganize items effortlessly

![Favorites Demo](https://via.placeholder.com/600x400/34495E/FFFFFF?text=Favorites+Window+Demo)

### 🎬 Scene Organizer

**Professional scene management made simple.**

- **Scene Groups**: Organize scenes by level, feature, or status
- **Split View**: Scenes on left, groups on right
- **Batch Loading**: Load entire groups at once
- **Recent Scenes**: Track your working history
- **In-Window Renaming**: Press F2 to rename scenes
- **Auto Backups**: Never lose your organization
- **Sort Modes**: Name, Path, or Recently Used

![Scene Organizer Demo](https://via.placeholder.com/600x400/2C3E50/FFFFFF?text=Scene+Organizer+Demo)

### 🔍 nSearch

**Find anything. Instantly.**

Press `Ctrl/Cmd + K` to unleash the power:

- **Multi-Mode Search**:
  - 🎯 Search scene hierarchy
  - 📁 Search project assets
  - ➕ Quick object creation
  - 🧮 Built-in calculator

- **Smart Calculator**:
  - Basic: `2 + 2`, `100 / 5`, `3 ^ 2`
  - Advanced: `sin(45)`, `sqrt(16)`, `log(100)`

- **Fuzzy Matching**: Type "trnsfm" to find "Transform"

![nSearch Demo](https://via.placeholder.com/600x300/16A085/FFFFFF?text=Spotlight+Search+Demo)

### 📝 Shared Notes System

**Document your assets. Automatically synced.**

- Add notes to ANY asset or scene
- Notes sync between Favorites and Scene Organizer
- Visual indicators (📝) show which assets have notes
- Hover for quick preview
- Perfect for team documentation

## ⌨️ Keyboard Shortcuts

| Shortcut | Action | Context |
|----------|--------|---------|
| `Ctrl/Cmd + K` | Open nSearch | Global |
| `Ctrl/Cmd + O` | Open selected scene | Scene Organizer |
| `Ctrl/Cmd + N` | Create new group | Scene Organizer |
| `Ctrl/Cmd + F` | Focus search | Favorites / Scene Organizer |
| `F2` | Rename scene | Scene Organizer |
| `Delete` | Remove from group | Favorites / Scene Organizer |
| `Esc` | Close dialogs | Global |

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [README.md](README.md) | Complete documentation and user guide |
| [QUICKSTART.md](QUICKSTART.md) | 5-minute getting started guide |
| [CHANGELOG.md](CHANGELOG.md) | Version history and roadmap |
| [TECHNICAL.md](TECHNICAL.md) | Architecture and developer guide |
| [DISTRIBUTION.md](DISTRIBUTION.md) | Installation and sharing guide |

## 🎓 Common Use Cases

### For Solo Developers
- Quickly access your most-used scripts, prefabs, and materials
- Organize scenes by completion status (WIP, Testing, Complete)
- Use notes to document your own decisions and TODOs
- Calculator for quick math without leaving Unity

### For Teams
- Share favorites and scene groups via Git
- Document assets with notes visible to all team members
- Consistent color coding for easier communication
- Recent scenes to track what everyone's working on

### For Large Projects
- Organize hundreds of assets into nested categories
- Scene groups for different areas (Levels 1-10, Menus, Systems)
- Search to find things fast in massive hierarchies
- Backup system to protect your organization

## 🛠️ Technical Details

- **Unity Version**: 2020.2 or higher
- **Dependencies**: None (100% Unity built-in APIs)
- **Implementation**: IMGUI for maximum compatibility
- **Data Storage**: ScriptableObjects in Assets/Editor/
- **Package Type**: Unity Package Manager (UPM)
- **License**: MIT (see [LICENSE.md](LICENSE.md))

## 🗺️ Roadmap

### v1.1.0 (Planned)
- [ ] UI Toolkit (UIElements) modernization
- [ ] Export/Import favorites and scene groups
- [ ] Project-wide asset tagging system
- [ ] Performance optimizations for 1000+ assets

### v2.0.0 (Under Consideration)
- [ ] Git integration (scene status indicators)
- [ ] Asset dependency viewer
- [ ] Custom asset templates
- [ ] Team collaboration features
- [ ] Dark/Light theme customization

See [CHANGELOG.md](CHANGELOG.md) for detailed roadmap.

## 🤝 Contributing

Contributions are welcome! Here's how:

1. **Report Bugs**: Open an issue with reproduction steps
2. **Request Features**: Share your ideas in Discussions
3. **Submit PRs**: Fork, create feature branch, test thoroughly
4. **Improve Docs**: Help make documentation clearer

See [TECHNICAL.md](TECHNICAL.md) for architecture details.

## 📊 Project Stats

- **Lines of Code**: ~6,000+
- **Files**: 20+ C# files
- **Windows**: 5 editor windows
- **Features**: 30+ major features
- **Dependencies**: 0 external packages

## 🐛 Known Issues

- None at v1.0.0 release

Report issues at: [GitHub Issues](https://github.com/YOUR_USERNAME/nUtilities/issues)

## 💬 Community

- **Questions**: Use GitHub Discussions
- **Bug Reports**: GitHub Issues
- **Feature Requests**: GitHub Issues with "enhancement" label
- **Showcase**: Share your setup in Discussions!

## 📜 License

This project is licensed under the MIT License - see [LICENSE.md](LICENSE.md) for details.

**TL;DR**: Free to use, modify, and distribute. No warranty. Keep the license notice.

## 🙏 Acknowledgments

- Inspired by macOS Spotlight for the search system
- Thanks to the Unity community for feedback and testing
- Built with ❤️ for Unity developers everywhere

## 📞 Support

- 📖 **Documentation**: See [README.md](README.md) and other guides
- 🚀 **Quick Start**: [QUICKSTART.md](QUICKSTART.md) - 5 minutes to productivity
- 🔧 **Technical**: [TECHNICAL.md](TECHNICAL.md) - For developers
- 🐛 **Issues**: [GitHub Issues](https://github.com/YOUR_USERNAME/nUtilities/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/YOUR_USERNAME/nUtilities/discussions)

## 📸 Screenshots

### Favorites Window in Action
![Favorites](https://via.placeholder.com/800x500/34495E/FFFFFF?text=Favorites+Window+Screenshot)

### Scene Organizer Split View
![Scene Organizer](https://via.placeholder.com/800x500/2C3E50/FFFFFF?text=Scene+Organizer+Screenshot)

### nSearch
![Spotlight](https://via.placeholder.com/800x300/16A085/FFFFFF?text=Spotlight+Search+Screenshot)

### Notes System Integration
![Notes](https://via.placeholder.com/600x400/9B59B6/FFFFFF?text=Notes+System+Screenshot)

---

<div align="center">

**⭐ If nUtilities helps you, consider giving it a star!**

Made with ❤️ for Unity Developers

[Report Bug](https://github.com/YOUR_USERNAME/nUtilities/issues) · [Request Feature](https://github.com/YOUR_USERNAME/nUtilities/issues) · [Documentation](README.md)

</div>

---

**Package Version**: 1.0.0  
**Last Updated**: 2024  
**Maintained by**: nUtilities Team