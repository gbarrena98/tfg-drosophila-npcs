# Referencia Python y extracción de conectividad

Esta carpeta contiene el modelo Python de referencia, la extracción de conectividad y los resultados de la validación angular inicial. Incluye dos trabajos separados:

1. **Auditoría de conectividad:** selección de 195 neuronas de FlyWire v783, 4.414 pares dirigidos y 17.944 sinapsis contabilizadas en la tabla derivada publicada por Shiu et al.
2. **Referencia funcional:** reimplementación de la ecuación y los parámetros de PFL3 de Mussells Pires et al., con un filtro y una conversión a giro añadidos y documentados. Esta simulación NO utiliza la lista extraída como matriz de pesos.

El banco Python mantiene una meta fija y simula únicamente el ángulo. La aplicación Unity está en la raíz del repositorio; sus pruebas de navegación se documentan en `Docs/VALIDACION.md`.

## Ejecutar la validación angular

Instalar Python 3.10 o posterior, descomprimir el paquete y abrir una terminal en su carpeta. Ejecutar:

```bash
python src/validate_reference.py
```

No requiere instalar bibliotecas adicionales. Repite los siete ángulos iniciales, el barrido de señales, las metas rotadas, los casos límite y la sensibilidad temporal y de ganancia. Escribe CSV e informe JSON en `results/`. El informe debe mostrar ocho comprobaciones verdaderas. Una de ellas verifica precisamente que los dos casos antipodales exactos con meta a 0° se conservan como fallos de establecimiento.

Para conservar los resultados incluidos al generar otros:

```bash
python src/validate_reference.py --output mis_resultados
```

Los resultados son deterministas y no utilizan semillas ni entrenamiento. Pequeñas diferencias de redondeo entre plataformas son posibles. El banco nominal usa pasos de 0,02 s, horizonte de 10 s, filtro de 0,10 s, ganancia de giro 1 y límite de 120 grados/s. Sus parámetros son de piloto, no la configuración final congelada de navegación.

## Repetir la extracción desde los archivos originales

Esta ruta es opcional para inspeccionar los resultados ya incluidos. Las dependencias fijadas de extracción y gráficos se han probado con Python 3.12; se recomienda usar esa versión para estas dos rutas. El banco angular anterior sí puede ejecutarse por separado desde Python 3.10. Se necesita conexión y aproximadamente 125 MB de descarga, además de memoria para procesar la tabla completa. Instalar las dependencias de extracción:

```bash
python -m pip install -r requirements_extraction.txt
python src/extract_connectivity.py --download
```

El programa conserva las descargas en `raw/`. Sus enlaces incluyen commits fijos y se verifica SHA-256 antes de procesarlas. Una huella distinta detiene el procedimiento. Si los archivos ya están descargados:

```bash
python src/extract_connectivity.py --raw ruta_a_los_archivos
```

Se incluyen la selección completa y los conteos, pero no las dos tablas completas. El campo `hemibrain_type` es una etiqueta de tipo en la anotación de FlyWire, no un identificador hemibrain. Los root_id de 18 dígitos deben tratarse como texto o enteros de 64 bits; no abrirlos en una hoja de cálculo con conversión numérica automática. `source_model_sign` conserva un signo del modelo de Shiu, distinto del conteo de sinapsis. `side` no se utiliza como lateralidad de salida PFL3.

## Regenerar las figuras

```bash
python -m pip install -r requirements_figures.txt
python src/make_figures.py
```

Se crean tres figuras en PNG y SVG. El SVG permite editar los elementos vectoriales; los valores proceden de las tablas y simulaciones incluidas.

## Organización

| Carpeta o archivo | Contenido |
| --- | --- |
| `docs/TFG_PEC2_Bloque1.md` | Estado del arte y descripción de la validación inicial. |
| `docs/fuentes_verificadas.csv` y `.json` | Registro de 21 referencias con procedencia, alcance y enlaces. |
| `docs/registro_busqueda.csv` | Consultas y familias de fuentes utilizadas. |
| `docs/referencias.bib` | Referencias importables en gestores bibliográficos. |
| `data/selected_neurons.csv` | 195 registros de neuronas, con identificadores exactos. |
| `data/selected_edges.csv` | 4.414 pares con conteos y signo de la fuente separados. |
| `data/connectivity_by_type.csv` | Número de pares y sinapsis por combinación de tipos. |
| `data/extraction_report.json` | Criterios, versiones, huellas, cobertura y entorno de extracción. |
| `data/reference_parameters.json` | Parámetros publicados y añadidos, separados por procedencia. |
| `results/pilot_summary.csv` | Resumen de los siete errores para PFL3 y la referencia proporcional. |
| `results/pilot_traces.csv` | Series temporales nominales completas. |
| `results/static_sweep.csv` | Las 1.260 combinaciones estáticas. |
| `results/rotated_goals.csv` | Las 252 pruebas con distintas metas absolutas. |
| `results/edge_cases.csv` | Casos límite, incluidos los fallos de ±180°. |
| `results/sensitivity.csv` | Las 54 combinaciones de sensibilidad. |
| `results/validation_report.json` | Configuración, comprobaciones y huellas del código de validación. |
| `src/` | Extracción, modelo, validación y gráficos. |
| `third_party/` | ModelLAL.m original y licencia MIT de MaimonLab. |

## Procedencia y alcance

La formulación y los parámetros de referencia proceden de Mussells Pires et al., Nature 626, 808-818 (2024), DOI 10.1038/s41586-023-07006-3, y de su repositorio MaimonLab/Converting_allo_goal_to_ego_steering, revisión 0a9d5f643d2ed63e4e02f3e3ffba9d70e19e21a2. Se conserva el archivo MATLAB original junto con su licencia MIT.

La conectividad procede de philshiu/Drosophila_brain_model, revisión 91bdd1e7dcf193f3e7ca5a8933497fcef63b7960; las anotaciones, de flyconnectome/flywire_annotations v2.0.0, revisión 327ae7bdcc18c7a6a5ac014a4bfa9c7560fe854e. Deben citarse también Dorkenwald et al. y Schlegel et al., Nature (2024), para el recurso FlyWire y sus anotaciones. Los registros derivados mantienen atribución y procedencia. No se asigna en este paquete una licencia nueva a los componentes de terceros.
