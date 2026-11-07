# Registro de validación y alcance de la evidencia

Versión: `0.2.0-pec2`. Los informes de `Validation/Executed` identifican el entorno y la versión de código de cada comprobación. El núcleo se ha verificado mediante el arnés .NET y desde Unity. Las pruebas locales de interfaz y Player se recogen en `Docs/ACEPTACION_UNITY.csv`. La release para Windows está publicada.

## Resultado verificado

| Capa | Evidencia | Resultado |
| --- | --- | --- |
| Núcleo C# | `Validation/Executed/core_checks.json` | 20 comprobaciones, cero fallos |
| Tasas del modelo | 252 casos × 24 unidades contra la referencia Python | Diferencia absoluta máxima: 7,105427357601002 × 10⁻¹⁵; tolerancia 10⁻⁹ |
| Bucle angular del bloque 1 | 3507 filas de rumbo, error y giro | Coincidencia dentro de 10⁻⁷ |
| Piloto de desarrollo | 90 configuraciones × 2 controladores | 180/180 llegadas; cero contactos en esas configuraciones |
| API de Unity | C# 9, destino .NET Standard 2.1, bibliotecas oficiales 6000.3.0f1 | Compilación sin errores ni advertencias |
| Apertura, shader y Play en Unity | `Docs/ACEPTACION_UNITY.csv` | Pruebas locales registradas como aprobadas |
| Player Windows | `Docs/ACEPTACION_UNITY.csv`, U13–U15 | Ejecución y exportación locales registradas como aprobadas |
| Distribución Windows | Release `v0.2.0-pec2`, registro U16 | ZIP completo, integridad y SHA-256 comprobados |

El informe de compilación externa identifica mediante huellas SHA256 los scripts y referencias comprobados en esa ejecución, conservados en `unity_api_compile.json`. Se usó SDK .NET 10.0.403 como compilador externo. Esa comprobación resuelve tipos y API de los scripts; no ejecuta el importador de Unity, el compilador de shaders ni el motor. Las DLL oficiales utilizadas no se incluyen en la distribución.

## Qué comprueban los 20 casos

La batería ejecuta: envoltura de ángulos, convención XZ de Unity, tasas publicadas contra Python, bucle angular contra Python, conservación del bloqueo antipodal, validez y repetición de 90 configuraciones, igualdad de información inicial entre brazos, pausa, cambio único de meta sin final prematuro en C, colisión continua, repetición determinista, interrupciones, rechazo de NaN, CSV con configuración española, llegada en los seis casos de demostración, último paso fraccionario, llegada inicial de duración cero, reconstrucción de medias desde la traza, exclusión de contactos con obstáculos no alcanzados y ausencia de falsos costes cero cuando la medición está desactivada.

Estas comprobaciones se encuentran en `Assets/Editor/ValidationChecks.cs`. Tanto el arnés .NET como el menú del editor llaman al mismo archivo y a los mismos controladores. Los valores de referencia se conservan en `Validation/Golden`, con la huella del modelo Python que los produjo.

## Piloto ejecutado

Se ejecutaron tres familias, 30 configuraciones por familia y dos controladores por configuración. Es un conjunto de **desarrollo**, usado para revisar la integración. La geometría de cada familia es sencilla y sus variantes son rotaciones y cambios de condiciones iniciales; no representa la diversidad completa de navegación.

| Escenario | Controlador | Éxitos | Mediana de llegada (s) | Mediana de error absoluto medio (°) | Contactos totales |
| --- | --- | --- | --- | --- | --- |
| A | Proporcional | 30/30 | 8,11 | 5,64 | 0 |
| A | PFL3 | 30/30 | 8,13 | 5,42 | 0 |
| B | Proporcional | 30/30 | 8,34 | 8,61 | 0 |
| B | PFL3 | 30/30 | 8,37 | 9,00 | 0 |
| C | Proporcional | 30/30 | 14,64 | 8,42 | 0 |
| C | PFL3 | 30/30 | 14,66 | 8,23 | 0 |

La mediana del error corresponde a las 30 medias temporales individuales, no a una mezcla de todas las filas de traza. No se calculan pruebas de superioridad ni se presenta este piloto como el estudio final previsto en PEC4. El coste del controlador está desactivado y no permite comparar rendimiento.

Hay trazas completas para A-00, B-00 y C-00 en `example_0`, `example_1` y `example_2`. Las figuras de `Docs/Figures` se reconstruyen a partir de esos CSV; **no son capturas de Unity**.

## Reproducir el resultado

Desde la raíz, con .NET SDK 10:

```powershell
dotnet run --project Tools/CoreHarness -- check --root .
dotnet run --project Tools/CoreHarness -- pilot --root .
python Tools/validate_repository.py
```

El segundo comando regenera resultados de desarrollo. Las métricas conductuales son deterministas en el mismo entorno; las fechas de sesión y cualquier medición de reloj habilitada no lo son. Para comparar entre plataformas se emplearán tolerancias numéricas y se anotarán las versiones, en lugar de exigir igualdad binaria de valores de punto flotante.

## Aceptación local y distribución

El registro de aceptación recoge las pruebas de importación, interfaz, pausa, repetición, configuración, exportación y ejecución del Player. El informe `Validation/Executed/unity_editor_core_checks.json` contiene las 20 comprobaciones del núcleo ejecutadas desde el editor, sin fallos.

Las pruebas locales usan Unity `6000.3.0f1` en Windows 11. En U16 se distingue la verificación de integridad del ZIP publicado de las pruebas de ejecución local del programa, recogidas en U13–U15. Esta comprobación de distribución no añade una ejecución de Windows en otro equipo.

La [release v0.2.0-pec2](https://github.com/gbarrena98/tfg-drosophila-npcs/releases/tag/v0.2.0-pec2) distribuye `Windows.zip`. Su contenido y su huella SHA-256 coinciden con el archivo publicado en GitHub.

## Limitaciones y decisiones abiertas

- Modelo PFL3 reducido y publicado: no utiliza los 17 944 recuentos sinápticos auditados de FlyWire como pesos funcionales.
- Percepción analítica del entorno y colisión 2D; la vista 3D no convierte el experimento en navegación física de un insecto.
- Evitación local sin garantía de solución para laberintos, encierros o configuraciones densas.
- Simetría antipodal conservada y ausencia de aprendizaje.
- Coste por paso sensible a resolución del reloj; pendiente de microbenchmark equilibrado en Player.
- Accesibilidad revisada en diseño mediante etiquetas, colores y atajos básicos; no se ha validado compatibilidad con lector de pantalla ni navegación completa por teclado.
- Las configuraciones finales, métricas confirmatorias y criterios estadísticos se fijarán antes de la evaluación final, con semillas reservadas.

