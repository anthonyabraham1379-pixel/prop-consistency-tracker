# Recursos gráficos (Graphic Assets)

Sección: **Ficha de Play Store → Recursos gráficos**. Especificaciones
oficiales + plan concreto de qué capturar.

## Obligatorios

| Recurso | Especificación | Estado |
|---|---|---|
| Icono de la app | PNG 32-bit, **512 × 512 px**, máx. 1 MB, sin transparencia en el marco final (Play aplica la máscara) | Exportar desde `assets/icon.png` a 512×512 |
| Gráfico destacado (feature graphic) | JPG o PNG 24-bit, **1024 × 500 px**, sin transparencia | **Falta crear** |
| Capturas de teléfono | Mínimo **2**, recomendado 4–8. JPG/PNG, lado entre 320 y 3840 px, relación 16:9 o 9:16 (usar 1080 × 1920 o superior) | **Faltan** |

## Opcionales (recomendados)

| Recurso | Especificación |
|---|---|
| Capturas tablet 7" | Mín. 1080 px en el lado menor, hasta 8 |
| Capturas tablet 10" | Ídem |
| Video promocional | URL de YouTube público |

## Gráfico destacado — brief de diseño

- Fondo oscuro `#08090D` (mismo del adaptive icon) con acentos
  verde/rojo/azul de la paleta de la app (`config/theme.js`).
- Texto grande: **"Tu challenge, bajo control"** (o "Prop Consistency
  Tracker"). Evitar claims tipo "pasa tu evaluación".
- Incluir un elemento visual del dashboard (donut de consistencia o
  sparkline) — puede ser un mockup, no necesita ser captura literal.
- No incluir el badge de Google Play ni precios dentro de la imagen.

## Plan de capturas de pantalla (teléfono, 1080 × 1920)

Tomarlas en un dispositivo/emulador con datos de ejemplo realistas (nunca la
app vacía). Orden sugerido — las 2 primeras son las que más se ven:

1. **Dashboard** con un challenge avanzado: % de consistencia, progreso a
   meta y drawdown visibles. Caption sugerido: *"Todas tus reglas en una
   sola pantalla"*.
2. **Calendario mensual** con días verdes/rojos. Caption: *"Tu mes de un
   vistazo"*.
3. **Registro de día** (modal de agregar día). Caption: *"Registra tu P&L en
   segundos"*.
4. **Analytics** con estadísticas. Caption: *"Conoce tus números"*.
5. **Onboarding/configuración de challenge** mostrando parámetros
   configurables. Caption: *"Cualquier prop firm, tus reglas"*.
6. **Selector multi-cuenta.** Caption: *"Todos tus challenges a la vez"*.

Consejos:
- Capturar con la barra de estado limpia (batería llena, sin notificaciones)
  o usar frames de dispositivo.
- Los captions se pueden superponer con cualquier herramienta
  (Figma/Canva); mantener tipografía y paleta consistentes con la app.
- Con datos ficticios está bien, pero que sean verosímiles (P&L diarios de
  cientos de dólares en cuenta de $100k, no millones).

## Cómo generar los datos de ejemplo

Crear un challenge "Tradeify Select 100k" (objetivo $6,000, drawdown $3,000
trailing, consistencia 40 %) y cargar ~15 días mezclando verdes y rojos, con
el mejor día alrededor del 30 % del total para que el donut se vea sano pero
interesante.
