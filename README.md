# Medikit Home — Windows

Gestor personal de medicamentos para Windows, con base SQLite local y sin cuentas. Interfaz en español e inglés, categorías múltiples, tratamientos y agenda, caducidades, fecha de compra, importación CSV y copias JSON. Esta distribución comienza vacía y no contiene inventarios personales.

## Instalar

Descarga el ZIP Windows x64 de Releases, extrae **todo** su contenido en una carpeta y abre **MedikitHome.exe**. Mantén las bibliotecas junto al ejecutable. Necesita Windows 10 1809 o posterior, o Windows 11, x64, y Microsoft Edge WebView2. Incluye .NET y Windows App SDK. No es un ejecutable de archivo único ni un instalador MSIX.

El ejecutable de esta entrega no tiene firma Authenticode comercial. Windows puede mostrar editor desconocido o SmartScreen. Publicar en GitHub no proporciona una firma ni certificación Microsoft. Para distribuir con identidad de editor hay que firmar con un certificado de firma de código o un servicio de firma propio antes de comprimir el paquete.

## Uso y privacidad

Añade medicamentos manualmente o importa `data/plantilla_medikit_home.csv`. Las fechas usan AAAA-MM o AAAA-MM-DD; las categorías y horas se separan con `|`. ES/EN cambia la interfaz y guarda la elección. Los datos escritos por el usuario mantienen su idioma. Los caducados se marcan en rojo.

La agenda de Windows muestra las tomas registradas. Esta edición **no emite notificaciones de escritorio**. La fecha de compra es un dato consultable. No contiene servicios ni código Android.

La información se guarda en `FileSystem.AppDataDirectory`; el directorio del ZIP no contiene la base del usuario. La base no tiene cifrado propio. Crea una copia JSON antes de desinstalar o cambiar de equipo. No hay sincronización ni recomendaciones de dosis. Consulta el prospecto y las indicaciones de tu profesional sanitario.

## Desarrollo

Instala .NET SDK 10 y la carga `maui-windows`, con las herramientas de Windows SDK y compilación de Visual Studio. Solo se requiere la plataforma Windows.

```powershell
dotnet workload install maui-windows
dotnet run --project tests/MedikitHome.Tests -- .
dotnet build src/HomePharma
./tools/Build-Release.ps1 -Version 1.2.0
```

El resultado está en `artifacts/MedikitHome-1.2.0-Windows-x64.zip`, con SHA256SUMS.txt. El script evita reutilizar una carpeta de publicación para no mezclar archivos de versiones anteriores. `src/HomePharma.Core` contiene los datos, CSV y SQLite; `src/HomePharma` contiene la interfaz MAUI/Blazor. Los nombres internos HomePharma se conservan para mantener compatibilidad de datos.

## Subir a GitHub y crear la primera Release

Sube **el contenido de esta carpeta como raíz del repositorio**. No subas su carpeta contenedora ni el proyecto Android original. `artifacts`, bases locales, claves y resultados de compilación están excluidos.

```powershell
git init -b main
git add .
git commit -m "Medikit Home Windows 1.2.0"
git remote add origin https://github.com/TU_USUARIO/MedikitHome.git
git push -u origin main
git tag -a v1.2.0 -m "Primera Release Windows"
git push origin v1.2.0
```

GitHub Actions compila, prueba y publica el ZIP y su hash al recibir la etiqueta. El flujo también compila los cambios de main y las pull requests sin publicar una Release. Puedes subir manualmente el ZIP compilado y SHA256SUMS.txt a una Release `v1.2.0` si prefieres usar esta compilación local. El flujo remoto requiere Actions habilitado; aún no se ha ejecutado en GitHub.

La versión de la etiqueta debe coincidir con `ApplicationDisplayVersion`. Las futuras versiones deben actualizar también `ApplicationVersion`. La primera Release pública es 1.2.0 para conservar la numeración del desarrollo previo.

Fuentes del procedimiento: [setup-dotnet](https://github.com/actions/setup-dotnet), [GitHub CLI releases](https://cli.github.com/manual/gh_release_create).
