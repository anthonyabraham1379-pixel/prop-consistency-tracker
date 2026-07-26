# SMC Order Flow Master — NinjaTrader 8

Port institucional de `SMC Confluence Master v2` (Pine v6) a NinjaScript C#,
con la pieza que TradingView **no puede dar**: delta real comprador/vendedor
sobre todo el histórico.

---

## 1. Por qué NinjaTrader es mejor para esta estrategia

| | TradingView | NinjaTrader 8 |
|---|---|---|
| Delta (compra/venta) en backtest | Solo ~2 semanas (`request.security_lower_tf`) → tuvimos que usar el proxy CLV | **Todo el histórico**, tick a tick |
| Llenado dentro de la vela | Aproximado (`process_orders_on_close`) | Real, contra serie de 1 tick |
| Walk-Forward Optimization | No existe | **Integrado** en Strategy Analyzer |
| Monte Carlo | No | **Integrado** |
| Replay tick a tick con bid/ask | No | **Market Replay** |
| Prop firms (Apex, TopStep…) | No ejecutan | Ejecutan NT8 nativo |

El punto 1 es el importante: en TradingView el filtro de order flow era el
proxy CLV `(close-low)/(high-low)`. Aquí es **delta de verdad**.

---

## 2. Instalación

1. NinjaTrader → **New → NinjaScript Editor**
2. Clic derecho en `Strategies` → **New Strategy** → Next → ponle cualquier
   nombre → Finish
3. Borra todo el contenido del archivo generado y pega
   `SmcOrderFlowMaster.cs` completo
4. **F5** (compilar). Debe compilar sin errores.
5. Gráfico de **ES ##-##** → clic derecho → **Strategies** →
   `SmcOrderFlowMaster` → Apply

> `##-##` es la notación de NinjaTrader para el contrato frontal automático.
> Úsala también en el campo del instrumento correlacionado (`NQ ##-##`).

### Datos que necesitas descargar antes

Tools → **Historical Data** → Load:

- **Tick** del instrumento (ES) para el rango que vas a probar. Es lo que
  alimenta el motor de delta y el llenado intrabar.
- **Minute** para la serie primaria y la HTF.
- **Market Replay** si quieres validar con bid/ask real (recomendado).

El histórico de 1 tick de ES es pesado. **Prueba de 1 a 3 meses por vez**, no
el año entero de golpe.

---

## 3. Configuración inicial (réplica de tu setup validado)

Estos son los defaults del código, iguales a tu configuración de TradingView:

| Parámetro | Valor |
|---|---|
| Sesión | 0730–1400 CT, lunch 1130–1300 bloqueado |
| Sesgo TF mayor | ON, 60 min, EMA 50 |
| VWAP como filtro | OFF |
| Pivote | 4 · ATR 14 · mecha 0.5×ATR · EQ 0.25×ATR |
| SMT | ON, `NQ ##-##` |
| Volumen | OFF |
| MSS | ON, pivote menor 2 |
| FVG obligatorio | OFF |
| Ventana tras barrido | 7 velas |
| **Score mínimo** | **5** |
| SL | estructura, colchón 0.5×ATR |
| TP1 / TP2 | 1.5R / 3.0R |
| Contratos | 1 |
| Breakeven | ON a 1.5R |
| **Order flow** | **Absorción ON · ratio de delta OFF · delta MSS OFF** |

**Ajuste horario a CT**: si tu NinjaTrader está configurado en hora Central,
déjalo en `0`. Si está en hora de Nueva York, ponlo en `-1`.

### Contratos y parciales

- `Contratos = 1` → una sola posición que busca **TP1** (igual que tu Pine con
  1 contrato).
- `Contratos = 2` o más → mitad sale en **TP1**, la otra mitad corre a **TP2**
  con el stop en breakeven. Esto es lo que realmente aprovecha el runner.

---

## 4. Las tres fuentes de delta

Parámetro **Fuente del delta** (grupo `5C) Order Flow`):

**`ReglaDelTick`** (default) — clasifica cada tick por uptick/downtick.
Funciona con **cualquier** feed y todo el histórico. Correlaciona ~90% con el
delta bid/ask real. **Empieza por aquí.**

**`BidAsk`** — añade dos series de 1 tick (Ask y Bid) y clasifica cada
operación según se ejecutó contra el ask (comprador agresor) o contra el bid
(vendedor agresor). Es el estándar institucional. Requiere que tu proveedor
tenga histórico de bid/ask; triplica el peso del backtest.

**`Volumetric`** — lee `BarDelta`, `TotalBuyingVolume` y `TotalSellingVolume`
de las barras Volumetric. Lo más exacto. **Requiere licencia Lifetime o la
suscripción Order Flow+**; si no la tienes, la estrategia lo avisa en el
Output y cae a delta cero (usa otro modo).

---

## 5. Las tres confirmaciones de order flow

| Confirmación | Qué mide | Default |
|---|---|---|
| **Ratio de delta en el barrido** | `(compra-venta)/(compra+venta)` de la vela del sweep ≥ umbral | OFF (umbral 0.00) |
| **Absorción (divergencia CVD)** | El precio hace un mínimo más bajo que el swing anterior pero el delta acumulado **no** lo acompaña → el vendedor empujó sin flujo detrás | **ON** |
| **Delta en la vela del MSS** | El rompimiento de estructura viene con participación real, no con una mecha | OFF (resta, ver abajo) |

Los tres son **gates independientes**: puedes exigir solo uno, o combinarlos.

### Calibración sobre tus ticks reales de ES

Los defaults **no son inventados**. Corrí la lógica exacta de este `.cs`
(pivote 4 → sweep con mecha 0.5×ATR → MSS pivote 2 → SL estructural, TP 1.5R,
1 contrato, sesión 0730–1400 CT) sobre los ticks de ES que subiste:
**60 trades en 41 días**.

| Configuración | n | WR | PF | trades/sem |
|---|---|---|---|---|
| Base, sin order flow | 60 | 42% | **1.14** | 7.3 |
| **Absorción (divergencia CVD)** | 36 | 50% | **1.77** | 4.4 |
| Delta del sweep ≥ 0.00 | 25 | 56% | **2.48** | 3.0 |
| Delta del sweep ≥ 0.10 | 10 | 60% | 3.42 | 1.2 |
| Absorción + delta ≥ 0 (los dos) | 18 | 56% | 2.69 | 2.2 |
| Delta en el MSS ≥ 0.10 | 26 | 35% | **0.96** | 3.2 |
| Delta en el MSS ≥ 0.15 | 16 | 25% | **0.47** | 2.0 |

Prueba de estabilidad, partiendo el set en dos mitades:

| | 1ª mitad | 2ª mitad |
|---|---|---|
| Base | 0.99 | 1.29 |
| **Absorción** | **1.48** | **2.04** |
| Delta ≥ 0 | 1.91 | 2.79 |
| Absorción + delta ≥ 0 | 1.09 | 3.77 |

**Conclusiones que fijaron los defaults:**

1. **La absorción es el filtro más robusto**: positivo en las dos mitades
   (1.48 / 2.04) *y* en los dos lados (largos 1.64, cortos 1.94), y deja 4.4
   trades/semana, dentro de tu objetivo de 3–7. Por eso viene **encendida**.
2. **El umbral de delta 0.10 era demasiado agresivo** (n=10). El dato soporta
   **0.00** — solo exigir que el delta sea positivo. Bajé el default.
3. **Los dos filtros juntos son peores que cada uno solo** (1ª mitad cae a
   1.09). Por eso ahora son gates independientes: no los enciendas los dos.
4. **El delta en la vela del MSS resta** (PF 0.96 y 0.47). Entras persiguiendo
   el movimiento. Queda apagado.
5. El sesgo HTF, en esta muestra, no aportó (1.03 a favor vs 1.27 en contra).

> **Advertencia honesta:** 41 días y 60 trades base es una muestra **pequeña**.
> Esto son hipótesis calibradas, no conclusiones. El Walk-Forward de
> NinjaTrader sobre 6–12 meses es lo que las confirma o las tumba.

### Los dos modos de uso

- **Puerta dura** (`Order flow como puntos de score` = OFF): la señal se
  descarta si el flujo no confirma. Filtro de calidad, reduce trades.
- **Modo score** (= ON): el flujo suma hasta 3 puntos (score máximo 10) en vez
  de ser requisito. Sube `Score mínimo` a 7–8 si usas este modo.

---

## 6. Cómo validar (esto es lo que de verdad importa)

Ya vimos en TradingView que un PF de 3.6 en 3 semanas no sobrevive al año
completo. NinjaTrader tiene las herramientas correctas para no repetir eso:

### 6.1 Backtest de rango fijo
Strategy Analyzer → selecciona el instrumento, el rango de fechas y
`SmcOrderFlowMaster` → Run. Mira **Profit Factor, Max Drawdown, trades/semana**.

Configura antes:
- **Commission**: crea una plantilla de $2.50 por contrato y lado.
- El **Slippage** ya está fijado en 1 tick dentro del código.

### 6.2 Walk-Forward Optimization ← el paso clave
Strategy Analyzer → modo **Walk Forward**. Optimiza en un periodo, prueba en
el siguiente, y repite. Si el resultado *out-of-sample* se derrumba respecto
al *in-sample*, el sistema está sobreajustado. **Esta es la prueba que
TradingView no puede hacer y la razón principal para migrar aquí.**

Regla al optimizar: **un grupo pequeño de parámetros relacionados por vez,
con rangos cortos.** Nunca optimizar todo junto.

### 6.3 Prueba de sensibilidad
Cambia un umbral en ±1 paso (por ejemplo `Score mínimo` de 5 a 4 y a 6, o el
ratio de delta de 0.10 a 0.05 y a 0.15). Si el resultado se cae con un cambio
mínimo, el parámetro es frágil y estás sobreajustando.

### 6.4 Monte Carlo
Reordena los trades miles de veces para ver el rango real de drawdowns
posibles, no solo el que dio la secuencia histórica. Crítico para prop firms
con drawdown trailing.

### 6.5 Market Replay
Tools → Historical Data → Load → **Market Replay**. Repite el mercado tick a
tick con bid/ask real y la estrategia en modo simulado. Es lo más parecido a
operar en vivo sin arriesgar. Hazlo antes de cualquier cuenta real.

---

### 6.6 El embudo de diagnóstico

Al terminar cualquier backtest, la estrategia imprime en **New → NinjaScript
Output** el desglose de dónde murió cada setup:

```
===== SMC ORDER FLOW MASTER - EMBUDO =====
  Barridos detectados      : 412
  Confirmaron MSS          : 118
  Descartados por sesion   : 61
  Descartados por score    : 0
  Descartados por flujo    : 24
  Descartados por riesgo   : 3
  Descartados por lim.dia  : 0
  ENTRADAS                 : 30
==========================================
```

Esto responde en un vistazo *por qué* opera poco o mucho. Ojo con una cosa que
salió en el análisis: con `Score mínimo = 5` y VWAP/volumen apagados, **el score
puede no estar filtrando nada** (los filtros apagados no penalizan, así que el
score siempre llega a 5). Si ves `Descartados por score: 0`, es eso — el sistema
está funcionando solo con sesión + MSS + flujo. No es un error, pero conviene
saberlo antes de subir el mínimo a 6.

El mismo embudo aparece resumido en el panel del gráfico.

---

## 7. Control de riesgo para prop firm

Grupo `8D) Limite diario`:

- **Stop de pérdida diaria (USD)** — cierra lo abierto y bloquea el resto del
  día. `0` = apagado.
- **Objetivo diario (USD)** — para la **regla de consistencia**: evita que un
  solo día concentre demasiado del beneficio total. `0` = apagado.
- **Máximo de trades por día** — control de sobreoperación. `0` = apagado.

Los tres están apagados por defecto para no alterar el sistema validado.
Actívalos de uno en uno y mide.

---

## 8. Notas técnicas

- `Calculate = OnBarClose` está fijado a propósito: es el equivalente de
  `barstate.isconfirmed`. **No lo cambies a OnEachTick**, romperías la
  garantía de no repintado del sesgo HTF.
- Como el script usa `AddDataSeries()`, el **Order Fill Resolution** del
  Strategy Analyzer queda inhabilitado por diseño de NinjaTrader. Por eso
  todas las órdenes se envían a la serie de 1 tick (`EnterLong(IdxTick, …)`),
  que es la forma documentada de obtener llenados intrabar en scripts
  multi-serie.
- El breakeven se evalúa **tick a tick**, no al cierre de vela, para que el
  disparo sea exacto.
- El VWAP de sesión se calcula dentro del script (no usa `OrderFlowVWAP`), así
  que no depende de ninguna licencia.
- Si desactivas SMT, la estrategia carga una serie de datos menos y el
  backtest va notablemente más rápido.
- `IsInstantiatedOnEachOptimizationIteration` está en **true** a propósito. La
  estrategia guarda mucho estado (pivotes, CVD, setups, contadores); si se
  reutilizara el objeto entre iteraciones, ese estado se arrastraría y
  contaminaría el Walk-Forward. Cuesta algo de velocidad, pero los números son
  correctos.
- La **absorción solo se evalúa contra un swing de la misma sesión**. El CVD se
  reinicia en cada sesión, así que comparar el de hoy contra el de ayer no
  significa nada. Sobre tus ticks eso afecta al ~1% de los barridos.
- El **breakeven usa el precio medio real de la posición** (`Position.AveragePrice`),
  no el cierre de la vela de la señal. La entrada es a mercado y se llena en el
  tick siguiente: si usaras el cierre, el "breakeven" no sería breakeven.

---

## 9. Orden sugerido de trabajo

1. Compilar, aplicar en ES 1m, ver que el panel muestra delta y CVD vivos.
   Revisa el **embudo** en la ventana Output (sección 6.6) antes que nada.
2. Backtest de 1 mes con los defaults → anotar PF, DD, trades/semana.
3. Repetir con `Exigir absorcion` en OFF → comparar. Así mides exactamente
   cuánto aporta el flujo real (en tus ticks fue PF 1.14 → 1.77).
4. A/B contra la otra opción: absorción OFF y `Exigir delta del sweep a favor`
   ON con umbral 0.00 (en tus ticks dio PF 2.48 con menos trades). Uno por vez,
   nunca los dos juntos.
5. Walk-Forward sobre 6–12 meses.
6. Monte Carlo.
7. Market Replay de 2 semanas.
8. Recién entonces, simulada en vivo.
