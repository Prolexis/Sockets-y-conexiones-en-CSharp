using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SERVIDORES_SOCKETS
{
    /// <summary>
    /// ManejadorCliente encapsula TODO lo relativo a UN cliente conectado al servidor:
    ///  · Su TcpClient/socket dedicado.
    ///  · Metadatos de conexión: IP, puerto, nombre de usuario, hora de conexión.
    ///  · Métodos de envío que el servidor usa para escribirle mensajes y chunks de archivos.
    ///
    /// ServidorTcp crea UNA instancia de esta clase por cada cliente aceptado y la guarda
    /// en su lista thread-safe (_clientes).  Para reenviar datos, siempre llama a:
    ///     clienteDestino.EnviarMensajeAsync(...)      // mensajes de chat / protocolo
    ///     clienteDestino.EnviarArchivoChunkAsync(...) // chunks binarios de archivos
    /// sin tocar el socket directamente desde ServidorTcp.
    /// </summary>
    public class ManejadorCliente
    {
        // ── Propiedades de identificación ──────────────────────────────────────
        public TcpClient Socket       { get; }
        public string    IP           { get; }
        public int       Puerto       { get; }
        public string    Usuario      { get; set; }
        public DateTime  HoraConexion { get; }

        // StreamWriter exclusivo de este cliente (creado por ManejarClienteHijoAsync en ServidorTcp).
        private readonly StreamWriter _writer;

        // Lock de escritura: garantiza que solo un hilo (de todos los que enrutan mensajes)
        // escriba al StreamWriter en un momento dado, evitando mensajes entrelazados.
        private readonly object _writeLock = new();

        public ManejadorCliente(TcpClient socket, string ip, int puerto, string usuario, StreamWriter writer)
        {
            Socket       = socket  ?? throw new ArgumentNullException(nameof(socket));
            IP           = ip;
            Puerto       = puerto;
            Usuario      = usuario;
            HoraConexion = DateTime.Now;
            _writer      = writer  ?? throw new ArgumentNullException(nameof(writer));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  MÉTODOS DE ENVÍO
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Envía una línea de protocolo completa a este cliente de forma thread-safe.
        /// Devuelve false si el socket cayó, sin propagar la excepción hacia arriba.
        /// El bucle de lectura de ese mismo cliente detectará la desconexión y limpiará.
        /// </summary>
        public bool EnviarLinea(string linea)
        {
            lock (_writeLock)
            {
                try
                {
                    _writer.WriteLine(linea);
                    return true;
                }
                catch
                {
                    // Socket caído: el hilo de lectura de ese cliente lo detectará y limpiará.
                    return false;
                }
            }
        }

        /// <summary>
        /// Envía una línea de protocolo MSG (u otro comando) de forma async-friendly.
        ///
        /// El servidor llama a este método sobre la instancia del cliente destino:
        ///     _ = clienteDestino.EnviarMensajeAsync($"MSG|{emisor.Usuario}|{contenido}");
        ///
        /// Técnica: Se reutiliza el lock síncrono de EnviarLinea porque StreamWriter no es
        /// thread-safe y el lock ya existe. Devolver Task.CompletedTask evita alocar una
        /// Task extra cuando no hay trabajo verdaderamente asíncrono que realizar.
        /// </summary>
        public Task EnviarMensajeAsync(string mensajeProtocolo)
        {
            EnviarLinea(mensajeProtocolo);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Codifica un array de bytes (chunk de archivo) a Base64 y lo escribe al stream.
        /// Útil cuando el servidor (o un módulo futuro) dispone del chunk en memoria como
        /// bytes y necesita transmitirlo sin pasar por la capa de texto del protocolo.
        ///
        /// Técnica: Convert.ToBase64String es CPU-bound → se delega a Task.Run para no
        /// bloquear el hilo que lo llame. El WriteLineAsync posterior se protege con el
        /// mismo _writeLock que EnviarLinea para evitar entrelazado de datos en el stream.
        /// </summary>
        public async Task EnviarArchivoChunkAsync(byte[] datos)
        {
            // Codificación Base64 en un hilo de pool para no bloquear el hilo de red.
            string base64 = await Task.Run(() => Convert.ToBase64String(datos)).ConfigureAwait(false);
            EnviarLinea(base64);
        }

        /// <summary>
        /// Cierra la conexión del socket de forma segura (ignora excepciones).
        /// </summary>
        public void Close()
        {
            try { Socket.Close(); }
            catch { /* ignorar */ }
        }
    }
}
