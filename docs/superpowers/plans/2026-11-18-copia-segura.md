# Copia Segura Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Programa portable de Windows (WinForms, .NET 3.5) que guía una copia segura de una carpeta A a un disco B con `robocopy.exe`, con opciones explicadas, verificación e informe opcional.

**Architecture:** `src\Core` (lógica pura y testeable: opciones, comando, validación, escaneo, parser de progreso, runner de robocopy, verificador, ajustes, informe, textos de ayuda) + `src\UI` (asistente WinForms de 5 pasos que consume Core). Compilado con el `csc.exe` de Windows contra las referencias de .NET 3.5; un `.config` hace que en Windows 10/11 arranque con el .NET 4.x del sistema.

**Tech Stack:** C# 5 (csc de `C:\Windows\Microsoft.NET\Framework64\v4.0.30319`), referencias `Microsoft.NETFramework.ReferenceAssemblies.net35` (NuGet, solo para compilar), WinForms, mini-ejecutor de pruebas propio, `robocopy.exe` del sistema.

**Spec:** `docs/superpowers/specs/2026-11-18-copia-segura-design.md` · Maqueta visual aprobada: `maqueta.html` (colores, textos y orden de los 5 pasos).

## Global Constraints

- Plataformas: Windows 7 SP1, 10 y 11. Destino de compilación **.NET Framework 3.5**: nada de `Directory.EnumerateFiles`, `Task`, `string.IsNullOrWhiteSpace`, `Stream.CopyTo`, `Enum.HasFlag`, `Tuple`, `Lazy`. Lenguaje **C# 5**: sin interpolación de cadenas, sin `?.`, sin miembros con cuerpo de expresión.
- Portable: sin instalador ni registro; ajustes y logs en `datos\` junto al `.exe`. Salida: `dist\CopiaSegura\` con `CopiaSegura.exe` + `CopiaSegura.exe.config` (~1 MB).
- `.config` con `<supportedRuntime version="v4.0"/>` primero y `v2.0.50727` después.
- Manifest `asInvoker` (no pide admin por defecto); botón opcional "Reiniciar como administrador".
- Interfaz y textos en español.
- El origen es **solo lectura**: el programa nunca escribe en él (ni logs ni temporales).
- `/MIR`, `/PURGE`, `/MOVE`: bloqueadas por defecto; desbloqueo solo escribiendo `BORRAR`.
- Comando base de la copia segura: `robocopy "ORIGEN" "DESTINO" /E /Z /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:2 /MT:2 /TEE /LOG:"..."`. Para poder calcular el progreso se añaden `/BYTES /NDL` (aparecen en la vista previa; el comando mostrado es exactamente el que se ejecuta).
- Hilos por defecto 2 (suave); modo disco dudoso: 1 hilo, 1 reintento. SHA-256 en secuencial.
- No hay repositorio git en el proyecto: el punto de control de cada tarea es `build.ps1` con todas las pruebas en verde (no hay pasos de commit).

## Review Focus

1. Origen en raíz de disco (`E:\`): la barra final rompe el entrecomillado de robocopy (`"E:\"`) y se cuelan `System Volume Information` y `$RECYCLE.BIN` → Task 2.
2. Rutas con espacios, `&`, acentos y `ñ` (Unicode) de punta a punta, sin corrupción → Tasks 5 y 6.
3. Subcarpeta ilegible o ruta de más de 260 caracteres durante el escaneo: no cierra el programa, se informa → Task 3.
4. Origen vacío (0 bytes): progreso sin división por cero y mensaje "nada que copiar" → Task 4.
5. Disco desconectado o origen que desaparece a mitad: código de salida ≥ 8 se muestra como fallo claro, nunca como "completada" → Task 5.

---

### Task 1: Andamiaje de compilación y mini-ejecutor de pruebas

**Files:**
- Create: `build.ps1`, `src\Program.cs`, `src\app.manifest`, `src\CopiaSegura.exe.config`, `tests\Harness.cs`, `tests\HarnessTests.cs`
- Directorios generados (no editar): `tools\refasm\`, `out\`, `dist\`

**Interfaces:**
- Produces: `build.ps1 [-TestFilter <texto>] [-Dist]`. Sin parámetros: descarga (una sola vez) el paquete NuGet `Microsoft.NETFramework.ReferenceAssemblies.net35` a `tools\refasm`, compila `out\Tests.exe` (todos los `tests\*.cs` + `src\Core\*.cs`) y `dist\CopiaSegura\CopiaSegura.exe` (todos los `src\**\*.cs`, `/target:winexe`, `/nostdlib /noconfig`, referencias solo a .NET 3.5: mscorlib, System, System.Core, System.Windows.Forms, System.Drawing), copia el `.config` junto a cada exe (`Tests.exe.config` también), ejecuta `Tests.exe [filtro]` y sale con código ≠ 0 si algo falla. Última línea de la salida: `PASSED n/n`.
- Produces: `tests\Harness.cs`: `[Test]` (atributo de método, `namespace CopiaSegura.Tests`), `static class Check { Equal(object expected, object actual, string msg = ""); True(bool, string msg = ""); Throws<T>(Action a) where T : Exception; }`, y `Main` que descubre por reflexión los métodos `[Test]` estáticos, filtra por substring del nombre `Clase.Metodo`, imprime `FAIL Clase.Metodo: detalle` y devuelve 1 si hay fallos.
- Produces: `src\Program.cs` con `Main(string[] args)`: con `--smoke` devuelve 0 sin abrir ventana (se amplía en Task 8).

- [ ] **Step 1: Escribir `tests\HarnessTests.cs`** con `[Test] Sanity_AddsUp` (`Check.Equal(4, 2+2)`) y `[Test] Check_Throws_DetectsException`.
- [ ] **Step 2: Ejecutar `./build.ps1`; verificar que falla** (aún no existen `Harness.cs` ni `build.ps1` completo).
- [ ] **Step 3: Implementar `build.ps1`, `Harness.cs`, `Program.cs`, `app.manifest` (asInvoker + supportedOS de Vista/7/8/8.1/10), `.config`.** Si la descarga NuGet falla, el script debe detenerse con un mensaje claro (URL: `https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net35`; verificar la versión disponible; el `.nupkg` es un zip con `build\.NETFramework\v3.5\*.dll`).
- [ ] **Step 4: Ejecutar `./build.ps1`** → esperado: `PASSED 2/2`; luego `dist\CopiaSegura\CopiaSegura.exe --smoke; $LASTEXITCODE` → `0`.

---

### Task 2: Opciones, perfiles y constructor del comando

**Files:**
- Create: `src\Core\CopyOptions.cs`, `src\Core\CommandBuilder.cs`
- Test: `tests\CommandBuilderTests.cs`

**Interfaces:**
- Produces (`namespace CopiaSegura.Core`):
  - `enum Profile { Segura, Rapida }`
  - `class CopyOptions` con propiedades públicas: `string Source, Destination, LogPath; bool IncludeSubfolders, PreserveDates, SkipJunctions, Restartable, WriteLog, DoubtfulDisk; int Retries, WaitSeconds, Threads; bool Mirror {get;} bool Move {get;} bool DangerousUnlocked {get;}`.
  - `static CopyOptions ForProfile(Profile p, string source, string destination)`; `bool TryUnlockDangerous(string phrase)` (true solo si `phrase == "BORRAR"` exacto); `void SetMirror(bool)` y `void SetMove(bool)` → `InvalidOperationException` si no está desbloqueado; `void ApplyDoubtfulDisk()` (Threads=1, Retries=1, WaitSeconds=2, Restartable=true, DoubtfulDisk=true).
  - Valores: **Segura** = E, DAT, XJ, Z, R1, W2, MT2, WriteLog. **Rápida** = E, DAT, XJ, sin Z, R0, W0, MT8, WriteLog.
  - `LogPath` por defecto: `<destino sin barra final>_log.txt` (p. ej. `D:\Respaldo_Alejandro_log.txt`); si el destino es una raíz (`D:\`) → `D:\CopiaSegura_log.txt`.
  - `static class CommandBuilder`: `string BuildArguments(CopyOptions o)`, `string Quote(string path)`, `bool IsDriveRoot(string path)`, `string[] RootExcludedDirs` (`"System Volume Information"`, `"$RECYCLE.BIN"`).

- [ ] **Step 1: Escribir tests** (`CommandBuilder.*`):
  - `Segura_ExactCommand`: origen `E:\Usuarios\Alejandro`, destino `D:\Respaldo_Alejandro` → `"E:\Usuarios\Alejandro" "D:\Respaldo_Alejandro" /E /Z /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:2 /MT:2 /TEE /BYTES /NDL /LOG:"D:\Respaldo_Alejandro_log.txt"`.
  - `Rapida_NoRestartable_8Threads_R0`: contiene `/MT:8 /R:0 /W:0` y no contiene `/Z`.
  - `DoubtfulDisk_1Thread_1Retry`.
  - `DriveRootSource_QuotedWithDot_AndExcludesSystemDirs`: origen `E:\` → contiene `"E:\."` y `/XD "System Volume Information" "$RECYCLE.BIN"` (justo tras `/XJ`); `Quote(@"D:\a b")` = `"D:\a b"`; `Quote` con `"` dentro → `ArgumentException`.
  - `Dangerous_LockedByDefault`: `SetMirror(true)` lanza; `TryUnlockDangerous("borrar")` es false; tras `TryUnlockDangerous("BORRAR")` `SetMirror(true)` añade `/MIR` y `SetMove(true)` añade `/MOVE` (antes de `/LOG`); `Mirror` y `Move` son false en cualquier perfil recién creado.
  - `LogPath_RootDestination`.
- [ ] **Step 2: Ejecutar `./build.ps1 -TestFilter CommandBuilder`** → FAIL (compilación o pruebas).
- [ ] **Step 3: Implementar `CopyOptions` y `CommandBuilder`** según las firmas. Orden de banderas: `/E /Z /COPY:DAT /DCOPY:DAT /XJ [/XD …] /R:n /W:n /MT:n /TEE /BYTES /NDL [/MIR] [/MOVE] /LOG:"…"`, omitiendo las desactivadas.
- [ ] **Step 4: Ejecutar `./build.ps1 -TestFilter CommandBuilder`** → todo PASSED.

---

### Task 3: Validación de rutas y escaneo del origen

**Files:**
- Create: `src\Core\PathValidator.cs`, `src\Core\TreeScanner.cs`
- Test: `tests\PathValidatorTests.cs`, `tests\TreeScannerTests.cs`

**Interfaces:**
- Consumes: `CommandBuilder.IsDriveRoot`, `CommandBuilder.RootExcludedDirs`.
- Produces:
  - `enum Severity { Ok, Warning, Error }`; `class CheckResult { Severity Severity; string Message; }`.
  - `static List<CheckResult> PathValidator.Validate(string source, string destination, long sourceBytes, long destFreeBytes)` (−1 = desconocido). Reglas: vacío o inexistente → Error; destino igual al origen o dentro de él → Error; origen dentro del destino → Warning; misma unidad → Warning; espacio libre < bytes → Error, < bytes×1,1 → Warning; todo bien → al menos un resultado `Ok` por comprobación. Comparación sin distinguir mayúsculas y sin barra final. Mensajes en español.
  - `static long PathValidator.GetFreeBytes(string path)` (−1 si falla, p. ej. ruta de red).
  - `class ScanResult { long Files; long Bytes; List<string> Unreadable; }`; `static ScanResult TreeScanner.Scan(string root, bool skipJunctions, Func<bool> cancel, Action<long, long> progress)` (`cancel`/`progress` pueden ser null). Salta puntos de reparación si `skipJunctions`; excluye `RootExcludedDirs` si `root` es raíz de disco.

- [ ] **Step 1: Escribir tests:** `PathValidator.DestinationInsideSource_IsError`, `SameFolder_IsError`, `NotEnoughSpace_IsError`, `TightSpace_IsWarning`, `Healthy_NoErrors`; `TreeScanner.CountsFilesAndBytes` (árbol temporal 3 archivos), `UnreadableFolder_ReportedNotThrown` (carpeta con ACL denegada a `Everyone` creada en el test y restaurada al final), `LongPath_ReportedNotThrown` (ruta >260 vía prefijo `\\?\` creada con `cmd /c mkdir`; el escáner no debe lanzar), `Cancel_StopsEarly`.
- [ ] **Step 2: Ejecutar `./build.ps1 -TestFilter Path` y `-TestFilter TreeScanner`** → FAIL.
- [ ] **Step 3: Implementar** ambas clases. Escaneo recursivo con `Directory.GetFiles/GetDirectories` por carpeta dentro de `try/catch` (UnauthorizedAccessException, PathTooLongException, IOException → `Unreadable`).
- [ ] **Step 4: Ejecutar las pruebas** → PASSED.

---

### Task 4: Parser de la salida de robocopy y seguimiento del progreso

**Files:**
- Create: `src\Core\ProgressParser.cs`
- Test: `tests\ProgressParserTests.cs`

**Interfaces:**
- Produces:
  - `enum LineKind { Other, FileStart, Percent, Error }`; `class ParsedLine { LineKind Kind; long FileSize; string Path; double Percent; string Text; }`.
  - `static ParsedLine ProgressParser.Parse(string line)`. Independiente del idioma de robocopy: archivo = `<etiqueta no numérica><tab(s)><tamaño entero><tab><ruta>`; porcentaje = `^\s*\d{1,3}([.,]\d+)?%\s*$`; error = línea que contiene `ERROR <n> (0x`.
  - `class ProgressTracker`: `ProgressTracker(long totalBytes)`, `void Feed(ParsedLine l)`, `void Finish()`, propiedades de solo lectura `long ProcessedBytes; double TotalPercent` (0–100, 0 si `totalBytes <= 0`); `string CurrentFile; long CurrentFileSize; double CurrentPercent; int ErrorCount; bool NothingToCopy` (true si `totalBytes == 0`).

- [ ] **Step 1: Escribir tests:** `Parse_SpanishNewFile` con `"\t\t    Nuevo arch  \t\t10619234816\tE:\\Usuarios\\Alejandro\\AppData\\backup_grande.dat"` → FileStart, FileSize 10619234816, Path correcto; `Parse_EnglishNewFile` (`New File`); `Parse_PercentLine` (`"  45,3%"` y `"100%"`); `Parse_ErrorLine` (`2026/11/18 10:00:00 ERROR 5 (0x00000005) Copying File E:\x.tmp`); `Parse_Garbage_IsOther`; `Tracker_TwoFiles_ReachesHundred` (totales 100+300 bytes: 50 % del primero → 12,5 %; tras `Finish()` → 100 %); `Tracker_ZeroTotal_NoDivideByZero_NothingToCopy`.
- [ ] **Step 2: Ejecutar `./build.ps1 -TestFilter ProgressParser`** → FAIL.
- [ ] **Step 3: Implementar.** Un nuevo `FileStart` cierra el archivo anterior (suma su tamaño completo a lo ya procesado); `Percent` actualiza `CurrentPercent`; `TotalPercent` acotado a 100.
- [ ] **Step 4: Ejecutar** → PASSED.

---

### Task 5: Ejecución de robocopy

**Files:**
- Create: `src\Core\RobocopyRunner.cs`
- Test: `tests\RobocopyRunnerTests.cs`

**Interfaces:**
- Consumes: `CopyOptions`, `CommandBuilder.BuildArguments`, `ProgressParser`, `ProgressTracker`.
- Produces:
  - `class RunResult { int ExitCode; bool StoppedByUser; int ErrorCount; List<string> ErrorLines; bool Success { get; } }` — `Success` = `ExitCode < 8 && !StoppedByUser`; `string Describe()` devuelve texto claro en español (0 = "Nada que copiar: ya estaba todo", 1 = "Copia completada", 2/3 = "Completada con archivos extra en el destino", 4–7 = "Completada con diferencias", ≥8 = "FALLÓ: hubo errores, revisa el registro", `StoppedByUser` = "Detenida por el usuario").
  - `class RobocopyRunner`: `static string FindRobocopy()` (`%SystemRoot%\System32\robocopy.exe`, lanza `FileNotFoundException` si no existe); `event Action<string> OutputLine`; `event Action<ProgressTracker> ProgressChanged`; `RunResult Run(CopyOptions o, long totalBytes)` (bloqueante, para hilo de trabajo; separa la salida por `\r` y `\n`; codificación = página OEM de la cultura actual); `void Stop()` (mata el proceso; seguro llamarlo antes de `Run` o varias veces). Pausar = `Stop()` y reanudar = volver a llamar a `Run` (robocopy omite lo ya copiado).

- [ ] **Step 1: Escribir tests** (usan robocopy real y carpetas temporales):
  - `Run_CopiesTree_SourceUntouched`: árbol con carpeta `Ñandú & Co áé`, archivo con espacios y uno de 5 MB; perfil Segura; verificar `Success`, `ExitCode == 1`, `ProgressChanged` llegó a `TotalPercent == 100`, contenido idéntico en destino y hash SHA-256 del origen igual antes y después (el origen no se modifica ni aparecen archivos nuevos en él).
  - `Run_MissingSource_IsFailureNotSuccess`: origen inexistente → `ExitCode >= 8`, `Success == false`, `Describe()` contiene `FALLÓ`.
  - `Run_EmptySource_NothingToCopy`: carpeta vacía → no lanza, `Success == true`.
  - `Stop_BeforeRun_DoesNotThrow`.
- [ ] **Step 2: Ejecutar `./build.ps1 -TestFilter RobocopyRunner`** → FAIL.
- [ ] **Step 3: Implementar** con `Process`, `RedirectStandardOutput`, `UseShellExecute=false`, `CreateNoWindow=true`; lectura carácter a carácter para cortar líneas por `\r`/`\n`; cada línea → `ProgressParser.Parse` → `tracker.Feed`; al terminar `tracker.Finish()`.
- [ ] **Step 4: Ejecutar** → PASSED.

---

### Task 6: Verificación de la copia

**Files:**
- Create: `src\Core\Verifier.cs`
- Test: `tests\VerifierTests.cs`

**Interfaces:**
- Produces: `enum VerifyMode { Quick, Sha256 }`; `class VerifyResult { long FilesChecked; List<string> Missing, SizeMismatch, HashMismatch, Unreadable; bool Cancelled; bool Ok { get; } }` (`Ok` = todas las listas vacías y no cancelada); `static VerifyResult Verifier.Verify(string source, string destination, VerifyMode mode, bool skipJunctions, ICollection<string> skipRelativePaths, Func<bool> cancel, Action<long, long, string> progress)` — `progress(hechos, total, archivoActual)`; recorre el origen (mismas exclusiones que `TreeScanner`), compara rutas relativas en el destino; Quick = existencia + tamaño; Sha256 = además hash (`SHA256Managed`, secuencial, lectura por bloques de 1 MB). `skipRelativePaths` (archivos que robocopy ya reportó como error) no cuentan como faltantes.

- [ ] **Step 1: Escribir tests:** `Quick_IdenticalTree_Ok`, `Quick_MissingFile_Reported`, `Quick_TruncatedFile_SizeMismatch`, `Sha256_FlippedByteSameSize_DetectedByShaNotByQuick` (criterio de éxito del spec), `Sha256_UnicodeAndSpacesPaths_Ok`, `SkipList_ExcludesKnownFailures`, `Cancel_SetsCancelled`.
- [ ] **Step 2: Ejecutar `./build.ps1 -TestFilter Verifier`** → FAIL.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Ejecutar** → PASSED.

---

### Task 7: Ajustes portables, informe y textos de ayuda

**Files:**
- Create: `src\Core\Settings.cs`, `src\Core\ReportWriter.cs`, `src\Core\HelpTexts.cs`
- Test: `tests\SettingsTests.cs`, `tests\ReportWriterTests.cs`, `tests\HelpTextsTests.cs`

**Interfaces:**
- Produces:
  - `class Settings { string LastSource, LastDestination, TechnicianName; Profile LastProfile; static string ResolveDataDir(string exeDir, out bool usedFallback); static Settings Load(string dataDir); void Save(string dataDir); }` — formato `clave=valor` UTF-8 en `<dataDir>\ajustes.txt`. `ResolveDataDir` devuelve `<exeDir>\datos` creándola; si no se puede escribir (pendrive de solo lectura) devuelve `%TEMP%\CopiaSegura_datos` con `usedFallback = true`. Archivo corrupto o ausente → valores por defecto, sin lanzar.
  - `class ReportData { CopyOptions Options; string Computer, Technician, CommandLine; DateTime Start, End; long Files, Bytes; RunResult Run; VerifyResult Verify; }` (`Verify` null = no verificada); `static string ReportWriter.BuildHtml(ReportData d)` (HTML UTF-8 imprimible, todo codificado con `HttpUtility`-equivalente propio sin depender de System.Web); `static void ReportWriter.Save(string path, ReportData d)`.
  - `class OptionHelp { string Key, Title, Explanation, Flag; }`; `static List<OptionHelp> HelpTexts.All` con claves: `subfolders`, `dates`, `junctions`, `restartable`, `retries`, `threads`, `log`, `mirror`, `move`, `doubtful`; textos tomados de la maqueta (`maqueta.html`, paso 2), en español, sin jerga.

- [ ] **Step 1: Escribir tests:** `Settings.RoundTrip`, `Settings.CorruptFile_Defaults`, `Settings.ReadOnlyExeDir_FallsBackToTemp` (carpeta con ACL denegando escritura), `ReportWriter.EscapesAmpersandInPaths` (`A&B` → `A&amp;B`), `ReportWriter.NullVerify_SaysNotVerified` (contiene `NO verificada`), `ReportWriter.ListsErrors`, `HelpTexts.EveryOptionHasSpanishTextAndFlagWhereApplicable` (ningún campo vacío; `mirror` y `move` mencionan "BORRA").
- [ ] **Step 2: Ejecutar `./build.ps1 -TestFilter Settings`, `ReportWriter`, `HelpTexts`** → FAIL.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Ejecutar** → PASSED.

---

### Task 8: Asistente WinForms — pasos 1 a 3

**Files:**
- Create: `src\UI\MainForm.cs`, `src\UI\WizardState.cs`, `src\UI\IWizardPage.cs`, `src\UI\PageSource.cs`, `src\UI\PageOptions.cs`, `src\UI\PageReview.cs`
- Modify: `src\Program.cs` (arranca `MainForm`; `--smoke` construye el formulario, recorre las páginas 1–3 con datos de ejemplo y sale 0 sin mostrarlo)
- Test: `tests\` no puede probar WinForms cómodamente → verificación con `--smoke` más lista manual.

**Interfaces:**
- Consumes: todo `Core`.
- Produces: `interface IWizardPage { string StepTitle { get; } bool CanAdvance(out string reason); void OnEnter(WizardState s); void OnLeave(WizardState s); }`; `class WizardState { CopyOptions Options; Profile Profile; ScanResult Scan; List<CheckResult> Checks; RunResult Run; VerifyResult Verify; Settings Settings; string DataDir; DateTime Start, End; }`; `MainForm` con cabecera de pasos clicables (solo hacia atrás o a pasos ya validados), pie con Atrás/Siguiente, barra de título "Copia Segura".
- Comportamiento (copiar el aspecto de `maqueta.html`):
  - **Paso 1:** dos cuadros de ruta + "Examinar…" (`FolderBrowserDialog`); al cambiar la ruta se escanea en un hilo de trabajo (`TreeScanner.Scan`) y se muestran los `CheckResult` con color (verde/ámbar/rojo); "Siguiente" desactivado mientras haya algún `Error`. Si el origen está en una unidad extraíble (`DriveInfo.DriveType == Removable` o unidad USB), añade el aviso USB 2.0/3.0 (consejo general, sin afirmar la versión).
  - **Paso 2:** perfiles Segura (por defecto) y Rápida; cada opción de `HelpTexts.All` con casilla/selector, explicación y etiqueta del parámetro; reintentos (1/3/5), espera (2/5 s), hilos (2/4/8); casilla "Disco dudoso" (llama `ApplyDoubtfulDisk` y muestra aviso de que puede convenir clonar el disco antes; al desmarcar o cambiar de perfil se vuelve a aplicar `ForProfile`); opciones `mirror` y `move` deshabilitadas con botón "Desbloquear…" que pide escribir `BORRAR` (`TryUnlockDangerous`) y muestra advertencia roja.
  - **Paso 3:** resumen en lenguaje llano (cuántos archivos/GB, de dónde a dónde, "no se borrará nada en el origen/destino" solo si `Mirror`/`Move` son false; si no, advertencia roja explícita), tiempo estimado orientativo, comando `robocopy ` + `CommandBuilder.BuildArguments` en cuadro de solo lectura, botón "▶ Iniciar copia" (avanza al paso 4 y lanza la copia).
  - Pie: botón "Reiniciar como administrador" visible solo si el proceso no es admin (`Process.Start` con verbo `runas`, cierra la instancia actual).
  - Guardar `Settings` (última ruta/perfil) al salir del paso 3.

- [ ] **Step 1: Ampliar `Program.cs` con el modo `--smoke` real** (construye `MainForm`, llama a `OnEnter/CanAdvance/OnLeave` de cada página 1–3 con un árbol temporal, devuelve 0 o 1 con el error por stderr) y ejecutar `dist\CopiaSegura\CopiaSegura.exe --smoke` → esperado FAIL (páginas aún no existen).
- [ ] **Step 2: Implementar los archivos de UI** según el comportamiento descrito.
- [ ] **Step 3: Ejecutar `./build.ps1` y `dist\CopiaSegura\CopiaSegura.exe --smoke`** → `PASSED n/n` y código 0.
- [ ] **Step 4: Verificación manual** (abrir el `.exe`): paso 1 con `C:\Windows\Fonts` → carpeta temporal; el origen igual al destino bloquea "Siguiente"; los checkboxes `mirror/move` no se pueden marcar hasta teclear `BORRAR`; el comando del paso 3 coincide con el de la Task 2.

---

### Task 9: Asistente WinForms — pasos 4 y 5 e informe

**Files:**
- Create: `src\UI\PageProgress.cs`, `src\UI\PageVerify.cs`
- Modify: `src\UI\MainForm.cs` (registrar los pasos 4–5), `src\Program.cs` (`--smoke` recorre los 5 pasos con una copia real pequeña)

**Interfaces:**
- Consumes: `RobocopyRunner`, `ProgressTracker`, `Verifier`, `ReportWriter`, `WizardState`.
- Comportamiento:
  - **Paso 4:** ejecuta `RobocopyRunner.Run` en un `BackgroundWorker`/hilo; refresca por temporizador (500 ms) dos barras (total y archivo actual), GB copiados/total, velocidad (media móvil de 10 s), tiempo restante, errores, archivo actual, y el registro en vivo en un cuadro desplegable ("Ver registro"). Texto fijo: "Puedes dejarlo trabajando… No cierres el programa ni desconectes ningún disco". Si `NothingToCopy` muestra "No hay nada que copiar". Botones: **Pausar** (`Stop()`, cambia a **Reanudar**, que vuelve a llamar a `Run`), **Cancelar** (`MessageBox` Sí/No con No por defecto; si Sí → `Stop()`). Al terminar guarda `Run`/`End` en `WizardState` y avanza al paso 5. El cierre de la ventana durante una copia pide la misma confirmación.
  - **Paso 5:** muestra `RunResult.Describe()` con color (verde solo si `Success`; rojo si ≥ 8), conteos, lista de archivos con error (`ErrorLines`); selector verificación Rápida/Profunda con barra de avance y botón "Verificar ahora" (`Verifier.Verify`, `skipRelativePaths` = rutas de `ErrorLines`); botón "Reintentar solo los fallidos" = volver a ejecutar `Run` con las mismas opciones (robocopy omite lo ya copiado) y actualizar la lista; botón "Guardar informe…" (`SaveFileDialog`, nombre `Informe_copia_yyyyMMdd_HHmm.html`, equipo = `Environment.MachineName`, técnico = `Settings.TechnicianName` editable) — el informe nunca se genera sin pulsar el botón. Texto fijo: "Terminar no significa verificado. Conserva el origen hasta verificar y haz una segunda copia en otro soporte."

- [ ] **Step 1: Ampliar `--smoke`** para ejecutar los 5 pasos sobre un árbol temporal (copia + verificación Rápida + informe a un archivo temporal) y comprobar que el informe contiene el nombre del equipo; ejecutar → FAIL.
- [ ] **Step 2: Implementar los archivos de UI.**
- [ ] **Step 3: Ejecutar `./build.ps1` y `dist\CopiaSegura\CopiaSegura.exe --smoke`** → verde y código 0.
- [ ] **Step 4: Verificación manual:** copiar un archivo de ~2 GB generado con `fsutil file createnew`; comprobar que las dos barras avanzan, que Pausar/Reanudar continúa sin recopiar lo ya hecho, que Cancelar pide confirmación y que desconectar el USB destino a mitad muestra el paso 5 en rojo con `FALLÓ` (nunca "completada").

---

### Task 10: Empaquetado portable y comprobación en Windows 7

**Files:**
- Modify: `build.ps1` (parámetro `-Dist`)
- Create: `src\LEEME.txt`

**Interfaces:**
- Produces: `./build.ps1 -Dist` deja `dist\CopiaSegura\` con `CopiaSegura.exe`, `CopiaSegura.exe.config`, `LEEME.txt` (uso en 5 líneas, aviso "no instala nada, deja datos en la carpeta `datos\`", requisitos Windows 7 SP1 o superior) y `dist\CopiaSegura.zip`.

- [ ] **Step 1: Ejecutar `./build.ps1 -Dist`** y comprobar que el zip contiene exactamente esos tres archivos y pesa < 5 MB.
- [ ] **Step 2: Comprobar** (con `System.Reflection.AssemblyName.GetAssemblyName` o `monodis`/`ildasm` si existe; si no, `dumpbin`/lectura de los metadatos desde PowerShell) que el exe referencia `mscorlib 2.0.0.0` (destino .NET 3.5) y no 4.0.0.0.
- [ ] **Step 3: Probar desde una carpeta nueva** copiando `dist\CopiaSegura` a `C:\Temp\prueba` y ejecutar con `--smoke` → código 0 y aparece `C:\Temp\prueba\datos`; repetir desde una ruta de solo lectura → usa el respaldo en `%TEMP%`.
- [ ] **Step 4: Prueba manual en Windows 7 SP1** (máquina física o virtual del usuario, sin .NET 4 instalado): copiar la carpeta, ejecutar, hacer una copia real corta y verificarla. Si el exe no arranca por falta de .NET 3.5 SP1, anotarlo en `LEEME.txt` como requisito (viene de serie en Windows 7).

---

## Autorrevisión

- **Cobertura del spec:** flujo de 5 pasos (T8, T9), perfiles y opciones peligrosas (T2, T8), disco dudoso (T2, T8), informe opcional (T7, T9), verificación rápida/profunda (T6, T9), portable y fallback (T1, T7, T10), checks de rutas y espacio (T3), solo lectura en origen (T5 test de hash), Windows 7/10/11 (T1 config, T10).
- **Consistencia de tipos:** `CopyOptions`, `ScanResult`, `RunResult`, `VerifyResult`, `ProgressTracker` y `WizardState` se usan con los mismos nombres en todas las tareas.
- **Pendiente de decidir al construir (dejado al constructor por el spec):** detalles visuales de WinForms, estimación de tiempo restante y textos exactos de ayuda (T7 los toma de la maqueta).
