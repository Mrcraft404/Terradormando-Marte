using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Spawns
{
    /// <summary>
    /// Lista de zonas que se muestran en el menú de selección de aterrizaje, y la zona elegida
    /// para pasarla de la escena de menú a la escena de juego.
    /// </summary>
    [CreateAssetMenu(fileName = "LandingZoneCatalog", menuName = "Marte/Catálogo de zonas")]
    public class LandingZoneCatalog : ScriptableObject
    {
        public List<LandingZoneData> zones = new List<LandingZoneData>();

        /// <summary>Zona elegida en el menú. La lee el LandingZoneSpawner al cargar la escena de juego.</summary>
        public static LandingZoneData SelectedZone { get; set; }

        public LandingZoneData FindById(string zoneId)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && zones[i].zoneId == zoneId) return zones[i];
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            SelectedZone = null;
        }
    }
}
