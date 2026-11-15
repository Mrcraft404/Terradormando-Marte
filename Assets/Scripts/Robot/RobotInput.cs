using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Lectura de controles aislada en un único sitio. Hoy usa el Input Manager clásico
    /// (viene activado por defecto en Unity). Para pasar al nuevo Input System basta con
    /// cambiar este archivo; el controlador y la reparación no se enteran.
    ///
    /// Controles: WASD mover · Q/E girar · ratón mirar · V cámara 1ª/3ª persona · rueda zoom ·
    /// R (mantener) reparar · T herramienta · G módulo · C limpiar paneles · Esc liberar cursor.
    /// </summary>
    public static class RobotInput
    {
        /// <summary>
        /// Si es true, el rover no recibe ninguna tecla (p. ej. mientras se inspecciona la nave con
        /// la cámara libre). Lo activa y desactiva quien toma el control de la entrada.
        /// </summary>
        public static bool Suspended { get; set; }

#if ENABLE_LEGACY_INPUT_MANAGER
        public static Vector2 Move
        {
            get
            {
                if (Suspended) return Vector2.zero;
                // Ejes del Input Manager (WASD y flechas) y, por si alguien los borra, teclas directas.
                var axis = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (axis.sqrMagnitude > 0.01f) return axis;
                float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                float y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                return new Vector2(x, y);
            }
        }

        /// <summary>Giro con teclado: Q izquierda (−1), E derecha (+1).</summary>
        public static float KeyboardTurn
        {
            get { if (Suspended) return default; return (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f); }
        }

        /// <summary>V: alternar primera / tercera persona.</summary>
        public static bool CameraTogglePressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.V); }
        }

        /// <summary>Rueda del ratón: acercar/alejar la cámara en tercera persona.</summary>
        public static float Zoom
        {
            get { if (Suspended) return default; return Input.GetAxis("Mouse ScrollWheel"); }
        }

        /// <summary>F: usar la máquina de la base más cercana (abrir/cerrar su menú).</summary>
        public static bool InteractPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.F); }
        }

        /// <summary>B: entrar/salir del modo construcción.</summary>
        public static bool BuildModePressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.B); }
        }

        /// <summary>En construcción: R gira la pieza 90°.</summary>
        public static bool BuildRotatePressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.R); }
        }

        /// <summary>En construcción: Tab cambia de categoría.</summary>
        public static bool BuildCategoryPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.Tab); }
        }

        /// <summary>En construcción: clic izquierdo o Intro colocan la pieza.</summary>
        public static bool BuildPlacePressed
        {
            get { if (Suspended) return default; return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return); }
        }

        /// <summary>Tecla numérica 1–9 (índice 0–8).</summary>
        public static bool NumberPressed(int index)
        {
            if (Suspended) return false;
            return index >= 0 && index < 9 && Input.GetKeyDown(KeyCode.Alpha1 + index);
        }

        /// <summary>L: foco del robot.</summary>
        public static bool FlashlightPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.L); }
        }

        /// <summary>Z: escáner geológico.</summary>
        public static bool ScanPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.Z); }
        }

        /// <summary>X (mantener): usar la herramienta del brazo delantero (taladro, pala o soplador).</summary>
        public static bool DrillHeld
        {
            get { if (Suspended) return default; return Input.GetKey(KeyCode.X); }
        }

        /// <summary>F mantenida (interacciones largas, como minar una roca).</summary>
        public static bool InteractHeld
        {
            get { if (Suspended) return default; return Input.GetKey(KeyCode.F); }
        }

        /// <summary>I: inventario (bahía de carga).</summary>
        public static bool InventoryPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.I); }
        }

        /// <summary>J: bitácora (registro de mensajes).</summary>
        public static bool LogPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.J); }
        }

        /// <summary>Intro o espacio: avanzar el diálogo.</summary>
        public static bool DialogueAdvancePressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space); }
        }

        public static Vector2 Look
        {
            get { if (Suspended) return default; return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); }
        }

        public static bool RepairHeld
        {
            get { if (Suspended) return default; return Input.GetKey(KeyCode.R); }
        }

        public static bool NextToolPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.T); }
        }

        public static bool NextModulePressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.G); }
        }

        public static bool CleanPanelsPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.C); }
        }

        public static bool ReleaseCursorPressed
        {
            get { if (Suspended) return default; return Input.GetKeyDown(KeyCode.Escape); }
        }

        public static bool ClickPressed
        {
            get { if (Suspended) return default; return Input.GetMouseButtonDown(0); }
        }
#else
        // El proyecto solo tiene activado el nuevo Input System: ver Project Settings ▸ Player ▸
        // Active Input Handling = "Both", o implementar aquí la lectura con UnityEngine.InputSystem.
        private static bool _warned;

        private static void Warn()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("[RobotInput] Input Manager clásico desactivado. Pon 'Active Input Handling' en 'Both' o adapta RobotInput.cs.");
        }

        public static Vector2 Move { get { Warn(); return Vector2.zero; } }
        public static Vector2 Look { get { return Vector2.zero; } }
        public static float KeyboardTurn { get { return 0f; } }
        public static bool CameraTogglePressed { get { return false; } }
        public static float Zoom { get { return 0f; } }
        public static bool InteractPressed { get { return false; } }
        public static bool BuildModePressed { get { return false; } }
        public static bool BuildRotatePressed { get { return false; } }
        public static bool BuildCategoryPressed { get { return false; } }
        public static bool BuildPlacePressed { get { return false; } }
        public static bool NumberPressed(int index) { return false; }
        public static bool FlashlightPressed { get { return false; } }
        public static bool ScanPressed { get { return false; } }
        public static bool DrillHeld { get { return false; } }
        public static bool InteractHeld { get { return false; } }
        public static bool InventoryPressed { get { return false; } }
        public static bool LogPressed { get { return false; } }
        public static bool DialogueAdvancePressed { get { return false; } }
        public static bool RepairHeld { get { return false; } }
        public static bool NextToolPressed { get { return false; } }
        public static bool NextModulePressed { get { return false; } }
        public static bool CleanPanelsPressed { get { return false; } }
        public static bool ReleaseCursorPressed { get { return false; } }
        public static bool ClickPressed { get { return false; } }
#endif
    }
}
