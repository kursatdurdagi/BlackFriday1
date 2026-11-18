using Mirror;
using UnityEngine;

namespace BlackFriday
{
    /// <summary>Co-op ağ yöneticisi (Mirror, KCP). Host / Katıl / Tek başına.</summary>
    public class BFNetworkManager : NetworkManager
    {
        public const int MaxPlayers = 4;
        public static BFNetworkManager Instance => singleton as BFNetworkManager;

        /// <summary>Ana menüde gösterilecek son durum/hata mesajı.</summary>
        public static string LastStatus;
        bool leavingOnPurpose;

        public override void Awake()
        {
            base.Awake();
            maxConnections = MaxPlayers;
        }

        public void HostGame(bool solo)
        {
            leavingOnPurpose = false;
            LastStatus = null;
            maxConnections = solo ? 1 : MaxPlayers;
            ApplySelectedLevel();
            StartHost();
        }

        public void JoinGame(string address)
        {
            leavingOnPurpose = false;
            LastStatus = null;
            networkAddress = string.IsNullOrWhiteSpace(address) ? "localhost" : address.Trim();
            StartClient();
        }

        public void Leave()
        {
            leavingOnPurpose = true;
            if (NetworkServer.active && NetworkClient.isConnected) StopHost();
            else if (NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
        }

        void ApplySelectedLevel()
        {
            var lvl = GameCatalog.Levels[Mathf.Clamp(GameCatalog.SelectedLevel, 0, GameCatalog.Levels.Count - 1)];
            if (lvl.available && !string.IsNullOrEmpty(lvl.scenePath)) onlineScene = lvl.scenePath;
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            // tur başladıysa yeni oyuncu yine girebilir ama lobi dolu ise reddet
            if (numPlayers >= maxConnections)
            {
                conn.Disconnect();
                return;
            }
            base.OnServerConnect(conn);
        }

        public override void OnClientDisconnect()
        {
            if (!leavingOnPurpose && !NetworkServer.active)
                LastStatus = "Sunucuya bağlanılamadı ya da bağlantı koptu.";
            base.OnClientDisconnect();
        }

        public override void OnClientError(TransportError error, string reason)
        {
            LastStatus = "Bağlantı hatası: " + reason;
            base.OnClientError(error, reason);
        }
    }
}
