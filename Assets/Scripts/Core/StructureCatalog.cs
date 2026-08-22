using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// StructureType to color/size mapping shared by every visual. Colors
    /// per the interim deck: red wires, orange studs. Sizes are wall-local
    /// (X along wall, Y up, Z out of wall).
    /// </summary>
    public static class StructureCatalog
    {
        public static Color GetColor(StructureType type)
        {
            switch (type)
            {
                case StructureType.CableRun: return new Color(0.85f, 0.15f, 0.15f);
                default: return new Color(0.95f, 0.55f, 0.1f);
            }
        }

        public static Vector3 GetLocalSize(StructureType type)
        {
            switch (type)
            {
                case StructureType.CableRun: return new Vector3(0.04f, 1.2f, 0.04f);
                default: return new Vector3(0.1f, 1.6f, 0.05f);
            }
        }
    }
}
