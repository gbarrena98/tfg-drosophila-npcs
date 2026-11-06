# PEC2 — Prototipo de navegación PFL3

Estado: borrador. Pendientes las pruebas del ejecutable y la publicación.

Versión propuesta: `v0.2.0-pec2`.

Prototipo de Unity con comparación entre control proporcional y un modelo funcional publicado de 24 unidades PFL3. Incluye tres escenarios de desarrollo, configuración, pausa, repetición, resultados y exportación de configuración, métricas, trazas y eventos.

La extracción FlyWire se conserva como evidencia de procedencia; sus pesos no intervienen en el modelo funcional. Las configuraciones incluidas son de desarrollo y no constituyen la evaluación final.

## Descarga y ejecución

Descargar el asset `DrosophilaNavigationLab_v0.2.0-pec2_Windows_x64.zip`, extraer **todos** sus archivos y abrir `DrosophilaNavigationLab.exe` conservando sus carpetas y DLL contiguas. El ZIP automático de código fuente no contiene un Player.

## Validación registrada

- Núcleo: 20 comprobaciones aprobadas y piloto de desarrollo 180/180 llegadas.
- Compilación externa contra API oficial de Unity 6000.3.0f1: sin errores ni advertencias.
- **Pendiente de completar antes de publicar:** versión efectiva de Unity, fecha, sistema, commit probado y resultado de las pruebas del Player y de descarga.

## Limitaciones

Modelo reducido; evitación local sin garantía global; caso antipodal conservado; las mediciones computacionales dependen del equipo. No se afirma superioridad del controlador bioinspirado.

Créditos y licencias: `THIRD_PARTY_NOTICES.md` y `LICENCIA.md` del repositorio.
