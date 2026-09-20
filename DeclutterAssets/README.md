# Declutter Assets

A lightweight, non-destructive Unity editor tool that keeps your Project window clean by separating imported assets, plugins, and third-party packages into a dedicated virtual **Imports** section alongside **Assets** and **Packages**.

---

## Key Features

- **Non-Destructive**: 100% virtual organization in memory. Disk paths, meta files, asset GUIDs, and references are never moved or modified.
- **Root Sibling Hierarchy**: Displays `Imports` at the root level alongside `Assets` and `Packages`.
- **Drag & Drop**: Drag any folder or asset to `Imports` to hide it from `Assets`, or drag it back to restore it.
- **Context Menus**: Right-click any folder or asset $\rightarrow$ `Assets/Declutter/Move to Imports` (or `Move to Assets`).
- **Full Project Browser Support**: Works seamlessly in both One-Column and Two-Column layout modes with native foldouts, search, and package visibility toggling.
- **Shared Settings**: Tracked folder lists are stored in `ProjectSettings/DeclutterAssetsSettings.json` for easy team synchronization via version control.

---

## How to Use

1. **Move to Imports**:
   - Drag and drop any folder in your Project window onto the **Imports** header.
   - Or right-click a folder/asset and select **Declutter $\rightarrow$ Move to Imports**.

2. **Restore to Assets**:
   - Drag the item from **Imports** onto the **Assets** header.
   - Or right-click the item and select **Declutter $\rightarrow$ Move to Assets**.
