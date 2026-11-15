using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Core
{
    /// <summary>
    /// Registro mínimo de servicios (service locator) para que los sistemas se encuentren
    /// sin usar FindObjectOfType en cada frame ni singletons rígidos.
    ///
    /// Uso:
    ///   GameServices.Register(this);              // en Awake del sistema
    ///   GameServices.TryGet(out TerrainManager t); // en cualquier otro sistema
    ///   GameServices.Unregister(this);            // en OnDestroy
    /// </summary>
    public static class GameServices
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>Registra un servicio por su tipo concreto. Sustituye al anterior si existía.</summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null) return;
            Services[typeof(T)] = service;
        }

        /// <summary>Elimina el servicio solo si es exactamente la instancia registrada.</summary>
        public static void Unregister<T>(T service) where T : class
        {
            object current;
            if (Services.TryGetValue(typeof(T), out current) && ReferenceEquals(current, service))
            {
                Services.Remove(typeof(T));
            }
        }

        /// <summary>Intenta obtener un servicio registrado.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            object obj;
            if (Services.TryGetValue(typeof(T), out obj))
            {
                service = obj as T;
                // Un objeto de Unity destruido compara igual a null: lo limpiamos.
                var unityObj = service as UnityEngine.Object;
                if (service != null && (!(service is UnityEngine.Object) || unityObj != null))
                {
                    return true;
                }
                Services.Remove(typeof(T));
            }
            service = null;
            return false;
        }

        /// <summary>Limpia el registro al recargar el dominio (útil con "Enter Play Mode Options").</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Services.Clear();
        }
    }
}
