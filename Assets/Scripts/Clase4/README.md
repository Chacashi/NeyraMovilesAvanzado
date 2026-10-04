# Clase4: combate, estado y eventos

## 1. Matriz de autoridad (definida antes de implementar)

| Dato | Estado o evento | Quien escribe / decide | Lectores o receptores | Transporte |
| --- | --- | --- | --- | --- |
| Vida | Estado persistente: NetworkVariable<int> | Servidor | Everyone | NetworkVariable, sincroniza tambien clientes tardios |
| Posicion y orientacion | Estado continuo: NetworkTransform | Servidor | Todos los clientes | NetworkTransform |
| Direccion de movimiento | Solicitud continua, no estado persistente | Propietario solicita; servidor limita e integra | Servidor | RPC Owner, Unreliable, repetido cada tick |
| Solicitud de golpe | Evento | Propietario solicita; servidor valida | Servidor | RPC Owner, Reliable |
| Impacto y resta de vida | Decision del servidor | Servidor calcula objetivo, alcance, direccion, linea de vision, cooldown y dano | La vida resultante se replica a todos | No se acepta dano ni objetivo enviados por el cliente |
| IsDead | Estado derivado local: Health.Value <= 0 | Nadie lo replica por separado | Cada instancia | Sin otra NetworkVariable<bool> |
| Barra, numero de vida, material e input | Presentacion derivada de vida | Cada instancia, desde OnValueChanged y OnNetworkSpawn | UI y avatar local | Sin RPC de UI |
| Sonido de golpe | Evento de acompanamiento | Solo servidor lo emite | Everyone | RPC Reliable |
| Particulas y dano flotante | Evento cosmetico | Solo servidor lo emite, con dano real infligido | Everyone | RPC Unreliable |
| Cooldown de ataque | Estado privado de validacion | Servidor | Solo servidor | No se replica |
| Reinicio de ronda | Accion de prueba | Solo servidor | Vida/posicion resultantes se replican | Boton Test del host, con comprobacion IsServer |

El movimiento es server-authoritative: el cliente no transmite posiciones ni vida. Esto evita que editar su Transform local le permita validar golpes lejanos en servidor. La direccion recibida se valida y limita; una entrada antigua deja de mover al jugador. El servidor valida ataques aunque el cliente ya tenga el input bloqueado por muerte. Esta demo no incluye prediccion ni compensacion de lag.

`[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]` es el equivalente actual de `[Rpc(SendTo.Server, RequireOwnership = true)]`; RequireOwnership esta obsoleto en NGO 2.13.1.

## Escena y controles

- Abrir `Assets/Scenes/Clase4.unity`. Usar una instancia como host y dos como clientes, iniciando la red desde el Inspector de NetworkManager.
- WASD / stick: moverse; el avatar mira hacia su ultima direccion de movimiento.
- Clic izquierdo / accion Player.Attack: golpe cuerpo a cuerpo hacia delante.
- Vida inicial: 100. Dano: 20. Cooldown: 0.6 s. Los valores se editan en `CombatPlayer.prefab`.
- Con 0 de vida: material gris, input desactivado y solicitudes de movimiento/ataque rechazadas en servidor.
- Para otra ronda, el host pulsa el boton `test` del objeto tESTER: restablece vida y posicion desde servidor. Los clientes no pueden reiniciar la ronda.

## Estado frente a evento

La UI World Space esta guardada en el prefab: barra de vida, TMP de vida y TMP de dano flotante. No se crea Canvas ni TMP por codigo. La vida se pinta inmediatamente en OnNetworkSpawn y cambia mediante OnValueChanged; las suscripciones se retiran en OnNetworkDespawn.

Un cliente tardio ve la vida y muerte actuales pero no reproduce sonidos/particulas de impactos anteriores. Perder un evento Unreliable puede ocultar una particula o un numero, pero nunca cambia la vida real.

El AudioSource y ParticleSystem estan en el prefab. Se puede asignar un AudioClip propio al campo Hit Clip; sin clip se utiliza un pequeno sonido sintetizado localmente, pues el proyecto no tenia audio de impacto. Estos recursos visuales/sonoros son locales y no tienen NetworkObject propio.

## Pruebas con tres participantes

1. Host y clientes se mueven: desde las tres ventanas se ven los mismos jugadores y barras.
2. Golpear de frente, cerca y sin obstaculos: baja exactamente 20; la barra y numero coinciden en todos. Sonido y VFX incluyen al host.
3. Golpear fuera de alcance o de espaldas: no baja vida. Pulsar repetidamente: no supera el cooldown del servidor.
4. Cinco impactos validos: vida 0, material gris, input bloqueado. Un muerto no puede atacar ni moverse mediante solicitudes de red.
5. Reiniciar desde Test del host: todos vuelven a su posicion inicial, con 100 de vida e input habilitado.
6. Conectar tarde despues de varios golpes o una muerte: vida/barra/material correctos; no se reproducen impactos viejos.
7. Con perdida de paquetes simulada, comprobar que omitir feedback cosmetico no cambia la vida. Para esta prueba agregar Network Simulator del paquete Multiplayer Tools y configurar perdida; no usar el DebugSimulator obsoleto del Transport.
8. Intentar modificar la vida o Transform en una instancia cliente: el servidor conserva la vida y posicion autoritativas. La solicitud de ataque no recibe dano, vida ni objetivo del cliente.

Registrar capturas y resultados de las tres instancias. La compilacion y referencias se verifican automaticamente; estas pruebas de ejecucion requieren Unity y no se consideran realizadas solo porque compile.
