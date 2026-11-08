# GatoHablador

Addon y aplicación de escritorio de código abierto para traducir conversaciones de World of Warcraft del inglés al español.

> Estado: prototipo temprano. Todavía no se recomienda para uso diario.

## Principios

- Código visible y auditable.
- Sin lectura de memoria ni inyección en el juego.
- Sin envío automático de mensajes.
- Sin telemetría, publicidad o actualizador automático.
- Sin OCR, PowerShell ni descargas automáticas en el MVP.
- El chat permanece local mientras se use un traductor local.

## Arquitectura

1. El addon Lua escucha eventos de chat permitidos por la API de WoW.
2. Codifica cada mensaje en una pequeña matriz binaria visible.
3. La aplicación captura únicamente esa matriz desde la ventana del juego.
4. El núcleo valida CRC-16, reconstruye mensajes y entrega el texto al traductor.
5. El usuario copia manualmente cualquier respuesta traducida al juego.

```text
WoW chat -> addon Lua -> matriz de pixeles -> app Windows -> traductor -> interfaz
```

## Estructura

- `addon/GatoHablador`: addon de World of Warcraft.
- `src/GatoHablador.Core`: protocolo, ensamblado y traducción.
- `src/GatoHablador.Desktop`: aplicación WPF para Windows 10/11.
- `tests/GatoHablador.Core.Tests`: pruebas sin dependencias externas.
- `docs`: arquitectura, privacidad y modelo de amenazas.

## Compilar

Requisitos: .NET 8 SDK o posterior.

```powershell
dotnet build GatoHablador.sln -c Release
dotnet run --project tests/GatoHablador.Core.Tests -c Release
```

## Ejecutar el prototipo

1. Usa WoW en modo ventana o ventana sin bordes.
2. Copia `addon/GatoHablador` dentro de `World of Warcraft/_retail_/Interface/AddOns/`.
3. Activa el addon y ejecuta `/reload`.
4. Abre la aplicación de escritorio.
5. Ajusta el desplazamiento y tamaño de celda si la matriz no se detecta.

El traductor incluido en esta fase es un glosario pequeño para probar la tubería. Un motor local completo inglés-español llegará en una fase posterior.

## Aviso

Este proyecto es independiente y no está afiliado, aprobado ni respaldado por Blizzard Entertainment. World of Warcraft y Blizzard Entertainment son marcas de sus respectivos propietarios. El uso de software externo queda sujeto a las condiciones vigentes de Blizzard.

## Licencia

MIT. Consulta [LICENSE](LICENSE).

