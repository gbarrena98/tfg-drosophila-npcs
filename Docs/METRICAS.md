# Definición operativa de métricas

Plano de movimiento XZ, unidades de Unity interpretadas como metros. Rumbo 0° hacia +Z, +90° hacia +X. Todos los cálculos del núcleo usan `double`; la conversión a `float` se limita a la vista de Unity.

## Muestreo y error circular

Cada fila de `trace_*.csv` describe el estado al **inicio** del intervalo `[time_s, time_s + dt_s)`. La actividad L/R, el giro y el coste se obtienen al actualizar el controlador para ese intervalo; la posición y el rumbo registrados son anteriores al movimiento. La posición terminal exacta no ocupa una fila adicional. La última duración se recorta si el límite temporal no es múltiplo del paso.

El error firmado es `wrap(reference − heading)`, con `wrap(a) = atan2(sin(a), cos(a))` tras convertir grados a radianes. Su valor absoluto queda entre 0° y 180°. No se promedian directamente ángulos firmados, porque errores de signos opuestos se cancelarían.

`mae_reference_deg = sum(abs(error_reference_deg) * dt_s) / sum(dt_s)`.

La referencia incluye la evitación local en B. `error_goal_deg` se conserva en la traza para distinguir alineación con la meta directa y alineación con la referencia de navegación. El error respecto a la referencia es la métrica de control principal del prototipo; no se interpreta como prueba aislada de una mejor navegación.

`mae_active_deg` excluye los intervalos de espera ante la primera meta de C. `mae_before_switch_deg` y `mae_after_switch_deg` separan las fases; la segunda queda vacía cuando no ha habido cambio. En A y B, el campo anterior al cambio equivale al ensayo completo. Cada media se deja vacía cuando su conjunto de muestras no tiene duración; no se exporta como un error de cero.

## Llegada, tiempo y recorrido

- Éxito: distancia al centro de la meta final ≤ 0,5 m, después de un paso y **antes** de 60 s. Si ya se empieza dentro de la meta final, el éxito se registra en t = 0 sin inventar muestras.
- C: el cambio se aplica en el primer inicio de paso que alcanza 10 s. Con el paso fijado, sucede exactamente en t = 10 s. La primera llegada solo produce espera; el éxito requiere llegar a la segunda meta.
- `elapsed_s`: tiempo simulado hasta el final, incluidos los intervalos de espera. Una pausa de la interfaz no lo incrementa.
- `arrival_s`: igual al tiempo de éxito; vacío en tiempo agotado o interrupción. No reemplazar estos vacíos por 60 para calcular una media de llegadas.
- `route_m`: suma de distancias efectivamente recorridas tras resolver colisiones; no integra la velocidad solicitada cuando el movimiento está bloqueado.
- `collisions`: número de entradas en contacto con un obstáculo o pared. Un contacto persistente conserva su identidad y no suma otra colisión en cada paso. Un contacto simultáneo con dos superficies cuenta dos entradas. Los dos agentes de comparación no interactúan físicamente.

## Reorientación

Después del cambio de meta se busca el primer intervalo continuo de al menos 0,5 s en el que `abs(error_reference_deg) < 15°`. `reorientation_s` es el tiempo entre el cambio y **el comienzo** de ese intervalo, una vez confirmada su duración. Si no se completa, el campo queda vacío. No se confunde con el tiempo de llegada ni con la validación angular de meta fija del bloque 1, que utilizaba una banda de 2°.

## Coste computacional

Se utiliza `Stopwatch.GetTimestamp()` inmediatamente antes y después de `IHeadingController.Step`. Se excluyen navegación común, movimiento, CSV, interfaz y renderizado. Una instancia distinta se calienta 32 veces antes de iniciar cada controlador; la instancia del ensayo comienza con estado cero.

Se informa media aritmética y percentil 95 por interpolación lineal con posición `0.95*(n−1)` sobre las muestras ordenadas. El valor depende del equipo, compilación, reloj y entorno de ejecución: el prototipo no establece un rendimiento universal ni un ahorro energético. Las operaciones muy rápidas pueden tener resolución insuficiente; para el estudio final se recomienda medir lotes equilibrados y repetirlos fuera del editor, separando el ensayo conductual del microbenchmark.

En el piloto incluido, la medición está **desactivada**: `controller_timing_enabled=false`, campos de coste del resumen vacíos y `controller_ms=0` en las trazas como valor de almacenamiento. Esos ceros no son mediciones.

## Archivos por sesión

| Archivo | Contenido |
| --- | --- |
| `configuration.json` | Definición completa, semilla, posiciones, obstáculos y parámetros |
| `session.json` | Fecha UTC, versión, entorno, medición habilitada y papel de desarrollo |
| `summary.csv` | Una fila por controlador, con estado final y métricas |
| `trace_Proportional.csv`, `trace_Pfl3.csv` | 17 columnas de muestras; solo para los controladores ejecutados |
| `events_*.csv` | Inicio, pausa, continuación, cambio de meta y final |

La configuración completa guardada es la referencia de reproducción. La semilla ayuda a reconstruir el generador de esta versión, pero no sustituye a sus posiciones explícitas. Las sesiones con rumbo editado se identifican como `development-custom`. Los errores de escritura se muestran y el usuario puede reintentar el guardado.

