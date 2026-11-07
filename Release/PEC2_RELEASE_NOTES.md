# PEC2 — Prototipo de navegación PFL3

Versión publicada: [`v0.2.0-pec2`](https://github.com/gbarrena98/tfg-drosophila-npcs/releases/tag/v0.2.0-pec2). Unity `6000.3.0f1`. Windows de 64 bits.

El prototipo compara control proporcional y un modelo funcional publicado de 24 unidades PFL3. Incluye espacio abierto, obstáculos y cambio de meta, configuración, pausa, repetición, resultados y exportación de métricas, trazas y eventos.

La auditoría FlyWire se conserva como evidencia anatómica independiente. Sus recuentos de sinapsis no se utilizan como pesos del controlador. Las configuraciones incluidas pertenecen al piloto de desarrollo.

## Descarga y ejecución

El archivo [Windows.zip](https://github.com/gbarrena98/tfg-drosophila-npcs/releases/download/v0.2.0-pec2/Windows.zip) contiene el Player completo. Para ejecutarlo, extraer todos sus archivos y abrir `DrosophilaNavigationLab.exe`, conservando sus carpetas y DLL.

SHA-256:

```text
2fc375af6ced32ffaa69d2a7659de8cb2ec7cb86de3b7b1402897b774aa982c0
```

## Validación registrada

- Núcleo C#: 20 comprobaciones aprobadas, también ejecutadas desde el editor.
- Piloto de desarrollo: 180 ejecuciones con llegada a la meta final.
- Compilación externa contra la API de Unity: sin errores ni advertencias en la versión identificada por el informe.
- Editor y Player: pruebas locales recogidas en `Docs/ACEPTACION_UNITY.csv`.
- Distribución: contenido e integridad del ZIP publicado comprobados.

## Limitaciones

La evitación es local y no garantiza una solución general. Se conserva la limitación antipodal del modelo PFL3. La evaluación final utilizará configuraciones reservadas y mediciones de rendimiento específicas.

Los créditos y licencias se conservan en `THIRD_PARTY_NOTICES.md` y `LICENCIA.md`.
