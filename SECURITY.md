# Seguridad

## Alcance del MVP

La aplicación no debe:

- abrir el proceso de World of Warcraft;
- leer o escribir memoria del juego;
- inyectar DLL o código;
- simular teclado o ratón;
- enviar mensajes al juego automáticamente;
- ejecutar scripts descargados;
- iniciar servicios o crear persistencia;
- guardar chat o credenciales por defecto.

La captura se limita a una región configurable de la ventana del juego que contiene la matriz del addon.

## Motor local

La integración opcional con Ollama usa una dirección fija de loopback: `127.0.0.1:11434`. GatoHablador no
acepta una URL remota, no instala modelos y no inicia el servicio. El texto del chat y la respuesta del modelo
se tratan como datos no confiables y solo se muestran en la interfaz.

## Reportar problemas

No publiques claves, conversaciones privadas ni datos personales en un issue. Describe el comportamiento y proporciona pasos mínimos para reproducirlo.

