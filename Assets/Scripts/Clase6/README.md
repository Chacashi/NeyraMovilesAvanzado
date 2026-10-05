# Clase6: selector y ciclo de vida de objetos de red

**Continuacion Clase7:** esta misma escena ahora conecta `InventarioDeRed` y `PoolDeRed`. E guarda cubos propios/libres como datos y R los suelta. Consultar `Assets/Scripts/Clase7/README.md` para los controles actuales y el reciclaje; las instrucciones de abajo describen la base de Clase6.

## Autoridad y flujo

| Operacion / dato | Autoridad | Mecanismo |
| --- | --- | --- |
| Seleccion de personaje | Cliente propone; servidor valida | Un byte en ConnectionData |
| Aprobacion y prefab del jugador | Servidor | ConnectionApprovalCallback, CreatePlayerObject y PlayerPrefabHash |
| Posicion del personaje | Servidor | Direccion solicitada por el propietario; NetworkTransform Server |
| Crear cofre | Solo Guardian puede solicitar; servidor valida rol/cooldown/espacio | RPC Owner, InstantiateAndSpawn |
| Abrir cofre | Servidor valida distancia y apertura unica | RPC del jugador; SpawnWithOwnership para cada cubo |
| Propiedad del cubo | Servidor | OwnerClientId; ChangeOwnership al regalar |
| Color y feedback de propiedad | Presentacion local | OnNetworkSpawn, OnOwnershipChanged, OnGainedOwnership y OnLostOwnership |
| Cubo reclamado o libre | Estado del servidor | NetworkVariable<bool> Claimed (necesaria: ID 0 puede ser host o servidor sin propietario humano) |
| Consumir | Servidor, solo si pertenece al solicitante y esta cerca | Despawn(true), porque el cubo es dinamico |
| Desconexion del propietario | Servidor | DontDestroyWithOwner=true y RemoveOwnership; Claimed=false |
| Fisica del botin | Servidor aunque el cubo pertenezca a un cliente | NetworkTransform Server + NetworkRigidbody |

Secuencia: elegir personaje -> Crear partida/Unirse -> aprobacion -> prefab correspondiente -> OnNetworkSpawn. NGO crea el PlayerObject inicial usando la respuesta de aprobacion. El host tambien pasa por la aprobacion y puede elegir cualquiera de los dos roles.

Se parte de los scripts del profesor: `PlayerAssigner` conserva la aprobacion y `UIManager` conserva StartServer (StartHost) y StartClient. Los RPC se trasladan de PlayerAssigner (MonoBehaviour) a Clase6Player (NetworkBehaviour). Para el ejemplo de reemplazo, V llama SwitchCharacterRpc: el servidor despawnea el jugador anterior y usa SpawnAsPlayerObject para el nuevo, sin dejar dos PlayerObjects. Respeta el puesto unico de Guardian y conserva la propiedad de los cubos del mismo cliente.

Los roles de los jugadores no se transfieren; se transfieren los cubos. Los RPC de accion usan InvokePermission.Owner. No reciben dano, posiciones de aparicion ni el propietario destino desde el cliente: el servidor calcula proximidad, rol y destino.

## UI y controles

La escena usa una instancia de `RoleSelector.prefab`, con Canvas, TMP, Buttons y EventSystem. `SelectorButton.prefab` es el boton reutilizable. El codigo conecta los botones y actualiza texto; no instancia la UI. Todo el diseño se edita desde los prefabs.

1. Elegir **Guardian** o **Explorador**.
2. Pulsar **Crear partida (Host)** en una instancia y **Unirse (Cliente)** en las otras dos. Elegir antes de conectar decide el prefab aprobado.
3. WASD/stick: moverse; el personaje mira hacia su ultima direccion de movimiento.
4. **B**: Guardian solicita cofre frente a si. Explorador es rechazado en servidor. Hay un cooldown de 2 s.
5. **E**: abrir un cofre cercano (ambos roles), o reclamar el cubo libre mas cercano. Un cofre se abre una sola vez y produce tres cubos cuyo propietario inicial es quien lo abrio.
6. **G**: regalar el cubo propio mas cercano (alcance 3) a otro jugador cercano (alcance 4). El receptor no necesita reclamarlo de nuevo.
7. **X**: consumir el cubo propio cercano: se destruye en todas las instancias.
8. **Desconectarse**: deja los cubos existentes en el suelo, grises y libres. Otro jugador puede reclamarlos con E.
9. **V**: cambiar de personaje mediante el ejemplo SpawnAsPlayerObject del profesor. Explorador solo puede pasar a Guardian si ese puesto esta libre; Guardian puede pasar a Explorador y liberar el puesto.

El color del personaje y de sus cubos se deriva de OwnerClientId. El contador local de la UI excluye cubos libres aunque el servidor/host tenga ID 0. Los eventos de propiedad se registran en Console con NetworkObjectId y dueño; OnLostOwnership indica al antiguo propietario que ya no tiene autoridad sobre ese cubo.

`Claimed` no reemplaza la propiedad: distingue botin libre de botin asignado al host, porque ambos tienen OwnerClientId=0. Una cache privada de servidor recuerda el ultimo propietario asignado para liberar sus cubos aunque NGO ya haya retirado automaticamente su ownership antes del callback de desconexion.

## Configuracion

- Limite: tres participantes, incluido el host, con un solo Guardian (idea HasGuardian del profesor). El servidor rechaza una segunda seleccion Guardian con un motivo visible; ese cliente puede elegir Explorador y reintentar.
- Transporte local: `127.0.0.1:7786`. Para equipos diferentes cambiar `ServerAddress` en PlayerAssigner al IP del host; el servidor escucha en `0.0.0.0`.
- PlayerPrefab predeterminado Guardian; la aprobacion reemplaza PlayerPrefabHash por el prefab seleccionado.
- Guardian, Explorador, Chest y LootCube registrados en `Clase6NetworkPrefabs.asset`.
- Todos los cofres y cubos se crean dinamicamente, por lo que Despawn(true) es apropiado. Los cubos tienen DontDestroyWithOwner=true tanto en prefab como antes de SpawnWithOwnership.
- Los cubos que caen fuera del mapa vuelven a su punto de aparicion mediante Teleport del servidor.

## Pruebas con tres instancias

| Prueba | Resultado a comprobar |
| --- | --- |
| Host Explorador, cliente Guardian | Cada uno recibe el prefab elegido; solo el Guardian puede crear cofres |
| Elegir Guardian/Explorador antes de conectar | PlayerObject correcto y un solo jugador por conexion |
| Payload invalido / cuarto participante | Conexion rechazada por servidor con motivo |
| B como Guardian | Cofre aparece delante en las tres instancias |
| B como Explorador / spam de B | No crea cofre; reglas de servidor se mantienen |
| Dos jugadores abren el mismo cofre | Solo una apertura y tres cubos, propiedad del ganador |
| Host abre cofre | Cubos de ID 0 siguen reclamados, no se confunden con libres |
| G junto a otro jugador | Ownership y color cambian; anterior recibe OnLostOwnership, receptor OnGainedOwnership |
| X sobre cubo ajeno o lejano | No se consume |
| X sobre cubo propio cercano | Despawn(true) lo elimina para todos |
| Desconectar dueño con cubos | Cubos sobreviven, ownership se retira y quedan libres/grises |
| Reclamar botin abandonado | E asigna ownership al nuevo jugador |
| Cliente que entra tarde | Ve objetos y propietarios actuales mediante OnNetworkSpawn |
| V como Explorador con Guardian ocupado | Cambio rechazado; conserva su PlayerObject |
| V como Guardian y luego V de otro Explorador | El puesto se libera y el segundo jugador puede convertirse en Guardian |

La compilacion y las referencias se validan automaticamente. Las pruebas de aprobacion, callbacks y desconexion entre tres instancias deben ejecutarse en Unity y registrar sus resultados; no se consideran realizadas solo porque compile.
