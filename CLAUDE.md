# CLAUDE.md — Contexto del proyecto

## Proyecto
Prop Consistency Tracker — app móvil para traders de prop firms. Ver PRD.md
para el detalle completo del producto.

## Referencia de diseño
Antes de construir cualquier pantalla, lee `design-reference/app-mockup.jsx`.
Es un mockup en React web (no React Native) de las 3 pantallas principales:
onboarding, dashboard y configuración. Úsalo como referencia de:
- Paleta de colores y estilo visual (fondo oscuro, acentos verde/rojo/azul)
- Estructura y jerarquía de cada pantalla
- Componentes reutilizables (MetricCard, SegmentedControl, NumField, etc.)

Está en React web a propósito — tradúcelo a componentes de React Native
(`<div>` → `<View>`, `<input>` → `<TextInput>`, `style={{}}` inline →
StyleSheet.create, etc.). No lo copies literal, adapta el mismo diseño y
estructura al stack real del proyecto.

## Stack
- React Native + Expo (managed workflow)
- Navegación: React Navigation (stack + bottom tabs)
- Estado: React Context + useReducer (sin Redux, mantener simple)
- Almacenamiento local: AsyncStorage para V1 (migrar a expo-sqlite si el
  volumen de datos por usuario crece)
- Pagos: react-native-purchases (RevenueCat SDK)
- Estilo: StyleSheet nativo, sin librerías de UI pesadas. Paleta oscura
  (ver config/theme.js)

## Convenciones
- Componentes funcionales con hooks, sin clases
- Un componente por archivo, PascalCase
- Toda la lógica de cálculo de consistencia/drawdown vive en
  utils/calculations.js — no duplicar esa lógica en las pantallas
- Textos de UI en español (mercado objetivo inicial), pero centralizados en
  config/strings.js para poder internacionalizar después
- No hardcodear parámetros de ninguna prop firm específica — todo debe venir
  de la configuración del usuario (ver config/defaultParams.js como plantilla)

## Lo que la app NUNCA debe hacer
- No dar señales de trading ni recomendaciones de entrada/salida
- No conectarse a brokers ni ejecutar órdenes
- No prometer ni implicar que usar la app garantiza pasar una evaluación

## Prioridad de build (orden sugerido de sesiones con Claude Code)
1. Setup del proyecto Expo + navegación básica
2. Modelo de datos local (challenges + días) con AsyncStorage
3. Pantalla de onboarding: crear challenge con parámetros configurables
4. Pantalla dashboard (usar utils/calculations.js)
5. Pantalla de historial + editar/borrar día
6. Selector multi-cuenta
7. Integración RevenueCat + paywall
8. Pulido visual + iconos + splash screen
9. Build con EAS + subida a Google Play Console (internal testing track primero)

## Comandos útiles
```
npx create-expo-app . --template blank
npx expo install react-native-screens react-native-safe-area-context
npx expo install @react-navigation/native @react-navigation/native-stack
npx expo install @react-native-async-storage/async-storage
eas build --platform android --profile preview
```
