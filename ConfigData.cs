using System;
using System.IO;
using System.Text.Json;

namespace MazeJump
{
    /// <summary>
    /// Gameplay tuning values. Persisted to config.json so they can be tweaked without recompiling.
    /// </summary>
    public class ConfigData
    {
        public int Gravity { get; set; } = 1400;
        public int Speed { get; set; } = 260;
        public int JumpVelocity { get; set; } = 480;
        public int MaxFallSpeed { get; set; } = 1200;

        // Game-feel tuning (in seconds)
        public float CoyoteTime { get; set; } = 0.1f;
        public float JumpBufferTime { get; set; } = 0.12f;

        public static ConfigData Default() => new ConfigData();

        /// <summary>
        /// Loads config from a JSON file. Falls back to defaults if the file is missing or invalid.
        /// </summary>
        public static ConfigData Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return Default();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize<ConfigData>(json);
                if (loaded == null)
                {
                    return Default();
                }

                loaded.Clamp();
                return loaded;
            }
            catch
            {
                return Default();
            }
        }

        /// <summary>
        /// Saves the config to a JSON file.
        /// </summary>
        public void Save(string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // ignore write failures
            }
        }

        /// <summary>
        /// Clamps values to sane ranges so a bad config can't break the game.
        /// </summary>
        public void Clamp()
        {
            Gravity = Math.Clamp(Gravity, 100, 10000);
            Speed = Math.Clamp(Speed, 50, 2000);
            JumpVelocity = Math.Clamp(JumpVelocity, 100, 3000);
            MaxFallSpeed = Math.Clamp(MaxFallSpeed, 200, 5000);
            CoyoteTime = Math.Clamp(CoyoteTime, 0f, 0.5f);
            JumpBufferTime = Math.Clamp(JumpBufferTime, 0f, 0.5f);
        }
    }
}