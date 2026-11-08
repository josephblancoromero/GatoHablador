# Arquitectura

## Protocolo GHP/1

Cada fotograma contiene 64 bytes representados como una matriz de 64 columnas por 8 filas. Cada celda es un bit: blanco equivale a `1` y negro a `0`. Los bits se escriben del más significativo al menos significativo.

| Campo | Bytes |
| --- | ---: |
| Magic (`0xA7`) | 1 |
| Versión (`1`) | 1 |
| Secuencia | 1 |
| Índice de fragmento | 1 |
| Total de fragmentos | 1 |
| Longitud útil | 1 |
| Datos UTF-8, con relleno | 56 |
| CRC-16/CCITT-FALSE de los primeros 62 bytes | 2 |

El mensaje lógico utiliza campos escapados separados por tabulaciones:

```text
EVENTO<TAB>AUTOR<TAB>MENSAJE
```

Los caracteres `%`, tabulación, retorno y salto de línea se representan como `%25`, `%09`, `%0D` y `%0A`.

## Límites de confianza

- El addon es entrada no confiable: el receptor valida cabecera, longitud, CRC y límites.
- El texto del chat es entrada no confiable: nunca se interpreta como código o formato ejecutable.
- El traductor es intercambiable y recibe solamente texto normalizado.
- La interfaz no debe enviar texto al juego.

