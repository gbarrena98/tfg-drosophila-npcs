# Publicar el proyecto en GitHub

El repositorio puede alojarse en una cuenta personal. Nombre propuesto: `tfg-drosophila-npcs`. Descripción: «Prototipo Unity para comparar navegación proporcional y un modelo funcional PFL3 en NPCs. TFG de Ingeniería Informática, UOC».

El enunciado proporcionado parte de un repositorio público y exige acceso del equipo docente. Por ello, la opción preparada es **Public**. Una excepción con repositorio privado debe garantizar ese acceso y ajustarse a lo indicado por el tutor.

## Primera subida con GitHub Desktop

1. En GitHub, crear un repositorio desde **New repository**. En **Owner**, elegir la cuenta personal; introducir el nombre y elegir **Public**.
2. Dejar sin marcar la creación automática de README, `.gitignore` y licencia: el proyecto ya contiene estos archivos. Crear el repositorio vacío.
3. En GitHub Desktop, iniciar sesión con esa cuenta. Utilizar **File → Clone repository**, seleccionar el repositorio nuevo y elegir una carpeta local.
4. Descomprimir este paquete en otra ubicación. Copiar **el contenido de `DrosophilaNavigationLab`** dentro de la carpeta clonada. En la raíz deben verse `Assets`, `Packages`, `ProjectSettings`, `README.md`, etc. No subir el ZIP como sustituto del proyecto y no borrar la carpeta `.git` de la copia clonada.
5. En GitHub Desktop, revisar **Changes**. No deberían aparecer `Library`, `Temp`, `bin`, `obj` ni `Builds`. Si aparecen, corregir la ubicación o las reglas antes de confirmar.
6. Escribir un resumen fiel, por ejemplo: `PEC2: añadir prototipo de navegación y validación del núcleo`. Pulsar **Commit to main** y **Push origin**.
7. Abrir el repositorio en la web. Comprobar que el README se muestra y que `Assets/Scenes/NavigationLab.unity` está disponible. Copiar su URL para la ficha del trabajo.

La raíz clonada será la carpeta que se añada a Unity Hub. Se recomienda trabajar desde ella para que cada cambio aparezca en GitHub Desktop.

Como alternativa, si ya existe un repositorio Git local, GitHub Desktop permite **File → Add local repository** y **Publish repository**. Para cuenta personal, la opción de organización es **None**; para publicación pública se desmarca **Keep this code private**. No es necesario mezclar ambos procedimientos.

## Qué se sube

| Elemento | Dónde | Motivo |
| --- | --- | --- |
| `Assets`, incluidos todos los `.meta` | Historial Git | Código, escena, shader y referencias entre recursos |
| `Packages/manifest.json` y `packages-lock.json` | Historial Git | Dependencias y resolución de paquetes |
| `ProjectSettings` | Historial Git | Versión de Unity, escena de entrada y ajustes |
| `Tools` y configuración de Git | Historial Git | Reproducción, comprobaciones y mantenimiento |
| `Docs`, README y avisos de licencia | Historial Git | Instrucciones, diseño y atribución |
| `Validation/Golden` y evidencia seleccionada de `Executed` | Historial Git | Referencias verificables y piloto acotado |
| `Research/Bloque1` | Historial Git | Procedencia científica y base numérica |
| Player completo comprimido | Asset de una release | Descarga directa para ejecutar sin el editor |
| PDF académico y vídeo | Entrega indicada por la asignatura | Documentación final y demostración; enlazar desde README si procede |

No se necesita Git LFS para los pequeños archivos de este paquete. Si en el futuro se añaden recursos grandes, habrá que revisar su tamaño antes de versionarlos.

## Qué queda fuera

El `.gitignore` preparado excluye las cachés y carpetas generadas de Unity (`Library`, `Temp`, `Obj`, `Logs`, `UserSettings`), las salidas de compilación (`Builds`, `bin`, `obj`), las capturas de memoria, carpetas locales de resultados y datos de activación. Tampoco se deben añadir credenciales, tokens, descargas completas del conectoma ni instaladores del editor.

Los CSV pequeños incluidos en `Validation/Executed` sí son evidencia seleccionada. Las sesiones de la aplicación se escriben fuera del proyecto, en el directorio de datos del usuario. Un `.gitignore` no retira archivos que ya estuvieran versionados; por eso conviene revisar el primer commit.

## Flujo de trabajo posterior

Cada cambio coherente debe quedar en un commit comprensible. Ejemplos: `corregir pausa y repetición de ensayos`, `registrar validación del Player PEC2` o `actualizar capítulo de estrategia`. Hacer **Push origin** después de confirmar cambios que deba poder consultar el tutor.

El flujo de **Actions** comprueba el núcleo C# y la estructura al subir cambios. Un resultado verde valida ese alcance: no significa que se haya abierto Unity ni que exista un ejecutable válido. Revisar las diferencias de `ProjectSettings` y `Packages` que genere la primera apertura y confirmar las que sean necesarias para reproducir el proyecto.

## Tag y release de la PEC2

Antes de publicar, ejecutar el Player real y registrar la aceptación. El enunciado pide, para cada entrega, **tag + release con ejecutable**. El archivo automático «Source code (zip)» de GitHub contiene fuentes y no cumple esa función por sí solo.

1. Compilar desde **TFG → Compilar Windows 64 bits**, probar el `.exe` y empaquetar toda la carpeta siguiendo el README.
2. Confirmar y subir el estado probado del código. Registrar en las notas la versión real de Unity, sistema de pruebas y resultado de la aceptación.
3. En la web, abrir **Releases → Draft a new release**. Crear el tag **`v0.2.0-pec2`** sobre el commit verificado de `main`.
4. Título: **PEC2 — Prototipo de navegación PFL3**. Adaptar `Release/PEC2_RELEASE_NOTES.md` eliminando únicamente los pendientes que se hayan completado.
5. Adjuntar `DrosophilaNavigationLab_v0.2.0-pec2_Windows_x64.zip` y su archivo SHA256. Publicar la release después de completar las pruebas locales del ejecutable.
6. Descargar el ZIP publicado en una carpeta distinta, comprobar su SHA256, extraerlo y abrir el `.exe`. Conservar todos sus archivos. Comprobar un ensayo y la exportación para registrar U16. Si la descarga falla, corregir la distribución antes de entregar.

Para cambios posteriores a la entrega, crear otra versión; conservar el tag entregado como referencia estable.

## Enlaces que habrá que incluir en la memoria

Después del texto descriptivo de la ficha del trabajo, incluir la URL real del repositorio y la de su release PEC2. El patrón será `https://github.com/TU_USUARIO/tfg-drosophila-npcs` y `/releases/tag/v0.2.0-pec2`, respectivamente. Sustituir `TU_USUARIO` por la cuenta utilizada y comprobar ambos enlaces después de publicar.

## Fuentes oficiales consultadas

- GitHub, «Creating a new repository»: https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-new-repository
- GitHub, «Cloning a repository from GitHub to GitHub Desktop»: https://docs.github.com/en/desktop/adding-and-cloning-repositories/cloning-a-repository-from-github-to-github-desktop
- GitHub, «Adding an existing project to GitHub using GitHub Desktop»: https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-an-existing-project-to-github-using-github-desktop
- GitHub, «Managing releases in a repository»: https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository

Consulta: 4 de noviembre de 2026. La selección de archivos y el flujo de validación anteriores corresponden a este proyecto.
