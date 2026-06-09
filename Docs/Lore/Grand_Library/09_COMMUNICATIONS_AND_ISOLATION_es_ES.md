<!-- localization_status: draft_machine_or_llm_es_ES -->
# COMUNICACIONES, TELEMETRÍA Y SILENCIO ORBITAL

> **Fuente:** manual de guardia de comunicaciones del Black Keel, notas de formación sobre relés de salvamento, anotaciones Merodeadoras recuperadas.  
> **Alcance:** Por qué las tripulaciones en HECTON-8 se sienten solas, qué puede transmitirse realmente a través del océano y cómo el silencio se convierte en física y política a la vez.  
> **Nota para el lector:** No hay llamada FTL a casa, no hay canal de rescate instantáneo y no existe una línea limpia entre una señal fallida y una respuesta retenida.

---

## 1. Ningún canal milagroso

HECTON-8 enseña la misma lección a cada buzo nuevo: la distancia no es lo único que te separa de la ayuda.

Ran está lo bastante lejos para que el tráfico interestelar ordinario llegue como horario, no como conversación. La órbita de Aegir está lo bastante cerca para verse en instrumentos y aun así demasiado lejos para sentirse misericordiosa. Entre el buzo y el Black Keel hay un océano lleno de sal, iones metálicos, capas térmicas, polvo mineral suspendido, infraestructura rota, película viva, espejos de salmuera y la mala costumbre de la presión de convertir fallos pequeños en fallos de sistema.

No hay ansible. No hay rayo de emergencia que atraviese la luna. No hay operador de rescate esperando oír una última frase heroica. Deep Reach vendía "conciencia operativa continua" en los contratos porque la frase era útil. Lo que recibieron las tripulaciones fue una cadena de canales estrechos, retrasados y con pérdidas que funcionaban mejor cuando nadie los necesitaba desesperadamente.

Esa diferencia importa. En HECTON-8, el aislamiento no es solo emocional. Está construido con física, ancho de banda, lenguaje legal y el coste de mantener despierto a un humano al otro lado.

*[Nota al margen: Si el folleto dice "conectado", pregunta conectado a qué. Un servidor de nóminas no es un amigo.]*

## 2. Lo que el océano hace con la señal

El océano no bloquea todas las señales de la misma manera. Es peor que eso.

La radio falla rápido porque el agua conductora, las sales disueltas, el sedimento rico en metal, restos de casco, masa de cables y polvo de pressure glass devoran el alcance útil. Los enlaces láser mueren en dispersión y nubes de partículas. Las señales ópticas estrechas solo funcionan en líneas de visión cortas y limpias, y HECTON-8 rara vez da líneas limpias durante mucho tiempo. La inducción magnética puede cojear a distancias muy cortas, suficiente para equipo acoplado, herramientas emparejadas o un saludo de traje, pero no para una conversación con órbita.

La acústica viaja más lejos, pero trae sus propios problemas. El sonido se curva en gradientes térmicos. Las capas de salmuera lo reflejan. La maquinaria en movimiento lo ensucia. Animales grandes y cascos viejos pueden enmascararlo. Una frontera de densidad puede lanzar un paquete de lado y hacer que el receptor crea que el emisor se movió. El océano no necesita ser una jaula perfecta. Solo necesita ser lo bastante inconsistente para que la certeza se vuelva cara.

Por eso "apagón" es una palabra engañosa. Un apagón suena a ausencia. HECTON-8 da a las tripulaciones algo más cruel: fragmentos. Una alerta de presión llega sin la ruta que la explica. Un ping de socorro llega después de que la sala ha cambiado. Un nombre pasa limpio, pero falla el checksum de coordenadas. Un canal muerto repite el paquete de ayer hasta que un buzo agotado empieza a contestarle.

## 3. Telemetría acústica

La mayor parte de la comunicación de largo alcance a través del agua usa telemetría acústica de baja frecuencia.

En los diagramas de entrenamiento ideales, el buzo envía un paquete a un relé local. El relé lo empuja por un canal de baja frecuencia. Una boya superior, un cable spine o un receptor orientado a órbita recibe el paquete, lo valida y reenvía el evento a los sistemas del Black Keel. En campo, cada paso puede ser doblado por geología, tráfico, pérdida de energía, corrosión o un relé que aún tiene número de serie pero ya no guarda lealtad útil a la red que lo rodea.

El ancho de banda no es cinematográfico. Es estrecho, lento y racionado. Una tripulación puede enviar códigos de estado, alertas de presión de traje, route tags, hashes de manifiesto, ráfagas breves de texto, firmas de reclamación y evidence flags comprimidos. No puede emitir vídeo desde el casco en el fondo del basin. No puede sostener una llamada normal con órbita. No puede explicar deprisa una sala complicada salvo que ya preparase las etiquetas correctas antes de que la sala se volviera complicada.

El retraso tampoco es un único número. Una buena ruta somera puede sentirse casi receptiva. Una ruta profunda a través del ruido de un cañón de salmuera puede convertir una respuesta en ritual. Ocho minutos son lo bastante comunes para volverse chiste; quince lo bastante comunes para dejar de tener gracia. Bajo presión, incluso noventa segundos pueden durar más que una decisión humana.

*[Nota al margen: El manual dice "envía código de socorro". No dice qué hacer mientras el océano decide si el código sigue siendo tuyo.]*

## 4. Relés, huesos e infraestructura muerta

Deep Reach no dependía de un transmisor limpio. Construyó capas.

Las rutas superiores usaban mástiles de boya, pilones de servicio, nodos tether y repetidores de plataforma. El Cable Reef se convirtió en un esqueleto de comunicaciones denso y feo: troncos de energía, data umbilicals, abrazaderas de reparación, carcasas de relé y hardware cubierto de biofilm que aún despierta con el voltaje correcto. Los sistemas más profundos usaban acoustic pingers, cachés de mantenimiento, pressure-rated memory spools y route beacons capaces de guardar un mensaje hasta que un receptor pasara dentro del alcance.

Tras el Great Tide, esas capas no murieron simplemente. Algunas murieron. Algunas quedaron en loop. Algunas se volvieron locales. Algunas aceptaban paquetes y nunca los reenviaban. Algunas reenviaban paquetes viejos con timestamps nuevos. Algunas aún responden a la lógica de continuidad de Atlas en vez de al procedimiento del Black Keel. Algunas son útiles precisamente porque ninguna oficina recuerda que existen.

Los buenos Merodeadores aprenden la diferencia entre un relé y un fantasma. Un relé prueba un camino. Un fantasma solo prueba que algo tuvo energía y una razón para hablar.

Esa distinción se vuelve gameplay. El jugador puede restaurar un route beacon y abrir navegación más segura. Puede encontrar un memory spool y recuperar un mensaje que nadie arriba quería indexar. Puede usar un relé muerto como señuelo, decoy o listening post. El hardware de comunicación no es escenografía. Es poder viejo, custody vieja y miedo viejo intentando moverse todavía.

## 5. El régimen de escucha del Black Keel

El Black Keel escucha. No es lo mismo que responder.

Como claim tender, el Keel prioriza custody events: subida de manifiesto, prueba de material, identidad de contratista, estado de ruta, solvencia de traje, recoverable evidence y señales que afectan responsabilidad. Reconoce lo que el sistema puede valorar. Escala lo que podría dañar la estructura de reclamación. Registra más de lo que consuela.

Hay watch officers humanos a bordo, pero no están sentados en un canal dramático esperando salvar a un buzo. Gestionan ventanas, colas, revisión de paquetes corruptos, arbitration holds, security flags y el trabajo constante de probar que el Keel respondió según política. Un oficial de guardia puede preocuparse. La cola, no. La política es donde la preocupación va para volverse admisible o inútil.

Deep Reach llamaba a esta disciplina "orbital silence" durante periodos de reclamación activa. El término sonaba a seguridad operativa. En la práctica significaba que el tender evitaría iniciar contacto innecesario, preferiría receipts antes que conversación y trataría el habla no estructurada como fuente de responsabilidad.

Por eso un Merodeador puede gritar en un canal y recibir solo un número limpio de acuse de recibo.

*[Nota al margen: El Keel te oyó. Esa nunca fue la pregunta.]*

## 6. Rutas de fallo

Los fallos de comunicación en HECTON-8 rara vez llegan como una sola luz roja.

Una cola de paquetes puede llenarse mientras la tripulación cree que el relé transmite. Un traje puede reenviar la misma alerta de presión hasta que el receptor la suprima como ruido duplicado. Un relé puede estar físicamente presente pero seguir vinculado a un custody owner antiguo. Un route beacon puede despertar tras una subida de energía y sobrescribir un mapa nuevo con una ruta pre-Tide. Un watch system puede poner en quarantine un mensaje porque un evidence flag, un debt flag y un distress flag llegaron en el orden equivocado.

Los malos datos no siempre son silencio. A veces los malos datos son confianza.

Los fallos más peligrosos son stale handles: IDs de contacto antiguos, confianza antigua en relés, nombres de ruta antiguos, sellos de autorización antiguos. Un buzo cree que habla con el Black Keel. El paquete en realidad rebota por una caché local que no ha visto órbita en veinte años. Una tripulación sigue una respuesta que era válida antes de que se moviera un labio de falla. Un salvage manifest llega a custody, pero la petición de ayuda adjunta cae porque no forma parte del schema aceptado.

Por eso las tripulaciones marcan sus propias rutas y guardan pruebas físicas. La pintura en una escotilla puede sobrevivir a una cuenta de relé. Una línea atada puede superar una coordenada limpia. Una etiqueta de cuerpo puede llevar una verdad que la telemetría se negó a clasificar.

## 7. Aislamiento como presión sobre el jugador

El aislamiento no debe sentirse como una excusa de lore. Debe sentirse como un sistema de presión.

El jugador puede recibir pings, fragmentos, receipts, advertencias retrasadas, mensajes corruptos, viejos fantasmas de ruta, acuses del Black Keel, respuestas locales de Atlas y marcas hechas por tripulaciones. Ninguno debe sentirse como narrador perfecto. Cada señal exige juicio. ¿Quién la envió? ¿Cuándo? ¿Por qué relé? ¿Qué omite? ¿A quién beneficia que el jugador confíe?

Esto da al setting una soledad específica. El jugador no está solo porque el universo lo haya olvidado. El jugador está solo porque los sistemas disponibles pueden ver partes de él y aun así no convertirse en ayuda.

Un enlace de comunicaciones operativo puede ser más aterrador que uno muerto. Un enlace muerto dice la verdad claramente. Un enlace operativo puede decirte que tu alerta de oxígeno fue recibida, tu reclamación sigue activa, tu carga está pendiente y no se implica derecho de rescate.

Ese es el silencio de HECTON-8. No ausencia de sonido. Presencia de sistemas que oyeron lo suficiente para facturar el momento, pero no lo suficiente para salvarlo.
