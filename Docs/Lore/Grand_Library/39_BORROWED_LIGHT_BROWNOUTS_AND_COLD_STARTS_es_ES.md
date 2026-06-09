<!-- localization_status: draft_machine_or_llm -->
# LUZ PRESTADA, CAIDAS DE TENSION Y ARRANQUES EN FRIO

> **Fuente:** curso de energía de emergencia de Deep Reach, tarjetas de servicio de salas de interruptores, notas de valoración de Black Keel y grabaciones de merodeadores en anexos restaurados.  
> **Alcance:** prioridad de cargas, conducta en brownout, arranques en frío, reservas prestadas y registros de energía usados para rutas, reclamaciones y decisiones de supervivencia.  
> **Uso de campo:** leer antes de confiar en una sala iluminada, puentear un interruptor, despertar un panel muerto, abrir un armario alimentado o mover una celda que quizá alimenta otra cosa.

---

## 1. Una sala iluminada es una carga

En HECTON-8, la luz no prueba que una sala esté sana. Prueba que un circuito sigue gastando energía.

Una lámpara puede funcionar con reserva de emergencia mientras el controlador de la puerta a su lado ya no tiene autoridad para desbloquear. Un corredor puede brillar porque el hábitat todavía protege iluminación de ánimo después de que las bombas útiles hayan dejado de informar. Una estación puede despertar lo justo para pedir login y morir antes de que la respuesta llegue al búfer. Una perla verde de estado puede seguir viva porque está en el lado protegido de un interruptor, no porque el sistema detrás viva.

Deep Reach construyó la jerarquía de energía alrededor de supervivencia, responsabilidad y costumbre. Aire primero. Control de presión después. Calor donde el calor mantenía flexibles las juntas. Refrigeración donde muestras, medicina o cuerpos creaban exposición legal. Datos donde los logs evitaban pérdidas médicas o contractuales. La luz venía después, salvo que la propia oscuridad creara riesgo de tropiezo, corte o pánico.

Tras el abandono, esa jerarquía se volvió más difícil de leer. Una sala puede parecer ocupada porque un circuito barato aún funciona. Puede parecer muerta porque una carga más prioritaria tomó cada celda a su alcance. La pregunta útil no es si la luz está encendida. La pregunta útil es qué carga está pagando por ella.

## 2. Orden de brownout

Un brownout no es un apagón. Es una secuencia.

Cuando cae el voltaje, los sistemas mantenidos descargan cargas en un orden diseñado. Las tiras de luz mural caen antes que las bombas de circulación. Los terminales no críticos duermen antes que la lógica de presión. Los motores de puerta se ralentizan antes de que los cierres duros liberen. Los armarios médicos pueden conservar refrigeración mientras se niegan a abrir. Una sala segura puede preservar intercambio de aire matando cada toma que una cuadrilla de reparación esperaba usar.

Los sistemas abandonados no siempre siguen el gráfico viejo. Contactores salados se pegan. Corredores parcheados retroalimentan paneles que debían morir. Un sensor muerto puede mantener viva una luz de aviso porque el circuito de aviso es más fácil de alimentar que la verdad que antes comunicaba. Una bomba puede funcionar sin informar porque su raíl de telemetría murió primero.

El orden de brownout importa porque dice cuándo una sala dejó de mantenerse. Qué cargas murieron primero puede mostrar si una muestra siguió fría, si una puerta fue sellada por procedimiento o por hambre de energía, si una baliza de socorro tenía energía cuando Black Keel la marcó inactiva, y si alguien movió un interruptor después de cerrar el log oficial de ruta.

## 3. Arranques en frío

Arrancar en frío una sala muerta no es lo mismo que encenderla.

Un arranque en frío pide a máquinas viejas que se muevan después de que presión, sal y tiempo hayan cambiado sus tolerancias. Rodamientos despiertan secos. Contactores arquean a través de película mineral. Pilas de baterías aceptan carga de forma desigual. Ventiladores lanzan polvo asentado, moho o vapor químico a un aire que parecía respirable. La lógica de seguridad compara una sala dañada con umbrales escritos para una colonia con personal y declara sospechosa media zona.

A veces la máquina acierta al sospechar. Una puerta puede bloquearse para proteger un estado de presión que ya no existe. Un calentador puede ablandar una junta que sobrevivía solo por estar fría. Un servidor puede sobrescribir el último registro útil de fallo con un error fresco de arranque. Una bomba puede sacar agua de una sala y empujarla por una bandeja rajada hacia otra.

Las buenas cuadrillas no despiertan una sala entera de golpe. Despiertan primero medición, luego contención, luego movimiento, luego comodidad. Si el orden debe cambiar, escriben por qué. Un arranque en frío es una apuesta sobre secuencia, y HECTON-8 cobra a quienes adivinan por costumbre.

## 4. Energía prestada

La energía prestada hace un trabajo que su etiqueta no admite.

HECTON-8 está llena de ella: celdas de emergencia cruzadas por corredores parcheados, cargadores de drones manteniendo luces de sala segura, un laboratorio muerto robando corriente de goteo a una matriz de antenas, un congelador médico preservando una muestra al quitar autoridad de motor a seis puertas. La colonia rara vez falló en islas limpias. Las cargas siguieron negociando después de que desapareciera la gente que entendía el trato.

Los merodeadores usan energía prestada porque puede convertir una ruta muerta en una ruta pagada. Una línea puente puede despertar una consola lo bastante para tasar un lote de rescate. Una celda portátil puede abrir un armario antes de que la junta se seque. Un cargador puede mover una bomba los minutos necesarios para cruzar un sumidero.

El mismo puente puede drenar la última reserva que alimenta una baliza de prueba, borrar un hueco temporal en un log de energía o hacer que una puerta segura falle cerrada con medicina dentro. A los auditores de Black Keel les gusta la energía prestada cuando aumenta valor recuperable. Les disgusta cuando el nuevo camino de energía explica por qué su vieja negativa era falsa.

## 5. Salas de interruptores

Una sala de interruptores es un mapa con marcas de quemadura.

Las etiquetas de Deep Reach son útiles hasta que dejan de serlo. Un interruptor marcado `Hab Lighting B` puede alimentar una bomba después de tres parches de emergencia. Una manija tapada con cinta puede ocultar una alimentación cruzada improvisada de soporte vital. Un interruptor limpio en una sala sucia suele significar que alguien lo tocó después de la inundación. Un interruptor caliente en un anexo frío merece atención antes que la puerta de al lado.

Las grabaciones de merodeadores prefieren hechos verificables rápido: posición de manija, temperatura de bus, sal en la bisagra, olor en el contactor, qué cargas parpadean cuando muerden las pinzas de celda. Las explicaciones largas matan gente en salas de interruptores. Las etiquetas cortas mantienen las manos más honestas.

La mejor nota no es `safe`. La mejor nota es `alimenta cierre de clínica, salta a 11 A, no puentear durante ciclo de bomba`.

## 6. Energía como prueba

Los registros de energía pueden probar secuencia cuando las salas mienten.

Un disparo de interruptor puede mostrar que una puerta se abrió después de una evacuación declarada. Una curva de carga puede mostrar que una celda portátil se conectó al lado equivocado de un sello de custodia. Un log de brownout puede mostrar que la refrigeración sobrevivió lo bastante para que una muestra conservara valor. Una ausencia de corte puede mostrar que alguien editó el archivo o lo alimentó desde una línea no declarada.

La energía también crea responsabilidad. Una cuadrilla que restaura luz puede revelarse en un log de receptor. Una cuadrilla que mantiene viva una bomba puede destruir un registro de terminal. Una cuadrilla que roba una celda puede convertir una sala segura silenciosa en una muerta y dejar la factura en el rastro de voltaje.

Cada cable movido cambia la historia que la sala puede probar.

## 7. Regla de campo

Antes de confiar en la luz, encuentra su carga.

Antes de puentear energía, nombra qué perderá energía.

Antes de arrancar en frío una sala, despierta los instrumentos que pueden decirte cuándo parar.

En HECTON-8, la oscuridad no prueba vacío y la luz no prueba seguridad. Ambas son estados de energía con dueños, costes y registros. No sobreviven las cuadrillas con las celdas más grandes. Sobreviven las que saben qué interruptor hará que la sala diga la mentira menos cara.
