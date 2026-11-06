# Drosophila Navigation Lab

Prototipo de navegación de NPCs desarrollado para el TFG de Germán Barrena Alonso, del Grado de Ingeniería Informática de la UOC.

El proyecto compara dos formas de orientar un agente hacia una meta: un controlador proporcional y una adaptación de un modelo de 24 unidades PFL3 de *Drosophila melanogaster*. La aplicación permite observar sus recorridos y exportar los resultados de cada ensayo.

## Abrir el proyecto

La versión de referencia es **Unity 6000.3.0f1**, con el renderizador integrado (Built-in).

1. Clonar el repositorio o descomprimir el proyecto.
2. En Unity Hub, seleccionar **Add → Add project from disk** y añadir la carpeta que contiene `Assets`, `Packages` y `ProjectSettings`.
3. Esperar a que termine la importación y abrir **TFG → Abrir laboratorio**. La escena también está en `Assets/Scenes/NavigationLab.unity`.
4. Abrir la pestaña **Game**, pulsar **Play** y entrar en **Configurar ensayo**.

Para una primera prueba, seleccionar **A → Comparación → A-00**. Se recomienda una ventana de 1440 × 900 o mayor. Si se usa otra versión de Unity, registrar el cambio y repetir las comprobaciones.

## Escenarios y controles

| Escenario | Descripción |
| --- | --- |
| A — Espacio abierto | El agente debe alcanzar una meta fija. |
| B — Obstáculos | La misma tarea con tres obstáculos circulares y evitación local. |
| C — Cambio de meta | La meta cambia a los 10 segundos de simulación. Si el agente llega antes a la primera, espera hasta el cambio. |

Cada escenario incluye 30 configuraciones. Se puede ejecutar un controlador por separado o comparar ambos. En la comparación, los agentes parten de las mismas condiciones y se simulan de forma independiente, sin colisionar entre sí.

- **Enter:** iniciar desde la configuración.
- **Espacio / Esc:** pausar o continuar.
- **R:** repetir desde los resultados.
- **Detener y guardar:** interrumpir el ensayo conservando sus datos.
- **1× / 4×:** cambiar la velocidad de presentación. El paso de simulación se mantiene en 0,02 s.

El botón **Caso 180°** permite observar el bloqueo del modelo PFL3 cuando la referencia está exactamente opuesta al rumbo.

## Resultados

Al terminar un ensayo se guardan estos archivos:

- `configuration.json`: escenario y parámetros utilizados.
- `session.json`: información de la sesión.
- `summary.csv`: métricas de cada controlador.
- `trace_*.csv`: muestras de la trayectoria y del estado del agente.
- `events_*.csv`: eventos del ensayo.

El botón **Abrir carpeta CSV** muestra la carpeta de la sesión. Los archivos se guardan en `Application.persistentDataPath/Trials`, fuera de la instalación. En Windows, la ruta habitual es:

```text
%USERPROFILE%\AppData\LocalLow\German Barrena Alonso\Drosophila Navigation Lab\Trials
```

Los CSV usan coma como separador y punto decimal. Para abrirlos en Excel con configuración española, utilizar **Datos → Desde texto/CSV**. La definición de cada métrica está en [Docs/METRICAS.md](Docs/METRICAS.md).

## Comprobaciones

El menú **TFG → Ejecutar comprobaciones del nucleo** ejecuta las pruebas del núcleo C# y guarda el informe en `Validation/Executed/unity_editor_core_checks.json`.

También se pueden ejecutar sin Unity, con **.NET SDK 10**, desde la raíz del proyecto:

```powershell
dotnet run --project Tools/CoreHarness -- check --root .
```

Los informes incluidos recogen 20 comprobaciones del núcleo, un piloto de 180 ejecuciones y la compilación de los scripts contra la API de Unity. **La apertura en el editor y las pruebas del ejecutable siguen pendientes** y se registran en [Docs/ACEPTACION_UNITY.csv](Docs/ACEPTACION_UNITY.csv). El piloto utiliza configuraciones de desarrollo; la evaluación final del TFG se realizará por separado.

El detalle de las pruebas y sus límites está en [Docs/VALIDACION.md](Docs/VALIDACION.md).

## Compilar para Windows

1. Salir del modo Play y seleccionar **TFG → Compilar Windows 64 bits**. La instalación de Unity debe disponer del soporte de compilación para Windows.
2. Abrir `Builds/Windows/DrosophilaNavigationLab.exe` y comprobar su funcionamiento fuera del editor.
3. Tras completar las pruebas del ejecutable, empaquetarlo con Python 3:

```powershell
python Tools/package_release.py --player-verified
```

El comando genera en `Release` un ZIP y su archivo SHA256. El ZIP incluye toda la carpeta del ejecutable: son necesarios el `.exe`, `UnityPlayer.dll`, la carpeta `_Data` y los demás archivos generados.

Los pasos para subir las fuentes y publicar la release están en [Docs/GITHUB.md](Docs/GITHUB.md). Este repositorio contiene el proyecto fuente; el ejecutable debe adjuntarse a la release correspondiente.

## Organización

| Carpeta | Contenido |
| --- | --- |
| `Assets/Scripts/Core` | Controladores, simulación y exportación, sin dependencias de Unity. |
| `Assets/Scripts/Runtime` | Interfaz y representación del escenario. |
| `Assets/Editor` | Menús de apertura, comprobación y compilación. |
| `Assets/Scenes` y `Assets/Resources` | Escena, shader y configuraciones. |
| `Tools` | Herramientas de comprobación y empaquetado. |
| `Validation` | Referencias numéricas e informes de pruebas. |
| `Research/Bloque1` | Modelo Python, extracción de conectividad y documentación científica. |
| `Docs` | Diseño, métricas y guías de ejecución. |

## Modelo y fuentes

El controlador PFL3 adapta el modelo publicado en [Converting an allocentric goal into an egocentric steering signal](https://doi.org/10.1038/s41586-023-07006-3), *Nature* 626, 808–818 (2024), y el [código de MaimonLab](https://github.com/MaimonLab/Converting_allo_goal_to_ego_steering). El filtrado temporal, la conversión a giro y la evitación de obstáculos forman parte de la adaptación del prototipo.

La extracción de FlyWire se conserva en `Research/Bloque1` para documentar el circuito estudiado. Sus recuentos de sinapsis no se utilizan como pesos del controlador. El movimiento se calcula sobre un plano, con colisiones geométricas 2D y una representación visual 3D. La evitación es local y el modelo conserva la limitación del caso de 180°.

Las atribuciones y condiciones de los componentes externos están en [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). La licencia de la aportación original se recoge en [LICENCIA.md](LICENCIA.md).
