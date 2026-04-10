using System;
using UnityEngine;

namespace EscapeED
{
    [Serializable]
    public class GridSize
    {
        public int x;
        public int y;
        public int z;
    }

    [Serializable]
    public class ArrowData
    {
        public string id;
        public int[] path;
        public string headEnd; // "start" or "end"
        public int[] headDir;  // direction vector [dx, dy, dz] in grid space
    }

    [Serializable]
    public class LevelData
    {
        public GridSize gridSize;
        public ArrowData[] arrows;
    }

    /// <summary>
    /// Professional Utility to handle JSON level loading.
    /// </summary>
    public static class LevelLoader
    {
        public static LevelData LoadFromJSON(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) {
                Debug.LogError("[LevelLoader] JSON text is empty!");
                return null;
            }

            try
            {
                // Clean up any hidden characters (BOM, leading spaces)
                string cleanedJson = jsonText.Trim();
                LevelData data = JsonUtility.FromJson<LevelData>(cleanedJson);
                
                if (data == null || data.arrows == null) {
                    Debug.LogError("[LevelLoader] JsonUtility failed - Check class name/property naming!");
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelLoader] Error parsing JSON: {e.Message}");
                return null;
            }
        }
    }
}
