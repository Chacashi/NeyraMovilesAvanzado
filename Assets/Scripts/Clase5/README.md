# Clase5: cofre y objetos bajo latencia

## Preparacion

1. Abrir `Assets/Scenes/Clase5.unity` y dejar que Unity instale **Multiplayer Tools 2.2.12** (declarado en `Packages/manifest.json`).
2. Usar 3-4 instancias con Multiplayer Play Mode: una como host y las demas como clientes. Iniciar la red desde el Inspector de `ManagerNetwork` / NetworkManager.
3. En cada instancia, `ManagerNetwork` tiene un **Network Simulator** con el preset `Latency200ms.asset`: 200 ms de retardo artificial por paquete, jitter 0 y perdida 0. No equivale necesariamente a 200 ms de RTT; medir el RTT aparte. `UnityTransport.DebugSimulator` esta obsoleto y no produce este efecto.
4. En pruebas con builds usar **Development Build** para la simulacion. Para diferentes equipos cambiar la direccion de conexion al IP del host y su `ServerListenAddress` a `0.0.0.0`; la escena viene preparada para pruebas locales en `127.0.0.1:65535`.

## Controles

- Movimiento: accion `Player/Move` (WASD/flechas o stick).
- Mantener **E** cerca del cofre durante **3 segundos**: abrirlo.
- Soltar E antes de terminar, alejarse o desconectarse: cancelar, reiniciar progreso y liberar el bloqueo.
- Pulsar **E** cerca de un cubo libre: recoger el mas cercano.
- Pulsar **E** llevando un cubo: soltarlo sin impulso.
- **Clic izquierdo** (`Player/Attack`) llevando un cubo: lanzarlo hacia donde mira el avatar.
- Salir del plano y caer por debajo de Y = -10: reaparecer con `Teleport` llamado por el propietario. Los cubos que caen se devuelven con `Teleport` llamado por el servidor.

No es necesario asignar un cofre de escena dentro del prefab del jugador: el servidor lo busca al interactuar. El RPC del avatar llama directamente a los metodos de servidor del cofre; no reenvia un segundo RPC perdiendo el remitente original.

## Feedback visual del cofre

`Chest.prefab` contiene la UI como parte del asset:

```
Chest
└── ProgressCanvas (Canvas World Space + fondo Image)
    └── CountdownText (TextMeshProUGUI)
```

Al iniciar una apertura aceptada por el servidor, todos los clientes ven `ABRIENDO...`, los segundos restantes y el porcentaje. Soltar E o alejarse oculta el panel; completar la apertura despawnea el cofre con su UI. Los clientes tardios leen el estado actual en OnNetworkSpawn.

El script solo actualiza el texto y la visibilidad mediante los eventos de `UserOpening` y `Progress`: no instancia Canvas ni TMP. Posicion, escala, fondo, tipografia y tamano se editan en el prefab. `Face Camera` orienta el Canvas a la camara de cada cliente; se puede desactivar desde Chest. La escena usa una instancia del prefab y se pueden colocar mas instancias, cada una con su bloqueo y progreso independientes. El avatar elige el cofre mas cercano dentro de alcance.

## Casillas del Inspector (antes de ejecutar)

| Configuracion | `Player.prefab` | `Item.prefab` |
| --- | --- | --- |
| NetworkTransform / AuthorityMode | Owner | Server |
| Posicion sincronizada | X/Z; Y apagada | X/Y/Z |
| Rotacion sincronizada | Y; X/Z apagadas | X/Y/Z |
| Escala sincronizada | Apagada | Apagada |
| Interpolate | Activado | Activado |
| Position/Rotation Interpolation Type | Lerp (base para comparar) | LegacyLerp |
| Use Unreliable Deltas | Activado | Apagado |
| Use Half Float Precision | Activado | Apagado |
| Switch Transform Space When Parented | Apagado | Activado |
| NetworkRigidbody | No aplica | Auto Update Kinematic State activado |

**Importante:** en esta version de NGO, `Switch Transform Space When Parented` y `Use Unreliable Deltas` no pueden estar activados juntos. El ahorro de ancho de banda se aplica al avatar; los cubos conservan la sincronizacion fiable para cambiar entre espacio local y mundo.

Los cubos siguen siendo propiedad del servidor incluso mientras un cliente los sostiene. `HeldBy` bloquea una segunda recogida; `TrySetParent` los fija al avatar, con collider desactivado y cuerpo cinematico mientras estan sostenidos. Soltar/arrojar quita el padre conservando el mundo, habilita fisica solo en servidor y aplica velocidad inicial. Los clientes permanecen cinematicos mediante NetworkRigidbody.

## Prueba de interpolacion con 200 ms

Las tres variantes **deben compararse mirando un avatar remoto**, no el propio: el propietario mueve inmediatamente su personaje y no interpola su propio movimiento.

1. Detener Play Mode. Abrir `Player.prefab` y cambiar Position y Rotation Interpolation Type a **LegacyLerp**.
2. Iniciar host y clientes con el preset de 200 ms. Hacer caminar a un jugador en linea recta, cambiar bruscamente de direccion y parar. Observar desde otra instancia y grabar 20-30 segundos.
3. Repetir el mismo recorrido con **Lerp** y luego **SmoothDampening**, manteniendo iguales el TickRate (30), Max Interpolation Time (0.1), velocidad, latencia y numero de jugadores.
4. Registrar suavidad, retraso visual al parar/girar, correcciones bruscas y RTT observado. No presentar observaciones esperadas como resultados medidos.

| Variante | RTT observado | Suavidad | Retraso al parar/girar | Correcciones visibles |
| --- | --- | --- | --- | --- |
| LegacyLerp | Pendiente | Pendiente | Pendiente | Pendiente |
| Lerp | Pendiente | Pendiente | Pendiente | Pendiente |
| SmoothDampening | Pendiente | Pendiente | Pendiente | Pendiente |

## Verificaciones multijugador

- Dos jugadores mantienen E al mismo tiempo: el servidor acepta solo uno; `UserOpening` identifica al ganador, incluido el host (ID 0 es valido). Quien pierde debe soltar y volver a pulsar para intentar otra vez.
- Soltar E a mitad de apertura: `Progress` vuelve a 0 cuando llega la cancelacion al servidor. Con latencia la respuesta no es instantanea.
- Completar apertura: aparecen exactamente cinco cubos en cinco puntos diferentes; solo el servidor los instancia y despawnea el cofre.
- Intentar recoger el mismo cubo a la vez: solo uno lo obtiene, nadie puede robar el que otro lleva.
- Caminar/girar con un cubo: queda en `HandSlot` local, sin perseguir una posicion interpolada del mundo.
- Soltar y lanzar desde host y clientes: todos ven el mismo resultado fisico, calculado por servidor.
- Lanzar fuera del plano: al cruzar `FallLimit` el servidor pone velocidades lineal/angular a cero y hace Teleport al punto de aparicion.
- Caer con el avatar llevando un cubo: el propietario hace Teleport a su punto inicial y el servidor devuelve el cubo a su punto de aparicion.
- Desconectarse mientras se abre o se lleva un objeto: se libera el cofre y se recupera el cubo.

La compilacion y las referencias serializadas se pueden verificar automaticamente. Los resultados visuales bajo latencia y las carreras entre clientes requieren ejecutar estas pruebas en Unity; la tabla queda pendiente hasta realizarlas.
