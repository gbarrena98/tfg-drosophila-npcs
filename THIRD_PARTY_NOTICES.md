# Procedencia y avisos de terceros

## Modelo funcional PFL3

- Mussells Pires et al., «Converting an allocentric goal into an egocentric steering signal», *Nature* 626, 808–818 (2024), https://doi.org/10.1038/s41586-023-07006-3.
- Código: https://github.com/MaimonLab/Converting_allo_goal_to_ego_steering
- Revisión: `0a9d5f643d2ed63e4e02f3e3ffba9d70e19e21a2`, archivo `MATLAB/ModelLAL.m`.
- Se conserva el archivo original y el aviso **MIT**, Copyright (c) 2023 pmussp, dentro de `Research/Bloque1/third_party/`.
- `Pfl3Controller.Rate`, sus cuatro constantes y preferencias angulares adaptan esa formulación a C#. El prototipo añade el filtrado temporal, la conversión a giro y la integración en el NPC.

La autorización MIT, su atribución y su cláusula de ausencia de garantías deben conservarse en la distribución. El archivo de licencia citado forma parte de este paquete.

El proceso de compilación copia también ese aviso a la raíz del Player como `MaimonLab_MIT_LICENSE.txt`, junto con los avisos de terceros y las instrucciones de ejecución.

## Extracción anatómica de FlyWire

El material de `Research/Bloque1` incluye registros derivados de `philshiu/Drosophila_brain_model`, revisión `91bdd1e7dcf193f3e7ca5a8933497fcef63b7960`, y de `flyconnectome/flywire_annotations`, versión `v2.0.0`, revisión `327ae7bdcc18c7a6a5ac014a4bfa9c7560fe854e`.

Las citas de Dorkenwald et al., Schlegel et al. y Shiu et al., junto con fuentes, huellas y criterios de selección, se conservan en `Research/Bloque1/docs` y `data/extraction_report.json`. No se aplica una licencia nueva a los datos de terceros. Los pesos de esa extracción **no se cargan ni intervienen** en el controlador ejecutable.

## Herramientas

No se incluyen DLL del editor Unity, el SDK .NET ni paquetes propietarios. El proyecto declara tres módulos integrados de Unity y no incluye recursos de Asset Store, música, texturas o tipografías externas.

Las bibliotecas oficiales usadas para verificar la compilación se identifican mediante sus huellas en `Validation/Executed/unity_api_compile.json`, sin redistribuirlas.
