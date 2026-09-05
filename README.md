# Prop Consistency Tracker — starter

Este es el punto de partida: PRD, contexto para Claude Code, la lógica de
cálculo (la parte que más importa que esté bien) y la configuración base.
Todavía **no es una app corriendo** — faltan las pantallas y la navegación,
que se construyen con Claude Code paso a paso.

## Qué hay aquí
- `PRD.md` — especificación del producto
- `CLAUDE.md` — contexto que Claude Code lee automáticamente (incluye la
  referencia al mockup de diseño, ya enlazada)
- `design-reference/app-mockup.jsx` — mockup visual de las 3 pantallas
  principales (onboarding, dashboard, configuración). Claude Code lo lee
  solo, no necesitas pegarlo en ningún prompt.
- `utils/calculations.js` — toda la lógica de consistencia/drawdown/progreso,
  ya escrita y lista para usar (no depende de ninguna prop firm específica)
- `config/defaultParams.js` — estructura de un challenge configurable + presets
- `config/theme.js` — paleta de colores
- `tradingview/` — indicador de lectura estructural para TradingView
  (Pine Script v6, ES/MES). Es una herramienta aparte de la app: no forma
  parte del build de Expo. Ver `tradingview/README.md`.

## Cómo continuar (en tu computadora, no aquí en el chat)

1. **Instala Node.js** (si no lo tienes): https://nodejs.org

2. **Instala Claude Code**
   ```
   npm install -g @anthropic-ai/claude-code
   ```

3. **Copia esta carpeta a tu computadora** y entra en ella:
   ```
   cd prop-consistency-app
   claude
   ```

4. **Primer prompt sugerido para Claude Code** (cópialo tal cual, no necesitas
   pegar ningún código — Claude Code ya tiene acceso a todos los archivos
   de la carpeta):
   > Lee PRD.md, CLAUDE.md y design-reference/app-mockup.jsx. Inicializa un
   > proyecto Expo en esta carpeta (sin sobrescribir utils/, config/ ni
   > design-reference/), con navegación básica (stack + bottom tabs) y una
   > pantalla de onboarding que use la estructura de config/defaultParams.js
   > para crear un nuevo challenge, siguiendo el estilo visual del mockup.

5. **Sigue construyendo pantalla por pantalla** (ver el orden sugerido en
   CLAUDE.md, sección "Prioridad de build"). Pide una cosa a la vez y prueba
   en tu teléfono con la app Expo Go antes de seguir.

## Para publicar en Google Play (cuando la app esté lista)

1. Crea cuenta en Google Play Console ($25 pago único):
   https://play.google.com/console

2. Configura RevenueCat (gratis hasta cierto volumen de ingresos):
   https://www.revenuecat.com — conecta tu producto de suscripción de
   Google Play Billing ($3.99/mes) desde ahí.

3. Compila con EAS:
   ```
   npm install -g eas-cli
   eas build --platform android --profile preview
   ```

4. Sube primero a **Internal Testing** en Play Console antes de producción —
   te permite probar el flujo de pago real sin exponerlo públicamente.

5. Antes de publicar, revisa la política de Google para apps financieras:
   https://support.google.com/googleplay/android-developer/answer/9876821
   Necesitarás un disclaimer visible de que la app es una herramienta de
   seguimiento y no asesoría financiera ni garantía de resultados.

## Nota
No soy asesor legal ni financiero — los pasos de cumplimiento con Google
Play y las políticas de suscripciones conviene revisarlos directamente en
la documentación oficial antes de publicar, ya que cambian con el tiempo.
