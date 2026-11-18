# Copia Segura

Programa portable para Windows que guía paso a paso la copia de una carpeta (equipo A) a un disco externo (disco B) usando `robocopy.exe`, con cada opción explicada, verificación posterior e informe opcional. Pensado para mantenimientos y migraciones de equipos.

- **Sin instalación:** un `.exe` de ~90 KB que no toca el registro.
- **Seguro por defecto:** el origen solo se lee; no se borra nada que no esté en el origen; las opciones peligrosas (`/MIR`, `/PURGE`, `/MOVE`) están bloqueadas hasta escribir `BORRAR`.
- **Windows 7 SP1, 10 y 11.** Windows XP no está soportado.

## Descarga

**https://yanker.github.io/Robocopy-Mtno/** · descarga directa: [`CopiaSegura.zip`](https://yanker.github.io/Robocopy-Mtno/CopiaSegura.zip)

La web vive en la rama `gh-pages` (se actualiza con `./publicar.ps1`).

## Uso

1. Ejecuta `CopiaSegura.exe` (déjalo junto a `CopiaSegura.exe.config`).
2. **Origen y destino:** elige las carpetas; el programa comprueba espacio, anidamiento y legibilidad.
3. **Opciones:** perfil *Copia segura* (por defecto) o *Copia rápida*; opción *Disco dudoso* para discos con sospecha de fallo.
4. **Revisar:** resumen en lenguaje llano y el comando exacto que se ejecutará.
5. **Copiando:** progreso total y por archivo, velocidad, tiempo restante, pausar, reanudar y cancelar.
6. **Verificar y terminar:** verificación rápida (cantidad, tamaño y fecha) o profunda (SHA-256), reintento de los fallidos e informe HTML opcional.

> Terminar la copia no significa que esté verificada. Conserva el origen hasta verificar y haz una segunda copia en otro soporte.

## Comando base

```
robocopy "ORIGEN" "DESTINO" /E /Z /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:2 /MT:2 /TEE /BYTES /NDL /UNILOG+:"registro.txt"
```

`/BYTES /NDL` permiten calcular el progreso; `/UNILOG+` guarda el registro en Unicode y lo añade al anterior si se reanuda.

## Distribución de carpetas

```
ROBOCOPY/
├── README.md                  Este archivo
├── build.ps1                  Compila, ejecuta las pruebas y empaqueta
├── publicar.ps1               Compila y publica zip + web en la rama gh-pages
├── .gitignore                 Ignora dist/, out/ y tools/refasm/ (se generan solos)
├── maqueta.html               Maqueta visual aprobada (referencia del diseño)
├── site/                      Web de descarga (GitHub Pages)
│   ├── index.html                 Plantilla ({{FECHA}}, {{TAMANO}}, {{SHA256}})
│   ├── favicon.svg                Logo vectorial
│   ├── generar-iconos.ps1         Genera src\icono.ico y img\logo-512.png
│   └── img/                       Logo y capturas reales de cada paso
├── copia-segura-robocopy.md   Conversación original que dio origen al proyecto
│
├── src/                       Código fuente de la aplicación
│   ├── Program.cs             Punto de entrada (--smoke y --shots para pruebas)
│   ├── app.manifest           Ejecución sin administrador; compatible Windows 7-10
│   ├── icono.ico              Icono del programa (el logo)
│   ├── CopiaSegura.exe.config Usa .NET 4.x si existe; si no, .NET 3.5 (Windows 7)
│   ├── LEEME.txt              Instrucciones que viajan dentro del paquete
│   ├── Core/                  Lógica sin interfaz (es lo que prueban los tests)
│   │   ├── CopyOptions.cs         Opciones, perfiles y bloqueo de opciones peligrosas
│   │   ├── CommandBuilder.cs      Construye el comando de robocopy (rutas y comillas seguras)
│   │   ├── PathValidator.cs       Comprobaciones de origen/destino y espacio
│   │   ├── TreeScanner.cs         Recorre el origen contando archivos y bytes
│   │   ├── ProgressParser.cs      Interpreta la salida de robocopy (cualquier idioma)
│   │   ├── RobocopyRunner.cs      Ejecuta robocopy, para/reanuda y resume el resultado
│   │   ├── Verifier.cs            Verificación rápida y SHA-256
│   │   ├── ReportWriter.cs        Informe HTML
│   │   ├── Settings.cs            Ajustes portables en datos\
│   │   └── HelpTexts.cs           Explicación en español de cada opción
│   └── UI/                    Interfaz WinForms: asistente de 5 pasos
│       ├── MainForm.cs            Ventana, cabecera de pasos y navegación validada
│       ├── PageSource.cs          Paso 1 · origen y destino
│       ├── PageOptions.cs         Paso 2 · opciones
│       ├── PageReview.cs          Paso 3 · revisar
│       ├── PageProgress.cs        Paso 4 · copiando
│       ├── PageVerify.cs          Paso 5 · verificar, reintentar e informe
│       ├── WizardState.cs         Estado compartido entre pasos
│       ├── UiKit.cs               Colores, fuentes y controles auxiliares
│       └── SmokeTest.cs           Prueba de humo y capturas de pantalla
│
├── tests/                     Pruebas automáticas (67)
│   ├── Harness.cs                 Mini-ejecutor de pruebas propio ([Test], Check)
│   ├── Tmp.cs                     Utilidades: carpetas temporales, hashes
│   └── *Tests.cs                  Una clase de pruebas por componente de Core
│
├── docs/superpowers/          Documentación de diseño
│   ├── specs/                     Documento de diseño aprobado
│   └── plans/                     Plan de implementación y registro de decisiones (progress.md)
│
├── dist/                      GENERADO · salida de la compilación
│   ├── CopiaSegura/               .exe + .config recién compilados
│   ├── pkg/CopiaSegura/           Carpeta portable limpia que se comprime
│   └── CopiaSegura.zip            Paquete para distribuir (exe + config + LEEME)
├── out/                       GENERADO · Tests.exe y capturas temporales
└── tools/refasm/              GENERADO · referencias .NET 3.5 descargadas una vez
```

Las carpetas `dist/`, `out/` y `tools/` se generan con `build.ps1`; se pueden borrar y se recrean solas.

## Compilar y probar

Requisitos: Windows con .NET Framework 4.x (trae `csc.exe`) y conexión la primera vez para descargar las referencias de .NET 3.5 (paquete NuGet oficial de Microsoft, solo se usa para compilar).

```powershell
./build.ps1                       # compila, ejecuta las 67 pruebas y la prueba de humo
./build.ps1 -TestFilter Verifier  # solo las pruebas cuyo nombre contenga "Verifier"
./build.ps1 -Dist                 # además genera dist\CopiaSegura.zip
```

Para ver el aspecto de cada pantalla sin abrir la aplicación a mano (así se generan las capturas de la web):

```powershell
dist\CopiaSegura\CopiaSegura.exe --shots site\img
```

Para publicar una nueva versión de la descarga:

```powershell
./publicar.ps1    # compila, prueba, empaqueta y sube zip + web a la rama gh-pages
```

## Detalles técnicos

- C# 5 compilado contra .NET Framework 3.5: Windows 7 lo trae de serie; en Windows 10/11 el `.config` hace que use el .NET 4.x del sistema.
- Dentro de `src/Core` no hay referencias a la interfaz, y los tests solo compilan esa carpeta.
- Los ajustes viven en `datos\` junto al `.exe`; si esa carpeta es de solo lectura se usa `%TEMP%\CopiaSegura_datos`.

## Licencia

[MIT-0](LICENSE) (MIT sin atribución): úsalo, cópialo, modifícalo, véndelo o redistribúyelo como quieras, sin ninguna condición y sin tener que citar a nadie. Se entrega «tal cual», sin garantía: **antes de fiarte de una copia, verifícala**.

El programa llama al `robocopy.exe` que ya trae Windows (de Microsoft); no lo incluye ni lo redistribuye.

## Limitaciones conocidas

- Rutas de más de 260 caracteres: Robocopy las copia, pero el programa no puede recorrerlas ni verificarlas.
- Si ejecutas el programa desde dentro de la carpeta de origen, `datos\` se crea allí.
- Los enlaces simbólicos pueden aparecer como «FALTA» en la verificación (la copia los omite con `/XJ`).
- Con varios hilos (`/MT`) Robocopy puede no mostrar el avance por archivo.
- Si el destino ya tiene archivos con el mismo nombre, se sustituyen por los del origen.
- Pendiente de comprobar en un Windows 7 SP1 real.
