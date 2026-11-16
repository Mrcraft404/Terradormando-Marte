using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Pieza colocada en una estructura (se guarda con la escena).</summary>
    [DisallowMultipleComponent]
    public sealed class PlacedPiece : MonoBehaviour
    {
        public string pieceId;
        public PieceSlot slot;

        public BuildPieceDefinition Definition
        {
            get { return BuildPieceCatalog.Find(pieceId); }
        }

        /// <summary>True si ahora mismo cierra el paso del aire (una puerta abierta no lo cierra).</summary>
        public bool SealsNow
        {
            get
            {
                BuildPieceDefinition def = Definition;
                if (def == null || !def.seals) return false;
                var door = GetComponent<PieceDoor>();
                return door == null || !door.IsOpen;
            }
        }
    }
}
