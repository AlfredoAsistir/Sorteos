# AGENTS.md — eLotto Frontend

## Objetivo del proyecto

**eLotto** es una plataforma web para la administración y operación de sorteos.

Este frontend está desarrollado con **Angular 22 Standalone** y consume el backend ASP.NET Core Web API ubicado en el proyecto `eLotto_Bak`.

El frontend forma parte del mismo producto que el backend, pero ambos proyectos deben mantenerse físicamente separados y comunicarse exclusivamente mediante la API.

El objetivo principal es construir un producto:

* mantenible;
* responsive;
* mobile-first;
* claro;
* predecible;
* seguro;
* fácil de evolucionar.

Evitar deuda técnica innecesaria.

---

# Principios generales

1. **La estabilidad tiene prioridad sobre el refactor.**
2. Nunca modificar código fuera del alcance de la tarea solicitada.
3. Los cambios deben ser pequeños, fáciles de revisar y fáciles de revertir.
4. No inventar reglas de negocio.
5. Las reglas funcionales deben provenir de las instrucciones del proyecto o del usuario.
6. Antes de crear una solución nueva, revisar la implementación existente relacionada.
7. Mantener las convenciones ya establecidas en el proyecto.
8. No realizar cambios arquitectónicos por preferencia personal.

---

# Arquitectura obligatoria

## Angular

El proyecto utiliza:

* Angular 22;
* componentes Standalone;
* Lazy Loading;
* Reactive Forms;
* SCSS;
* TypeScript estricto.

No introducir `NgModules` nuevos salvo que exista una razón técnica muy justificada.

Preferir las APIs modernas de Angular compatibles con la versión instalada.

No actualizar Angular ni TypeScript sin autorización explícita.

---

# Separación frontend/backend

El frontend no contiene lógica de persistencia ni acceso directo a base de datos.

Toda información persistente debe obtenerse o modificarse mediante `eLotto_Bak`.

El frontend no debe:

* conectarse directamente a SQL Server;
* conocer connection strings;
* almacenar secretos;
* contener API keys privadas;
* ejecutar reglas críticas únicamente del lado cliente.

La autoridad final para reglas de negocio pertenece al backend.

---

# Backend

El frontend **no puede cambiar unilateralmente contratos de API**.

No modificar por iniciativa propia:

* nombres de endpoints;
* métodos HTTP;
* parámetros;
* query parameters;
* payloads;
* DTOs del backend;
* tipos de datos;
* autenticación;
* autorización;
* estructura de respuestas.

Cuando una funcionalidad requiera un cambio de contrato:

1. revisar también `eLotto_Bak`;
2. identificar el contrato actual;
3. realizar los cambios necesarios en ambos proyectos;
4. mantener ambos lados sincronizados.

Si la tarea afecta frontend y backend, aplicar también las reglas del `AGENTS.md` ubicado en la raíz del producto.

---

# Arquitectura de carpetas

Respetar la organización existente del proyecto.

No reorganizar carpetas únicamente por preferencia.

Mantener separación clara entre elementos como:

* páginas;
* componentes;
* servicios;
* modelos;
* guards;
* interceptors;
* directivas;
* componentes reutilizables;
* utilidades.

Antes de crear una carpeta o abstracción nueva, verificar si ya existe una ubicación apropiada.

---

# Routing

Mantener las rutas utilizando Lazy Loading cuando corresponda.

No modificar el routing principal salvo que la tarea lo requiera.

Las rutas administrativas protegidas deben utilizar los mecanismos de autenticación y autorización definidos por el proyecto.

No confiar únicamente en ocultar elementos visuales para proteger una funcionalidad.

---

# Responsive

Toda pantalla nueva debe diseñarse **mobile-first**.

Debe funcionar correctamente en:

* móvil;
* tablet;
* desktop.

Evitar layouts exclusivos para escritorio.

No asumir resoluciones específicas.

Los formularios, tablas, tarjetas, botones, modales y navegación deben conservar una experiencia utilizable en pantallas pequeñas.

---

# Experiencia de usuario

La interfaz debe priorizar:

1. claridad;
2. facilidad de captura;
3. prevención de errores;
4. retroalimentación inmediata;
5. navegación consistente;
6. responsive;
7. estética.

No sacrificar claridad funcional por efectos visuales innecesarios.

---

# Estado de la aplicación

Separar conceptualmente:

* DTOs o modelos provenientes del backend;
* estado de UI;
* modelos específicos de presentación cuando realmente sean necesarios.

No agregar propiedades puramente visuales a interfaces que representan directamente respuestas HTTP.

Ejemplos de estado visual:

* `isSelected`;
* `isExpanded`;
* `isLoading`;
* `isEditing`.

Cuando esas propiedades no formen parte del contrato de API, mantenerlas fuera del modelo HTTP cuando sea razonable.

---

# Modelos e interfaces

Utilizar interfaces o tipos TypeScript bien definidos.

Evitar `any` salvo que sea estrictamente necesario.

Los modelos que representan contratos del backend deben mantenerse compatibles con sus DTOs correspondientes.

No duplicar interfaces idénticas sin necesidad.

No crear DTOs frontend adicionales únicamente para renombrar las mismas propiedades del backend.

---

# Componentes

Cada componente debe tener una responsabilidad clara.

Evitar componentes excesivamente grandes.

Como guía, intentar evitar componentes de más de aproximadamente 300 líneas cuando sea razonable, pero no dividirlos artificialmente solo para cumplir un número.

Extraer funcionalidad cuando:

* sea reutilizable;
* tenga una responsabilidad independiente;
* reduzca duplicación;
* simplifique claramente el componente.

No fragmentar componentes sin beneficio real.

---

# Servicios

Los servicios Angular deben utilizarse principalmente para:

* acceso HTTP;
* lógica compartida;
* estado compartido cuando corresponda;
* integración con infraestructura frontend.

No colocar lógica de negocio compleja dentro de componentes.

No crear servicios innecesarios cuando una funcionalidad pertenece claramente a un servicio existente.

---

# Consumo HTTP

Las llamadas a `eLotto_Bak` deben centralizarse en servicios.

No realizar llamadas HTTP directamente desde múltiples componentes si existe o debe existir un servicio responsable de esa entidad.

Utilizar `HttpClient`.

Mantener manejo consistente de:

* errores;
* loading;
* autenticación;
* headers;
* respuestas.

No duplicar manejo transversal que pueda resolverse mediante interceptors existentes.

---

# Autenticación

Respetar completamente el mecanismo de autenticación existente del proyecto.

No modificar:

* login;
* registro;
* tokens;
* almacenamiento de sesión;
* guards;
* interceptors;
* expiración;
* refresh;
* autorización;

salvo que la tarea lo solicite.

Los guards mejoran la navegación, pero la seguridad real debe existir también en backend.

No asumir que una ruta está protegida solamente porque no aparece en el menú.

---

# eLotto — áreas funcionales

El frontend puede contener pantallas o flujos relacionados con áreas como:

* autenticación;
* usuarios;
* participantes;
* sorteos;
* boletos;
* selección de números;
* apartados;
* confirmaciones;
* pagos;
* cuentas de depósito;
* ganadores;
* administración.

No implementar funcionalidades nuevas únicamente porque aparezcan en esta lista.

Cada módulo debe desarrollarse conforme sea solicitado.

---

# Sorteos

Las pantallas relacionadas con sorteos deben consumir la información proporcionada por backend.

No calcular en frontend reglas críticas relacionadas con:

* disponibilidad;
* estado del sorteo;
* vigencia;
* ganador;
* apertura;
* cierre.

El frontend puede representar visualmente esas condiciones, pero backend debe seguir siendo la autoridad.

---

# Boletos y números

Las interfaces de selección de boletos deben priorizar facilidad de uso, especialmente en dispositivos móviles.

El frontend puede mostrar estados como:

* disponible;
* seleccionado;
* reservado;
* no disponible;

según lo recibido del backend y el estado temporal de UI.

Una selección visual no garantiza que el boleto haya sido asignado.

La confirmación final debe depender siempre de la respuesta del backend.

Si otra persona obtiene un boleto antes de completar la operación, el frontend debe manejar correctamente la respuesta del servidor y actualizar la interfaz.

---

# Concurrencia

No intentar resolver conflictos de concurrencia únicamente desde Angular.

Angular puede:

* deshabilitar acciones temporalmente;
* mostrar loaders;
* actualizar estados;
* volver a consultar disponibilidad;
* informar conflictos.

Pero la autoridad sobre la asignación real de un boleto pertenece a backend.

---

# WhatsApp

El frontend puede iniciar acciones relacionadas con WhatsApp mediante endpoints del backend.

Nunca almacenar en Angular:

* tokens del proveedor;
* API keys privadas;
* secretos;
* credenciales.

No realizar llamadas privadas directamente desde Angular a proveedores externos de WhatsApp.

---

# Pagos

La interfaz puede mostrar información y acciones relacionadas con pagos, pero no debe decidir por sí sola que una operación está pagada o confirmada.

El estado final siempre debe provenir del backend.

No almacenar información sensible de pago innecesariamente en frontend.

---

# Dependencias

## Prohibido sin autorización

No:

* instalar paquetes;
* actualizar paquetes;
* eliminar paquetes;
* cambiar versiones de Angular;
* cambiar versiones de TypeScript;
* reemplazar librerías existentes.

Si una librería parece innecesaria, reportarlo pero no eliminarla.

Antes de proponer una dependencia nueva, verificar si la funcionalidad puede resolverse correctamente utilizando Angular o las dependencias existentes.

---

# Angular Material

Si el proyecto utiliza Angular Material, conservarlo como librería principal de componentes donde corresponda.

No reemplazar Angular Material por otra librería sin autorización explícita.

Reutilizar componentes, estilos y patrones existentes antes de crear alternativas nuevas.

---

# Refactors

Solo realizar refactors cuando:

* el usuario los solicite explícitamente;
* sean indispensables para completar la tarea.

No:

* reordenar archivos;
* renombrar carpetas;
* cambiar estilos de código;
* mover componentes;
* extraer abstracciones;

únicamente por preferencia.

Una tarea funcional no autoriza una limpieza general del proyecto.

---

# Estilo de código

Mantener las convenciones existentes:

* comillas simples;
* indentación de 2 espacios;
* interfaces o tipos definidos;
* kebab-case para archivos y carpetas;
* nombres descriptivos;
* TypeScript estricto.

Evitar `any`.

Evitar nombres genéricos como:

```text
data
item
obj
value
```

cuando exista un nombre de dominio más claro.

---

# Formularios

Utilizar **Reactive Forms** para formularios de captura.

No introducir formularios template-driven salvo que exista una razón técnica concreta.

Mantener:

* validadores claros;
* mensajes de error consistentes;
* estados `dirty`;
* estados `pristine`;
* estados `touched`;
* estados `disabled`.

Las validaciones de frontend mejoran la experiencia, pero no sustituyen las validaciones del backend.

---

# Convenciones de formularios de catálogo

Estas reglas representan el patrón estándar de captura de eLotto y deben utilizarse en los nuevos formularios de catálogo, salvo que una tarea indique expresamente un comportamiento diferente.

## Modelo y formulario

* Utilizar Angular Reactive Forms.
* Para los CRUD de catálogos, trabajar directamente con la entidad o modelo que representa el contrato del backend cuando ambas estructuras sean equivalentes.
* No crear DTOs frontend únicamente para duplicar la misma estructura.
* No agregar servicios de negocio innecesarios si el formulario puede consumir claramente el servicio HTTP correspondiente.
* Al cargar datos desde backend, restablecer el formulario y marcarlo como `pristine`.
* Los botones Guardar o Actualizar solo deben habilitarse después de cambios realizados por el usuario y cuando el formulario sea válido.

---

# Navegación por teclado

La captura administrativa debe estar orientada a reducir el uso del mouse.

* `Enter` debe funcionar como `Tab`: avanzar al siguiente control capturable.
* `Enter` no debe enviar el formulario ni activar accidentalmente botones, selectores o menús.
* `Escape` debe navegar al control capturable anterior.
* `Escape` no debe cerrar, abrir ni ejecutar accidentalmente el control activo.
* `Tab` y `Shift + Tab` deben conservar su comportamiento normal.
* Los iconos auxiliares dentro de inputs deben quedar fuera del tab stop mediante `tabindex="-1"` cuando corresponda.
* Reutilizar `FormNavigationDirective`.
* No duplicar esta navegación manualmente en cada componente.
* En controles como `mat-select`, interceptar Enter y Escape cuando sea necesario para evitar abrir o cerrar accidentalmente el dropdown durante la navegación.

---

# Campos numéricos

No utilizar `input type="number"` como patrón estándar para campos de captura numérica.

Reutilizar:

```text
NumericInputComponent
```

cuando exista y corresponda.

Configurar por campo:

```text
integerDigits
decimalDigits
```

Ejemplo conceptual:

```text
(3,2) → 123.36
(3,0) → 123
```

El componente debe aceptar un único signo negativo `-` solamente al inicio cuando `allowNegative` esté habilitado.

El signo positivo `+` no debe aceptarse.

Los límites de captura no sustituyen los validadores del formulario.

---

# Campos monetarios

Para cantidades de dinero:

* evitar `number` HTML cuando exista el componente numérico estándar;
* controlar decimales explícitamente;
* evitar cálculos con texto formateado;
* enviar al backend valores numéricos válidos;
* no introducir símbolos monetarios dentro del valor del `FormControl` salvo que la implementación existente lo requiera.

La representación visual puede incluir formato monetario sin modificar el contrato HTTP.

---

# Campos de fecha y hora

Reutilizar:

```text
DateTimeFieldComponent
```

cuando exista.

No duplicar máscaras o selectores.

Reglas:

* Mostrar Fecha y Hora como capturas visualmente separadas cuando el formulario lo requiera.
* Manejar visualmente la fecha como `dd/MM/yyyy`.
* Permitir captura manual y selección mediante datepicker.
* El usuario no debe tener que escribir `/` si el componente implementa máscara automática.
* Conservar correctamente ceros de día y mes.
* Manejar hora en formato de 24 horas.
* No utilizar AM/PM.
* Cuando el `FormControl` esté deshabilitado, deshabilitar también sus controles auxiliares.
* Al enviar, utilizar la estrategia de fecha/zona horaria definida por el proyecto.
* No introducir conversiones paralelas o implícitas.

No asumir horas iniciales o finales específicas salvo que la regla funcional del formulario las defina.

---

# Catálogos paginados de un registro

Para los formularios administrativos que sigan el patrón de catálogo de un registro:

* mostrar un registro por página dentro del mismo formulario;
* no agregar automáticamente una tabla o grid inferior;
* navegación estándar:

```text
primero
anterior
n de total
siguiente
último
```

* utilizar iconos claros;
* mantener una barra responsive;
* colocar `Nuevo` dentro de la misma zona de navegación cuando corresponda;
* el backend debe resolver orden y paginación;
* después de actualizar, recargar y conservar la página actual;
* después de crear, navegar al registro nuevo conforme al orden definido por backend;
* `Deshacer` debe volver a consultar el registro al backend;
* no restaurar únicamente una copia local cuando el patrón requiera refrescar desde servidor.

Si el backend ordena por creación descendente, página 1 puede representar el registro más reciente.

No asumir ese orden si el endpoint correspondiente define otro.

---

# Loading en catálogos

Mantener el comportamiento de loading definido por los componentes existentes.

Cuando el patrón existente utilice retraso de loading para navegación entre registros, reutilizarlo.

No crear múltiples implementaciones distintas del mismo comportamiento.

Operaciones como:

* guardar;
* actualizar;
* eliminar;
* carga inicial;

pueden mantener loading inmediato según el patrón existente.

---

# Acciones del formulario

Como convención para entidades con identificadores numéricos:

```text
Id = 0, null o undefined
```

representa normalmente un registro nuevo.

Utilizar `POST` para creación cuando ese sea el contrato del backend.

```text
Id > 0
```

representa normalmente un registro existente.

Utilizar `PUT` para actualización cuando ese sea el contrato del backend.

Reglas visuales estándar:

* Mostrar `Guardar` para altas.
* Mostrar `Actualizar` para registros existentes.
* Guardar y Actualizar utilizan icono `save`.
* En registros existentes sin cambios, mostrar `Nuevo` con icono `add` cuando corresponda.
* Al iniciar un alta o modificar el registro, mostrar `Deshacer` con icono `undo`.
* Eliminar solamente debe estar disponible para registros persistidos.
* Una eliminación debe requerir confirmación.

Estas convenciones no permiten cambiar contratos HTTP existentes.

---

# Eliminaciones

Toda eliminación iniciada por el usuario debe requerir confirmación visual.

Utilizar la infraestructura de confirmaciones existente.

Cuando el proyecto utilice SweetAlert para operaciones destructivas:

* conservar SweetAlert;
* mostrar acción afirmativa clara;
* mostrar acción de cancelación;
* ejecutar `DELETE` únicamente después de confirmación.

Nunca eliminar automáticamente al primer clic.

---

# Notificaciones

Utilizar:

```text
NotificationService
```

para notificaciones cuando sea el servicio estándar del proyecto.

Los componentes no deben depender directamente de diferentes sistemas de notificación si ya existe este servicio centralizado.

Los mensajes deben declarar un tipo:

```text
success
error
warning
info
```

Las confirmaciones destructivas pueden utilizar SweetAlert aunque las notificaciones ordinarias utilicen otro mecanismo.

---

# Loaders

Reutilizar el mecanismo global de loading existente.

No crear loaders independientes por pantalla sin necesidad.

Evitar parpadeos innecesarios para operaciones extremadamente rápidas cuando ya exista una estrategia de retraso.

No ocultar operaciones largas sin retroalimentación visual.

---

# Manejo de errores

Los errores HTTP deben presentarse de manera comprensible para el usuario.

No mostrar directamente:

* stack traces;
* excepciones internas;
* objetos JSON completos;
* mensajes técnicos innecesarios.

Mantener el detalle técnico disponible para desarrollo cuando corresponda, sin exponerlo como mensaje principal de UI.

---

# Accesibilidad básica

Las pantallas deben mantener prácticas básicas de accesibilidad.

Cuando corresponda:

* utilizar labels;
* asociar inputs correctamente;
* permitir navegación por teclado;
* mantener foco visible;
* evitar controles exclusivamente dependientes del mouse;
* utilizar botones reales para acciones;
* utilizar atributos ARIA cuando sean necesarios.

No sacrificar accesibilidad para lograr un diseño visual.

---

# Antes de modificar código

Siempre realizar este proceso:

1. Leer el `AGENTS.md` raíz.
2. Analizar los archivos involucrados.
3. Buscar componentes, servicios o directivas reutilizables.
4. Identificar cualquier contrato de API involucrado.
5. Determinar si también será necesario modificar `eLotto_Bak`.
6. Explicar brevemente el plan.
7. Implementar únicamente lo solicitado.
8. Ejecutar las verificaciones correspondientes.

---

# Después de cada implementación

Indicar siempre:

1. Qué archivos fueron modificados.
2. Qué se cambió en cada archivo.
3. Si cambió alguna:

   * ruta;
   * interface;
   * modelo;
   * servicio;
   * directiva;
   * contrato consumido del backend.
4. Si existe algún riesgo.
5. Qué debe probar manualmente el usuario.
6. Resultado de:

```bash
npx ng build
```

7. Si el build falla:

   * indicar el error relevante;
   * indicar si fue provocado por el cambio;
   * no corregir errores o warnings no relacionados sin autorización.
8. Qué ruta o pantalla debe probarse.
9. Si el cambio requiere modificación correspondiente en backend.

Cuando no se necesiten cambios de backend, indicar:

```text
No requiere cambios en backend.
```

---

# Lo que Codex debe evitar

Nunca realizar estas acciones sin autorización explícita:

* borrar código funcional;
* cambiar autenticación;
* cambiar routing principal;
* modificar environments de producción;
* cambiar diseño global de la plantilla;
* reemplazar Angular Material;
* instalar paquetes;
* actualizar dependencias;
* crear archivos innecesarios;
* realizar refactors generales;
* cambiar contratos de API unilateralmente;
* cambiar convenciones existentes por preferencia.

---

# Código reutilizable

Antes de implementar una funcionalidad transversal, buscar componentes y utilidades existentes.

Especialmente revisar antes de duplicar:

```text
FormNavigationDirective
NumericInputComponent
DateTimeFieldComponent
NotificationService
```

y cualquier otro componente compartido existente.

Si una implementación reutilizable ya resuelve el problema, utilizarla.

---

# Prioridad de desarrollo

Cuando existan varias opciones válidas, seguir este orden:

1. Funcionalidad del negocio.
2. Correctitud.
3. Seguridad.
4. Consistencia con el proyecto.
5. Mantenibilidad.
6. Simplicidad.
7. Rendimiento.
8. Experiencia de usuario.
9. Estética.

---

# Filosofía del proyecto

eLotto debe ser un producto comercial mantenible durante varios años.

La prioridad es escribir código:

* claro;
* explícito;
* consistente;
* predecible;
* fácil de entender.

Preferir código sencillo y mantenible antes que soluciones innecesariamente abstractas o "ingeniosas".

No introducir una arquitectura más compleja que la requerida por el problema.

---

# Regla final

Antes de implementar una funcionalidad, entender:

1. qué necesita hacer el usuario;
2. qué información proporciona backend;
3. qué estado pertenece exclusivamente a la interfaz;
4. qué reglas deben validarse en servidor;
5. qué componentes existentes pueden reutilizarse.

El frontend debe concentrarse en ofrecer una excelente experiencia de usuario sin convertirse en la autoridad de las reglas de negocio.
---

## Estándar definitivo de diálogos del sistema

### Diálogos y mensajes del sistema

* Utilizar siempre `NotificationService` y el diálogo estándar del sistema para mostrar confirmaciones, avisos, éxitos, advertencias y errores.
* No utilizar directamente `window.alert`, `window.confirm`, `alert()` o `confirm()` en los componentes.
* En toda pregunta de confirmación destructiva, el botón afirmativo debe ser rojo para comunicar peligro y el botón negativo debe ser azul para comunicar la acción segura.
* Los botones de confirmación deben tener el mismo ancho y alto, conservar separación visible y usar los textos `Si, Eliminar` y `No, Abortar` cuando se confirme una eliminación.
* El foco inicial debe favorecer la opción segura `No, Abortar`.
* La operación destructiva solo puede ejecutarse cuando el diálogo devuelva una confirmación afirmativa explícita.
* Los avisos sin pregunta deben reutilizar el mismo diálogo y representar visualmente su tipo: `success`, `error`, `warning` o `info`.

### Catálogos paginados

* Los catálogos administrativos paginados muestran un registro por página dentro del mismo formulario.
* La navegación estándar es: primero, anterior, `n de total`, siguiente y último.
* El backend controla el orden, la página y el total de registros; el frontend no descarga todo el catálogo para navegar localmente.
* Después de actualizar se recarga la página actual. Después de crear se navega al nuevo registro según el orden del backend.
* `Deshacer` vuelve a consultar el registro al backend.
* `Nuevo`, `Guardar`, `Actualizar`, `Deshacer` y `Eliminar` deben respetar el estado válido, persistido y modificado del formulario.
* Toda eliminación requiere el diálogo estándar de confirmación.

### Navegación de formularios

* En formularios de catálogo, `Enter` avanza al siguiente control capturable como `Tab` y no debe enviar el formulario accidentalmente.
* `Escape` regresa al control capturable anterior y no debe activar ni cerrar accidentalmente el control actual.
* `Tab` y `Shift + Tab` conservan su comportamiento normal.
* Reutilizar `FormNavigationDirective`; no duplicar esta lógica dentro de cada componente.
* Los iconos auxiliares de inputs deben quedar fuera del orden de tabulación mediante `tabindex="-1"` cuando corresponda.

# Reglas de ejecución de Codex

## Ejecución eficiente y manejo de archivos

Codex debe trabajar de forma directa, eficiente y con la menor narración intermedia posible.

El objetivo principal es **analizar, implementar y validar**, evitando consumir tiempo y contexto explicando acciones internas que puede resolver automáticamente.

---

## Uso del workspace y archivos

* Considera todos los archivos ubicados dentro de este proyecto/workspace como autorizados para lectura y modificación cuando la tarea solicitada lo requiera.
* Usa preferentemente rutas relativas desde la raíz del proyecto para leer, crear, modificar o eliminar archivos.
* Evita utilizar rutas absolutas de Windows cuando una ruta relativa sea suficiente.
* No solicites confirmación adicional para modificar archivos cuando la tarea del usuario ya autorice claramente dichos cambios.
* No solicites nuevamente permiso para crear archivos necesarios para una implementación que ya fue autorizada.
* No amplíes el alcance de escritura fuera del workspace o de los archivos necesarios para completar la tarea.
* Antes de crear archivos nuevos, revisa si existe una implementación, estructura o archivo equivalente que deba reutilizarse.

---

## Errores de sandbox, aislamiento, permisos o escritura

Si una operación de lectura, escritura, parche o ejecución falla debido a:

* sandbox;
* aislamiento del entorno;
* permisos;
* rutas absolutas;
* bloqueo de archivos;
* mecanismo de parche;
* mecanismo de escritura;
* restricciones temporales del entorno;

Codex debe actuar de la siguiente manera:

1. Intentar automáticamente una alternativa segura disponible dentro del workspace.
2. Priorizar rutas relativas desde la raíz del proyecto.
3. Si falla un mecanismo de modificación, intentar otro mecanismo permitido.
4. Mantener siempre el intento alternativo dentro del alcance originalmente autorizado.
5. No detener la tarea únicamente porque el primer mecanismo haya fallado.
6. No pedir autorización adicional cuando la alternativa continúa dentro del workspace y del alcance ya autorizado.
7. Realizar los reintentos razonables de forma silenciosa.
8. Solo informar al usuario cuando exista un bloqueo real que impida completar la tarea después de haber agotado alternativas razonables.

Codex nunca debe intentar evadir restricciones reales de seguridad, permisos o aislamiento del entorno.

Si el entorno exige explícitamente autorización del usuario para determinada operación, debe respetarse ese requisito.

---

## Reintentos automáticos

Cuando una operación falle y exista una alternativa segura:

**intentar automáticamente la alternativa antes de informar al usuario.**

Por ejemplo:

* Si una ruta absoluta falla, intentar con una ruta relativa.
* Si un parche falla, intentar nuevamente mediante otro mecanismo permitido de edición.
* Si una lectura falla mediante un mecanismo, intentar otro mecanismo disponible.
* Si un comando no puede ejecutarse desde una ubicación determinada, revisar si puede ejecutarse correctamente desde la raíz correspondiente del workspace.

No realizar reintentos infinitos.

Si después de intentos razonables el problema persiste, detener únicamente la operación afectada e informar claramente el bloqueo en la respuesta final.

---

## Comunicación durante la ejecución

Evita narrar acciones internas que no requieren intervención del usuario.

No enviar mensajes intermedios innecesarios como:

* "Voy a reintentar el parche."
* "El entorno aislado está fallando."
* "Intentaré ahora con rutas relativas."
* "Voy a utilizar otro mecanismo."
* "Primero leeré estos archivos."
* "Ahora modificaré los siguientes archivos."
* "La petición ya autoriza estos cambios."
* "Procederé a crear la entidad."
* "Voy a revisar la compilación."
* "El primer intento falló, intentaré nuevamente."

Si estas acciones pueden resolverse automáticamente, deben realizarse sin narrarlas.

Solo comunicar durante la ejecución cuando:

* sea necesaria una decisión del usuario;
* falte información indispensable;
* exista riesgo de realizar una operación fuera del alcance solicitado;
* el entorno requiera autorización explícita;
* exista un bloqueo que Codex no pueda resolver de manera segura.

---

## Evitar explicaciones innecesarias antes de implementar

Cuando la solicitud sea clara, no repetir extensamente lo que el usuario acaba de pedir.

Evitar respuestas previas del tipo:

> "Entiendo que deseas modificar X, crear Y y posteriormente actualizar Z. Primero revisaré..."

En su lugar:

1. Analizar la solicitud.
2. Revisar el código necesario.
3. Implementar.
4. Validar.
5. Informar el resultado.

Si la tarea puede ejecutarse directamente, debe ejecutarse directamente.

---

## Mantener el alcance de la tarea

Los reintentos automáticos no autorizan a ampliar el alcance de una tarea.

Codex debe:

* modificar únicamente los archivos necesarios;
* evitar refactors no solicitados;
* evitar cambios cosméticos no relacionados;
* evitar actualizar dependencias sin necesidad;
* evitar modificar configuraciones globales para resolver problemas locales;
* evitar eliminar código que no esté claramente relacionado con la tarea;
* preservar el comportamiento existente que no forme parte del cambio solicitado.

Si durante la implementación detecta mejoras adicionales, puede mencionarlas al finalizar, pero no debe implementarlas automáticamente si están fuera del alcance.

---

## Base de datos y operaciones sensibles

La autorización para modificar código **no implica automáticamente autorización para modificar una base de datos real**.

Codex puede, cuando corresponda a la tarea:

* crear o modificar entidades;
* crear configuraciones de Entity Framework;
* modificar `DbContext`;
* crear repositorios;
* crear servicios;
* modificar controladores;
* generar código relacionado con persistencia;
* crear o modificar migraciones;
* generar una migración cuando haya sido solicitada o sea claramente parte de la implementación autorizada.

Sin autorización expresa del usuario, Codex NO debe:

* ejecutar `Update-Database`;
* ejecutar `dotnet ef database update`;
* aplicar migraciones directamente;
* ejecutar scripts destructivos;
* eliminar tablas de una base de datos real;
* eliminar información;
* modificar datos de producción;
* conectarse a producción para aplicar cambios;
* realizar operaciones irreversibles sobre datos reales.

Generar una migración y aplicarla a una base de datos son operaciones diferentes.

La generación de una migración puede formar parte de la implementación.

La aplicación de esa migración requiere autorización cuando afecte una base de datos real.

---

## Validaciones

Después de implementar cambios, realiza las validaciones razonables disponibles para el proyecto.

Por ejemplo:

### Backend

Cuando corresponda:

```text
dotnet build
```

o las pruebas existentes relacionadas con la modificación.

### Frontend

Cuando corresponda:

```text
ng build
```

o:

```text
npm run build
```

según la configuración existente del proyecto.

No modificar código ajeno a la tarea únicamente para eliminar warnings históricos o problemas preexistentes.

Si una validación falla debido a un problema que ya existía antes del cambio, indícalo brevemente en el resultado final.

---

## Respuesta final

Después de completar una implementación, entregar una respuesta breve y útil.

La respuesta debe concentrarse en:

* qué se implementó;
* archivos relevantes creados;
* archivos relevantes modificados;
* migraciones generadas, cuando corresponda;
* validaciones realizadas;
* resultado de compilación o pruebas;
* decisiones técnicas importantes;
* cualquier bloqueo real que haya quedado pendiente.

No incluir un historial detallado de:

* comandos ejecutados;
* archivos simplemente leídos;
* búsquedas internas;
* intentos fallidos recuperados;
* reintentos;
* errores temporales del sandbox que finalmente fueron resueltos;
* mecanismos internos utilizados para modificar archivos.

Si un problema fue resuelto automáticamente, normalmente no es necesario mencionarlo.

---

## Uso eficiente del contexto

Evita consumir contexto innecesariamente.

* No repetir grandes cantidades de código que ya existen en el proyecto.
* No copiar archivos completos en la respuesta salvo que el usuario lo solicite.
* No explicar línea por línea cambios sencillos.
* No repetir la solicitud original.
* No generar documentación adicional que no haya sido solicitada.
* No producir resúmenes excesivamente largos para cambios pequeños.
* No narrar cada paso del razonamiento interno.
* No mostrar procesos internos de análisis.

Utiliza el contexto principalmente para comprender e implementar correctamente la tarea.

---

## Principio general de trabajo

La secuencia preferida de trabajo es:

```text
ANALIZAR
   ↓
IMPLEMENTAR
   ↓
VALIDAR
   ↓
INFORMAR
```

Evitar:

```text
ANALIZAR
   ↓
NARRAR
   ↓
INTENTAR
   ↓
NARRAR EL ERROR
   ↓
PEDIR PERMISO INNECESARIO
   ↓
REINTENTAR
   ↓
NARRAR
   ↓
IMPLEMENTAR
   ↓
EXPLICAR TODO EL PROCESO
```

Cuando exista autorización suficiente y la tarea sea clara:

**analiza, implementa, valida y entrega el resultado.**

Prioriza siempre la ejecución eficiente, manteniendo la seguridad, el alcance solicitado y la calidad del código.

