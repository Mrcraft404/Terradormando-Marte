using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>Grupos raíz de la escena (jerarquía limpia y desacoplada).</summary>
    public enum SceneRoot
    {
        Environment,    // _Environment: luz global, cielo, niebla, terreno, sondas de reflejos
        Structures,     // _Structures: base, nave nodriza, estaciones, estructuras construidas
        Props,          // _Props: atrezo y objetos sueltos (rocas, vehículos, restos, cajas)
        PhysicsSystem   // _PhysicsSystem: controladores y gestores globales
    }

    /// <summary>
    /// Crea (o encuentra) los grupos raíz y coloca en ellos lo que el juego monta por código.
    /// La escena de juego se construye al arrancar (TestSceneBootstrap), así que la organización
    /// se hace en tiempo de juego; Marte ▸ Arquitectura de escena crea los mismos grupos en el editor.
    /// </summary>
    public static class SceneRoots
    {
        public static string NameOf(SceneRoot root)
        {
            switch (root)
            {
                case SceneRoot.Environment: return "_Environment";
                case SceneRoot.Structures: return "_Structures";
                case SceneRoot.Props: return "_Props";
                default: return "_PhysicsSystem";
            }
        }

        public static Transform Get(SceneRoot root)
        {
            string name = NameOf(root);
            GameObject go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            return go.transform;
        }

        /// <summary>Mueve un objeto de la raíz de la escena a su grupo (conserva su posición en el mundo).</summary>
        public static void Move(GameObject go, SceneRoot root)
        {
            if (go == null) return;
            if (go.transform.parent != null) return; // solo objetos de la raíz: no se rompen jerarquías propias
            if (go.scene.name == "DontDestroyOnLoad") return;
            go.transform.SetParent(Get(root), true);
        }

        public static void Move(string objectName, SceneRoot root)
        {
            Move(GameObject.Find(objectName), root);
        }

        /// <summary>Ordena la escena de juego por nombre de los objetos que crea el arranque.</summary>
        public static void OrganizeGameScene()
        {
            string[] environment = { "Sol", "Clima", "Terreno" };
            string[] structures = { "Base (cohete)" };
            string[] props = { "Rocas", "MarteCar", "Restos de una sonda antigua", "Módulo de aterrizaje abandonado" };
            string[] systems = { "Arranque", "Clima espacial", "Minimapa", "Modo de juego", "Control de la tripulación", "Gestor de diálogos",
                "Ventana de inventario y fabricación", "Sistema de avisos" };
            foreach (string n in environment) Move(n, SceneRoot.Environment);
            foreach (string n in structures) Move(n, SceneRoot.Structures);
            foreach (string n in props) Move(n, SceneRoot.Props);
            foreach (string n in systems) Move(n, SceneRoot.PhysicsSystem);
        }
    }
}
