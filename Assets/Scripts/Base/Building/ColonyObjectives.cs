using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Lista de preparación para la llegada de la primera tripulación: el objetivo principal del
    /// juego. La recibe la antena de comunicaciones junto con mensajes de la Tierra.
    /// </summary>
    public static class ColonyObjectives
    {
        public const int OxygenGoal = 150;
        public const int WaterGoal = 150;
        public const float PowerGoalWatts = 3000f;

        private static readonly string[] Labels =
        {
            Tr.T("Reserva de oxígeno: ") + OxygenGoal + Tr.T(" unidades"),
            Tr.T("Reserva de agua: ") + WaterGoal + Tr.T(" unidades"),
            Tr.T("Energía estable: ") + PowerGoalWatts.ToString("0") + Tr.T(" W de potencia instalada"),
            Tr.T("Refugio contra radiación construido"),
            Tr.T("Pista de aterrizaje nivelada construida"),
            Tr.T("Antena de comunicaciones construida"),
        };

        /// <summary>Mensajes de la Tierra que llegan según el progreso (con el retraso real de unos minutos).</summary>
        private static readonly string[] EarthMessages =
        {
            Tr.T("Control de misión: señal recibida con 12 minutos de retraso. Buen trabajo con la base. Prioridad: oxígeno y agua."),
            Tr.T("Control de misión: las reservas avanzan. Recuerda que las tormentas tapan el sol: necesitaréis baterías o un reactor."),
            Tr.T("Control de misión: la tripulación ya entrena. Necesitamos un refugio contra la radiación antes del despegue."),
            Tr.T("Control de misión: la nave está casi lista. Falta una pista llana donde posarla."),
            Tr.T("Control de misión: ¡todo en verde! La primera tripulación despega hacia Marte. Gracias, explorador."),
        };

        public static int Total
        {
            get { return Labels.Length; }
        }

        public static bool IsDone(RocketBase b, int index)
        {
            switch (index)
            {
                case 0: return b.Inventory.Get(ResourceType.Oxygen) >= OxygenGoal;
                case 1: return b.Inventory.Get(ResourceType.Water) >= WaterGoal;
                // Potencia instalada (no la del momento): si no, el objetivo se cumpliría a mediodía
                // y se "descumpliría" cada noche.
                case 2: return b.Grid.InstalledWatts >= PowerGoalWatts;
                case 3: return b.Grid.Count(StructureKind.RadiationShelter) > 0;
                case 4: return b.Grid.Count(StructureKind.LandingPad) > 0;
                default: return b.Grid.Count(StructureKind.CommsAntenna) > 0;
            }
        }

        public static int CompletedCount(RocketBase b)
        {
            int n = 0;
            for (int i = 0; i < Labels.Length; i++)
            {
                if (IsDone(b, i)) n++;
            }
            return n;
        }

        public static void DrawMenu(RocketBase b)
        {
            int done = CompletedCount(b);
            GUILayout.Space(6);
            GUILayout.Label(Tr.T("<b>Preparación para la tripulación: ") + done + "/" + Total + "</b>");
            for (int i = 0; i < Labels.Length; i++)
            {
                GUILayout.Label((IsDone(b, i) ? "<color=#80ff80>✓ " : "<color=#cccccc>○ ") + Labels[i] + "</color>");
            }

            // Los mensajes llegan a medida que se avanza (máximo 5).
            int messages = Mathf.Clamp(1 + (done * (EarthMessages.Length - 1)) / Total, 1, EarthMessages.Length);
            if (done >= Total) messages = EarthMessages.Length;
            GUILayout.Space(6);
            GUILayout.Label("<b>Mensajes de la Tierra</b>");
            for (int i = messages - 1; i >= 0; i--)
            {
                GUILayout.Label("• " + EarthMessages[i]);
            }
        }
    }
}
