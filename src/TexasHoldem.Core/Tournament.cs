﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Darkhood.TexasHoldem.Core
{
    public class Tournament
    {
        public GameSettings Settings { get; set; }
        public List<Player> Players { get; set; }
        private Dictionary<int, int> PlayerMap { get; set; }

        private object _lockObj = new object();

        public int CurrentTurnPlayerId
        {
            get
            {
                return this.currentTurnPlayerId;
            }
        }

        public decimal CurrentBet
        {
            get
            {
                return this.currentBet;
            }
        }

        private GameState _gameState;

        public event EventHandler<GameStateEventArgs> GameStateChanged;
        public event EventHandler<PlayerActionEventArgs> OnPlayerAction;

        public GameState GameState
        {
            get { return _gameState; }
            private set
            {
                lock (_lockObj)
                {
                    this._gameState = value;
                    switch (_gameState)
                    {
                        case GameState.PreFlop:
                            NewHand();
                            break;
                        case GameState.Flop:
                            sharedCards.Add(deck.TakeCard());
                            sharedCards.Add(deck.TakeCard());
                            sharedCards.Add(deck.TakeCard());
                            break;
                        case GameState.Turn:
                        case GameState.River:
                            sharedCards.Add(deck.TakeCard());
                            break;
                    }
                }
                GameStateChanged?.Invoke(this, new GameStateEventArgs { State = _gameState });
            }
        }

        //private bool SetNextGameState()
        //{
        //    // TODO: check all conditions (players checked/folded or called)
        //    switch(GameState)
        //    {
        //        case GameState.PreFlop:
        //            GameState = GameState.Flop;
        //            return true;
        //        case GameState.Flop:
        //            GameState = GameState.Turn;
        //            return true;
        //        case GameState.Turn:
        //            GameState = GameState.River;
        //            return true;
        //        case GameState.Lobby:
        //            GameState = GameState.GameStarted;
        //            return true;
        //        case GameState.GameStarted:
        //            GameState = GameState.PreFlop;
        //            return true;
        //    }
        //    return false;
        //}

        public Tournament()
        {
            // TODO
            Settings = GameSettings.GetInstance();
            Players = new List<Player>();
            PlayerMap = new Dictionary<int, int>();

            this.GameState = GameState.Lobby;
            this.deck = Deck.CreateDeck(true);
            this.sharedCards = new List<Card>(5);
            this.blindRaiseSum = Settings.BlindRaiseSum;
            this.table = new Table(this);
        }

        public Tournament(Deck deck)
            : this()
        {
            this.deck = deck;
        }

        private decimal blindRaiseSum;
        private int currentTurnPlayerId;
        private int lastDealerId;
        private int handCount = 0;
        private Deck deck;
        private decimal currentBet;
        private List<Card> sharedCards;
        private Table table;

        public Player GetPlayer(int playerId)
        {
            if (!PlayerMap.ContainsKey(playerId)) return null;
            int playerIndex = PlayerMap[playerId];
            return Players[playerIndex];
        }

        private void NewHand()
        {
            if (Players.Count < 2)
                return;
            lock (_lockObj)
            {
                // Reset deck cursor and shuffle
                table.ClearPots();
                //pot = 0;
                currentBet = table.Stake;
                sharedCards.Clear();
                deck.Reset();
                deck.Shuffle();
                // Reset player hands and blinds
                for (int i = 0; i < Players.Count; i++)
                {
                    Player player = Players[i];
                    player.IsSmallBlind = false;
                    player.IsBigBlind = false;
                    player.IsDealer = false;
                    player.CallSum = 0;
                    player.CalledSum = 0;
                    player.Folded = false;
                    player.Checked = false;
                    player.StartingHand.Clear();
                    player.CurrentHand.Clear();

                    // Starting hand
                    player.AddCard(deck.TakeCard());
                    player.AddCard(deck.TakeCard());
                }

                // Arrange new blinds
                if (lastDealerId == 0)
                {
                    Player firstPlayer = Players[0];
                    lastDealerId = firstPlayer.PlayerId;
                    firstPlayer.IsDealer = true;
                    if (Players.Count > 2)
                    {
                        Players[1].IsSmallBlind = true;
                        Players[2].IsBigBlind = true;

                        int underTheGunIndex = 3 >= Players.Count ? 0 : 3;
                        currentTurnPlayerId = Players[underTheGunIndex].PlayerId;
                    }
                    else
                    {
                        // only two players (heads-up), 
                        // small blind is also a dealer 
                        Players[0].IsSmallBlind = true;
                        Players[0].IsDealer = true;
                        currentTurnPlayerId = Players[0].PlayerId;

                        Players[1].IsBigBlind = true;
                    }
                }
                else
                {
                    int newDealerIndex = PlayerMap[lastDealerId] + 1;
                    if (newDealerIndex >= Players.Count)
                    {
                        newDealerIndex = 0;
                    }
                    Players[newDealerIndex].IsDealer = true;
                    lastDealerId = Players[newDealerIndex].PlayerId;
                    int sbIndex = newDealerIndex + 1 >= Players.Count ? 0 : newDealerIndex + 1;
                    int bbIndex = newDealerIndex + 2 >= Players.Count ? 0 : newDealerIndex + 2;
                    Players[sbIndex].IsSmallBlind = true;
                    Players[bbIndex].IsBigBlind = true;

                    int underTheGunIndex = bbIndex + 1 >= Players.Count ? 0 : bbIndex + 1;
                    currentTurnPlayerId = Players[underTheGunIndex].PlayerId;
                }

                // Blinds bet
                foreach (Player player in Players)
                {
                    if (player.IsBigBlind)
                    {
                        player.CallSum = 0;
                        table.Contribute(player, table.Stake);
                    }
                    else if (player.IsSmallBlind)
                    {
                        decimal betSum = table.Stake / 2;
                        player.CallSum = betSum;
                        table.Contribute(player, betSum);
                    }
                    else
                    {
                        player.CallSum = table.Stake;
                    }
                }

                handCount++;
            }
        }

        public Card[] SharedCards
        {
            get
            {
                return sharedCards.ToArray();
            }
        }

        public Player CurrentTurnPlayer 
        {
            get 
            {
                int currentPlayerIndex = PlayerMap[currentTurnPlayerId];
                return Players[currentPlayerIndex];
            }
        }

        public Player GetNextTurnPlayer()
        {
            int currentPlayerIndex = PlayerMap[currentTurnPlayerId];
            Player nextTurnPlayer = null;

            do
            {
                int nextTurnPlayerIndex = currentPlayerIndex + 1 >= Players.Count ? 0 : currentPlayerIndex + 1;
                nextTurnPlayer = Players[nextTurnPlayerIndex];
                // If a player is out of chips, he checks anyway
                if (!nextTurnPlayer.Folded && nextTurnPlayer.Chips > 0)
                {
                    return nextTurnPlayer;
                }
                currentPlayerIndex = nextTurnPlayerIndex;
            } while (nextTurnPlayer.PlayerId != currentTurnPlayerId);
            return null;
        }

        private void NextPlayerTurn()
        {
            // TODO: Optimize this. Called or folded players can be counted once per hand. 
            int calledPlayerCount = 0;
            int foldedPlayerCount = 0;
            Player lastNotFoldedPlayer = null;
            foreach (Player p in Players)
            {
                if (!p.Folded && p.CallSum <= 0 && p.Checked)
                {
                    calledPlayerCount++;
                }
                if (p.Folded)
                {
                    foldedPlayerCount++;
                }
                else
                {
                    lastNotFoldedPlayer = p;
                }
            }

            // Check if anyone is eligible to take the pots
            if (foldedPlayerCount == Players.Count - 1)
            {
                if (lastNotFoldedPlayer == null)
                {
                    // TODO: throw exception
                }

                List<Pot> pots = table.Pots;
                foreach (Pot pot in pots)
                {
                    lastNotFoldedPlayer.Chips += pot.TotalChips;
                    pot.Clear();
                }

                this.GameState = GameState.PreFlop;
                return;
            }

            Player nextTurnPlayer = GetNextTurnPlayer();
            if (calledPlayerCount == Players.Count || nextTurnPlayer == null)
            {
                // change the hand state
                ResetHandRound();
                switch (GameState)
                {
                    case GameState.PreFlop:
                        GameState = GameState.Flop;
                        break;
                    case GameState.Flop:
                        GameState = GameState.Turn;
                        break;
                    case GameState.Turn:
                        GameState = GameState.River;
                        break;
                    case GameState.River:
                        Showdown();
                        break;
                }
            }
            if (nextTurnPlayer != null)
                currentTurnPlayerId = nextTurnPlayer.PlayerId;
        }

        private void ResetHandRound()
        {
            currentBet = table.Stake;
            // reset player round specific flags
            foreach (Player p in Players)
            {
                if (p.Folded)
                    continue;
                p.CallSum = 0;
                p.CalledSum = 0;
                if (p.Chips > 0)
                {
                    // if a player went All-In, he checks anyway
                    p.Checked = false;
                }
            }
        }

        private void Showdown()
        {
            if (this.GameState == GameState.PreFlop)
            {
                GameState = GameState.Flop;
            }
            else if (this.GameState == GameState.Flop)
            {
                GameState = GameState.Turn;
            }
            else if (this.GameState == GameState.Turn)
            {
                GameState = GameState.River;
            }

            // TODO: move this to Pot class

            // Using pot contributors array get contributors who did not fold
            // and determine if their hand is winning 
            foreach (Pot pot in table.Pots)
            {
                var contributors = pot.Contributions.Keys;
                if (contributors.Count == 0)
                    continue;
                foreach (Player contributor in contributors)
                {
                    if (contributor.Folded)
                        continue;
                    contributor.CurrentHand = contributor.GetBestPossibleHand(sharedCards);
                }

                // for each hand iterate through all other hands, if it's greater, increment its value
                // create hashMap of value (as a key) and player hands
                // Sort hands by rank DESC and get the top rank, 
                // get all hands from hashMap by this rank, those should be the winners

                // A map for one-to-many connection between hand value and players
                Dictionary<int, List<Player>> handValuePlayerMap = new Dictionary<int, List<Player>>();
                foreach (Player contributor in contributors)
                {
                    int handValue = 0;

                    foreach (Player otherContributor in contributors)
                    {
                        // skip self
                        if (otherContributor == contributor)
                            continue;
                        // if this contributor's hand is better that the other's, increment its value
                        if (contributor.CurrentHand.CompareTo(otherContributor.CurrentHand) > 0)
                        {
                            handValue++;
                        }
                    }
                    // Map players and their hand values
                    if (!handValuePlayerMap.ContainsKey(handValue))
                    {
                        handValuePlayerMap.Add(handValue, new List<Player>());
                    }
                    handValuePlayerMap[handValue].Add(contributor);
                }
                List<int> handValueKeys = new List<int>(handValuePlayerMap.Keys);
                handValueKeys.Sort();
                handValueKeys.Reverse();
                int highestHandValue = handValueKeys[0];

                // get winners with this hand value
                List<Player> potWinners = handValuePlayerMap[highestHandValue];
                foreach (Player winner in potWinners)
                {
                    pot.AddWinner(winner);
                }
                // split pot sum among winners
                if (potWinners.Count > 1)
                {
                    // TODO: optimization, same loop twice
                    decimal potChips = pot.TotalChips;
                    decimal winnerChips = potChips / potWinners.Count;
                    decimal remainingChips = potChips - (winnerChips * potWinners.Count);
                    foreach (Player winner in potWinners)
                    {
                        winner.Chips += winnerChips;
                    }
                    potWinners[0].Chips += remainingChips;
                }
                else
                {
                    potWinners[0].Chips += pot.TotalChips;
                }
            }

            GameState = GameState.Showdown;
        }

        public bool AddPlayer(Player player)
        {
            this.Players.Add(player);
            int index = Players.Count - 1;
            if (PlayerMap.ContainsKey(player.PlayerId))
            {
                throw new PlayerAlreadyEnteredException(player);
            }
            this.PlayerMap.Add(player.PlayerId, index);
            return true;
        }

        public void RemovePlayer(Player player)
        {
            throw new NotImplementedException("TODO: implement safe removal from the tournament");
        }

        public bool Fold(Player player)
        {
            return Fold(player.PlayerId);
        }

        public bool Fold(int playerId)
        {
            if (playerId != currentTurnPlayerId)
                return false;
            lock (_lockObj)
            {
                Player playerFold = null;
                int foldedPlayerCount = 0;
                foreach (Player player in Players)
                {
                    if (player.PlayerId == playerId)
                    {
                        playerFold = player;
                    }
                    if (player.Folded)
                    {
                        foldedPlayerCount++;
                    }
                }
                if (Players.Count - foldedPlayerCount == 1)
                {
                    // can't fold if there is only one player left
                    return false;
                }
                if (playerFold != null)
                {
                    playerFold.Folded = true;
                    NextPlayerTurn();
                    OnPlayerAction?.Invoke(this, new PlayerActionEventArgs { Player = playerFold });
                    return true;
                }
                return false;
            }
        }

        public void NextRound()
        {
            this.GameState = GameState.PreFlop;
        }

        public bool Bet(Player player, int betSum)
        {
            return Bet(player.PlayerId, betSum);
        }

        public bool Bet(int playerId, int betSum)
        {
            if (playerId != currentTurnPlayerId)
                return false;
            lock (_lockObj)
            {
                // Can't bet if there is already a bet. 
                // Player should raise instead.
                if (currentBet > table.Stake)
                {
                    return false;
                }
                // The minimum bet sum is current bet sum.
                if (betSum < currentBet)
                    return false;
                Player player = GetPlayer(playerId);
                if (player != null && player.Chips >= betSum)
                {
                    currentBet = betSum;
                    table.Contribute(player, betSum);
                    player.CalledSum = betSum;
                    player.CallSum = player.CallSum - betSum < 0 ? 0 : player.CallSum - betSum;

                    foreach (Player p in Players)
                    {
                        if (p == player || p.Folded)
                            continue;
                        p.CallSum = currentBet;
                    }
                    player.Checked = true;

                    //if (player.chips == 0) {
                    //// Forced to going All-In
                    //}

                    NextPlayerTurn();
                    OnPlayerAction?.Invoke(this, new PlayerActionEventArgs { Player = player });
                    return true;
                }
                return false;
            }
        }

        public bool Raise(Player player, decimal raiseSum)
        {
            return Raise(player.PlayerId, raiseSum);
        }

        public bool Raise(int playerId, decimal raiseSum)
        {
            if (playerId != currentTurnPlayerId)
                return false;
            lock (_lockObj)
            {
                // The minimum raise sum is current bet sum.
                if (raiseSum < currentBet)
                    return false;
                Player player = GetPlayer(playerId);
                if (player != null && player.Chips >= raiseSum)
                {
                    foreach (Player p in Players)
                    {
                        if (p == player || p.Folded)
                            continue;
                        decimal difference = raiseSum - p.CalledSum;
                        p.CallSum += difference;
                    }
                    player.CalledSum = raiseSum;
                    player.CallSum = player.CallSum - raiseSum < 0 ? 0 : player.CallSum - raiseSum;
                    currentBet = raiseSum;
                    table.Contribute(player, raiseSum);
                    player.Checked = true;
                    NextPlayerTurn();
                    OnPlayerAction?.Invoke(this, new PlayerActionEventArgs { Player = player });
                    return true;
                }
                return false;
            }
        }

        public bool CanCall(int playerId)
        {
            if (GameState == GameState.Showdown ||
                GameState == GameState.Lobby ||
                GameState == GameState.GameStarted)
                return false;
            if (playerId != currentTurnPlayerId)
                return false;
            Player player = GetPlayer(playerId);
            if (player == null) 
                return false;
            if (player.Folded || player.Checked)
                return false;
            if (player.CallSum > 0)
                return true;
            return false;
        }

        public bool CanCheck(int playerId)
        {
            if (GameState == GameState.Showdown ||
                GameState == GameState.Lobby ||
                GameState == GameState.GameStarted)
                return false;
            if (playerId != currentTurnPlayerId)
                return false;
            Player player = GetPlayer(playerId);
            if (player == null)
                return false;
            if (player.Folded || player.Checked)
                return false;
            if (player.CallSum > 0)
                return false;
            return true;
        }

        public bool Call(Player player)
        {
            return Call(player.PlayerId);
        }

        public bool Call(int playerId)
        {
            if (playerId != currentTurnPlayerId)
                return false;
            lock (_lockObj)
            {
                Player player = GetPlayer(playerId);
                if (player != null && player.CallSum > 0)
                {
                    if (player.Chips < player.CallSum)
                    {
                        player.CallSum -= player.Chips;
                        player.CalledSum += player.Chips;
                        // Forced to going All-In
                        table.Contribute(player, player.Chips);
                    }
                    else
                    {
                        player.CalledSum += player.CallSum;
                        player.CallSum = 0;
                        table.Contribute(player, player.CalledSum);
                    }
                    player.Checked = true;
                    NextPlayerTurn();
                    OnPlayerAction?.Invoke(this, new PlayerActionEventArgs { Player = player });
                    return true;
                }
                return false;
            }
        }

        public bool Check(Player player)
        {
            return Check(player.PlayerId);
        }

        public bool Check(int playerId)
        {
            if (playerId != currentTurnPlayerId)
                return false;
            lock (_lockObj)
            {
                Player player = GetPlayer(playerId);
                if (player != null)
                {
                    // if there wasn't any bet or raise,
                    // the player is eligible to check
                    if (player.CallSum == 0)
                    {
                        player.Checked = true;
                        NextPlayerTurn();
                        OnPlayerAction?.Invoke(this, new PlayerActionEventArgs { Player = player });
                        return true;
                    }
                }
                return false;
            }
        }
    }

}
