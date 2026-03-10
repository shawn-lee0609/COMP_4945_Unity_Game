// Assets/Scripts/Storage/ScoreStorage.cs
// NAMESPACE: Storage
// Persistent local storage using Unity PlayerPrefs

using UnityEngine;

namespace Storage
{
    public static class ScoreStorage
    {
        private const string WinsKey = "bomberman_wins";
        private const string LossesKey = "bomberman_losses";
        private const string GamesKey = "bomberman_games";
        private const string NameKey = "bomberman_name";

        public static int Wins
        {
            get => PlayerPrefs.GetInt(WinsKey, 0);
            set { PlayerPrefs.SetInt(WinsKey, value); PlayerPrefs.Save(); }
        }
        public static int Losses
        {
            get => PlayerPrefs.GetInt(LossesKey, 0);
            set { PlayerPrefs.SetInt(LossesKey, value); PlayerPrefs.Save(); }
        }
        public static int GamesPlayed
        {
            get => PlayerPrefs.GetInt(GamesKey, 0);
            set { PlayerPrefs.SetInt(GamesKey, value); PlayerPrefs.Save(); }
        }
        public static string LastPlayerName
        {
            get => PlayerPrefs.GetString(NameKey, "Player");
            set { PlayerPrefs.SetString(NameKey, value); PlayerPrefs.Save(); }
        }

        public static void RecordWin() { Wins++; GamesPlayed++; }
        public static void RecordLoss() { Losses++; GamesPlayed++; }
        public static string GetSummary() => $"W:{Wins} L:{Losses} G:{GamesPlayed}";
    }
}
