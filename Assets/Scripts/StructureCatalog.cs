using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// StructureType to color/size/label mapping shared by every visual. Colors
    /// per the interim deck: red wires, blue pipes, orange studs. Sizes are
    /// wall-local (X along wall, Y up, Z out of wall).
    /// </summary>
    public static class StructureCatalog
    {
        public static Color GetColor(StructureType type)
        {
            switch (type)
            {
                case StructureType.CableRun: return new Color(0.85f, 0.15f, 0.15f);
                case StructureType.Pipe: return new Color(0.15f, 0.35f, 0.9f);
                default: return new Color(0.95f, 0.55f, 0.1f);
            }
        }

        public static Vector3 GetLocalSize(StructureType type)
        {
            switch (type)
            {
                case StructureType.CableRun: return new Vector3(0.04f, 1.2f, 0.04f);
                case StructureType.Pipe: return new Vector3(1.2f, 0.08f, 0.08f);
                default: return new Vector3(0.1f, 1.6f, 0.05f);
            }
        }

        public static string GetLabel(StructureType type)
        {
            switch (type)
            {
                case StructureType.CableRun: return "Cable";
                case StructureType.Pipe: return "Pipe";
                default: return "Stud";
            }
        }
    }
}
