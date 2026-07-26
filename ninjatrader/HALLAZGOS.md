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
