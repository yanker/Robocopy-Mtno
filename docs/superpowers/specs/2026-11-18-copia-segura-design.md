# Copia Segura — Documento de diseño

Fecha: 2026-11-18 · Estado: pendiente de aprobación

## 1. Intención y por qué
Herramienta de escritorio para Windows que un técnico usa en mantenimientos y migraciones de equipos: copia una carpeta de un equipo/disco A (origen) a un disco externo B (destino) de forma **segura, guiada y comprensible**, apoyándose en `robocopy.exe` de Windows.

Origen: una conversación previa (`copia-segura-robocopy.md`) donde el comando recomendado fue
`robocopy "ORIGEN" "DESTINO" /E /Z /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:2 /MT:2 /TEE /LOG:"..."`
con la premisa: datos posiblemente irremplazables, origen intocable, sin `/MIR` ni `/PURGE`.

Problemas que resuelve: comandos difíciles de recordar, riesgo de borrar datos por una opción mal puesta, progreso confuso (dos porcentajes), y falta de verificación posterior.

## 2. Usuario
El propio técnico (uso personal en mantenimientos). Nivel avanzado pero quiere simplicidad, y que cada opción esté explicada.

## 3. Objetivos
- Flujo guiado de 5 pasos (ver maqueta `maqueta.html`, aprobada por el usuario):
  1. **Origen y destino** con comprobaciones automáticas.
  2. **Opciones** con perfiles y explicación de cada opción.
  3. **Revisar**: resumen en lenguaje llano + comando exacto.
  4. **Copiando**: progreso total, progreso del archivo actual, velocidad, tiempo restante, errores, pausar/cancelar, log en vivo.
  5. **Verificar y terminar**: resumen, archivos con error, verificación rápida (cantidad/tamaño) o profunda (SHA-256), reintentar solo fallidos.
- **Perfiles:** "Copia segura" (por defecto) y "Copia rápida".
- **Modo disco dudoso (opcional):** menos hilos (1), 1 reintento, avisa de que puede convenir clonar el disco antes. Se activa a mano.
- **Informe opcional** (TXT/PDF o HTML imprimible): fecha, equipo, origen, destino, resultado, errores, resultado de la verificación. Solo se genera si el usuario lo pide.
- **Portable:** sin instalador; la configuración y los logs viven junto al `.exe` (carpeta `datos\`), sin tocar el registro del equipo.
- Todo en español.

## 4. Fuera de alcance (no-objetivos)
- Windows XP (sin Robocopy moderno ni .NET compatible).
- Sincronización/espejo, copias programadas, copia en red entre equipos, compresión/ZIP, clonado de discos.
- Instalador (MSI/Setup).

## 5. Anti-objetivos (fracaso aunque "funcione")
- Que se pueda borrar o modificar el origen, o borrar en destino sin querer.
- Que el usuario no entienda qué hace una opción.
- Que "terminó" se confunda con "verificado".

## 6. Restricciones y reglas de seguridad
- El origen es **solo lectura**: el programa nunca escribe en él (ni logs, ni temporales).
- **Bloqueadas por defecto** `/MIR`, `/PURGE`, `/MOVE`; desbloqueo solo con confirmación escrita explícita y advertencia. Valor por defecto: bloqueadas.
- Impedir destino dentro del origen, y origen = destino.
- Avisar (no bloquear) si el espacio libre es insuficiente → bloquear si claramente no cabe.
- Rutas con espacios y caracteres no ASCII entrecomilladas correctamente; evitar inyección de argumentos en el comando.
- No cancelar sin confirmación. La cancelación detiene Robocopy limpiamente.
- Pedir administrador solo si hace falta (p. ej. leer carpetas de otros usuarios); no por defecto.
- Windows 7 SP1, 10 y 11. Robocopy del propio sistema (trae `/MT`, `/XJ`).

## 7. Enfoques considerados
| Enfoque | Pros | Contras |
|---|---|---|
| A. .NET 8 autocontenido, un `.exe` | Moderno | ~70 MB; no corre en Windows 7 |
| B. .NET Framework 4.8 | Ligero | Windows 7 no lo trae instalado |
| **C. .NET Framework 3.5 + `.config` para usar 4.x si existe (elegido)** | ~1 MB, funciona en Windows 7 sin instalar nada y en 10/11 usando su 4.8; compilable con el compilador incluido en Windows | Interfaz más clásica |
| D. Script PowerShell/BAT | Muy simple | Interfaz pobre, difícil de hacer "intuitivo" |

Elegido: **C**, porque el usuario migra equipos con Windows 7 y quiere portabilidad real.

## 8. Decisiones del cómo
(Decididas en conversación)
- Portable en carpeta: `.exe` + `.config` + `datos\`.
- C# / interfaz de escritorio clásica reproduciendo la maqueta.
- Llama a `robocopy.exe` y lee su salida para el progreso.

## 9. Lo que se deja al constructor
- WinForms vs WPF clásico (según lo que dé mejor resultado en .NET 3.5).
- Cómo leer el progreso por archivo desde Robocopy (p. ej. `/NJH /NDL`, lectura de la salida en vivo).
- Formato exacto del informe (TXT, HTML imprimible o PDF) y del registro.
- Cálculo de hashes SHA-256 en paralelo vs secuencial (por defecto secuencial para no castigar el disco).
- Estimación de tiempo restante, textos exactos de ayuda de cada opción.
- Detección de USB 2.0 vs 3.0 (si no es fiable, mostrar solo un consejo general).
- Estructura del proyecto y pruebas.

## 10. Criterios de éxito
- Una copia de ejemplo completa se realiza sin tocar el origen y con el comando de la sección 1.
- Las opciones peligrosas no se pueden activar sin confirmación.
- El `.exe` arranca en Windows 7 SP1 y en Windows 10/11 sin instalar nada.
- La verificación detecta un archivo alterado a propósito en el destino.
