# Repositorio y versiones

El proyecto fuente está publicado en [https://github.com/gbarrena98/tfg-drosophila-npcs](https://github.com/gbarrena98/tfg-drosophila-npcs).

## Versión PEC2

| Elemento | Referencia |
| --- | --- |
| Versión del núcleo | `0.2.0-pec2` |
| Etiqueta de la entrega | [`v0.2.0-pec2`](https://github.com/gbarrena98/tfg-drosophila-npcs/tree/v0.2.0-pec2) |
| Release | [PEC2 — Prototipo de navegación PFL3](https://github.com/gbarrena98/tfg-drosophila-npcs/releases/tag/v0.2.0-pec2) |
| Aplicación para Windows de 64 bits | [Windows.zip](https://github.com/gbarrena98/tfg-drosophila-npcs/releases/download/v0.2.0-pec2/Windows.zip) |
| Editor utilizado | Unity `6000.3.0f1` |

El ZIP de Windows contiene el ejecutable, `UnityPlayer.dll`, la carpeta `_Data` y los archivos de soporte. Los archivos automáticos de código fuente de GitHub corresponden al proyecto editable. La rama `main` puede incorporar revisiones documentales posteriores a la etiqueta publicada.

SHA-256 del ZIP publicado:

```text
2fc375af6ced32ffaa69d2a7659de8cb2ec7cb86de3b7b1402897b774aa982c0
```

## Organización del repositorio

| Elemento | Contenido |
| --- | --- |
| `Assets` y sus archivos `.meta` | Código, escena, shader y referencias de Unity |
| `Packages` y `ProjectSettings` | Dependencias, versión del editor y configuración del proyecto |
| `Tools` | Comprobación del núcleo, integridad y empaquetado |
| `Validation/Golden` | Referencias numéricas de Python utilizadas por las comprobaciones C# |
| `Validation/Executed` | Informes de comprobación y registros del piloto de desarrollo |
| `Research/Bloque1` | Datos seleccionados, procedencia científica y modelo de referencia |
| `Docs` | Funcionamiento, métricas y registro de aceptación |

El `.gitignore` excluye cachés de Unity, archivos temporales, compilaciones locales y datos de activación. Las sesiones de navegación se guardan en el directorio persistente de la aplicación, fuera del repositorio.

## Comprobaciones automáticas

El flujo `core-checks.yml` ejecuta las comprobaciones del núcleo C# y de la estructura del proyecto. Las pruebas locales del editor y del Player se documentan por separado en `Docs/ACEPTACION_UNITY.csv`. El alcance de cada evidencia se describe en [VALIDACION.md](VALIDACION.md).
