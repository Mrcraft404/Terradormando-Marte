using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Investigación de la base: las estructuras con researchCost &gt; 0 se desbloquean gastando
    /// puntos de investigación (los produce el laboratorio a partir de las muestras del robot).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BaseResearch : MonoBehaviour
    {
        private readonly HashSet<string> _unlocked = new HashSet<string>();

        public bool IsUnlocked(StructureDefinition def)
        {
            return def.researchCost <= 0 || _unlocked.Contains(def.id);
        }

        /// <summary>Desbloquea sin coste (recompensas de misiones o diálogos).</summary>
        public void ForceUnlock(string structureId)
        {
            if (!string.IsNullOrEmpty(structureId)) _unlocked.Add(structureId);
        }

        public bool TryUnlock(StructureDefinition def, BaseInventory inventory)
        {
            if (IsUnlocked(def)) return false;
            var cost = new[] { new ResourceAmount(ResourceType.Research, def.researchCost) };
            if (!inventory.TrySpend(cost)) return false;
            _unlocked.Add(def.id);
            return true;
        }
    }

    /// <summary>Menú de investigación (se muestra dentro del laboratorio).</summary>
    public static class ResearchMenu
    {
        public static void Draw(RocketBase owner)
        {
            GUILayout.Space(6);
            GUILayout.Label(Tr.T("<b>Investigación</b>  (tienes ") + owner.Inventory.Get(ResourceType.Research) + Tr.T(" puntos; ") +
                            Tr.T("consigue muestras con el taladro del robot: tecla X)"));
            var all = StructureCatalog.All;
            bool any = false;
            for (int i = 0; i < all.Count; i++)
            {
                StructureDefinition def = all[i];
                if (def.researchCost <= 0) continue;
                any = true;
                bool done = owner.Research.IsUnlocked(def);
                GUILayout.BeginHorizontal();
                GUI.enabled = !done && owner.Inventory.Get(ResourceType.Research) >= def.researchCost;
                if (GUILayout.Button(done ? def.name + " ✓" : Tr.T("Desbloquear ") + def.name, GUILayout.Width(300)))
                {
                    owner.Research.TryUnlock(def, owner.Inventory);
                }
                GUI.enabled = true;
                GUILayout.Label(done ? "<color=#80ff80>desbloqueado</color>" : def.researchCost + Tr.T(" puntos"));
                GUILayout.EndHorizontal();
            }
            if (!any) GUILayout.Label(Tr.T("No hay nada pendiente de investigar."));
        }
    }
}
