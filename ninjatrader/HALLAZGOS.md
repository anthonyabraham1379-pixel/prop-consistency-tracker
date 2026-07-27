# Qué funciona y qué no — evidencia acumulada

Resumen de todo lo medido sobre ES: **año completo de velas 1m** (253 días,
326.011 velas) y **57 días de ticks reales** con volumen comprador/vendedor.

---

## 1. Veredicto por estrategia

| Concepto | Resultado | Veredicto |
|---|---|---|
| SMC barrido→MSS en **1m** | PF 0.88, DD 26.120 | ❌ Sin edge |
| Score (HTF + SMT + EQH/EQL) | PF 0.83–0.90; subir el mínimo empeora | ❌ No aporta |
| BOS de continuación | Pierde | ❌ |
| ORB (ruptura de apertura) | PF 0.88–1.03 | ❌ |
| Reversión a VWAP 2SD | PF medio **1.00** (rango 0.69–1.27 según la hora de inicio) | ❌ Era sobreajuste |
| Order flow por proxy CLV | 49.7% de acierto en la vela del barrido | ❌ Ruido |
| Delta real en el barrido | Apunta al **revés** de lo asumido; t = −2.11 pero inestable | ⚠️ Débil |
| DOM / Level 2 | No backtesteable + desajuste de horizonte | ❌ No vale la pena |
| **SMC barrido→MSS en 30m** | **PF 1.22, DD 2.751, walk-forward 1.18 / 1.26** | ⚠️ **Prometedor** |

---

## 2. Por qué el DOM no vale la pena

**Prueba directa** — poder predictivo del delta real de trades sobre ES,
18.720 minutos de RTH:

| Horizonte | Correlación | Acierta el signo |
|---|---|---|
| 1 min | −0.0094 | 49.4% |
| 5 min | −0.0054 | 50.3% |
| 15 min | +0.0011 | 49.9% |
| 30 min | −0.0034 | 49.9% |
| 60 min | −0.0065 | 49.6% |

Cero poder predictivo a **todos** los horizontes que operamos. 50% es una
moneda al aire.

**Contexto académico:** Cont, Kukanov y Stoikov (*The Price Impact of Order
Book Events*) muestran que el desequilibrio del libro predice precio **en
decenas de segundos**. Nuestra estrategia aguanta ~50 minutos: hay un
desajuste de horizonte de unas 100 veces.

**Además, en NinjaTrader:**
- `OnMarketDepth()` **no se dispara en backtest**. NinjaTrader no guarda el
  Level 2 en su base de datos.
- La única forma de probarlo es Market Replay, y primero hay que **grabar el
  DOM en vivo durante meses**.
- Spoofing e icebergs hacen que la liquidez mostrada no sea la real.

**Conclusión:** aunque el DOM tuviera alfa, vive en un horizonte 100× más
corto que el nuestro, y no se puede validar sin gastar meses grabando.

---

## 3. El hallazgo nuevo: la temporalidad

El mismo sistema SMC, sin cambiar un solo parámetro, en distintas
temporalidades:

| TF | n | WR | PnL | PF | DD | MFE med | MAE med |
|---|---|---|---|---|---|---|---|
| 1m | 502 | 38% | −19.911 | 0.88 | 26.120 | 0.80R | **1.03R** |
| 3m | 232 | 39% | −27.652 | 0.77 | 32.614 | 0.74R | 1.01R |
| 5m | 162 | 43% | +9.710 | 1.11 | 13.025 | 0.69R | 1.00R |
| 15m | 76 | 45% | +2.323 | 1.07 | 9.566 | 0.63R | 0.60R |
| **30m** | **46** | **43%** | **+4.003** | **1.22** | **2.751** | **0.47R** | **0.50R** |

El dato que explica todo es la última columna. En 1m el precio va **más en
contra que a favor** (MAE 1.03R > MFE 0.80R): eso es ruido puro. A medida que
sube la temporalidad la relación se equilibra y el drawdown se desploma de
26.120 a 2.751.

**Walk-forward:**

| TF | 1ª mitad | 2ª mitad | Meses positivos |
|---|---|---|---|
| 5m | 0.81 | 1.36 | 5/13 |
| 15m | 0.64 | 1.75 | 5/13 |
| **30m** | **1.18** | **1.26** | **7/13** |

Solo el de 30m es positivo en **las dos mitades** y tiene mayoría de meses
positivos, sin que un solo mes cargue el resultado.

**Advertencia:** n = 46 en un año (menos de 1 trade por semana). Con 23 trades
por mitad **no se puede establecer un edge**. Es una hipótesis para validar
hacia delante, no un sistema validado.

---

## 4. El delta apunta al revés

Test condicional en la vela del barrido (n = 369, horizonte 30 min, retorno
normalizado por ATR):

| | n | Retorno futuro medio |
|---|---|---|
| Delta **a favor** del trade | 148 | **−0.863 ATR** |
| Delta **en contra** del trade | 221 | **+0.168 ATR** |

t = −2.11. Los barridos con flujo agresivo **a favor** funcionan peor. Es la
tesis clásica de la absorción: quieres que el flujo agresivo vaya **contra** ti
(stops saltando contra un pasivo que absorbe), no contigo.

Pero no es sólido: por mitades t = −1.66 y −1.31; por lados, largos t = −0.91
y cortos t = −2.18. Y contradice el backtest de 41 días. **Tratar como
hipótesis, no como hecho.**

---

## 5. Qué hacer

1. **Descartar el DOM.** No es cuestión de esfuerzo: el horizonte no cuadra y
   no se puede validar.
2. **Dejar el 1 minuto.** Está medido: en 1m el precio va más en contra que a
   favor. No es un problema de filtros, es ruido estructural.
3. **Probar el sistema en 30m** con delta real en NinjaTrader. Es lo único que
   aguantó walk-forward, y encima con drawdown de 2.751 — muy compatible con
   una prop firm.
4. **Invertir el filtro de order flow** al probarlo (exigir delta *en contra*,
   no a favor), midiendo el A/B.
5. **No llevar nada a real** hasta tener walk-forward propio en NinjaTrader
   sobre 6–12 meses con delta real.

---

## 6. AMD / Power of Three (Acumulación → Manipulación → Distribución)

Hipótesis **distinta** a lo anterior: en vez de barrer pivotes locales sin
horario, se barre el **rango de una sesión** en una **ventana de tiempo
concreta** (el *judas swing*).

Estructura probada: rango de acumulación → el precio sale de él entre las
09:30 y 10:30 ET y cierra de vuelta dentro → se entra al lado contrario,
stop tras el extremo, objetivo 2R.

### Rejilla completa (5m, 16 combinaciones)

| Rango de acumulación | Mejor PF | Peor PF | ¿Todas positivas? |
|---|---|---|---|
| Asia 19–23 ET | 0.93 | 0.90 | ❌ ninguna |
| Londres 02–08:30 | 1.20 | 1.00 | ~ |
| Overnight 18–09:30 | 1.56 | 1.09 | ✅ |
| **Premarket 04–09:30** | **1.36** | **1.22** | ✅ |

### Sensibilidad a parámetros — la mejor de toda la investigación

Moviendo los bordes del rango de acumulación (12 variantes):
**PF entre 1.17 y 1.44, todas positivas.** Para comparar, la reversión a VWAP
oscilaba entre 0.69 y 1.27 haciendo lo mismo.

Moviendo el múltiplo de R: 1.11 / 1.13 / 1.31 / 1.23 / 1.33 / 1.41. Todas
positivas y creciendo de forma ordenada.

### Y aun así, no pasa

| Prueba | Resultado |
|---|---|
| Walk-forward (mitades) | **0.75 / 1.88** ❌ |
| Meses positivos | 6/13 ❌ |
| Total del año | +15.855 |
| **Quitando los 2 mejores meses** | **−4.105** ❌ |
| Febrero 2026 solo | +11.358 (**71% del total**) |

Parecía haber un filtro de régimen: por terciles de volatilidad previa
(ATR de 20 días, sin mirar al futuro) daba **0.48 / 1.59 / 1.92**, monotónico
y con sentido mecánico. Pero al quitar los dos mejores meses del tercil alto,
**1.92 baja a 1.07**. El "régimen" eran los meses.

**Control:** el mismo corte de volatilidad sobre el SMC de 1m da 0.87 → 0.97.
La volatilidad alta ayuda un poco a todo, pero no convierte un sistema
perdedor en ganador. Confirma que el salto de AMD no era el régimen.

**Veredicto:** el concepto AMD es el **más robusto a la elección de
parámetros** de todo lo probado — eso es real y no lo consigue ningún otro.
Pero no tiene edge demostrable sobre el año: un mes carga el 71% del
resultado.

---

## 7. La conclusión de fondo

Probado sobre un año de ES: barrido→MSS (en 1m, 3m, 5m, 15m y 30m), BOS de
continuación, ORB, reversión a VWAP, order flow por proxy, order flow con
delta real, DOM y AMD en 16 configuraciones.

**Todo aterriza en PF ≈ 1.0 con inestabilidad temporal.**

Eso no es un fallo del método — es la respuesta. El ES intradía es de los
mercados más eficientes que existen, y **el edge marginal de una estructura
mecánica de entrada es aproximadamente cero**.

Lo cual tiene una consecuencia que encaja con este mismo proyecto: si la
señal de entrada no es donde está el margen, entonces está en la **gestión**
— tamaño de posición, límite diario, consistencia, no romper las reglas de la
prop firm. Que es exactamente lo que hace la app de este repositorio.

---

## 8. Por que en TradingView si y en NinjaTrader no

La pregunta mas util de todo el proyecto. Medida, no razonada.

### Los sospechosos habituales no explican nada

Misma logica SMC, mismo instrumento, mismo ano, cambiando SOLO los supuestos
de ejecucion:

| Supuesto | TradingView | NinjaTrader | Diferencia |
|---|---|---|---|
| Entrada en cierre vs apertura siguiente | PF 0.95 | PF 0.95 | 0.00 |
| Intrabar optimista vs pesimista | PF 0.92 | PF 0.92 | 0.00 |
| Slippage 1 tick vs 0 | 0.95 -> 1.00 | — | pequena |

`process_orders_on_close=true` es el sospechoso clasico y aqui **no vale
nada**. Conviene saberlo para no perder tiempo persiguiendolo.

### Lo que si lo explica

Barrido del objetivo, misma logica, 5 minutos, ano completo:

```
TP 1.0R  PF 0.93     TP 2.5R  PF 0.95
TP 1.5R  PF 1.11     TP 3.0R  PF 0.99
TP 2.0R  PF 1.01
```

Media 1.00 sin estructura. **Cuando hay ventaja real esa curva tiene forma.**

Mes a mes del "mejor" caso (5m, TP 1.5R, PF 1.11):

```
2025-09  -4,626     2026-02   +4,713
2025-10  -1,594     2026-03  +14,840  <<<
2026-01  -1,886     2026-04   -6,321
2026-06  -2,823     2026-07     -420

meses positivos: 7 de 13     total +9,259
quitando los 2 mejores meses: -10,294 USD
```

Marzo de 2026 aporta mas que la ganancia de todo el ano.

**Conclusion: TradingView no miente en la ejecucion. Ensena una ventana que
contiene el tramo bueno.** Sobre el ano entero el resultado se promedia a
PF 1.00. Dos implementaciones independientes (el port de NinjaTrader y el
backtester de Python) coinciden en ~1.0; la que se sale de la fila es TV.

### El protocolo que hay que aplicar SIEMPRE antes de creerse un backtest

1. **Barrido del parametro principal.** Si el PF salta sin estructura
   alrededor de 1.0, es ruido con un ganador de loteria.
2. **Mes a mes.** Contar meses positivos y quitar los dos mejores.
3. **Quitar las N mejores operaciones.** Si el resultado se desmorona
   quitando 5, no hay sistema.
4. **Tercios cronologicos.** Los tres tienen que ser positivos.

### El unico candidato que aprueba las cuatro

Esqueleto de precio de InstitutionalOrderFlow (sweep sobre pools de 5m,
nivel institucional, mecha de rechazo, contexto de 15m, objetivo 3R), 272
operaciones, PF 1.45:

```
quitando las 5 mejores operaciones : +13,056 (de +19,504)
quitando las 10 mejores            :  +7,545
operacion mas grande               : 7.6% del total

primer tercio  PF 1.72 | segundo tercio PF 1.37 | ultimo tercio PF 1.32

objetivo 2.0R PF 1.50   3.0R PF 1.45   4.0R PF 1.55
objetivo 2.5R PF 1.47   3.5R PF 1.49   5.0R PF 1.54
```

Sensibilidad **plana** entre 2R y 5R, tres tercios positivos, no depende de
ninguna operacion. Es lo contrario del perfil del SMC.

Aviso que sigue vigente: el backtester de Python corre sistematicamente
optimista frente a NinjaTrader (dijo 1.27 donde se midio 0.87; 1.31 donde se
midio 1.04). Lo esperable en plataforma es **1.10-1.25**, no 1.45.
