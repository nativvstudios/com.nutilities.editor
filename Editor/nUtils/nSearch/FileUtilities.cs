using System.IO;
using UnityEditor;
using UnityEngine;

public static class FileUtilities
{
    public static Texture2D GetFileTypeIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        string iconName = "DefaultAsset Icon";

        switch (extension)
        {
            case ".unity":
                iconName = "SceneAsset Icon";
                break;
            case ".cs":
                iconName = "cs Script Icon";
                break;
            case ".js":
                iconName = "js Script Icon";
                break;
            case ".dll":
                iconName = "Assembly Icon";
                break;
            case ".asmdef":
                iconName = "AssemblyDefinitionAsset Icon";
                break;
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".tga":
            case ".bmp":
            case ".psd":
            case ".tiff":
            case ".gif":
                iconName = "Texture Icon";
                break;
            case ".mat":
                iconName = "Material Icon";
                break;
            case ".prefab":
                iconName = "Prefab Icon";
                break;
            case ".fbx":
            case ".obj":
            case ".blend":
            case ".dae":
            case ".3ds":
            case ".max":
                iconName = "PrefabModel Icon";
                break;
            case ".controller":
                iconName = "AnimatorController Icon";
                break;
            case ".anim":
                iconName = "Animation Icon";
                break;
            case ".wav":
            case ".mp3":
            case ".ogg":
            case ".aif":
            case ".aiff":
                iconName = "AudioClip Icon";
                break;
            case ".mp4":
            case ".mov":
            case ".avi":
            case ".asf":
            case ".mpg":
            case ".mpeg":
                iconName = "VideoClip Icon";
                break;
            case ".shader":
            case ".shadergraph":
                iconName = "Shader Icon";
                break;
            case ".ttf":
            case ".otf":
                iconName = "Font Icon";
                break;
            case ".asset":
                iconName = "ScriptableObject Icon";
                break;
            case ".physicmaterial":
            case ".physicsmaterial":
                iconName = "PhysicMaterial Icon";
                break;
            case ".txt":
            case ".json":
            case ".xml":
            case ".csv":
            case ".yaml":
            case ".md":
                iconName = "TextAsset Icon";
                break;
            case ".lighting":
                iconName = "Lighting Icon";
                break;
            case ".mixer":
                iconName = "AudioMixerController Icon";
                break;
        }

        var iconContent = EditorGUIUtility.IconContent(iconName);
        if (iconContent != null && iconContent.image != null)
        {
            return iconContent.image as Texture2D;
        }

        // Fallback to default asset icon
        return EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
    }
}
