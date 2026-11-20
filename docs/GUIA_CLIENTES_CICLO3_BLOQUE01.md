# Guía del bloque 01 de clientes

## Qué recibirás

Contratos y servicios base de Application, un adaptador al hash de identidad
existente y 110 casos unitarios preparados. También se incluyen el documento de
contratos y la solicitud de dependencias al DBA. No se añaden pantallas todavía.

## Copiar en Windows

1. Detén Web y API si están ejecutándose.
2. Descomprime el ZIP en la raíz PortalMercado, junto al archivo de solución.
3. Las carpetas Application, Infrastructure, Tests y docs deben quedar dentro de
   la solución, no en una subcarpeta adicional PortalMercado/PortalMercado.
4. No ejecutes ningún script SQL nuevo: este bloque no contiene scripts.

Los proyectos SDK incluyen automáticamente los .cs nuevos; no se necesita editar
los .csproj. No se modifican Program.cs, controladores, vistas ni el esquema.
El registro de estos servicios en DI y la selección de proveedores se incorporarán
cuando exista una implementación utilizable; no añadirlos manualmente todavía.

## Rama de trabajo

Desde Git Bash, comprueba tu rama y cambios:

```bash
git branch --show-current
git status --short
```

Si aún no creaste la rama del ciclo, créala desde tu versión actual del ciclo 2:

```bash
git switch -c feature/b-clientes-portal-ciclo3
```

Si la rama ya existe, usa git switch feature/b-clientes-portal-ciclo3 en lugar de
volver a crearla. Conserva tus cambios y revisa git status antes de cambiar de rama.

## Ejecutar el bloque

En Git Bash, desde la raíz PortalMercado:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque01.trx"
```

Importante: el filtro incluye ~. Sin ese operador no selecciona los casos previstos.
No necesitas activar variables PM_RUN_* para estos casos: son unitarios, no SQL.
Se han preparado 110 casos. Revisar que realmente se descubran y que no haya
pruebas fallidas u omitidas. Guardar la salida y el TRX original.

El entorno de elaboración no dispone de dotnet y no se ejecutó esta suite aquí.
Si aparece un error, compartir el primer mensaje y nombre de archivo o prueba,
además del resumen. No basta con el final de la pila de llamadas.

## Qué debería cambiar al abrir la Web

Las pantallas se mantienen: este bloque no activa registro ni perfil del comprador.
El bloque 02 añadirá portada pública y búsqueda global reutilizando tiendas y
catálogo. Luego se conectarán registro/login, perfil, direcciones y consentimientos.
No se llama a un repositorio inexistente ni se presenta una cuenta de prueba como
registro persistido.

El comando de arranque SQL local de tiendas del ciclo anterior sigue siendo el
mismo; no es una configuración nueva de clientes.

## Registro de cambios

- Contratos cerrados para registro, perfil, dirección y consentimiento.
- Validación de capacidades contra el esquema original y retorno local seguro.
- Registro con política de identidad existente y versiones de documentos del servidor.
- Perfil limitado a la cuenta autenticada y comprobación de propiedad.
- Adaptador IPasswordHasher<Usuario> y pruebas base.
- Dependencias y decisiones pendientes documentadas, sin cambios SQL.
