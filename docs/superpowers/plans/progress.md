# SDD ledger — plan: C:\laragon\www\ROBOCOPY\docs\superpowers\plans\2026-11-18-copia-segura.md

Pre-flight: Task 2 -> 3 (IsDriveRoot, RootExcludedDirs); 2 -> 5 (BuildArguments); 4 -> 5 (Parser/Tracker); 5,6,7 -> 8,9 (UI). Interfaces consistent.
Ruling: no git repo and no sdd scripts -> ledger kept by hand, checkpoint = ./build.ps1 green — plan says so — cost: none.
Task 1: complete (build.ps1 -> PASSED 2/2; dist exe --smoke exit 0; refs net35 downloaded to tools\refasm)
Task 2: complete (build.ps1 -TestFilter CommandBuilder -> PASSED 6/6)
Task 3: complete (build.ps1 full suite -> PASSED 20/20)
Task 4: complete (build.ps1 full suite -> PASSED 28/28)
Task 5: complete (build.ps1 full suite -> PASSED 33/33; robocopy real, origen intacto por hash)
Task 6: complete (build.ps1 full suite -> PASSED 40/40; SHA detecta byte cambiado que Quick no ve)
Task 7: Ruling: ReportData gains field `VerifyMode VerifyMode` — el informe debe decir si la verificacion fue rapida o profunda — coste si falla: ninguno, campo extra
Task 6: Ruling: TreeScanner.Scan gains 5-arg overload with List<string> collect — Verifier necesita la lista de archivos sin duplicar el recorrido — coste: ninguno, la sobrecarga de 4 args sigue igual
Task 7: complete (build.ps1 full suite -> PASSED 53/53)
Task 9: Ruling: Verifier.RelativePathsFromErrors(IEnumerable<string>, string) anadido a Core — el paso 5 necesita pasar a Verify las rutas ya fallidas — coste: ninguno
Task 8: complete (build + SMOKE OK; capturas de los 5 pasos revisadas visualmente; 2 defectos de layout del paso 2 corregidos)
Task 9: complete (build.ps1 -> PASSED 54/54 + SMOKE OK: copia real, verificacion Quick y SHA-256, informe con nombre de equipo)
Task 10: complete (build.ps1 -Dist -> zip 36.667 bytes con exe+config+LEEME; exe referencia mscorlib 2.0.0.0; smoke OK desde carpeta nueva y desde carpeta solo lectura con respaldo en TEMP). Pendiente manual: Windows 7 SP1 real.
Ruling: SmokeTest.Shots + modo --shots anadidos al exe — permiten revisar visualmente la UI sin intervencion manual — coste: ~60 lineas extra en el exe
Ruling: PageOptions Rapida muestra R0/W0 (reintentos 0) y el desplegable incluye 0 — el plan fija Rapida=R0 — coste: ninguno
Final: review by fresh reviewer (Fable) done. fixed C1 — Verifier.Skipped, SkipList_ReportedAsNotVerified_NeverOk + SkippedOrUnreadable_NeverReportedAsVerified RED->GREEN, suite 62/62
Final: fixed I1 — Quick_DifferentTimestampSameSize_Reported RED->GREEN
Final: fixed I2 — NegativeExitCode_IsFailure RED->GREEN
Final: fixed I3 — smoke: TryShowStep bloquea destino invalido (RED por compilacion -> GREEN)
Final: fixed I4 — MirrorOrMove_BlockedWhenSourceInsideDestination RED->GREEN
Final: fixed I5 — smoke: PageVerify.CanVerify=false con Move
Final: fixed I6 — smoke: resumen avisa de que se sustituyen archivos del mismo nombre
Final: fixed I7 — ClearHiddenSystem_RemovesAttributesFromFolder RED->GREEN
Final: fixed I8 — BytesStillNeeded_SubtractsWhatDestinationAlreadyHas RED->GREEN
Final: fixed (regraded Minor->Important) parada perdida — Stop_BeforeRun_RunCopiesNothingAndReportsStopped RED->GREEN
Final: Ruling: la verificacion ya no excluye los archivos con ERROR de robocopy (salen como FALTA si no estan) — es la opcion honesta; coste: tras un error la verificacion no dara Ok hasta reintentar y copiar bien
Final: minor (deferred): cancelar en pausa pierde errores de tramos anteriores
Final: minor (deferred): /LOG se sobrescribe al reanudar (usar /LOG+ y /UNILOG)
Final: minor (deferred): codigos 4-7 se muestran en verde
Final: minor (deferred): ErrorCount cuenta reintentos
Final: minor (deferred): rutas se pasan sin normalizar a robocopy (/MIR como destino acaba en error 16)
Final: minor (deferred): rutas >260 caracteres hacen que la verificacion nunca de Ok
Final: minor (deferred): informe no lista archivos con diferencias cuando se cancela (parcialmente cubierto)
Final: minor (deferred): boton Reiniciar como administrador visible durante la copia
Final: minor (deferred): cerrar durante Reintentar no pide confirmacion
Final: minor (deferred): origen solo con archivos de 0 bytes muestra No hay nada que copiar
Final: minor (deferred): /XJ omite symlinks de archivo pero el escaner los lista
Final: minor (deferred): con /MT no hay porcentaje por archivo; progreso total tras reanudar reinicia
Final: minor (deferred): datos\ se escribe junto al exe (si se ejecuta dentro del origen)
Minors batch: fixed /UNILOG+ (log Unicode que se anade; EnsureUnicodeLog porque UNILOG+ sobre archivo nuevo pierde la n) — Run_AppendsToLog_NotOverwrites RED->GREEN
Minors batch: fixed codigos 2-7 en ambar (RunResult.IsClean) — IsClean_OnlyForCodes0And1
Minors batch: fixed ErrorCount sin reintentos (ProgressParser.ErrorKey) — Tracker_RetriedErrorCountsOnce
Minors batch: fixed rutas normalizadas antes de robocopy — Paths_AreNormalized_SoSlashPathCannotBecomeASwitch
Minors batch: fixed errores acumulados al cancelar en pausa (RunResult.AddErrors) — AddErrors_AccumulatesAcrossRunsWithoutDuplicates
Minors batch: fixed boton administrador oculto durante la copia; cerrar durante Reintentar pide confirmacion; origen solo con archivos de 0 bytes (UI, sin prueba automatica: smoke + revision de codigo)
Minors batch: documented in LEEME (no arreglable con seguridad): rutas >260, datos junto al exe si se ejecuta dentro del origen, symlinks con /XJ, /MT sin avance por archivo
Suite final: 67/67 + SMOKE OK
