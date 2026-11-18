using System;
using Mirror;
using UnityEngine;

namespace BlackFriday
{
    public enum RoundState : byte { Lobby, Countdown, Playing, Ended }
    public enum FeedKind : byte { Info, Good, Bad, Announce }

    /// <summary>
    /// Tüm oyun modlarının tabanı. Yeni mod = bu sınıftan türet, bölüm sahnesine koy.
    /// Lobi → Geri sayım → Oynanış → Bitiş akışını yönetir.
    /// </summary>
    public abstract class GameModeBase : NetworkBehaviour
    {
        public static GameModeBase Current;
        public static event Action<string, FeedKind> OnFeed;
        public static event Action<GameModeBase> OnStateChanged;

        [Header("Mod")]
        public float countdownSeconds = 3f;
        public float roundSeconds = 180f;

        // NOT: state en sonda tanımlı; hook çalıştığında diğer değerler güncel olsun
        [SyncVar] public double stateEndTime;
        [SyncVar] public int score;
        [SyncVar] public bool success;
        [SyncVar] public int stars;
        [SyncVar(hook = nameof(OnStateHook))] public RoundState state = RoundState.Lobby;

        public abstract string ModeName { get; }
        public abstract string ModeHint { get; }

        public float TimeLeft => Mathf.Max(0f, (float)(stateEndTime - NetworkTime.time));
        public virtual bool PlayersCanMove => state != RoundState.Countdown;
        public virtual bool CanPickUp => state == RoundState.Playing;

        protected virtual void Awake() { Current = this; }
        protected virtual void OnDestroy() { if (Current == this) Current = null; }

        void OnStateHook(RoundState old, RoundState now)
        {
            if (now == RoundState.Countdown) Sfx.Play(SfxId.Beep);
            if (now == RoundState.Playing) Sfx.Play(SfxId.Go);
            if (now == RoundState.Ended) Sfx.Play(success ? SfxId.Win : SfxId.Lose);
            OnStateChanged?.Invoke(this);
        }

        public override void OnStartServer()
        {
            Current = this;
            state = RoundState.Lobby;
            ServerOnLobby();
        }

        public override void OnStartClient() { Current = this; }

        // ------------------------------------------------------------ server flow
        [Server]
        public void ServerStartRound()
        {
            if (state == RoundState.Countdown || state == RoundState.Playing) return;
            score = 0; success = false; stars = 0;
            foreach (var p in BFPlayer.All) { p.deliveredCount = 0; if (p.IsCarrying) p.ServerDropHeld(Vector3.zero); }
            ServerPrepareRound();
            state = RoundState.Countdown;
            stateEndTime = NetworkTime.time + countdownSeconds;
        }

        [Server]
        public void ServerEndRound(bool won)
        {
            if (state == RoundState.Ended) return;
            success = won;
            stars = ServerComputeStars(won);
            state = RoundState.Ended;
            stateEndTime = NetworkTime.time;
        }

        [ServerCallback]
        protected virtual void Update()
        {
            switch (state)
            {
                case RoundState.Countdown:
                    if (NetworkTime.time >= stateEndTime)
                    {
                        state = RoundState.Playing;
                        stateEndTime = NetworkTime.time + roundSeconds;
                        ServerOnRoundBegin();
                    }
                    break;
                case RoundState.Playing:
                    ServerTickRound();
                    if (state == RoundState.Playing && NetworkTime.time >= stateEndTime) ServerEndRound(ServerIsComplete());
                    break;
            }
        }

        [Server]
        public void ServerFeed(string msg, FeedKind kind) { RpcFeed(msg, kind); }

        [ClientRpc]
        void RpcFeed(string msg, FeedKind kind) { OnFeed?.Invoke(msg, kind); }

        // ------------------------------------------------------------ mod kancaları
        // (hepsi sadece sunucuda çağrılır)
        protected virtual void ServerOnLobby() { }
        protected abstract void ServerPrepareRound();
        protected virtual void ServerOnRoundBegin() { }
        protected virtual void ServerTickRound() { }
        protected abstract bool ServerIsComplete();
        protected abstract int ServerComputeStars(bool won);
        public abstract void ServerOnDeliver(BFPlayer player, ProductItem item, CheckoutZone zone);
    }
}
