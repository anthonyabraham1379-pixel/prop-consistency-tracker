# SMC Confluence Master v2 — Indicador para TradingView

Indicador de confluencias SMC (Smart Money Concepts) para ES/NQ en 15m y 1H,
escrito en **Pine Script v6** (la versión actual de TradingView).
Sigue el modelo de entrada en 3 pasos: **barrido de liquidez → MSS/CHoCH →
retorno al FVG**, filtrado por sesión, sesgo del TF mayor, VWAP, volumen y SMT.

## Instalación

1. Abre TradingView → **Editor Pine** (panel inferior).
2. Pega el contenido completo de `SMC_Confluence_Master.pine`.
3. Pulsa **Añadir al gráfico**.
4. Para alertas: **Crear alerta** → condición: *SMC Confluence Master v2* →
   **Cualquier función alert()**. El mensaje incluye entrada, SL, TP1 y TP2.

## Cómo se lee una señal

La etiqueta COMPRA/VENTA solo aparece cuando se cumple la secuencia completa
**al cierre de vela** (sin repintado):

1. **Barrido** — el precio toma un swing (o EQH/EQL) con mecha de rechazo y
   cierra de vuelta dentro. Triángulo rojo/verde en el gráfico.
2. **MSS (CHoCH)** — tras el barrido, el precio rompe la estructura menor en
   dirección contraria (etiqueta "MSS"). Esto separa un sweep real de una
   simple continuación de tendencia.
3. **Retorno al FVG** — el desplazamiento del MSS deja un FVG; la entrada es
   cuando el precio vuelve a esa zona (caja verde/roja).
4. Todo dentro de tu ventana horaria y con **score de confluencia ≥ mínimo**.

### Score (máx. 7)

| Componente | Puntos |
|---|---|
| Barrido válido (base) | 2 |
| Sesgo del TF mayor a favor (EMA 50 de vela HTF **cerrada**) | +1 |
| Precio del lado correcto del VWAP de sesión | +1 |
| Volumen alto en el barrido (≥ 1.3× media) | +1 |
| SMT: el símbolo correlacionado NO barrió su nivel | +1 |
| El nivel barrido era EQH/EQL (pool de liquidez doble) | +1 bonus |

Los filtros apagados no penalizan. Por defecto se exige score ≥ 5.

## Cambios de la v2 respecto a la v1

### Añadido

- **Confirmación MSS/CHoCH obligatoria (configurable)**. La v1 podía dar señal
  con solo barrido + score; ese es el fallo clásico de los indicadores de
  sweep: muchos barridos son continuación, no reversión. El modelo estándar
  (ICT 2022) exige la ruptura de estructura antes de entrar.
- **Barrido en 2 velas**: la vela A rompe y cierra fuera del nivel, la vela B
  cierra de vuelta dentro. La v1 solo detectaba el sweep de 1 vela con mecha
  y se perdía la mitad de los barridos reales.
- **Punto de score por EQH/EQL**: barrer highs/lows iguales (doble pool de
  liquidez) es objetivamente más fuerte; antes solo se mencionaba en la
  etiqueta sin afectar al score.
- **Sesgo HTF sin repintado**: la v1 leía la EMA del TF mayor con la vela HTF
  aún abierta, así que el sesgo (y por tanto la señal) podía cambiar a mitad
  de vela. Ahora usa la última vela HTF **cerrada** (idiom `[1]` +
  `lookahead_on`), estable en tiempo real.
- **Líneas de E/SL/TP en el gráfico** además de la etiqueta.
- **FVG dinámico**: la caja activa se extiende hasta que el precio la
  atraviesa; entonces queda gris (invalidada) en vez de desaparecer.
- **Toggle de volumen** (antes penalizaba siempre, sin poder apagarse) y
  guardas para símbolos sin volumen (`nz`).
- **Panel ampliado**: estado de datos SMT (aviso si el símbolo correlacionado
  no devuelve datos), aviso si el TF del gráfico ≥ TF mayor, progreso del
  setup (MSS✔/FVG…) y modo activo.
- **Limpieza de estado**: el setup expira de verdad al agotar la ventana
  (antes las variables quedaban colgadas y el panel podía mostrar
  información obsoleta).

### Descartado a propósito

- **No se añadieron osciladores** (RSI, MACD, estocástico…): son redundantes
  con VWAP + EMA HTF y degradan la claridad del modelo. Más filtros ≠ mejores
  señales; el valor está en la secuencia sweep→MSS→FVG bien medida.
- **No se generan señales "solo barrido"** por defecto: puedes recuperar el
  comportamiento de la v1 desactivando "Exigir MSS" y "Exigir retorno a FVG",
  pero no se recomienda.
- **No se añadieron order blocks**: con FVG como zona de entrada cumplen el
  mismo papel y duplicarían cajas en el gráfico.

## Ajustes recomendados

| Parámetro | 5m | 15m | 1H |
|---|---|---|---|
| Longitud de pivote | 5 | 5 | 4–5 |
| Pivote menor (MSS) | 3 | 3 | 2–3 |
| Ventana tras barrido | 20 | 15 | 10 |
| Score mínimo | 5 | 5 | 4–5 |

- Si operas **NQ**, cambia el símbolo correlacionado a `CME_MINI:ES1!`.
- Si el panel muestra **"SIN DATOS ⚠"** en SMT, tu plan de TradingView no
  tiene datos en tiempo real de ese símbolo: apaga SMT o usa un proxy con
  datos disponibles para no perder ese punto de score sistemáticamente.
- En cuentas de prop firm: el indicador da E/SL/TP, pero el tamaño de la
  posición debe salir de tu límite de riesgo diario, no del indicador.

## Aviso

Este indicador es una herramienta de análisis. No es asesoramiento
financiero ni garantiza resultados; opera siempre con gestión de riesgo.
