# AGENTS.md — eLotto

## Objetivo

Este directorio contiene los proyectos que forman el sistema **eLotto**.

Actualmente el sistema está compuesto por:

* `eLotto_Front`: frontend Angular.
* `eLotto_Bak`: backend ASP.NET Core Web API.

Ambos proyectos pertenecen al mismo producto y deben considerarse partes de un único sistema.

**eLotto es una plataforma para la administración y operación de sorteos.**

El sistema permitirá gestionar sorteos, boletos y números disponibles, selección y apartado de boletos, participantes, usuarios, confirmaciones, pagos, ganadores, comunicación mediante WhatsApp y las funciones administrativas necesarias para operar la plataforma.

El proyecto debe desarrollarse utilizando una arquitectura moderna, manteniendo frontend y backend separados y estableciendo contratos claros entre ambos mediante la API.

La implementación debe priorizar:

1. Correctitud de las reglas de negocio.
2. Integridad y consistencia de los datos.
3. Seguridad.
4. Simplicidad.
5. Mantenibilidad.
6. Buena separación de responsabilidades.
7. Experiencia de usuario.
---

## Convenciones obligatorias de interfaz

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

