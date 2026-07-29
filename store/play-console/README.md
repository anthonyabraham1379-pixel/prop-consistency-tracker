# Documentos para Google Play Console

Todo lo necesario para registrar y publicar **Prop Consistency Tracker** en
Google Play Console. Cada archivo corresponde a una sección del Console:

| Archivo | Sección de Play Console |
|---|---|
| [01-ficha-de-la-tienda.md](01-ficha-de-la-tienda.md) | Presencia en la tienda → Ficha de Play Store |
| [02-assets-graficos.md](02-assets-graficos.md) | Ficha de Play Store → Recursos gráficos |
| [03-clasificacion-de-contenido.md](03-clasificacion-de-contenido.md) | Contenido de la app → Clasificación de contenido (IARC) |
| [04-seguridad-de-datos.md](04-seguridad-de-datos.md) | Contenido de la app → Seguridad de datos |
| [05-contenido-de-la-app.md](05-contenido-de-la-app.md) | Contenido de la app → resto de declaraciones |
| [06-notas-de-version.md](06-notas-de-version.md) | Versiones → Notas de la versión |

La carpeta [`en/`](en/) tiene el equivalente en inglés de los 6 archivos
(mismo orden, mismos números). Úsala para:
- Agregar **en-US** como segundo idioma de ficha en Play Console (Presencia
  en la tienda → Ficha de Play Store → "Administrar traducciones") — ahora
  tiene sentido porque la app ya tiene traducción completa al inglés
  (Configuración → Idioma).
- Rellenar el cuestionario IARC y el formulario de Seguridad de datos en
  inglés si prefieres trabajar en ese idioma (son declaraciones globales,
  no por idioma de ficha — solo se llenan una vez).

Páginas públicas que Play Console pide por URL (se sirven con GitHub Pages
desde la carpeta `docs/`), cada una con su par en inglés enlazado arriba de
la página:

- **Política de privacidad:** `docs/index.html` (ES) / `docs/index-en.html` (EN) →
  `https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/`
- **Eliminación de cuenta y datos:** `docs/delete-account.html` (ES) /
  `docs/delete-account-en.html` (EN) →
  `https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/delete-account.html`

Play Console solo permite **una** URL de política de privacidad y **una**
URL de eliminación de datos (no son por idioma de ficha) — usa las
versiones en español como las oficiales registradas; las páginas en inglés
están enlazadas desde ellas para los usuarios que prefieran leerlas en ese
idioma.

> Para que esas URLs funcionen, activa GitHub Pages en el repo:
> **Settings → Pages → Deploy from a branch → `master` / `/docs`**.

---

## ⚠️ Bloqueadores antes de publicar (revisar primero)

1. **AdMob usa el App ID de PRUEBA de Google.** En `app.json` está
   `ca-app-pub-3940256099942544~...`, que es el ID de ejemplo de Google.
   Hay que crear la app en [AdMob](https://apps.admob.com), obtener el App ID
   real y los IDs de bloques de anuncios reales, y reemplazarlos antes del
   build de producción. Publicar con el ID de prueba = anuncios que nunca
   pagan y posible rechazo.
2. **RevenueCat usa una clave de prueba.** `config/premium.js` tiene
   `test_KIVyUaLbmuUKLryIJWSJbfLBXPd`. Hay que usar la clave pública de
   producción de Google (empieza con `goog_`) del proyecto de RevenueCat.
3. **Crear la suscripción en Play Console** (Monetizar → Productos →
   Suscripciones): p. ej. ID `premium_monthly`, $3.99 USD/mes, y vincularla
   en RevenueCat al entitlement `premium`. Sin producto creado, el paywall
   no puede cargar ofertas.
4. **Correo de contacto de la política de privacidad.** `docs/index.html`
   usa `a.isis.a0412@gmail.com`. Verifica que sea el correo que quieres
   exponer públicamente (el de la cuenta de desarrollador es
   `anthonyabraham1379@gmail.com`).
5. **El package name es permanente:** `com.anthonyaal.propconsistencytracker`.
   Una vez subido el primer AAB no se puede cambiar.
6. **Capturas de pantalla y feature graphic siguen sin existir** (archivo
   02) — es lo único de la ficha de tienda que no se puede resolver solo
   con texto, hay que generarlas a mano desde la app corriendo.

---

## Checklist completo de registro (en orden)

### A. Cuenta de desarrollador (una sola vez)
- [ ] Crear cuenta en [play.google.com/console](https://play.google.com/console)
      (pago único de $25 USD) y completar la verificación de identidad.
- [ ] Si es **cuenta personal** creada después de nov. 2023: Google exige una
      **prueba cerrada con al menos 12 testers durante 14 días continuos**
      antes de poder solicitar acceso a producción. Planifícalo desde el
      inicio (recluta testers en comunidades de trading/Discord).

### B. Crear la app
- [ ] "Crear app" → Nombre: `Prop Consistency Tracker`, idioma
      predeterminado: **español (Latinoamérica)**, tipo: **App**,
      **gratis** (con compras dentro de la app). *Ojo: gratis→pago no se
      puede cambiar después.*

### C. Contenido de la app (panel "Contenido de la app")
- [ ] URL de política de privacidad → ver arriba.
- [ ] Acceso a la app, anuncios, clasificación de contenido, público
      objetivo, seguridad de datos, funciones financieras, etc. →
      respuestas exactas en los archivos 03, 04 y 05.

### D. Ficha de la tienda
- [ ] Textos del archivo 01 + recursos gráficos del archivo 02.
- [ ] Categoría: **Finanzas**. Correo de contacto de la ficha.
- [ ] Opcional pero recomendado: agrega **en-US** como segundo idioma de
      ficha con los textos de `en/01-store-listing.md` — la app ya soporta
      inglés completo, así que el listado en inglés ya no sería solo
      cosmético.

### E. Primera versión (internal testing)
- [ ] Reemplazar IDs de AdMob y clave de RevenueCat (bloqueadores 1–3).
- [ ] `eas build --platform android --profile production` (genera AAB;
      `autoIncrement` ya gestiona el versionCode).
- [ ] Versiones → Pruebas internas → crear versión, subir el AAB, pegar
      notas de la versión (archivo 06), agregar tu correo a la lista de
      testers internos.
- [ ] Instalar desde el link de opt-in y verificar: onboarding, registro de
      días, sync con Google, anuncios (con dispositivo de prueba), compra
      de suscripción (con tester de licencias en Play Console →
      Configuración → Pruebas de licencias) y el cambio de idioma
      ES/EN desde Configuración General (revisa que el nombre del
      challenge creado por preset y las alertas cambien de idioma también).

### F. Prueba cerrada → Producción
- [ ] Promover a prueba cerrada, cumplir el requisito de 12 testers/14 días
      (si aplica), solicitar acceso a producción y lanzar.
