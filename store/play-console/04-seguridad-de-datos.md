# Formulario de Seguridad de datos (Data Safety)

Sección: **Contenido de la app → Seguridad de datos**. Estas respuestas
reflejan lo que la app hace hoy: datos locales en AsyncStorage; si el
usuario inicia sesión con Google, sync a Supabase; AdMob en el tier
gratuito; RevenueCat para suscripciones.

> Si más adelante quitas AdMob, agregas analytics o cambias el login,
> este formulario DEBE actualizarse — las inconsistencias entre el
> formulario y el comportamiento real de la app son causa de suspensión.

## Preguntas generales

| Pregunta | Respuesta |
|---|---|
| ¿Tu app recopila o comparte alguno de los tipos de datos requeridos? | **Sí** |
| ¿Todos los datos del usuario se cifran en tránsito? | **Sí** (Supabase, RevenueCat y AdMob usan HTTPS/TLS) |
| ¿Ofreces una forma de solicitar la eliminación de los datos? | **Sí** |
| URL de eliminación de cuenta/datos | `https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/delete-account.html` |

## Tipos de datos a declarar

### 1. Información personal → Dirección de correo electrónico
*(Google Sign-In vía Supabase Auth)*

| Campo | Respuesta |
|---|---|
| ¿Se recopila? | Sí |
| ¿Se comparte? | No (Supabase actúa como proveedor de servicio) |
| ¿Se procesa de forma efímera? | No |
| ¿Es opcional u obligatoria? | **Opcional** (la app funciona sin iniciar sesión) |
| Finalidades | Funcionalidad de la app, Gestión de la cuenta |

### 2. Información financiera → Otra información financiera
*(P&L diario y parámetros de challenges que el usuario ingresa manualmente,
sincronizados a Supabase solo si inicia sesión)*

| Campo | Respuesta |
|---|---|
| ¿Se recopila? | Sí |
| ¿Se comparte? | No |
| ¿Se procesa de forma efímera? | No |
| ¿Es opcional u obligatoria? | **Opcional** (sin login, los datos nunca salen del dispositivo) |
| Finalidades | Funcionalidad de la app |

*Nota: son datos que el propio usuario escribe sobre cuentas de
evaluación/práctica; la app no accede a cuentas financieras reales. Aun
así, declararlos como financieros es la lectura conservadora y segura.*

### 3. Compras → Historial de compras
*(RevenueCat / Google Play Billing)*

| Campo | Respuesta |
|---|---|
| ¿Se recopila? | Sí |
| ¿Se comparte? | No (RevenueCat es proveedor de servicio) |
| ¿Se procesa de forma efímera? | No |
| ¿Es opcional u obligatoria? | Opcional (solo si compra Premium) |
| Finalidades | Funcionalidad de la app |

### 4. Dispositivo u otros IDs → IDs del dispositivo
*(ID de publicidad, recopilado por el SDK de Google Mobile Ads / AdMob)*

| Campo | Respuesta |
|---|---|
| ¿Se recopila? | Sí |
| ¿Se comparte? | **Sí** (con Google/partners de publicidad, según la divulgación del SDK de AdMob) |
| ¿Se procesa de forma efímera? | No |
| ¿Es opcional u obligatoria? | Obligatoria para usuarios del tier gratuito |
| Finalidades | Publicidad o marketing |

### 5. Actividad en la app → Interacciones con la app
*(Interacciones con anuncios, recopiladas por AdMob)*

| Campo | Respuesta |
|---|---|
| ¿Se recopila? | Sí |
| ¿Se comparte? | Sí (Google, para publicidad) |
| ¿Se procesa de forma efímera? | No |
| Finalidades | Publicidad o marketing |

> **Verificar antes de enviar:** Google publica la divulgación de datos
> oficial del SDK de Google Mobile Ads en
> https://developers.google.com/admob/android/play-data-disclosure —
> contrastar las secciones 4 y 5 con la versión vigente del SDK
> (`react-native-google-mobile-ads` envuelve ese SDK). Lo mismo con
> https://www.revenuecat.com/docs/platform-resources/google-plays-data-safety
> para RevenueCat.

## Lo que NO se declara (porque no se recopila)

Ubicación, contactos, fotos/videos, audio, archivos, calendario, historial
web, información de salud, mensajes, datos de apps instaladas, nombre,
dirección, teléfono, IDs de usuario ajenos a la cuenta. La app tampoco usa
SDKs de analytics ni de crash reporting hoy — si se agregan (p. ej.
Sentry/Firebase), hay que volver a este formulario.
