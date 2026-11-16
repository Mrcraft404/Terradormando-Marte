using System;
using System.Collections.Generic;
using UnityEngine;
using MarsRobot.Localization;

namespace MarsRobot.Dialogue
{
    /// <summary>Quién habla.</summary>
    public enum DialogueSpeaker
    {
        RocketNavigationAI,  // IA de Navegación del Cohete
        ProbeArchive,        // Registro Histórico de la Sonda
        HumanCommander,      // Comandante Humano
        MissionControl,      // Control de Misión (Tierra)
        RoverSystem,         // Sistema del rover
        Custom               // nombre libre (customSpeakerName)
    }

    /// <summary>Qué pasa al terminar un diálogo.</summary>
    public enum DialogueEventType
    {
        None,
        StartMission,       // stringParam: texto de la misión
        RepairModule,       // stringParam: id del módulo (RobotModuleId) o "all"
        UnlockTechnology,   // stringParam: id de estructura (p. ej. "reactor")
        GiveResource,       // stringParam: ResourceType (p. ej. "Research"), intParam: cantidad
        AddLogEntry,        // stringParam: texto para la bitácora
        Notify              // stringParam: texto de la notificación
    }

    /// <summary>Respuesta del jugador.</summary>
    [Serializable]
    public class DialogueChoice
    {
        public string text;
        public DialogueNode next;

        public string LocalizedText => Localizer.T(text);
    }

    /// <summary>Evento al terminar el nodo.</summary>
    [Serializable]
    public class DialogueEvent
    {
        public DialogueEventType type;
        public string stringParam;
        public int intParam;
    }

    /// <summary>
    /// Un mensaje o línea de diálogo. Crear con Assets ▸ Create ▸ Marte ▸ Diálogo.
    /// Los nodos en Resources/Dialogue se pueden lanzar por su nodeID (ver DialogueLibrary).
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueNode", menuName = "Marte/Diálogo")]
    public class DialogueNode : ScriptableObject
    {
        [Tooltip("Identificador único (también se usa para cargarlo desde Resources/Dialogue).")]
        public string nodeID = "nodo";
        public DialogueSpeaker speaker = DialogueSpeaker.RocketNavigationAI;
        [Tooltip("Solo si el remitente es Custom.")]
        public string customSpeakerName;
        [TextArea(3, 10)] public string text;

        [Header("Audio")]
        [Tooltip("Voz sintetizada (opcional). Si no hay, suena el tecleo de terminal.")]
        public AudioClip voiceClip;
        [Tooltip("Añadir estática robótica de fondo (mensajes recuperados, sondas viejas…).")]
        public bool roboticStatic;

        [Header("Flujo")]
        [Tooltip("Respuestas del jugador. Si está vacío, se continúa con 'next' o se cierra.")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        public DialogueNode next;
        [Tooltip("Pausa el juego mientras se muestra (para diálogos importantes).")]
        public bool pauseGame;
        [Tooltip("Guardar en la bitácora del robot.")]
        public bool addToLog = true;

        [Header("Al terminar")]
        public List<DialogueEvent> onFinish = new List<DialogueEvent>();

        public string SpeakerName
        {
            get { return Localizer.T(SpeakerLabel(speaker, customSpeakerName)); }
        }

        public string LocalizedText => Localizer.T(text);

        /// <summary>¿Habla Marti, la mascota guía? (la IA del cohete y el sistema del rover son ella).</summary>
        public bool IsMascot => speaker == DialogueSpeaker.RocketNavigationAI || speaker == DialogueSpeaker.RoverSystem;

        public static string SpeakerLabel(DialogueSpeaker s, string custom)
        {
            switch (s)
            {
                case DialogueSpeaker.RocketNavigationAI: return Tr.T("Marti, tu guía");
                case DialogueSpeaker.ProbeArchive: return Tr.T("Registro Histórico de la Sonda");
                case DialogueSpeaker.HumanCommander: return Tr.T("Comandante Humano");
                case DialogueSpeaker.MissionControl: return Tr.T("Control de Misión (Tierra)");
                case DialogueSpeaker.RoverSystem: return Tr.T("Marti, tu guía");
                default: return string.IsNullOrEmpty(custom) ? Tr.T("Desconocido") : custom;
            }
        }
    }
}
