using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Dialogue
{
    /// <summary>
    /// Busca diálogos por nodeID: primero en Resources/Dialogue (assets editables) y, si no hay,
    /// usa los de por defecto creados por código. Así el juego siempre tiene sus textos, y los
    /// guionistas pueden sustituirlos creando assets con el mismo nodeID.
    /// </summary>
    public static class DialogueLibrary
    {
        private static Dictionary<string, DialogueNode> _nodes;

        public static DialogueNode Get(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            Ensure();
            DialogueNode n;
            return _nodes.TryGetValue(nodeId, out n) ? n : null;
        }

        private static void Ensure()
        {
            if (_nodes != null) return;
            _nodes = new Dictionary<string, DialogueNode>();
            BuildDefaults();
            DialogueNode[] assets = Resources.LoadAll<DialogueNode>("Dialogue");
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] != null && !string.IsNullOrEmpty(assets[i].nodeID)) _nodes[assets[i].nodeID] = assets[i];
            }
        }

        private static DialogueNode Node(string id, DialogueSpeaker who, string text, bool pause = false, bool staticNoise = false)
        {
            var n = ScriptableObject.CreateInstance<DialogueNode>();
            n.name = id;
            n.nodeID = id;
            n.speaker = who;
            n.text = text;
            n.pauseGame = pause;
            n.roboticStatic = staticNoise;
            _nodes[id] = n;
            return n;
        }

        private static void BuildDefaults()
        {
            // Llegada: la IA del cohete se presenta y da la misión principal.
            DialogueNode intro = Node("intro_nav", DialogueSpeaker.RocketNavigationAI,
                "Aterrizaje completado. Sistemas del rover en línea. Soy la IA de navegación del cohete nodriza. " +
                "Nuestra tarea: preparar este lugar para la primera tripulación humana.", true);
            DialogueNode info = Node("intro_info", DialogueSpeaker.RocketNavigationAI,
                "Necesitamos oxígeno, agua, energía estable, un refugio contra la radiación y una pista llana. " +
                "Usa el taladro (X) y la pala para conseguir materiales; la fundición y la refinería harán el resto. " +
                "Y apila regolito alrededor del cohete: la berma nos protegerá de la radiación.");
            DialogueNode go = Node("intro_go", DialogueSpeaker.RocketNavigationAI,
                "Entendido. Abriendo la bodega. Te mantendré informado del clima espacial: las erupciones solares " +
                "no perdonan aquí, sin campo magnético.");
            intro.choices.Add(new DialogueChoice { text = "¿Qué necesitamos exactamente?", next = info });
            intro.choices.Add(new DialogueChoice { text = "Empecemos.", next = go });
            info.next = go;
            go.onFinish.Add(new DialogueEvent { type = DialogueEventType.StartMission, stringParam = "Preparar la zona para la tripulación (ver antena y terminal)" });

            // Registro encontrado en los restos de una sonda antigua (ficticia).
            DialogueNode probe = Node("probe_archive", DialogueSpeaker.ProbeArchive,
                "...registro recuperado... Sol 4127. Batería al 3 %. El polvo cubre los paneles. Último análisis: " +
                "arcillas y sulfatos en este valle; el agua líquida estuvo aquí. Transmitiendo coordenadas de interés... " +
                "fin del registro.", false, true);
            probe.onFinish.Add(new DialogueEvent { type = DialogueEventType.GiveResource, stringParam = "Research", intParam = 5 });
            probe.onFinish.Add(new DialogueEvent { type = DialogueEventType.AddLogEntry, stringParam = "Sonda antigua: el valle muestra arcillas y sulfatos (agua en el pasado)." });

            // Mensaje del comandante cuando la base avanza.
            DialogueNode commander = Node("commander_hello", DialogueSpeaker.HumanCommander,
                "Aquí la comandante de la tripulación. Hemos visto tus datos: buen trabajo. Seguimos entrenando para " +
                "el viaje. Cuida esa berma: es lo que se interpondrá entre nosotros y la radiación.");
            commander.onFinish.Add(new DialogueEvent { type = DialogueEventType.UnlockTechnology, stringParam = "condenser" });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _nodes = null;
        }
    }
}
