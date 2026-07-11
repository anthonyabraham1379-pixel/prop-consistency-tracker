# PRD — Prop Consistency Tracker

## Qué es
App móvil (Android primero, iOS después) para traders que operan cuentas de
prop firms (Tradeify, FTMO, Apex, TopStep, etc.). Registra el P&L diario y
calcula en tiempo real si el trader cumple con las reglas de su evaluación:
- Regla de consistencia (% máximo que un solo día puede representar del total)
- Drawdown máximo (estático o trailing)
- Objetivo de profit
- Días mínimos rentables

## Quién la usa
Traders retail en evaluación o cuenta fondeada de cualquier prop firm.
No se limita a Tradeify — los parámetros son configurables porque cada
firm tiene reglas distintas.

## Qué NO hace (alcance explícito)
- No ejecuta operaciones ni se conecta al bróker/plataforma de trading
- No da señales de entrada/salida ni recomendaciones de trading dentro del producto
- No garantiza aprobación de evaluaciones — es una herramienta de seguimiento,
  no asesoría financiera
- V1 no incluye sincronización automática con brokers (registro manual)

## Parámetros configurables por el usuario
Al crear una cuenta/challenge dentro de la app, el usuario define:
| Parámetro | Ejemplo |
|---|---|
| Nombre de la cuenta/challenge | "Tradeify Select 100k" |
| Tamaño de cuenta | $100,000 |
| Objetivo de profit | $6,000 |
| Drawdown máximo | $3,000 |
| Tipo de drawdown | Estático / Trailing |
| Regla de consistencia | 40% / 30% / Ninguna |
| Días mínimos rentables | 3 |
| Meta diaria sugerida (opcional, calculada o manual) | $1,200 |
| Moneda | USD (fijo en V1) |

El usuario puede tener **múltiples cuentas/challenges** activos a la vez
(ej. una personal y una de evaluación), cada una con sus propios parámetros
y su propio historial.

## Funcionalidad core (V1)
1. Onboarding: crear cuenta/challenge con parámetros propios
2. Registrar día (ganancia/pérdida) — input simple, sin fricción
3. Dashboard: % de consistencia actual, progreso a meta, drawdown usado,
   días rentables vs. mínimo requerido
4. Historial de días con opción de editar/borrar
5. Alertas locales: aviso cuando un día se acerca a romper el % de consistencia
6. Multi-cuenta: selector para cambiar entre challenges activos

## Modelo de negocio
- Suscripción mensual $3.99 USD vía Google Play Billing (RevenueCat como capa de gestión)
- Free tier sugerido: 1 cuenta/challenge activa, historial limitado a 30 días
- Premium ($3.99/mes): cuentas ilimitadas, historial ilimitado, exportar a CSV,
  alertas configurables

## Stack técnico
- React Native + Expo (Android primero, iOS con el mismo código después)
- Almacenamiento local: AsyncStorage / SQLite (expo-sqlite) — no requiere backend para V1
- Pagos: RevenueCat + Google Play Billing
- Sin backend propio en V1 (reduce costo y complejidad inicial). Backend
  (Supabase) se agrega en V2 si se necesita sync entre dispositivos.

## Riesgos a vigilar
- Políticas de Google Play para apps de finanzas/trading: revisar la sección
  "Financial Services" de las políticas de contenido antes de publicar —
  puede requerir disclaimers visibles de que no es asesoría financiera
- No prometer resultados ("pasa tu evaluación") en el copy de la tienda —
  Google es estricto con claims financieros
