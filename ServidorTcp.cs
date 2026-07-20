using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using System.Linq;

namespace SERVIDORES_SOCKETS
{
    public enum LogLevel
    {
        Info,
        Success,
        Error
    }

    public class ServidorTcp
    {
        private TcpListener? _listener;
        private bool _running;

        // Diccionario/lista de ManejadorCliente: una instancia POR cada cliente conectado.
        private readonly List<ManejadorCliente> _clientes = new();
        private readonly object _clientesLock = new();

        // Eventos para comunicación desacoplada con la UI
        public event Action<string, LogLevel>? OnLog;
        public event Action<ManejadorCliente>? OnClientConnected;
        public event Action<ManejadorCliente>? OnClientDisconnected;
        public event Action<bool>? OnStateChanged;

        public bool IsRunning => _running;

        /// <summary>
        /// Obtiene una copia de la lista de ManejadorCliente de forma thread-safe.
        /// </summary>
        public List<ManejadorCliente> GetClientes()
        {
            lock (_clientesLock)
            {
                return new List<ManejadorCliente>(_clientes);
            }
        }

        /// <summary>
        /// Inicia la escucha del servidor en todas las interfaces de red (0.0.0.0) en el puerto indicado.
        /// </summary>
        public void Start(int port)
        {
            if (_running) return;

            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start();
                _running = true;

                // Obtener IPs locales de la máquina
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ips = host.AddressList
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.ToString());
                string ipList = string.Join(", ", ips);

                OnLog?.Invoke($"Servidor escuchando en todas las interfaces (0.0.0.0), puerto {port}. IPs locales detectadas: {ipList}", LogLevel.Info);
                OnStateChanged?.Invoke(true);

                // Iniciar el bucle de aceptación asíncrono en segundo plano
                Task.Run(() => AceptarClientesAsync());
            }
            catch (SocketException ex) when (ex.ErrorCode == 10048) // AddressAlreadyInUse
            {
                OnLog?.Invoke($"Error: El puerto {port} ya está en uso por otra aplicación.", LogLevel.Error);
                Stop();
                throw;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error al iniciar el servidor: {ex.Message}", LogLevel.Error);
                Stop();
                throw;
            }
        }

        /// <summary>
        /// Bucle de aceptación asíncrono. Corre en tarea secundaria para no bloquear la UI.
        /// </summary>
        private async Task AceptarClientesAsync()
        {
            while (_running && _listener != null)
            {
                try
                {
                    TcpClient clientSocket = await _listener.AcceptTcpClientAsync();
                    // Cada cliente tiene su propio hilo/tarea dedicada
                    _ = Task.Run(() => ManejarClienteHijoAsync(clientSocket));
                }
                catch (ObjectDisposedException)
                {
                    break; // Listener detenido limpiamente
                }
                catch (Exception ex)
                {
                    if (_running)
                        OnLog?.Invoke($"Error al aceptar conexión de cliente: {ex.Message}", LogLevel.Error);
                    break;
                }
            }
        }

        /// <summary>
        /// Maneja la comunicación con un cliente específico en su propio hilo.
        /// Crea una instancia de ManejadorCliente para encapsular ese socket.
        /// </summary>
        private async Task ManejarClienteHijoAsync(TcpClient clientSocket)
        {
            string clientEndPoint = "Desconocido";
            ManejadorCliente? cliente = null;

            try
            {
                // Obtener endpoint remoto
                if (clientSocket.Client.RemoteEndPoint is IPEndPoint ipEndPoint)
                    clientEndPoint = $"{ipEndPoint.Address}:{ipEndPoint.Port}";

                using NetworkStream stream = clientSocket.GetStream();
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // ── 1. Fase de Identificación ──────────────────────────────────────
                string? infoLinea = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(infoLinea) || !infoLinea.StartsWith("IDENTIFY|"))
                {
                    OnLog?.Invoke($"Conexión de {clientEndPoint} rechazada por falta de identificación.", LogLevel.Error);
                    clientSocket.Close();
                    return;
                }

                string usuario = infoLinea.Substring("IDENTIFY|".Length).Trim();
                if (string.IsNullOrEmpty(usuario)) usuario = "Anónimo";

                string ip = "127.0.0.1";
                int puerto = 0;
                if (clientSocket.Client.RemoteEndPoint is IPEndPoint ep)
                {
                    ip     = ep.Address.ToString();
                    puerto = ep.Port;
                }

                // ── 2. Registro Thread-Safe ───────────────────────────────────────
                // Se crea UNA instancia de ManejadorCliente que encapsula el socket y el writer.
                cliente = new ManejadorCliente(clientSocket, ip, puerto, usuario, writer);

                lock (_clientesLock)
                {
                    bool existe = _clientes.Exists(c => c.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase));
                    if (existe)
                    {
                        writer.WriteLine("ERROR|El usuario ya está en uso.");
                        clientSocket.Close();
                        OnLog?.Invoke($"Conexión rechazada: el usuario '{usuario}' ya está conectado desde {clientEndPoint}.", LogLevel.Error);
                        return;
                    }
                    _clientes.Add(cliente);
                }

                writer.WriteLine("OK|Conectado");
                OnLog?.Invoke($"Cliente [{usuario}] conectado desde {clientEndPoint}", LogLevel.Success);
                OnClientConnected?.Invoke(cliente);

                BroadcastListaUsuarios();

                // ── 3. Bucle de Lectura ───────────────────────────────────────────
                while (_running)
                {
                    string? linea = await reader.ReadLineAsync();

                    if (linea == null) break; // Desconexión limpia (0 bytes)

                    if (linea.Equals("PING", StringComparison.OrdinalIgnoreCase))
                    {
                        cliente?.EnviarLinea("PONG");
                    }
                    else if (linea.StartsWith("MSG|", StringComparison.OrdinalIgnoreCase))
                    {
                        // Formato: MSG|<destino>|<contenido>
                        string[] partes = linea.Split(new[] { '|' }, 3);
                        if (partes.Length == 3)
                        {
                            string destino  = partes[1].Trim();
                            string contenido = partes[2];
                            OnLog?.Invoke($"[CHAT] {usuario} -> {(string.IsNullOrEmpty(destino) || destino.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? "todos" : destino)}: {contenido}", LogLevel.Info);
                            EnrutarMensaje("MSG", destino, cliente!, contenido, incluirEmisorEnBroadcast: true);
                        }
                    }
                    else if (linea.StartsWith("FILE_", StringComparison.OrdinalIgnoreCase))
                    {
                        // Formato: FILE_INFO|<destino>|<fileId>|<nombre>|<tamaño>
                        //          FILE_CHUNK|<destino>|<fileId>|<base64>
                        //          FILE_END|<destino>|<fileId>
                        string[] partesFile = linea.Split('|');
                        if (partesFile.Length < 3)
                        {
                            OnLog?.Invoke($"Comando de archivo malformado de {usuario}, ignorado.", LogLevel.Error);
                            continue;
                        }

                        string comandoFile = partesFile[0];
                        string destinoFile = partesFile[1].Trim();
                        string restoFile   = string.Join("|", partesFile, 2, partesFile.Length - 2);

                        if (comandoFile.Equals("FILE_INFO", StringComparison.OrdinalIgnoreCase))
                            OnLog?.Invoke($"[ARCHIVO] {usuario} envía archivo a {(string.IsNullOrEmpty(destinoFile) || destinoFile.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? "todos" : destinoFile)}", LogLevel.Info);

                        // El servidor hace pass-through del archivo: nunca lee el contenido binario,
                        // solo retransmite la línea de protocolo al destino correspondiente.
                        EnrutarMensaje(comandoFile, destinoFile, cliente!, restoFile, incluirEmisorEnBroadcast: false);
                    }
                    else if (linea.Equals("DISCONNECT", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }
            catch (IOException)  { /* Conexión cortada abruptamente */ }
            catch (SocketException) { /* Pérdida de red */ }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Excepción en el hilo del cliente {clientEndPoint}: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                // ── 4. Limpieza y Desconexión ─────────────────────────────────────
                if (cliente != null)
                {
                    lock (_clientesLock) { _clientes.Remove(cliente); }
                    cliente.Close();
                    OnLog?.Invoke($"Cliente [{cliente.Usuario}] ({clientEndPoint}) desconectado.", LogLevel.Info);
                    OnClientDisconnected?.Invoke(cliente);
                    BroadcastListaUsuarios();
                }
                else
                {
                    clientSocket.Close();
                }
            }
        }

        /// <summary>
        /// Detiene el servidor y desconecta a todos los clientes.
        /// </summary>
        public void Stop()
        {
            if (!_running) return;

            _running = false;

            try { _listener?.Stop(); }
            catch { /* ignorar */ }

            lock (_clientesLock)
            {
                foreach (var cliente in _clientes)
                {
                    try { cliente.EnviarLinea("SERVER_SHUTDOWN"); } catch { }
                    cliente.Close();
                }
                _clientes.Clear();
            }

            OnLog?.Invoke("Servidor detenido.", LogLevel.Info);
            OnStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Envía un mensaje de chat a TODOS los clientes conectados.
        /// </summary>
        public void EnviarATodos(string remitente, string contenido)
        {
            List<ManejadorCliente> copia;
            lock (_clientesLock) { copia = new List<ManejadorCliente>(_clientes); }

            string linea = $"MSG|{remitente}|{contenido}";
            foreach (var c in copia)
                _ = c.EnviarMensajeAsync(linea);
        }

        /// <summary>
        /// Envía un mensaje de chat privado a un usuario específico.
        /// Devuelve false si el usuario destino no existe.
        /// </summary>
        public bool EnviarPrivado(string destinatarioUsuario, string remitente, string contenido)
        {
            ManejadorCliente? destino;
            lock (_clientesLock)
                destino = _clientes.Find(c => c.Usuario.Equals(destinatarioUsuario, StringComparison.OrdinalIgnoreCase));

            if (destino == null) return false;

            string linea = $"MSG|{remitente}|{contenido}";
            // El servidor llama EnviarMensajeAsync sobre la instancia ManejadorCliente destino,
            // nunca manipula el socket directamente.
            _ = destino.EnviarMensajeAsync(linea);
            return true;
        }

        /// <summary>
        /// Reenvía una línea de protocolo "<comando>|<emisor>|<resto>" al destino indicado
        /// (o a todos si destino es vacío/"ALL"). Usado tanto por MSG como por FILE_*.
        ///
        /// Para mensajes de chat (MSG): llama clienteDestino.EnviarMensajeAsync(lineaSaliente).
        /// Para archivos (FILE_*): el servidor hace pass-through usando EnviarLinea ya que
        /// la línea de protocolo ya viene formateada con base64 desde el cliente emisor.
        /// </summary>
        private void EnrutarMensaje(string comando, string destino, ManejadorCliente emisor, string resto, bool incluirEmisorEnBroadcast)
        {
            string lineaSaliente = $"{comando}|{emisor.Usuario}|{resto}";

            List<ManejadorCliente> copia;
            lock (_clientesLock) { copia = new List<ManejadorCliente>(_clientes); }

            bool esMensaje = comando.Equals("MSG", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(destino) || destino.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var c in copia)
                {
                    if (!incluirEmisorEnBroadcast && c == emisor) continue;

                    if (esMensaje)
                        // El servidor delega el envío al ManejadorCliente destino mediante su método async.
                        _ = c.EnviarMensajeAsync(lineaSaliente);
                    else
                        c.EnviarLinea(lineaSaliente); // pass-through de línea FILE_ ya formateada
                }
            }
            else
            {
                var dest = copia.Find(c => c.Usuario.Equals(destino, StringComparison.OrdinalIgnoreCase));
                if (dest != null)
                {
                    if (esMensaje)
                        _ = dest.EnviarMensajeAsync(lineaSaliente);
                    else
                        dest.EnviarLinea(lineaSaliente);
                }
                else
                {
                    // Solo avisamos al emisor en el FILE_INFO / MSG inicial.
                    emisor.EnviarLinea($"ERROR|Usuario '{destino}' no encontrado o desconectado.");
                }
            }
        }

        /// <summary>
        /// Difunde la lista actualizada de usuarios conectados a TODOS los clientes
        /// para que puedan poblar su selector de destinatario (USERLIST).
        /// </summary>
        private void BroadcastListaUsuarios()
        {
            List<ManejadorCliente> copia;
            lock (_clientesLock) { copia = new List<ManejadorCliente>(_clientes); }

            string payload = string.Join(",", copia.Select(c => c.Usuario));
            string linea   = $"USERLIST|{payload}";
            foreach (var c in copia)
                c.EnviarLinea(linea);
        }
    }
}
