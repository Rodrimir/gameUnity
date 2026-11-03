using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GameYunit.Network
{
    public enum ConnectionProtocol { Udp, WebSocket }

    public static class ConnectionRules
    {
        public const int MaxPlayers = 2;
        public const int Version = 1;

        public static bool TryEndpoint(string address, string portText, out string ip, out ushort port)
        {
            ip = address?.Trim();
            port = 0;
            return IPAddress.TryParse(ip, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork
                && !parsed.Equals(IPAddress.Any) && !parsed.Equals(IPAddress.Broadcast)
                && ushort.TryParse(portText, out port) && port > 0;
        }

        public static string CleanName(string value)
        {
            var result = new StringBuilder();
            foreach (char c in (value ?? "").Trim())
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') result.Append(c);
                if (result.Length == 24) break;
            }
            while (Encoding.UTF8.GetByteCount(result.ToString()) > 60) result.Length--;
            return result.Length == 0 ? "Explorador" : result.ToString().Trim();
        }

        public static string Rejection(int reservedSlots, int version, int color)
        {
            if (version != Version) return "Versão incompatível. Atualize o projeto nas duas máquinas.";
            if (color < 0 || color > 1) return "Cor de jogador inválida.";
            return reservedSlots >= MaxPlayers ? "Sala cheia (2/2)." : null;
        }

        /// <summary>
        /// Mantém uma escolha de catálogo (magia, personagem) dentro da lista. Um índice fora da lista não
        /// derruba a conexão: cai na primeira opção, porque recusar a entrada por isso seria pior que a
        /// correção — catálogos podem divergir entre máquinas por arte não importada.
        /// </summary>
        public static int CleanIndex(int index, int catalogCount) =>
            catalogCount <= 0 || index < 0 || index >= catalogCount ? 0 : index;
    }
}
