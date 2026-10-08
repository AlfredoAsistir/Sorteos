# AGENTS.md — eLotto Backend

## Objetivo del proyecto

**eLotto** es una plataforma web para la administración y operación de sorteos.

Este backend está desarrollado con:

* ASP.NET Core Web API;
* .NET 8;
* Entity Framework Core;
* SQL Server;
* JWT para autenticación.

El frontend principal consume esta API desde Angular mediante el proyecto `eLotto_Front`.

El objetivo es mantener un backend:

* estable;
* predecible;
* seguro;
* mantenible;
* fácil de evolucionar;
* con responsabilidades claramente separadas.

No realizar refactors innecesarios ni introducir arquitectura adicional sin una necesidad real.

---

# Principios generales

1. La estabilidad tiene prioridad sobre el refactor.
2. Nunca modificar código fuera del alcance de la tarea solicitada.
3. Los cambios deben ser pequeños, fáciles de revisar y fáciles de revertir.
4. No asumir reglas de negocio que no hayan sido definidas.
5. Antes de implementar una solución, revisar la estructura existente relacionada.
6. No realizar mejoras generales simplemente porque se detecten durante una tarea.
7. Los problemas encontrados fuera del alcance deben reportarse, no corregirse automáticamente.
8. Mantener la arquitectura y convenciones actuales salvo instrucción explícita.

---

# Arquitectura actual

La solución está compuesta principalmente por:

* `eLotto`: proyecto ASP.NET Core Web API.
* `eLotto.Core`: entidades, DTOs, DbContext, repositorios y services.

Mantener esta estructura salvo que el usuario solicite explícitamente una reorganización.

No introducir nuevos proyectos o capas únicamente por preferencia.

No implementar sin autorización explícita:

* Clean Architecture;
* CQRS;
* MediatR;
* Unit of Work adicional;
* Event Bus;
* arquitectura de microservicios;
* capas adicionales innecesarias.

La arquitectura existente debe mantenerse simple.

---

# ASP.NET Core

Mantener:

* .NET 8;
* Controllers;
* Dependency Injection existente;
* JWT Bearer Authentication;
* Swagger/OpenAPI;
* Entity Framework Core;
* SQL Server.

No actualizar:

* versión de .NET;
* Entity Framework Core;
* paquetes NuGet;

sin autorización explícita.

---

# Flujo arquitectónico general

La separación estándar debe ser:

```text id="uwlacs"
Controller
    ↓
Interface
    ↓
Repository / Service
    ↓
DbContext / Infrastructure
```

La elección entre repository y service depende de la responsabilidad.

## Repository

Utilizar repository para:

* consultas EF Core;
* acceso a datos;
* persistencia;
* filtros;
* ordenamiento;
* paginación;
* composición simple de respuestas;
* operaciones CRUD.

## Service

Utilizar service cuando exista:

* lógica de negocio;
* proceso reutilizable;
* coordinación de múltiples repositories;
* integración externa;
* transacciones complejas;
* transformación significativa;
* procesos que no pertenecen exclusivamente al acceso a datos.

No crear services vacíos únicamente para agregar otra capa.

---

# Controllers

Los controllers deben mantenerse simples.

Deben encargarse principalmente de:

* recibir la petición;
* validar parámetros HTTP básicos;
* validar consistencia de ruta/body cuando corresponda;
* delegar trabajo;
* construir la respuesta HTTP.

Evitar lógica de negocio compleja directamente en controllers.

No inyectar directamente `DbContext` en controllers cuando la funcionalidad corresponda a un repository o service.

No escribir consultas EF Core directamente dentro de controllers para CRUD o funcionalidades que deben seguir el patrón de repository.

---

# Repositories

Mantener el patrón de repositories del proyecto.

Los repositories pueden encargarse de:

* consultas EF Core;
* acceso a datos;
* persistencia;
* filtros;
* ordenamiento;
* paginación;
* composición de DTOs simples;
* comprobación de existencia;
* operaciones CRUD.

Cada repository debe exponer una interfaz cuando ese sea el patrón del área.

Ejemplo:

```csharp id="ao7ldv"
public interface ISorteoRepository
{
}
```

```csharp id="s85fsz"
public class SorteoRepository : ISorteoRepository
{
}
```

Los controllers deben depender de la interfaz, no de la implementación concreta.

---

# Services

Los services deben utilizarse cuando exista una responsabilidad que vaya más allá del acceso directo a datos.

Ejemplos:

* lógica de apartados;
* confirmación de boletos;
* reglas de disponibilidad;
* coordinación entre participante, sorteo y boleto;
* integración con WhatsApp;
* procesos de autenticación;
* generación de documentos;
* procesos transaccionales complejos.

No crear un service simplemente para reenviar cada método de un repository.

Un CRUD sencillo puede utilizar:

```text id="t1ka5u"
Controller → IRepository → Repository → DbContext
```

sin capa de service adicional.

---

# Inyección de dependencias

Todas las dependencias deben registrarse utilizando el contenedor de ASP.NET Core.

Registrar repositories siguiendo el patrón:

```csharp id="krq20u"
builder.Services.AddScoped<ISorteoRepository, SorteoRepository>();
```

Registrar services cuando sean necesarios:

```csharp id="s73vzb"
builder.Services.AddScoped<ISorteoService, SorteoService>();
```

No:

* crear repositories con `new` dentro de controllers;
* crear services con `new`;
* resolver dependencias manualmente con `IServiceProvider`;
* utilizar service locator;
* crear dependencias estáticas para evitar DI.

Verificar siempre que:

* interfaz;
* implementación;
* namespace;
* constructor;
* registro en `Program.cs`;

sean consistentes.

---

# Entity Framework Core

## Regla crítica

Nunca ejecutar automáticamente:

```text id="8gzl0r"
Add-Migration
dotnet ef migrations add
Update-Database
dotnet ef database update
dotnet ef migrations remove
```

Tampoco:

* eliminar migraciones;
* modificar directamente el esquema SQL;
* ejecutar scripts destructivos;

sin autorización explícita del usuario.

Si una tarea requiere cambios de base de datos:

1. explicar qué entidad, tabla o campo necesita cambiar;
2. implementar el cambio en el código cuando la tarea lo autorice;
3. indicar que requiere migración;
4. explicar qué migración sería necesaria;
5. esperar autorización antes de crearla;
6. no ejecutar la migración contra SQL Server salvo autorización específica.

---

# Migraciones automáticas

Si el proyecto utiliza:

```csharp id="9w2loi"
Database.MigrateAsync()
```

como estrategia de aplicación automática de migraciones, conservar ese comportamiento.

No eliminar ni modificar esta estrategia por preferencia.

Una tarea específica puede revisar este comportamiento únicamente si el usuario lo solicita.

---

# Base de datos

SQL Server es la base de datos oficial.

No realizar sin autorización:

* `DROP TABLE`;
* eliminación de columnas;
* cambios destructivos;
* modificaciones masivas de datos;
* SQL manual contra producción;
* cambios de esquema fuera de EF Core;
* eliminación de información.

Los cambios estructurales deben realizarse mediante EF Core y migraciones cuando corresponda.

---

# DbContext

Mantener el `DbContext` central definido por el proyecto.

Cuando se agregue una nueva entidad:

1. crear la entidad en la organización existente;
2. crear su configuración EF Core;
3. agregar su `DbSet<TEntity>`;
4. registrar la configuración según el patrón actual;
5. verificar relaciones;
6. indicar si requiere migración.

No crear un segundo `DbContext` sin una necesidad explícitamente aprobada.

---

# Configuración de entidades

Crear una configuración separada mediante:

```csharp id="okn8pi"
IEntityTypeConfiguration<TEntity>
```

para entidades persistentes cuando ese sea el patrón del proyecto.

Ejemplo:

```csharp id="cdfznb"
public class SorteoConfiguration : IEntityTypeConfiguration<Sorteo>
{
    public void Configure(EntityTypeBuilder<Sorteo> builder)
    {
    }
}
```

Definir explícitamente cuando corresponda:

* tabla;
* clave primaria;
* propiedades requeridas;
* nulabilidad;
* longitudes máximas;
* precisión decimal;
* conversiones de enums;
* valores por defecto;
* índices;
* claves foráneas;
* relaciones;
* restricciones.

No depender únicamente de convenciones cuando exista una regla explícita de dominio o almacenamiento.

---

# Entidades

Las entidades representan información persistente del sistema.

Respetar:

* nulabilidad;
* tipos correctos;
* relaciones;
* enums;
* precisión;
* restricciones.

No utilizar `string` para representar información que naturalmente tenga un tipo más apropiado.

Ejemplos:

* fechas → tipos de fecha;
* cantidades → tipos numéricos;
* dinero → `decimal`;
* estados controlados → enums cuando corresponda.

No modificar tipos existentes sin revisar impacto en:

* EF Core;
* migraciones;
* DTOs;
* frontend;
* contratos API.

---

# DTOs

Utilizar DTOs cuando exista una necesidad real de contrato diferente a la entidad.

Ejemplos:

* login;
* respuestas compuestas;
* información parcial;
* datos agregados;
* requests que no corresponden exactamente a una entidad;
* respuestas que no deben exponer toda la entidad;
* contratos públicos específicos.

Para CRUD sencillos de catálogo, trabajar directamente con la entidad cuando ese sea el patrón y no exista una necesidad real de DTO adicional.

No crear DTOs que únicamente dupliquen todas las propiedades sin aportar valor.

---

# Consultas EF Core

Para consultas de solo lectura:

```csharp id="799ga5"
AsNoTracking()
```

cuando corresponda.

Aplicar:

* filtros en SQL;
* ordenamiento en SQL;
* paginación en SQL;
* proyecciones cuando reduzcan información innecesaria.

Evitar:

```csharp id="5zyw53"
await context.Table.ToListAsync();
```

para después filtrar toda la colección en memoria cuando puede resolverse directamente en SQL.

Mantener consultas legibles.

No realizar optimizaciones prematuras sin evidencia.

---

# LINQ

Preferir LINQ comprensible.

Las operaciones deben ejecutarse en base de datos cuando sea razonable.

Ejemplo correcto:

```csharp id="5s5t19"
await _context.Sorteos
    .AsNoTracking()
    .Where(x => x.Activo)
    .OrderByDescending(x => x.Id)
    .ToListAsync();
```

Evitar materializar datos antes de aplicar filtros.

No crear consultas extremadamente complejas si pueden dividirse de forma clara sin afectar comportamiento.

---

# Paginación estándar de catálogos

Los formularios administrativos de catálogo utilizan el patrón de **un registro por página**.

El backend controla:

* orden;
* posición;
* total de registros.

El frontend no debe descargar toda la colección para navegar localmente.

---

# GET paginado para catálogos

El endpoint de colección debe aceptar:

```text id="0kzsjk"
?page={page}
```

La numeración inicia en:

```text id="lxanl8"
1
```

Una página menor a 1 debe responder:

```text id="xobmry"
400 Bad Request
```

Por defecto, para catálogos ordenados por creación:

```text id="rsdukh"
Página 1 = registro más reciente
```

Ordenar:

```csharp id="195umm"
OrderByDescending(x => x.Id)
```

Para obtener un registro:

```csharp id="apyl4h"
OrderByDescending(x => x.Id)
    .Skip(page - 1)
    .FirstOrDefaultAsync();
```

Obtener también:

```csharp id="fpoxnb"
CountAsync()
```

para conocer el total.

---

# Respuesta paginada

La respuesta estándar debe incluir:

```json id="az1210"
{
  "data": {},
  "totalRecords": 10,
  "page": 1
}
```

`data` puede ser `null` cuando no existan registros.

La respuesta debe permitir que Angular muestre:

```text id="8hcpex"
1 de 10
```

No devolver toda la colección cuando el formulario únicamente necesita el registro actual.

El orden pertenece al backend.

---

# Repository para catálogo paginado

Un repository de catálogo debe incluir métodos explícitos para operaciones como:

```csharp id="pxb152"
GetPageAsync(int page)
GetByIdAsync(int id)
CreateAsync(Entity entity)
UpdateAsync(Entity entity)
DeleteAsync(int id)
```

Los nombres exactos pueden ajustarse a las convenciones existentes.

Las consultas de página deben:

1. validar o recibir página válida;
2. ordenar antes de paginar;
3. utilizar `AsNoTracking()` en lectura;
4. ejecutar `CountAsync()`;
5. evitar cargar toda la tabla.

---

# Endpoints CRUD de catálogo

El contrato estándar es:

```text id="upw29r"
GET    /Catalog?page={page}
GET    /Catalog/{id}
POST   /Catalog
PUT    /Catalog/{id}
DELETE /Catalog/{id}
```

Adaptar `Catalog` al nombre real del controller.

---

# GET colección paginada

```text id="avzp56"
GET /Catalog?page={page}
```

Debe:

* validar `page >= 1`;
* obtener el registro solicitado;
* incluir total;
* incluir página;
* responder `200 OK`.

No devolver la colección completa.

---

# GET por Id

```text id="hscy96"
GET /Catalog/{id}
```

Debe devolver:

```text id="2eq12p"
200 OK
```

cuando exista.

Si no existe:

```text id="oc0cta"
404 Not Found
```

---

# POST

```text id="vl4pzr"
POST /Catalog
```

Debe crear una entidad nueva.

Cuando corresponda, responder:

```text id="l26dre"
201 Created
```

utilizando:

```csharp id="kyvw6r"
CreatedAtAction(...)
```

No utilizar `PUT` para crear registros.

---

# PUT

```text id="gzemm2"
PUT /Catalog/{id}
```

Validar:

```text id="uxos9x"
id de ruta == Id de entidad
```

Si no coincide:

```text id="kkl224"
400 Bad Request
```

Comprobar que la entidad exista.

No permitir que un `PUT` cree accidentalmente una nueva entidad.

Si no existe:

```text id="8lnifn"
404 Not Found
```

Cuando la operación tenga éxito y no necesite body:

```text id="qbn39b"
204 No Content
```

---

# DELETE

```text id="zkzqpd"
DELETE /Catalog/{id}
```

Debe comprobar existencia.

Si no existe:

```text id="8qlcce"
404 Not Found
```

Cuando se elimine correctamente:

```text id="y35ot2"
204 No Content
```

No implementar soft delete automáticamente si la entidad no lo requiere.

---

# Actualización

Para actualizar:

* comprobar que el registro exista;
* evitar insertar accidentalmente;
* modificar únicamente la entidad esperada;
* guardar mediante EF Core;
* devolver un resultado que permita al controller construir el código HTTP adecuado.

No utilizar `Update()` indiscriminadamente si el patrón existente utiliza carga + modificación controlada.

---

# Eliminación

Para eliminar:

1. buscar la entidad;
2. si no existe, devolver un resultado adecuado;
3. eliminar;
4. guardar cambios.

El repository debe permitir al controller distinguir entre:

```text id="jkfwca"
eliminado
no encontrado
```

---

# Sincronización con Angular

Cuando cambie:

* endpoint;
* método HTTP;
* parámetro;
* DTO;
* tipo;
* respuesta paginada;
* código HTTP;

revisar también `eLotto_Front`.

No modificar un contrato consumido por Angular sin mantener ambos proyectos sincronizados cuando la tarea sea integrada.

Después de crear un registro en catálogo, Angular normalmente volverá a consultar:

```text id="azgl4m"
page=1
```

si el nuevo registro se ordena por `Id DESC`.

Después de actualizar, debe poder volver a consultar la misma página.

Backend no debe utilizar el número de página para decidir si una operación es `POST` o `PUT`.

---

# Áreas funcionales

El backend puede contener funcionalidades relacionadas con:

* autenticación;
* usuarios;
* participantes;
* sorteos;
* boletos;
* apartados;
* confirmaciones;
* pagos;
* cuentas de depósito;
* ganadores;
* administración;
* WhatsApp.

No implementar funcionalidad únicamente porque aparezca en esta lista.

Cada área debe desarrollarse cuando la tarea correspondiente lo solicite.

---

# Sorteos

Las reglas que determinen:

* estado;
* disponibilidad;
* apertura;
* cierre;
* ganador;
* vigencia;

deben implementarse principalmente en backend.

Frontend puede mostrarlas, pero no debe ser autoridad.

No inventar reglas sobre cantidad de sorteos activos, fechas o cierre si no han sido definidas.

---

# Boletos

Backend debe ser la autoridad sobre:

* disponibilidad;
* selección persistente;
* apartado;
* confirmación;
* liberación;
* propiedad;
* estado.

No confiar en estados enviados por Angular sin validación.

No permitir que una modificación de frontend determine por sí sola que un boleto se encuentra disponible.

---

# Concurrencia de boletos

Las operaciones sobre boletos deben considerar solicitudes simultáneas.

Caso conceptual:

```text id="t06amr"
Usuario A intenta obtener boleto 123.
Usuario B intenta obtener boleto 123.
```

La base de datos y backend deben garantizar consistencia.

No resolver esta regla exclusivamente mediante:

```text id="xofslm"
if (frontendDiceDisponible)
```

Considerar cuando sea necesario:

* transacciones;
* restricciones;
* validación de estado dentro de la operación;
* concurrencia optimista;
* índices únicos;
* aislamiento transaccional.

La estrategia concreta debe definirse conforme al caso solicitado.

No introducir mecanismos complejos sin necesidad demostrada.

---

# Transacciones

Utilizar transacciones cuando una operación de negocio requiera que múltiples modificaciones sean atómicas.

Ejemplos conceptuales:

* crear apartado y asignar boletos;
* confirmar pago y cambiar estados;
* liberar varios boletos;
* finalizar un proceso compuesto.

No envolver cada operación CRUD sencilla en una transacción manual si `SaveChangesAsync()` ya proporciona la atomicidad necesaria.

---

# Autenticación

Mantener el mecanismo de autenticación existente.

No modificar sin autorización:

* formato JWT;
* duración;
* claims;
* login;
* registro;
* recuperación de contraseña;
* roles;
* refresh tokens;
* políticas.

Una tarea específica de autenticación puede modificar estas áreas únicamente según lo solicitado.

---

# Autorización

Los endpoints sensibles deben protegerse desde backend.

No confiar en que Angular oculte opciones.

Utilizar:

```text id="0cp5gx"
[Authorize]
```

y políticas/roles cuando corresponda a la implementación existente.

No agregar roles o políticas nuevas sin requisito funcional.

---

# WhatsApp

La integración con WhatsApp debe implementarse en backend.

No exponer:

* API keys;
* tokens;
* secretos;
* credenciales;

al frontend.

Cuando la integración contenga suficiente lógica, encapsularla mediante un service.

Ejemplo conceptual:

```text id="3ng9he"
IWhatsAppService
WhatsAppService
```

No llamar proveedores externos directamente desde controllers si existe lógica reutilizable o configuración sensible.

---

# Integraciones externas

Las integraciones externas deben permanecer detrás de services cuando sea razonable.

Ejemplos:

* WhatsApp;
* almacenamiento;
* generación de documentos;
* servicios de pago;
* APIs de terceros.

Controllers no deben conocer detalles internos del proveedor.

---

# Secretos y configuración sensible

Nunca mostrar, copiar o exponer:

* connection strings;
* passwords;
* JWT secrets;
* API keys;
* tokens;
* credenciales;
* secretos de `appsettings.json`.

No repetir secretos encontrados en código o configuración.

Si se detecta un secreto expuesto, reportarlo sin copiar su valor.

No modificar secretos sin autorización.

---

# Manejo de errores

No exponer información sensible en respuestas.

Evitar devolver:

* connection strings;
* SQL interno;
* stack traces;
* secretos;
* excepciones completas.

Mantener códigos HTTP adecuados.

No reconstruir globalmente el manejo de excepciones durante una tarea no relacionada.

---

# Fechas y horarios

Mantener una estrategia consistente.

No cambiar globalmente UTC/hora local durante una tarea no relacionada.

Cuando una funcionalidad nueva necesite una decisión de zona horaria y el proyecto no tenga una regla definida, no introducir conversiones implícitas.

Reportar la necesidad de definición antes de aplicar una estrategia global.

---

# Dinero

Para cantidades monetarias utilizar:

```csharp id="tql896"
decimal
```

con precisión configurada mediante EF Core.

No utilizar `float` o `double` para dinero.

La precisión SQL debe declararse cuando corresponda.

Ejemplo:

```csharp id="4vm78w"
builder.Property(x => x.Price)
    .HasPrecision(18, 2);
```

La precisión exacta puede variar según la regla de negocio.

---

# Enums

Utilizar enums cuando representen un conjunto cerrado y claro de estados.

Definir su persistencia explícitamente cuando sea importante.

No cambiar valores numéricos de enums persistidos sin revisar compatibilidad con base de datos.

No convertir automáticamente todos los campos de estado a enums si el contrato existente utiliza otro esquema.

---

# Código asíncrono

Para operaciones I/O utilizar APIs async cuando estén disponibles.

Preferir:

```csharp id="efpleu"
ToListAsync()
FirstOrDefaultAsync()
CountAsync()
SaveChangesAsync()
```

No bloquear código asíncrono utilizando:

```csharp id="sn1ogu"
.Result
.Wait()
```

salvo una necesidad técnica excepcional.

Propagar `async/await` adecuadamente.

---

# CancellationToken

Cuando el patrón existente lo utilice, propagar `CancellationToken` en operaciones HTTP y EF Core.

No introducirlo de forma parcial o inconsistente únicamente por preferencia.

---

# Código existente

No borrar código funcional simplemente porque:

* pueda escribirse de otra manera;
* exista una alternativa más moderna;
* parezca redundante;
* pueda refactorizarse.

Una tarea debe modificar únicamente lo requerido.

Reportar oportunidades de mejora fuera del alcance.

---

# Dependencias

Prohibido sin autorización explícita:

* instalar NuGet packages;
* eliminar paquetes;
* actualizar paquetes;
* cambiar versión de EF Core;
* cambiar versión de .NET.

Si una dependencia parece innecesaria o desactualizada, reportarla.

No modificarla como efecto secundario.

---

# Estilo de código

Mantener el estilo predominante.

Preferir:

* nombres descriptivos;
* métodos pequeños;
* código legible;
* DTOs explícitos cuando aporten valor;
* LINQ comprensible;
* separación clara de responsabilidades.

Evitar:

* `dynamic`;
* métodos excesivamente grandes;
* lógica innecesariamente compleja;
* comentarios que simplemente repitan el código.

No reformatear archivos completos por una modificación pequeña.

---

# Antes de modificar código

Siempre:

1. leer el `AGENTS.md` raíz;
2. leer este `AGENTS.md`;
3. analizar archivos relacionados;
4. revisar repository/service correspondiente;
5. identificar dependencias con frontend;
6. identificar impacto de base de datos;
7. identificar si requiere migración;
8. explicar brevemente el plan;
9. modificar únicamente lo necesario.

---

# Después de modificar código

Siempre indicar:

1. archivos modificados;
2. qué cambió en cada archivo;
3. endpoints afectados;
4. DTOs afectados;
5. repositories afectados;
6. services afectados;
7. registros de DI agregados o modificados;
8. si requiere migración;
9. si la migración fue creada o no;
10. riesgos;
11. pruebas manuales recomendadas;
12. resultado de `dotnet build`.

Para endpoints indicar:

```text id="fe3kwu"
MÉTODO /ruta
```

Ejemplo:

```text id="fl1v32"
GET /Sorteos?page=1
```

Si no aplica:

```text id="4dm96a"
Endpoint afectado: No aplica.
```

---

# Compilación

Después de modificar código C# ejecutar:

```bash id="1lfia8"
dotnet build
```

cuando sea posible.

No corregir automáticamente errores o warnings no relacionados.

Si falla por un problema previo:

* reportarlo;
* distinguirlo de errores provocados por la tarea.

---

# Verificación de catálogos

Antes de finalizar un catálogo:

1. confirmar entidad;
2. confirmar configuración EF Core;
3. confirmar `DbSet`;
4. confirmar repository;
5. confirmar interfaz;
6. confirmar métodos utilizados;
7. confirmar registro DI;
8. confirmar que controller dependa de interfaz;
9. confirmar que controller no inyecte `DbContext`;
10. confirmar paginación;
11. confirmar códigos HTTP;
12. confirmar contrato Angular;
13. ejecutar `dotnet build`;
14. indicar explícitamente si requiere migración.

---

# Alcance de tareas

Una tarea debe resolver únicamente el problema solicitado.

Si la tarea es:

```text id="hdeork"
Crear catálogo de cuentas bancarias
```

no aprovechar para:

* cambiar autenticación;
* actualizar EF Core;
* reorganizar carpetas;
* cambiar CORS;
* modificar manejo global de fechas;
* crear nuevas capas;
* limpiar otros controllers;
* refactorizar repositories no relacionados.

---

# Filosofía del proyecto

eLotto debe evolucionar incrementalmente.

Preferimos:

* soluciones simples;
* comportamiento predecible;
* reglas explícitas;
* cambios pequeños;
* buena separación de responsabilidades;
* uso consistente de repositories y services;
* contratos claros con Angular;

antes que grandes refactors o abstracciones innecesarias.

---

# Regla de arquitectura final

Para una funcionalidad CRUD sencilla:

```text id="7bpq2m"
Controller
    ↓
IRepository
    ↓
Repository
    ↓
DbContext
```

Para una funcionalidad con lógica real:

```text id="edx3rn"
Controller
    ↓
IService
    ↓
Service
    ↓
IRepository(s)
    ↓
Repository(s)
    ↓
DbContext
```

No omitir repository accediendo directamente al `DbContext` desde controller.

No introducir service si únicamente repetiría los métodos del repository.

La arquitectura debe reflejar la complejidad real del caso de uso, no agregar capas por rutina.

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
