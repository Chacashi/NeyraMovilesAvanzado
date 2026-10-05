# Clase7: inventario de datos y pool, continuacion en Clase6

Se sigue usando `Assets/Scenes/Clase6.unity`, sus roles y cofres. La logica nueva vive en Clase7. Los enlaces minimos en Clase6 conectan E al inventario, conservan los datos al cambiar personaje y permiten que el cofre use el pool de cubos.

## Los cinco puntos del laboratorio

1. **Ranura unmanaged**: `Ranura.cs` contiene solo dos int (`tipo`, `cantidad`), implementa INetworkSerializable e IEquatable<Ranura>. No guarda GameObjects, strings ni referencias. La lista representa datos, no una coleccion de objetos ocultos.
2. **OnListChanged**: `InventarioDeRed` crea NetworkList<Ranura> en Awake, se suscribe al aparecer y se desuscribe al despawnear. El propietario registra Add, Value, RemoveAt, Clear y Full y refresca su UI. La lista se lee solo por Owner (y servidor), escribe solo Server. OnNetworkSpawn tambien pinta el estado inicial.
3. **Guardar y desaparecer**: E solicita al servidor recoger el cubo propio/libre mas cercano. Valida distancia, propiedad, capacidad y que no este ya guardandose. Add crea una ranura; asignar una copia modificada a Ranuras[index] incrementa la pila (Value). Luego Despawn(true) retira el objeto de la red. El inventario no conserva una referencia a ese GameObject.
4. **Pool de red**: PoolDeRed implementa INetworkPrefabInstanceHandler y se registra en Awake, antes de conectar, en TODAS las instancias. Los clientes reciben objetos del handler; el servidor toma una instancia explicitamente y hace SpawnWithOwnership. El pool tiene 12 objetos precalentados y retiene como maximo 64 inactivos. No crea un reemplazo automatico cada vez que se recoge/consume: reaparece por demanda al soltar o abrir otro cofre.
5. **Limpiar el pasado**: CuboReciclado reinicia el bloqueo de recogida, tipo pendiente, velocidad lineal/angular y estado cinematico. Clase6LootCube desuscribe sus eventos y limpia cache de dueño/punto de retorno al despawnear. Al aparecer pinta con el nuevo OwnerClientId y estado Claimed. Se restablece la escala del prefab y no se conserva una posicion de spawn anterior.

El bosque se sustituye por reciclaje del botin existente: el concepto de pool se practica con cubos.

## Controles y UI

- Elegir rol y arrancar host/clientes con el selector de Clase6.
- **B** como Guardian: crear cofre.
- **E** junto al cofre: abrirlo; los tres cubos nacen asignados a quien lo abrio.
- **E** junto a un cubo propio o libre: guardarlo como `tipo=1, cantidad`. No se pueden robar cubos ajenos; el dueño puede regalarlos con G antes de recogerlos.
- **R** o boton **Soltar**: sacar una unidad de la ranura seleccionada. Se vuelve a generar desde el pool, delante del jugador, libre/gris para que otro pueda recogerla.
- Boton **Consumir**: decrementar una unidad del dato almacenado, sin generar un objeto del mundo.
- **Anterior / Siguiente**: elegir ranura. Cuando una pila llega a cero, RemoveAt elimina la ranura y ajusta la seleccion.
- **X / G** conservan sus acciones de Clase6 sobre cubos en el mundo.
- **V** conserva el inventario al reemplazar el PlayerObject: copia datos en servidor y restaura la lista cuando aparece el nuevo personaje. No duplica objetos ni deja una copia del inventario anterior.

La UI esta guardada en `InventarioPanel.prefab` y colocada en Clase6. Se edita desde el Inspector. No se crea Canvas, TMP ni botones por codigo. Cada cliente ve solamente su inventario; el texto se reconstruye por eventos, no buscando items cada frame. El contador izquierdo distingue cubos del mundo de unidades guardadas.

Capacidad: 30 unidades por jugador, ajustable en los prefabs Guardian y Explorer. Actualmente existe un tipo de botin (Cubo, ID 1), apilable. El struct y la seleccion permiten añadir mas tipos sin guardar referencias a prefabs dentro de las ranuras.

## Ciclo de vida y memoria

`objeto del mundo -> datos en NetworkList -> Despawn -> handler Destroy -> instancia inactiva en pool`

`dato seleccionado -> instancia libre del pool -> nuevo SpawnWithOwnership -> reset de estado -> objeto del mundo`

Despawn(true) no implica Object.Destroy si ese prefab tiene handler: NGO llama al Destroy del pool. No se usa Despawn(false) sin avisar al handler, porque dejaria objetos visibles/activos o sin devolver en clientes. Una devolucion duplicada se ignora mediante HashSet. Las instancias en reserva son DontDestroyOnLoad para sobrevivir a la sincronizacion de escena; se limpian al destruir el gestor. NetworkBehaviour dispone la NetworkList al destruir el jugador.

Al desconectarse un jugador, el servidor convierte sus ranuras almacenadas en cubos libres en el suelo en el siguiente frame, evitando modificar colecciones de spawn durante el despawn del jugador. Los cubos que ya estaban en el mundo siguen usando la liberacion de ownership de Clase6. No se generan drops durante Shutdown de toda la partida.

## Pruebas con host y dos clientes

| Prueba | Resultado esperado |
| --- | --- |
| Recoger tres cubos de un cofre propio | Una ranura Cubo x3; un Add y dos Value; desaparecen tres objetos de red |
| Leer desde otra instancia | Solo el propietario tiene UI de esas ranuras |
| Recoger cubo ajeno | Rechazado; puede regalarse primero con G |
| Soltar una unidad | Value decrementa; aparece un cubo libre, cantidad total se conserva |
| Recoger el cubo soltado con otro jugador | Entra en el inventario de ese jugador |
| Soltar/consumir la ultima unidad | RemoveAt y UI vacia, botones deshabilitados |
| Pedir tipo inexistente o soltar inventario vacio | No aparece nada ni se resta cantidad |
| Llegar a 30 unidades | No se recoge ni despawnea el siguiente cubo |
| Alternar E y R 50 veces | Reutiliza GetInstanceID; cambia NetworkObjectId por vida; ObjetosCreados del pool no crece en cada ciclo |
| Objeto reciclado que antes tenia dueño o velocidad | Nuevo dueño/estado libre y velocidades reseteadas |
| Cambiar rol con V teniendo cubos guardados | Conserva cantidad sin soltar ni duplicar objetos |
| Desconectar un cliente con inventario | Aparecen sus unidades como cubos libres para los restantes |
| Detener y volver a arrancar host/cliente | Sin callbacks duplicados ni listas NativeList perdidas |

La compilacion y referencias serializadas se comprueban automaticamente. Las pruebas de reciclaje, eventos y desconexion requieren ejecutar las instancias en Unity y registrar resultados. El ejemplo `Caso01_ListaDentroDeVariable.cs` del profesor se conserva como demostracion y no se coloca en el jugador.
