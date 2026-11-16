using System.Collections.Generic;

namespace MarsRobot.Localization
{
    public static class Localizer
    {
        private static readonly Dictionary<string, (string en, string es)> Dictionary = new Dictionary<string, (string en, string es)>
        {
            // Main Menu & Buttons
            { "Terraformando", ("Terraforming", "Terraformando") },
            { "Marte", ("Mars", "Marte") },
            { "JUGAR", ("PLAY", "JUGAR") },
            { "OPCIONES", ("OPTIONS", "OPCIONES") },
            { "SALIR", ("EXIT", "SALIR") },
            { "ATRÁS", ("BACK", "ATRÁS") },
            { "CONTINUAR", ("CONTINUE", "CONTINUAR") },
            { "CANCELAR", ("CANCEL", "CANCELAR") },
            { "RESTABLECER", ("RESET", "RESTABLECER") },
            { "VOLVER", ("RETURN", "VOLVER") },
            { "CONFIRMAR", ("CONFIRM", "CONFIRMAR") },

            // Game Modes & Difficulty
            { "ELIGE EL MODO DE JUEGO", ("CHOOSE GAME MODE", "ELIGE EL MODO DE JUEGO") },
            { "CREATIVO", ("CREATIVE", "CREATIVO") },
            { "SUPERVIVENCIA", ("SURVIVAL", "SUPERVIVENCIA") },
            { "FÁCIL", ("EASY", "FÁCIL") },
            { "NORMAL", ("NORMAL", "NORMAL") },
            { "DIFÍCIL", ("HARD", "DIFÍCIL") },

            // Settings Menu
            { "Idioma", ("Language", "Idioma") },
            { "Nombre del comandante", ("Commander Name", "Nombre del comandante") },
            { "Volumen general", ("Master Volume", "Volumen general") },
            { "Música / ambiente", ("Music / Ambient", "Música / ambiente") },
            { "Efectos", ("Effects", "Efectos") },
            { "Brillo", ("Brightness", "Brillo") },
            { "Sensibilidad del ratón", ("Mouse Sensitivity", "Sensibilidad del ratón") },
            { "Calidad gráfica", ("Graphics Quality", "Calidad gráfica") },
            { "Mostrar FPS", ("Show FPS", "Mostrar FPS") },

            // Speakers
            { "Marti, tu guía", ("Marti, your guide", "Marti, tu guía") },
            { "Registro Histórico de la Sonda", ("Probe Historical Log", "Registro Histórico de la Sonda") },
            { "Comandante Humano", ("Human Commander", "Comandante Humano") },
            { "Control de Misión (Tierra)", ("Mission Control (Earth)", "Control de Misión (Tierra)") },
            { "Sistema del rover", ("Rover System", "Sistema del rover") },
            { "Desconocido", ("Unknown", "Desconocido") },

            // Dialogues
            { "intro_nav_text", ("Hi, explorer! I'm Marti, and this is my planet, Mars. You just landed! Your mission: get Mars ready so the first people can come and live here.", "¡Hola, explorador! Soy Marti, y este es mi planeta, Marte. ¡Acabas de aterrizar! Tu misión: preparar Marte para que vengan a vivir las primeras personas.") },
            { "intro_info_text", ("People need air, water, energy and a safe house. Drive the robot, dig rocks with the drill (X key) and bring them to the rocket. With those materials you build everything, just like building blocks!", "Las personas necesitan aire, agua, energía y una casa segura. Maneja el robot, saca piedras con el taladro (tecla X) y llévalas al cohete. ¡Con esos materiales construyes todo, como con bloques!") },
            { "intro_go_text", ("Great! When you want to build, press B and pick a piece from the pictures. I'll warn you if a storm comes from the Sun. Let's go!", "¡Muy bien! Cuando quieras construir, pulsa B y elige una pieza de las fotos. Yo te aviso si viene una tormenta del Sol. ¡Vamos!") },
            { "intro_opt1", ("What do I have to do?", "¿Qué tengo que hacer?") },
            { "intro_opt2", ("Let's go!", "¡Vamos!") },
            { "probe_text", ("...record recovered... Sol 4127. Battery at 3%. Dust covers panels. Final analysis: clays and sulfates in this valley; liquid water was here. Transmitting coordinates of interest... end of record.", "...registro recuperado... Sol 4127. Batería al 3 %. El polvo cubre los paneles. Último análisis: arcillas y sulfatos en este valle; el agua líquida estuvo aquí. Transmitiendo coordenadas de interés... fin del registro.") },
            // Marti, la mascota guía (también se usan para generar su voz)
            { "marti_astronaut_start", ("Now you are the astronaut. Go down to the hangar, on floor 1, and press P to drive the robot.", "Ahora eres el astronauta. Baja al hangar, en el piso 1, y pulsa P para manejar el robot.") },
            { "marti_robot_outside", ("The robot is outside. Bring it back to the rocket's hangar first, and then press P.", "El robot está fuera. Tráelo primero al hangar del cohete y luego pulsa P.") },
            { "marti_robot_to_hangar", ("To go back to the astronaut, take the robot up to the hangar with the elevator.", "Para volver al astronauta, sube el robot al hangar con el ascensor.") },
            { "marti_now_astronaut", ("Now you control the astronaut!", "¡Ahora manejas al astronauta!") },
            { "marti_now_robot", ("Now you control the robot! Go out through the big door and ride the elevator down with the F key.", "¡Ahora manejas el robot! Sal por la puerta grande y baja con el ascensor, con la tecla F.") },
            { "marti_build_place", ("Move the mouse to choose a spot. If it looks blue, it fits: click! If it looks red, try another place. The mouse wheel turns it.", "Mueve el ratón para elegir el sitio. Si se ve azul, cabe: ¡haz clic! Si se ve rojo, prueba en otro lugar. La rueda del ratón la gira.") },
            { "marti_workshop", ("This is the robot workshop! Choose what you want your robot for, and click a part to put it on.", "¡Este es el taller del robot! Elige para qué quieres tu robot y haz clic en una pieza para ponérsela.") },
            // Tutorial guiado (Marti; también se generan con su voz)
            { "tut_switch", ("First, press P to drive the robot. It is waiting in the hangar, on floor 1 of the rocket.", "Primero, pulsa P para manejar el robot. Te espera en el hangar, en el piso 1 del cohete.") },
            { "tut_move", ("Move with W, A, S and D, and look around with the mouse. Try it!", "Muévete con W, A, S y D, y mira a tu alrededor con el ratón. ¡Pruébalo!") },
            { "tut_exit", ("Now go out through the big hangar door and ride the elevator down by pressing F.", "Ahora sal por la puerta grande del hangar y baja con el ascensor pulsando F.") },
            { "tut_map", ("Press M to see the big map. You will see the rocket and the places to explore. Close it with M.", "Pulsa M para ver el mapa grande. Verás el cohete y los lugares para explorar. Ciérralo con M.") },
            { "tut_mine", ("Look for a rock, point at it and hold F to dig out stones with the drill.", "Busca una roca, apúntala y mantén pulsada la F para sacar piedras con el taladro.") },
            { "tut_unload", ("Great! Now go back to the rocket and leave the stones in the cargo hold with F.", "¡Muy bien! Ahora vuelve al cohete y deja las piedras en la bodega con la F.") },
            { "tut_build", ("Time to build! Press B, choose the solar panel and click near the rocket to place it.", "¡A construir! Pulsa B, elige el panel solar y haz clic cerca del cohete para colocarlo.") },
            { "tut_done", ("You did great! Now you know the basics. Keep building the base so people can come to Mars.", "¡Lo hiciste genial! Ya sabes lo básico. Sigue construyendo la base para que las personas puedan venir a Marte.") },
            { "commander_text", ("Crew Commander here. We saw your data: good work. We are continuing training for the voyage. Take care of that berm: it's what stands between us and radiation.", "Aquí la comandante de la tripulación. Hemos visto tus datos: buen trabajo. Seguimos entrenando para el viaje. Cuida esa berma: es lo que se interpondrá entre nosotros y la radiación.") }
        };

        /// <summary>Texto en el idioma elegido, escrito en el sitio: L("Hola", "Hello").</summary>
        public static string L(string es, string en)
        {
            return MarsRobot.Core.GameSettings.Language == 0 ? en : es;
        }

        /// <summary>True si el juego está en inglés.</summary>
        public static bool English
        {
            get { return MarsRobot.Core.GameSettings.Language == 0; }
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (!Dictionary.ContainsKey(key)) return key;
            return MarsRobot.Core.GameSettings.Language == 0 ? Dictionary[key].en : Dictionary[key].es;
        }
    }
}
