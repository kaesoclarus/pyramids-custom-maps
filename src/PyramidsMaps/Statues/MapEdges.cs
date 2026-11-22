using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// For maps a little bigger than the normal play area (with a matching MapInfo.PlayArea): takes the normal map's
    /// boundary walls down while the world runs, and puts them back when it ends. The game turns the walls on again
    /// during the world switch, so this runs after it (MapInfo.OnApplied).
    /// </summary>
    internal static class MapEdges
    {
        private static GameObject _walls;

        public static void Open()
        {
            var walls = GameObject.Find("/Map/Boundaries");
            if (walls == null || !walls.activeSelf) return;
            walls.SetActive(false);
            _walls = walls;
            Plugin.Log.LogInfo("Map edges: boundary walls down for this map (the play area is widened to match).");
        }

        public static void Close()
        {
            if (_walls != null) _walls.SetActive(true);
            _walls = null;
        }
    }
}
