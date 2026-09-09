# SMC · Contexto HTF / Ejecución LTF (Pine Script v6)

Segundo indicador del repositorio, independiente del lector de fases.
Archivo: [`smc-htf-ltf.pine`](./smc-htf-ltf.pine)

Hace una sola cosa: llevar el contexto de la temporalidad superior hasta una
zona de entrada concreta en la temporalidad de ejecución.

```
1H                                    5M
────────────────────────────          ──────────────────────────────
BOS / CHoCH  →  sesgo                 el precio entra en el POI
rango        →  premium/discount      barrida de liquidez
impulso      →  order block + FVG     cambio de carácter (CHoCH)
                = POI                 zona de entrada = OB/FVG del impulso
                                      retesteo con rechazo
                                      → SETUP CONFIRMADO + R:R
```

## Las seis fases

| Fase | Qué la dispara |
|---|---|
| **Esperando POI** | No hay nada activo. |
| **En POI** | El precio entra en una zona del contexto sin mitigar, del lado del sesgo y en la mitad correcta del rango. |
| **Barrida** | Dentro del POI se barre el último pivote confirmado y la vela cierra de vuelta. Aquí se **congela el extremo**: es la invalidación. |
| **Cambio de carácter** | Cierre más allá del último pivote contrario, a favor del sesgo. |
| **Zona de entrada** | Order block, FVG o 50% del impulso que hizo el CHoCH. |
| **Setup confirmado** | El precio retestea la zona y cierra rechazándola, **y** el R:R llega al mínimo. |

Si el R:R no llega, el setup se descarta con un aviso en vez de dibujarse.

## Riesgo

- **Entrada** — cierre de la vela que confirmó.
- **Invalidación** — detrás del extremo congelado de la barrida, con margen.
- **Objetivo 1** — la liquidez contraria más cercana, nunca menos de 1R.
- **Objetivo 2** — el extremo del rango del contexto, o 2R.
- **R:R** — calculado contra el objetivo 1. Es el filtro que decide si el setup
  se marca.

Si la invalidación queda por debajo del mínimo de ticks configurado, el setup se
marca igual pero con `⚠`.

## Panel

```
SMC 60 → 5      BOS
SESGO           ▲ ALCISTA
ZONA            DISCOUNT
POI VIVOS       2 demanda · 1 oferta
FASE            BARRIDA · falta CHoCH
ENTRADA/INVAL   —
R:R             —
```

## Alertas

`POI ALCANZADO` · `BARRIDA` · `CAMBIO DE CARÁCTER` · `SETUP CONFIRMADO` (y sus
variantes ▲ y ▼ para poder enrutarlas por separado) · `OBJETIVO 1` ·
`SETUP INVALIDADO`. Todas con **Once per bar close**.

## Inputs que más mueven el resultado

| Input | Por defecto | Efecto |
|---|---|---|
| Temporalidad de contexto | `60` | De dónde salen sesgo, rango y POI. |
| Pivotes del contexto | `2` | Más alto = estructura más limpia y más lenta. |
| POI mitigado cuando el precio llega a | `Toque` | `50%` o `Extremo opuesto` dejan los POI vivos más tiempo. |
| Exigir barrida / retesteo | ambos `on` | Apagarlos adelanta la señal y baja su calidad. |
| Zona de entrada desde | `Order block` | `FVG` entra más lejos; `50%` es el término medio. |
| R:R mínimo | `2.0` | El filtro duro. Súbelo y verás muchos menos setups. |
| Operar sólo a favor del sesgo | `on` | Apagarlo permite setups contra el contexto. |

## No repinta

Contexto leído con `[1]` + `lookahead_on` de la vela HTF ya cerrada, pivotes
confirmados a ambos lados, transiciones sobre vela cerrada y extremo de la
barrida congelado en su vela.

Con `Confirmar sólo con velas cerradas` activo, las marcas aparecen al cierre.

---

No es asesoría financiera. El indicador describe estructura y calcula
distancias: no envía órdenes ni garantiza resultados.
