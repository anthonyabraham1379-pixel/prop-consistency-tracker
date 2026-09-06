# Lector Estructural · Fases (Pine Script v6)

Indicador de **lectura estructural** para TradingView, para futuros intradía en
gráficos de 1 y 5 minutos. Neutral de instrumento: todos los umbrales se miden
en ticks sobre `syminfo.mintick`, con perfiles listos para ES/MES y NQ/MNQ.

Archivo: [`lector-estructural.pine`](./lector-estructural.pine)

---

## Qué hace y qué no hace

**Hace:** describe en qué fase está la estructura del precio y si el contexto
acompaña. Muestra el proceso completo:

```
nivel relevante → barrida / rechazo → ruptura estructural → retesteo
                → fallo del retesteo → ESTRUCTURA COMPLETA
```

**No hace:** no es una estrategia (`indicator()`, nunca `strategy()`), no usa
`strategy.entry` ni `strategy.exit`, no genera órdenes, no dice comprar ni
vender, no marca *long* ni *short*, no sugiere entradas ni salidas y no calcula
tamaño de posición. Las referencias de riesgo que dibuja son distancias
geométricas de la propia estructura, no instrucciones operativas.

Las dos secuencias que sigue se llaman **BARRIDA SUPERIOR** y **BARRIDA
INFERIOR**: describen de qué lado se barrió la liquidez, no una dirección
operativa.

## Instalación

1. TradingView → **Pine Editor** → *Abrir* → *Nuevo indicador*.
2. Borra la plantilla y pega el contenido de `lector-estructural.pine`.
3. **Guardar** → **Añadir al gráfico**.
4. Recomendado: gráfico de 1 o 5 minutos de `ES1!`, `MES1!`, `NQ1!` o `MNQ1!`,
   con la sesión extendida activada (el indicador necesita ver el overnight para
   calcular su máximo y mínimo).

## Perfil de instrumento

El grupo **0 · Perfil de instrumento** decide los valores iniciales de los
cuatro umbrales sensibles al producto:

| Perfil | Tolerancia | Desplazamiento | Ancho de zona | Distancia mínima de invalidación |
|---|---|---|---|---|
| **ES/MES** | 4 ticks | 12 ticks | 12 ticks | 4,75 puntos |
| **NQ/MNQ** | 6 ticks (rango útil 4–8) | 16 ticks | 16 ticks (rango útil 12–20) | 8 puntos |
| **Manual** | los del grupo 4 | los del grupo 4 | los del grupo 4 | el del grupo 5 |
| **Auto** | detecta por `syminfo.root` | | | |

`Auto` reconoce las raíces `ES`, `MES`, `NQ` y `MNQ`. Si la raíz es otra, está
vacía o no se puede leer, cae al perfil **Manual** — nunca falla ni bloquea el
indicador. El panel muestra siempre el perfil activo, la raíz detectada y los
cuatro umbrales que se están aplicando, así que no hay ambigüedad.

Para usar tus propios valores en cualquier instrumento: pon el perfil en
**Manual** y ajusta los inputs de los grupos 4 y 5. El resto del motor es
independiente del producto porque todo se mide en ticks.

---

## Lectura visual

El gráfico habla con **glifos y color**, no con frases. Cada marca lleva el
texto completo en el **tooltip**: pasa el ratón por encima y ves la descripción
entera sin saturar el gráfico.

| Glifo | Fase |
|:--:|---|
| `•` | Nivel alcanzado |
| `◇` | Posible barrida |
| `◆` | Rechazo detectado |
| `▬` | Ruptura estructural |
| `⋯` | Esperando retesteo |
| `◎` | Retesteo en observación |
| `●` | Estructura completa |
| `★` | Estructura completa con contexto alineado (A+) |
| `✕` | Estructura invalidada · línea de invalidación |
| `◐` | Referencia 1R alcanzada |
| `⚠` | Invalidación dentro del ruido · aviso de repetición |
| `‖` | Pausa sugerida |

Las líneas de referencia se etiquetan `✕ 12.50`, `●`, `1R` y `2R`; el resto va
en el tooltip.

### Panel compacto

Cinco filas, una palabra por celda y una barra de progreso de la secuencia:

```
◧ ESTRUCTURA   SUPERIOR             INFERIOR
FASE           ●●●○○  ▬ RUPTURA     ●○○○○  • NIVEL
CONTEXTO       ✓ ALINEADO           ~ MIXTO
INVALIDACIÓN   6.25                 —
AVISO          ‖ PAUSA              —
```

Los puntos `●●●○○` marcan cuántas de las cinco fases se han completado. Cada
celda tiene tooltip con el texto largo (fase completa, qué falta, distancia en
puntos y ticks, aviso íntegro, perfil y umbrales activos).

Con **Modo del panel → Detallado** vuelve la tabla larga de diez filas, con todo
escrito. Con **Etiquetas como símbolo → off**, las marcas del gráfico vuelven a
mostrar el texto completo.

## Los 9 estados visuales

| Estado | Cuándo aparece | Color |
|---|---|---|
| **SIN SECUENCIA** | No hay ninguna secuencia activa en ese lado. | Gris |
| **NIVEL ALCANZADO** | El precio llega a un nivel de referencia dentro de la tolerancia, sin superarlo. | Gris |
| **POSIBLE BARRIDA** | El precio supera el nivel y **cierra** de vuelta dentro de la zona. | Amarillo |
| **RECHAZO DETECTADO** | Cierre del lado correcto del nivel con una mecha ≥ al mínimo configurado. | Amarillo |
| **RUPTURA ESTRUCTURAL** | Cierre más allá del extremo del pullback previo a la barrida. Se dibuja la caja de la zona rota, acotada al ancho máximo configurado. | Azul |
| **ESPERANDO RETESTEO** | Tras la ruptura, el precio se desplazó al menos los ticks mínimos configurados y todavía no ha vuelto. | Azul |
| **RETESTEO EN OBSERVACIÓN** | El precio regresó a la zona de ruptura, en una vela posterior a la que confirmó el desplazamiento. Aún no hay confirmación. | Azul |
| **ESTRUCTURA COMPLETA** | El retesteo falló: la vela cierra fuera de la zona con cuerpo o mecha de rechazo. Verde si el contexto está alineado, azul si no. | Verde / Azul |
| **ESTRUCTURA INVALIDADA** | El precio cerró más allá del extremo **congelado** de la barrida. La secuencia se reinicia por completo. | Gris apagado |

Cuando la secuencia está a medias, la fila **Estado** del panel dice qué falta:

- `ESTRUCTURA INCOMPLETA · FALTA BARRIDA O RECHAZO`
- `ESTRUCTURA INCOMPLETA · FALTA RUPTURA`
- `ESTRUCTURA INCOMPLETA · FALTA RETESTEO`
- `ESTRUCTURA INCOMPLETA · FALTA RECHAZO DEL RETESTEO`

## Clasificación de la estructura

| Clasificación | Significado |
|---|---|
| **ESTRUCTURA A+** | Secuencia completa **y** todos los filtros de contexto activos alineados. |
| **ESTRUCTURA CONFIRMADA** | Secuencia completa, pero con contexto mixto o contrario. |
| **ESTRUCTURA ANTICIPADA** | Hubo barrida o rechazo, pero falta ruptura o retesteo. Nunca genera alerta de estructura completa. |

## Contexto

- **CONTEXTO ALINEADO** — todos los filtros activos apuntan al mismo lado.
- **CONTEXTO MIXTO** — unos sí y otros no.
- **CONTEXTO CONTRARIO** — ninguno acompaña.

Filtros que entran en el cálculo (cada uno se puede desactivar): EMA local,
VWAP de sesión, EMA del timeframe superior, secuencia de máximos y mínimos del
timeframe superior, y espacio libre hasta el siguiente nivel.

## Referencias de invalidación

Al completarse una estructura se dibujan, sólo como geometría:

- **INVALIDACIÓN ESTRUCTURAL** — detrás del extremo congelado de la barrida
  (nunca dentro del pullback), con la distancia en puntos y en ticks.
- **REFERENCIA ESTRUCTURAL** — el cierre de la vela que confirmó el fallo del
  retesteo.
- **REFERENCIA 1R** y **REFERENCIA 2R** — la misma distancia proyectada 1 y 2
  veces.

Si la distancia de invalidación es menor que el mínimo del perfil activo
(4,75 puntos en ES/MES, 8 en NQ/MNQ) aparece **INVALIDACIÓN DEMASIADO CERCANA
AL RUIDO**.

## Control de repetición

Avisos neutrales, nunca bloquean nada:

- `ESTRUCTURA REPETIDA EN EL MISMO NIVEL` — se completó otra estructura en un
  nivel ya usado.
- `SEGUNDA FALLA EN LA MISMA ZONA` — la segunda estructura completa invalidada
  en la misma zona.
- `PAUSA Y ESPERAR NUEVA ESTRUCTURA` — se alcanzó el número de fallas
  configurado.
- `NO HAY NUEVA CONFIRMACIÓN ESTRUCTURAL` — la estructura anterior ya llegó a
  1R y todavía no hay una secuencia completa nueva.

Los avisos se reinician cuando arranca una secuencia en **otro** nivel (fuera
del margen de «mismo nivel») o, si está activado, en la apertura de la sesión.

---

## Inputs

### 0 · Perfil de instrumento

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Perfil de instrumento | `Auto` | Auto / ES/MES / NQ/MNQ / Manual. Fija los valores iniciales de tolerancia, desplazamiento, ancho de zona y distancia mínima de invalidación. Auto detecta por `syminfo.root` y cae a Manual si no reconoce la raíz. |

### 1 · Sesión y contexto temporal

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Zona horaria | `America/New_York` | Referencia horaria de las sesiones. |
| Sesión regular (RTH) | `0930-1600` | Ventana de la sesión de Nueva York; pinta el fondo y marca el inicio del Opening Range. |
| Sesión overnight | `1800-0930` | Ventana con la que se calculan el máximo y mínimo overnight. |
| Timeframe de contexto (HTF) | `15` | Temporalidad de la que se leen EMA y estructura de contexto. |
| Opening Range (minutos) | `15` | Minutos desde la apertura que forman el rango de apertura. |
| Analizar sólo dentro de la sesión regular | `off` | Si se activa, la máquina de estados sólo corre en RTH. |
| Reiniciar secuencias y avisos en cada apertura | `on` | Empieza el día limpio: sin secuencias en curso ni contadores de fallas. |

### 2 · Niveles de referencia

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Máximo / mínimo overnight | `on` | Activa `ON H` / `ON L` como niveles y los dibuja. |
| Máximo / mínimo del día previo | `on` | Activa `PDH` / `PDL`. |
| Opening Range | `on` | Activa `OR H` / `OR L`. |
| Pivotes intradía confirmados | `on` | Activa `PIV H` / `PIV L`. |
| Sensibilidad de pivote · izquierda | `3` | Velas a la izquierda para validar un pivote. |
| Sensibilidad de pivote · derecha | `3` | Velas a la derecha. Es el retraso de confirmación: más alto = pivotes más fiables y más tardíos. |
| Etiquetar los niveles en la última vela | `on` | Muestra el nombre de cada nivel a la derecha del gráfico. |

Desactivar un grupo lo quita del gráfico **y** de la detección de barridas.

### 3 · Filtros de contexto

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Filtro EMA local | `on` / `20` | El precio debe estar del lado correcto de la EMA del gráfico. |
| Filtro VWAP de sesión | `on` | El precio debe estar del lado correcto del VWAP. |
| Filtro EMA del timeframe superior | `on` / `20` | El cierre HTF debe estar del lado correcto de su EMA. |
| Filtro secuencia de máximos/mínimos HTF | `on` | Pide máximos y mínimos descendentes (o ascendentes) en el HTF. |
| · Sensibilidad de pivote HTF | `2` | Pivotes usados para leer esa secuencia. |
| Filtro espacio libre hasta el próximo nivel | `off` | Penaliza estructuras con otro nivel demasiado cerca por delante. |
| · Espacio mínimo | `1.5` | Múltiplo de la distancia mínima de invalidación que debe quedar libre. |

### 4 · Detección estructural

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Tolerancia de barrida / retesteo (ticks) | `4` | Cuánto puede pasarse el precio de un nivel y seguir contando como barrida, y qué tan «pegado» debe estar el retesteo. Sólo se aplica con el perfil Manual. |
| Mecha mínima de rechazo | `0.50` | Proporción del rango de la vela que debe ser mecha para considerar rechazo. |
| Desplazamiento mínimo tras la ruptura (ticks) | `12` | El precio debe alejarse esto de la zona rota antes de que un regreso cuente como retesteo. Filtra rupturas sin impulso. Sólo se aplica con el perfil Manual. |
| Ancho máximo de zona de retesteo (ticks) | `12` | Techo del grosor de la zona de ruptura. Impide que una vela de desplazamiento enorme convierta todo su rango en zona válida. Sólo se aplica con el perfil Manual. |
| Ventana de la estructura del pullback (velas) | `20` | Respaldo para localizar el extremo del pullback cuando no hay pivote confirmado válido. |
| Velas máximas por fase antes de expirar | `30` | Si una fase se estanca, la secuencia expira en vez de quedarse colgada. |
| Confirmar sólo con velas cerradas | `on` | **Anti-repintado.** Desactívalo sólo si sabes lo que haces. |

### 5 · Referencias de invalidación

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Distancia mínima de invalidación (puntos) | `4.75` | Por debajo de esto se avisa que la invalidación queda dentro del ruido. Sólo se aplica con el perfil Manual. |
| Dibujar referencias 1R y 2R | `on` | Muestra las proyecciones geométricas. |
| Longitud de las líneas de referencia (velas) | `25` | Cuánto se extienden hacia la derecha. |

### 6 · Control de repetición

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Fallas en la misma zona antes de avisar pausa | `2` | Cuántas estructuras completas invalidadas en la misma zona disparan el aviso de pausa. |
| Margen para considerar «mismo nivel» (puntos) | `2.0` | Dos niveles dentro de este margen se tratan como el mismo. |

### 7 · Visual

| Input | Por defecto | Para qué sirve |
|---|---|---|
| Dibujar zonas | `on` | Cajas de barrida y de ruptura/retesteo. |
| Dibujar etiquetas de fase | `on` | Marcas de fase en el gráfico. |
| Mostrar panel de estado | `on` | Tabla con el estado de las dos secuencias. |
| Modo del panel | `Compacto` | `Compacto` (5 filas con glifos y tooltips) o `Detallado` (10 filas con todo el texto). |
| Etiquetas como símbolo | `on` | `on`: un glifo por marca y el detalle en el tooltip. `off`: texto completo en el gráfico. |
| Estructuras recientes a mantener en pantalla | `3` | Controla el número de cajas, líneas y etiquetas vivas para no agotar el límite de objetos. |
| Colores | verde / azul / amarillo / gris | Verde = completa y alineada · azul = completa sin alineación total o fase intermedia · amarillo = fase intermedia y advertencias · gris = niveles e invalidada. |

---

## Alertas

Se crean con `alertcondition`, así que se eligen desde el diálogo de alertas de
TradingView. Ninguna usa lenguaje operativo:

`NIVEL ALCANZADO` · `POSIBLE BARRIDA` · `RECHAZO DETECTADO` ·
`RUPTURA ESTRUCTURAL` · `ESPERANDO RETESTEO` · `RETESTEO EN OBSERVACIÓN` ·
`ESTRUCTURA COMPLETA` · `ESTRUCTURA A+` · `ESTRUCTURA INVALIDADA` ·
`INVALIDACIÓN DEMASIADO CERCANA` · `PAUSA Y ESPERAR NUEVA ESTRUCTURA`

Configúralas con la opción **Once per bar close**: todas las condiciones se
evalúan sobre vela cerrada.

## Cómo se evita el repintado

- Las transiciones de fase se procesan con `barstate.isconfirmed`.
- Los pivotes son `ta.pivothigh` / `ta.pivotlow` confirmados; aparecen con el
  retraso de las velas configuradas a la derecha, que es el precio de no
  repintar.
- El máximo y mínimo del día previo y el contexto HTF se piden con offset `[1]`
  y `lookahead_on`, es decir, siempre desde una vela ya cerrada.
- El máximo y mínimo overnight se congelan en la apertura de la sesión regular.

Consecuencia esperada: las etiquetas aparecen **al cierre** de la vela que
confirma cada fase, no durante su formación.

## Ajuste rápido

**ES / MES**
- **1 minuto:** tolerancia 4–8 ticks, desplazamiento 12–16 ticks, zona de
  retesteo 8–12 ticks, pivotes 3/3.
- **5 minutos:** tolerancia 4 ticks, desplazamiento 12 ticks, zona de retesteo
  12–16 ticks, pivotes 2/2 o 3/3.

**NQ / MNQ** (más ruido por tick, umbrales más anchos)
- **1 minuto:** tolerancia 6–8 ticks, desplazamiento 16–20 ticks, zona de
  retesteo 16–20 ticks, pivotes 3/3.
- **5 minutos:** tolerancia 4–6 ticks, desplazamiento 16 ticks, zona de retesteo
  12–16 ticks, pivotes 2/2 o 3/3.
- Si aparecen demasiadas secuencias, sube la sensibilidad de pivote o desactiva
  el grupo de niveles `PIV H` / `PIV L`.
- Si las estructuras llegan tarde, baja la sensibilidad de pivote derecha
  (menos retraso de confirmación) o el desplazamiento mínimo.

## Detalles del motor estructural

- **El extremo de la barrida queda congelado** en la vela donde se detecta.
  No se recalcula con máximos o mínimos posteriores, así que el punto de
  invalidación y la distancia de invalidación no se mueven durante la
  secuencia.
- **El rechazo exige contacto real con el nivel**: el extremo de la vela debe
  caer dentro de la banda `nivel ± tolerancia` por ambos lados. Una mecha
  grande lejos del nivel ya no cuenta.
- **La barrida tiene prioridad sobre el rechazo** cuando ambos podrían aplicar,
  y también frente a un rechazo en otro nivel.
- **Sólo cuentan como falla** las estructuras que llegaron a completarse y
  después se invalidaron. Una fase intermedia que expira o se invalida no suma
  al contador de repeticiones.

## Límites conocidos

- El extremo del pullback que debe romperse se fija en el momento de la barrida
  (último pivote confirmado válido; si no hay, el extremo de la ventana). Es una
  definición objetiva, pero no siempre coincidirá con la que tú trazarías a mano.
- Sólo se sigue una secuencia activa por lado. Una barrida nueva mientras hay
  otra en curso no abre una segunda secuencia; se ignora hasta que la actual se
  complete, se invalide o expire.
- Una barrida detectada sin haber pasado antes por `NIVEL ALCANZADO` arranca la
  secuencia directamente en la fase de barrida: la misma vela que barre el nivel
  es también la que lo alcanza.
- El indicador describe estructura; no mide probabilidad ni resultado.

---

Este indicador es una herramienta de lectura y disciplina. No es asesoría
financiera y no garantiza ningún resultado.
