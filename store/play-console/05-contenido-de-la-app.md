# Declaraciones de "Contenido de la app"

Sección: **Política → Contenido de la app**. Respuestas para cada
declaración obligatoria (además de Clasificación de contenido y Seguridad
de datos, que tienen sus propios archivos).

## 1. Política de privacidad

```
https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/
```
Requiere GitHub Pages activo (Settings → Pages → branch `master`, carpeta
`/docs`). Verificar que la URL cargue ANTES de pegarla — Google la valida.

## 2. Acceso a la app (App access)

**Respuesta: "Todas las funciones están disponibles sin acceso especial".**

Justificación: la app funciona completa sin iniciar sesión (el login con
Google solo activa la sincronización) y el contenido Premium está detrás de
una compra estándar de Google Play, que los revisores pueden inspeccionar
vía la ficha de la suscripción. No se necesitan credenciales de prueba.

*Si Google objetara (raro), la alternativa es crear una cuenta de Google de
prueba y proporcionarla en "Instrucciones de acceso".*

## 3. Anuncios

**Respuesta: Sí, mi app contiene anuncios.** (AdMob, banners en la versión
gratuita.)

## 4. Público objetivo y contenido (Target audience)

| Pregunta | Respuesta |
|---|---|
| Grupos de edad objetivo | **Solo 18+** |
| ¿La app podría atraer a niños sin querer? | No (temática financiera, UI sobria, sin elementos infantiles) |

Al declarar solo 18+, no aplican las políticas de Familias y no se requiere
participar en el programa "Diseñado para familias".

## 5. Funciones financieras (Financial features declaration)

**Respuesta: "Mi app no ofrece ninguna función financiera".**

Justificación (guardar por si hay revisión manual): Prop Consistency Tracker
es un diario/registro manual. No ofrece préstamos, no facilita trading ni
inversión, no se conecta a brokers, no ejecuta órdenes, no custodia fondos,
no da asesoría ni recomendaciones. Los "datos financieros" que muestra son
números que el usuario escribe a mano sobre sus evaluaciones de prop firms,
equivalente a una hoja de cálculo o diario.

> Coherencia importante: el copy de la ficha (archivo 01) ya incluye el
> disclaimer de "no es asesoría financiera, no se conecta a brokers, no
> garantiza resultados". Mantener ese disclaimer también visible dentro de
> la app (onboarding o Configuración) refuerza el caso si un revisor humano
> examina la app bajo la política de servicios financieros.

## 6. Apps de noticias

**No, no es una app de noticias.**

## 7. Apps gubernamentales

**No.**

## 8. Apps de salud

**No, no tiene funciones de salud.**

## 9. Seguridad de los datos / eliminación de cuenta

Como la app permite crear cuenta (Google Sign-In), Play exige una URL
pública donde el usuario pueda solicitar la eliminación de la cuenta y sus
datos **sin necesidad de instalar la app**:

```
https://anthonyabraham1379-pixel.github.io/prop-consistency-tracker/delete-account.html
```

(La página ya está creada en `docs/delete-account.html`; el proceso es por
correo mientras no exista eliminación self-service dentro de la app.)

## 10. IDs de publicidad (Advertising ID)

En la declaración de "ID de publicidad" (aparece para apps con target API
33+): **Sí, la app usa el ID de publicidad**, con finalidad **Publicidad o
marketing** (lo usa el SDK de AdMob). El permiso
`com.google.android.gms.permission.AD_ID` lo agrega automáticamente
`react-native-google-mobile-ads`.

## 11. Configuración adicional de la app (pestaña principal)

| Campo | Valor |
|---|---|
| ¿App gratuita o de pago? | Gratuita (con compras integradas) |
| Países de distribución | Empezar con: México, Colombia, Argentina, Chile, Perú, España, EE. UU. — o "todos los países" si se prefiere |
| Categoría | Finanzas |
| ¿Contiene compras integradas? | Sí ($3.99/mes, marcar rango de precios de IAP) |
