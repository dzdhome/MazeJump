using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace JumpGameMonoGame
{
    public class JumpGame : Game
    {
        private enum GameMode
        {
            Play,
            Editor,
            Settings
        }

        // Editor tile palette
        private static readonly int[] EditorTileTypes = { MapData.TileEmpty, MapData.TileSolid, MapData.TileEntrance, MapData.TileExit, MapData.TileLava };
        private static readonly string[] EditorTileNames = { "空", "方块", "入口", "出口", "岩浆" };
        private static readonly Color[] EditorTileColors = { Color.Gray, Color.LightSlateGray, Color.LimeGreen, Color.Gold, Color.OrangeRed };

        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch = null!;

        // Game state
        private MapCollection mapCollection = null!;
        private ConfigData config = null!;
        private GameMode mode = GameMode.Play;
        private bool isPlaying;
        private bool leftPressed;
        private bool rightPressed;
        private bool jumpPressed;
        private bool jumpConsumed;
        private float playerX;
        private float playerY;
        private float playerVx;
        private float playerVy;
        private bool grounded;
        private bool win;
        private string statusMessage = string.Empty;

        // Input state (edge detection)
        private KeyboardState previousKeyboardState;
        private MouseState previousMouseState;

        // Game-feel timers (seconds)
        private float coyoteTimer;
        private float jumpBufferTimer;

        // Play mode UI buttons
        private Rectangle playStartButton;
        private Rectangle playEditorButton;
        private Rectangle playSettingsButton;
        private Rectangle playPrevMapButton;
        private Rectangle playNextMapButton;

        // Editor state
        private int editorSelectedTile = MapData.TileSolid;
        private bool editorDirty;
        private int currentMapIndex = 0; // 0-based index into mapCollection.Maps
        private const int MaxMapCount = 9;
        private Rectangle[] editorTileButtons = new Rectangle[5];
        private Rectangle editorPrevMapButton;
        private Rectangle editorNextMapButton;
        private Rectangle editorExitButton;
        private Rectangle editorSaveButton;
        private Rectangle editorLoadButton;

        // Settings state
        private int settingsIndex;
        private int[] settingsValues = new int[3];
        private string[] settingsNames = { "重力加速度", "起跳初速度", "水平跑动速度" };
        private int[] settingsMin = { 100, 100, 50 };
        private int[] settingsMax = { 10000, 3000, 2000 };
        private int[] settingsStep = { 50, 10, 10 };
        private Rectangle[] settingsRowRects = new Rectangle[3];
        private Rectangle[] settingsMinusButtons = new Rectangle[3];
        private Rectangle[] settingsPlusButtons = new Rectangle[3];

        private const int TileSize = 40;
        private const int PlayerWidth = 22;
        private const int PlayerHeight = 36;
        private const int StatusBarHeight = 96;
        private readonly string mapsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "maps.json");
        private readonly string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        // Rendering
        private Texture2D pixelTexture = null!;

        public JumpGame()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Fixed 60 FPS timestep for consistent physics
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);

            // Set window size
            graphics.PreferredBackBufferWidth = MapData.Columns * TileSize;
            graphics.PreferredBackBufferHeight = MapData.Rows * TileSize + StatusBarHeight;
            graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            mapCollection = MapCollection.CreateDefault(MaxMapCount);
            config = ConfigData.Load(configFilePath);
            LoadMapsFile();
            ResetPlayer();
            statusMessage = "游戏准备就绪。按 G 开始游戏。";

            // Create 1x1 white pixel texture for drawing rectangles
            pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            InitializePlayButtons();

            base.Initialize();
        }

        private void InitializePlayButtons()
        {
            int btnY = MapData.Rows * TileSize + 8;
            int btnH = 24;
            int btnW = 90;

            playStartButton = new Rectangle(10, btnY, btnW, btnH);
            playEditorButton = new Rectangle(110, btnY, btnW, btnH);
            playSettingsButton = new Rectangle(210, btnY, btnW, btnH);

            // Map navigation buttons on second row
            int mapY = MapData.Rows * TileSize + 40;
            playPrevMapButton = new Rectangle(10, mapY, 30, btnH);
            playNextMapButton = new Rectangle(150, mapY, 30, btnH);
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            switch (mode)
            {
                case GameMode.Play:
                    UpdatePlay(keyboardState, mouseState, gameTime);
                    break;
                case GameMode.Editor:
                    UpdateEditor(keyboardState, mouseState);
                    break;
                case GameMode.Settings:
                    UpdateSettings(keyboardState, mouseState);
                    break;
            }

            previousKeyboardState = keyboardState;
            previousMouseState = mouseState;
            base.Update(gameTime);
        }

        /// <summary>
        /// Edge-triggered key press: true only on the frame the key is first pressed down.
        /// </summary>
        private bool WasKeyPressed(KeyboardState state, Keys key)
        {
            return state.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
        }

        /// <summary>
        /// Edge-triggered left click: true only on the frame the left mouse button is first pressed.
        /// </summary>
        private bool WasLeftClicked(MouseState state)
        {
            return state.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
        }

        // ==================== PLAY MODE ====================

        private void UpdatePlay(KeyboardState keyboardState, MouseState mouseState, GameTime gameTime)
        {
            // Start/restart game (works both before starting and after winning)
            if (WasKeyPressed(keyboardState, Keys.G) && !isPlaying)
            {
                ResetPlayer();
                isPlaying = true;
                statusMessage = "游戏进行中：A/D 移动，W/空格 跳跃，ESC 退出。";
            }

            // Pause game
            if (WasKeyPressed(keyboardState, Keys.Escape))
            {
                if (isPlaying)
                {
                    isPlaying = false;
                    statusMessage = "游戏暂停。按 G 继续。";
                }
            }

            // Enter editor
            if (WasKeyPressed(keyboardState, Keys.E))
            {
                EnterEditor();
                return;
            }

            // Enter settings
            if (WasKeyPressed(keyboardState, Keys.C))
            {
                EnterSettings();
                return;
            }

            // Mouse click on UI buttons
            if (WasLeftClicked(mouseState))
            {
                if (playStartButton.Contains(mouseState.X, mouseState.Y))
                {
                    ResetPlayer();
                    isPlaying = true;
                    statusMessage = "游戏进行中：A/D 移动，W/空格 跳跃，ESC 退出。";
                }
                else if (playEditorButton.Contains(mouseState.X, mouseState.Y))
                {
                    EnterEditor();
                    return;
                }
                else if (playSettingsButton.Contains(mouseState.X, mouseState.Y))
                {
                    EnterSettings();
                    return;
                }
                else if (playPrevMapButton.Contains(mouseState.X, mouseState.Y))
                {
                    SwitchMap(currentMapIndex - 1);
                }
                else if (playNextMapButton.Contains(mouseState.X, mouseState.Y))
                {
                    SwitchMap(currentMapIndex + 1);
                }
            }

            // Handle input
            HandleInput(keyboardState);

            // Update physics
            if (isPlaying)
            {
                UpdatePhysics(gameTime);
            }
        }

        private void HandleInput(KeyboardState keyboardState)
        {
            if (!isPlaying) return;

            // Movement input (continuous hold is intended)
            bool aPressed = keyboardState.IsKeyDown(Keys.A);
            bool dPressed = keyboardState.IsKeyDown(Keys.D);
            bool wPressed = keyboardState.IsKeyDown(Keys.W);
            bool spacePressed = keyboardState.IsKeyDown(Keys.Space);

            leftPressed = aPressed;
            rightPressed = dPressed;
            jumpPressed = wPressed || spacePressed;
        }

        private void UpdatePhysics(GameTime gameTime)
        {
            if (win) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0.05f) dt = 0.05f; // Cap max frame time

            float speed = config.Speed;
            float gravity = config.Gravity;
            float jumpSpeed = config.JumpVelocity;

            // Update game-feel timers
            coyoteTimer = grounded ? config.CoyoteTime : Math.Max(0f, coyoteTimer - dt);
            jumpBufferTimer = jumpPressed ? config.JumpBufferTime : Math.Max(0f, jumpBufferTimer - dt);

            // Horizontal movement
            float move = 0;
            if (leftPressed) move -= 1;
            if (rightPressed) move += 1;
            playerVx = move * speed;

            // Jumping: buffered input + coyote time, with re-arm on landing
            if (jumpBufferTimer > 0f && coyoteTimer > 0f && !jumpConsumed)
            {
                playerVy = -jumpSpeed;
                grounded = false;
                coyoteTimer = 0f;
                jumpBufferTimer = 0f;
                jumpConsumed = true;
            }

            // Apply gravity
            playerVy += gravity * dt;
            playerVy = Math.Min(playerVy, config.MaxFallSpeed);

            // Move player
            MoveHorizontal(playerVx * dt);
            MoveVertical(playerVy * dt);

            // Re-arm jump when landing
            if (grounded)
            {
                jumpConsumed = false;
            }

            // Check lava collision
            if (CheckTileCollision(MapData.TileLava))
            {
                ResetPlayer();
                statusMessage = "踩到岩浆！已回到入口。";
                return;
            }

            // Check exit
            if (CheckExit())
            {
                win = true;
                isPlaying = false;
                statusMessage = "恭喜通关！按 G 重新开始。";
            }
        }

        private void MoveHorizontal(float delta)
        {
            if (delta == 0) return;

            playerX += delta;
            int leftCol = (int)Math.Floor(playerX / TileSize);
            int rightCol = (int)Math.Floor((playerX + PlayerWidth - 1) / TileSize);
            int topRow = (int)Math.Floor(playerY / TileSize);
            int bottomRow = (int)Math.Floor((playerY + PlayerHeight - 1) / TileSize);

            for (int row = topRow; row <= bottomRow; row++)
            {
                int testCol = delta > 0 ? (int)Math.Floor((playerX + PlayerWidth) / TileSize) : leftCol;
                if (IsSolid(testCol, row))
                {
                    playerX = delta > 0 ? testCol * TileSize - PlayerWidth : (testCol + 1) * TileSize;
                    playerVx = 0;
                    break;
                }
            }

            // Bounds check
            if (playerX < 0) playerX = 0;
            if (playerX + PlayerWidth > MapData.Columns * TileSize) playerX = MapData.Columns * TileSize - PlayerWidth;
        }

        private void MoveVertical(float delta)
        {
            if (delta == 0) return;

            playerY += delta;
            int leftCol = (int)Math.Floor(playerX / TileSize);
            int rightCol = (int)Math.Floor((playerX + PlayerWidth - 1) / TileSize);
            int testRow = delta > 0 ? (int)Math.Floor((playerY + PlayerHeight) / TileSize) : (int)Math.Floor(playerY / TileSize);
            grounded = false;

            for (int col = leftCol; col <= rightCol; col++)
            {
                if (IsSolid(col, testRow))
                {
                    if (delta > 0)
                    {
                        playerY = testRow * TileSize - PlayerHeight;
                        grounded = true;
                    }
                    else
                    {
                        playerY = (testRow + 1) * TileSize;
                    }
                    playerVy = 0;
                    break;
                }
            }

            // Bounds check
            if (playerY < 0)
            {
                playerY = 0;
                playerVy = 0;
            }
            if (playerY + PlayerHeight > MapData.Rows * TileSize)
            {
                playerY = MapData.Rows * TileSize - PlayerHeight;
                grounded = true;
                playerVy = 0;
            }
        }

        private bool IsSolid(int col, int row)
        {
            return row >= 0 && row < MapData.Rows && col >= 0 && col < MapData.Columns && CurrentMap.Grid[row][col] == MapData.TileSolid;
        }

        private bool CheckTileCollision(int type)
        {
            int leftCol = (int)Math.Floor(playerX / TileSize);
            int rightCol = (int)Math.Floor((playerX + PlayerWidth - 1) / TileSize);
            int topRow = (int)Math.Floor(playerY / TileSize);
            int bottomRow = (int)Math.Floor((playerY + PlayerHeight - 1) / TileSize);

            for (int row = topRow; row <= bottomRow; row++)
            {
                for (int col = leftCol; col <= rightCol; col++)
                {
                    if (row >= 0 && row < MapData.Rows && col >= 0 && col < MapData.Columns && CurrentMap.Grid[row][col] == type)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CheckExit()
        {
            int leftCol = (int)Math.Floor(playerX / TileSize);
            int rightCol = (int)Math.Floor((playerX + PlayerWidth - 1) / TileSize);
            int topRow = (int)Math.Floor(playerY / TileSize);
            int bottomRow = (int)Math.Floor((playerY + PlayerHeight - 1) / TileSize);

            for (int row = topRow; row <= bottomRow; row++)
            {
                for (int col = leftCol; col <= rightCol; col++)
                {
                    if (row >= 0 && row < MapData.Rows && col >= 0 && col < MapData.Columns && CurrentMap.Grid[row][col] == MapData.TileExit)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private MapData CurrentMap => mapCollection.Maps[currentMapIndex];

        private void ResetPlayer()
        {
            var entrance = FindTile(MapData.TileEntrance);
            if (entrance.HasValue)
            {
                playerX = entrance.Value.Col * TileSize + (TileSize - PlayerWidth) / 2;
                playerY = entrance.Value.Row * TileSize - PlayerHeight;
            }
            else
            {
                playerX = TileSize + (TileSize - PlayerWidth) / 2;
                playerY = MapData.Rows * TileSize - TileSize - PlayerHeight;
            }
            playerVx = 0;
            playerVy = 0;
            grounded = false;
            win = false;
            jumpConsumed = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
        }

        private (int Row, int Col)? FindTile(int type)
        {
            for (int row = 0; row < MapData.Rows; row++)
            {
                for (int col = 0; col < MapData.Columns; col++)
                {
                    if (CurrentMap.Grid[row][col] == type)
                    {
                        return (row, col);
                    }
                }
            }
            return null;
        }

        // ==================== EDITOR MODE ====================

        private void EnterEditor()
        {
            mode = GameMode.Editor;
            isPlaying = false;
            editorDirty = false;

            // Build toolbar button rectangles
            int topY = MapData.Rows * TileSize + 8;
            int btnW = 88;
            int btnH = 24;
            int x = 10;
            for (int i = 0; i < editorTileButtons.Length; i++)
            {
                editorTileButtons[i] = new Rectangle(x, topY, btnW, btnH);
                x += btnW + 8;
            }

            // Map navigation buttons - placed after the tile buttons with extra spacing
            int mapX = x + 20;
            editorPrevMapButton = new Rectangle(mapX, topY, 30, btnH);
            editorNextMapButton = new Rectangle(mapX + 40, topY, 30, btnH);

            // Exit editor button (placed to the right of the map navigation)
            editorExitButton = new Rectangle(mapX + 150, topY, 70, btnH);

            // Save / load buttons on second row
            int row2Y = MapData.Rows * TileSize + 40;
            editorSaveButton = new Rectangle(10, row2Y, 70, btnH);
            editorLoadButton = new Rectangle(88, row2Y, 70, btnH);

            statusMessage = "左键绘制，右键擦除；1-5 选方块；鼠标或 [ ] 切换地图；S 保存，L 读取，E 退出。";
        }

        private void UpdateEditor(KeyboardState keyboardState, MouseState mouseState)
        {
            // Exit editor
            if (WasKeyPressed(keyboardState, Keys.E))
            {
                mode = GameMode.Play;
                statusMessage = "已退出编辑器。按 G 开始游戏。";
                return;
            }

            // Save all maps
            if (WasKeyPressed(keyboardState, Keys.S))
            {
                SaveMapsFile();
                editorDirty = false;
                statusMessage = $"全部地图已保存到 maps.json。";
            }

            // Load all maps
            if (WasKeyPressed(keyboardState, Keys.L))
            {
                LoadMapsFile();
                editorDirty = false;
                statusMessage = "已从 maps.json 读取全部地图。";
            }

            // Switch map with [ ]
            if (WasKeyPressed(keyboardState, Keys.OemOpenBrackets))
            {
                SwitchMap(currentMapIndex - 1);
            }
            if (WasKeyPressed(keyboardState, Keys.OemCloseBrackets))
            {
                SwitchMap(currentMapIndex + 1);
            }

            // Select tile type with number keys
            if (WasKeyPressed(keyboardState, Keys.D1)) editorSelectedTile = MapData.TileEmpty;
            if (WasKeyPressed(keyboardState, Keys.D2)) editorSelectedTile = MapData.TileSolid;
            if (WasKeyPressed(keyboardState, Keys.D3)) editorSelectedTile = MapData.TileEntrance;
            if (WasKeyPressed(keyboardState, Keys.D4)) editorSelectedTile = MapData.TileExit;
            if (WasKeyPressed(keyboardState, Keys.D5)) editorSelectedTile = MapData.TileLava;

            // Mouse click on toolbar buttons
            if (WasLeftClicked(mouseState))
            {
                for (int i = 0; i < editorTileButtons.Length; i++)
                {
                    if (editorTileButtons[i].Contains(mouseState.X, mouseState.Y))
                    {
                        editorSelectedTile = EditorTileTypes[i];
                        break;
                    }
                }
                if (editorPrevMapButton.Contains(mouseState.X, mouseState.Y)) SwitchMap(currentMapIndex - 1);
                else if (editorNextMapButton.Contains(mouseState.X, mouseState.Y)) SwitchMap(currentMapIndex + 1);
                else if (editorExitButton.Contains(mouseState.X, mouseState.Y))
                {
                    mode = GameMode.Play;
                    statusMessage = "已退出编辑器。按 G 开始游戏。";
                    return;
                }
                else if (editorSaveButton.Contains(mouseState.X, mouseState.Y))
                {
                    SaveMapsFile();
                    editorDirty = false;
                    statusMessage = "全部地图已保存到 maps.json。";
                }
                else if (editorLoadButton.Contains(mouseState.X, mouseState.Y))
                {
                    LoadMapsFile();
                    editorDirty = false;
                    statusMessage = "已从 maps.json 读取全部地图。";
                }
            }

            // Paint with mouse (continuous while pressed, only on the map area)
            if (mouseState.X >= 0 && mouseState.X < MapData.Columns * TileSize &&
                mouseState.Y >= 0 && mouseState.Y < MapData.Rows * TileSize)
            {
                int col = mouseState.X / TileSize;
                int row = mouseState.Y / TileSize;

                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    if (CurrentMap.Grid[row][col] != editorSelectedTile)
                    {
                        CurrentMap.Grid[row][col] = editorSelectedTile;
                        editorDirty = true;
                    }
                }
                if (mouseState.RightButton == ButtonState.Pressed)
                {
                    if (CurrentMap.Grid[row][col] != MapData.TileEmpty)
                    {
                        CurrentMap.Grid[row][col] = MapData.TileEmpty;
                        editorDirty = true;
                    }
                }
            }
        }

        private void SwitchMap(int newIndex)
        {
            if (newIndex < 0 || newIndex >= MaxMapCount || newIndex == currentMapIndex) return;

            // Auto-save unsaved changes before switching
            if (editorDirty)
            {
                SaveMapsFile();
            }

            currentMapIndex = newIndex;
            editorDirty = false;
            ResetPlayer();
            statusMessage = $"已切换到地图 {currentMapIndex + 1}/{MaxMapCount}。";
        }

        // ==================== SETTINGS MODE ====================

        private void EnterSettings()
        {
            mode = GameMode.Settings;
            isPlaying = false;
            settingsIndex = 0;
            settingsValues[0] = config.Gravity;
            settingsValues[1] = config.JumpVelocity;
            settingsValues[2] = config.Speed;

            // Build layout rectangles
            int panelWidth = 460;
            int panelHeight = 250;
            int panelX = (MapData.Columns * TileSize - panelWidth) / 2;
            int panelY = (MapData.Rows * TileSize - panelHeight) / 2;

            int rowY = panelY + 58;
            int rowHeight = 42;
            for (int i = 0; i < 3; i++)
            {
                settingsRowRects[i] = new Rectangle(panelX + 15, rowY, panelWidth - 30, rowHeight);
                settingsMinusButtons[i] = new Rectangle(panelX + 330, rowY + 8, 30, 26);
                settingsPlusButtons[i] = new Rectangle(panelX + 370, rowY + 8, 30, 26);
                rowY += rowHeight + 5;
            }

            statusMessage = "设置：点击选择，或 ↑/↓ 选择；点击 - + 或 ←/→ 调整；Enter 保存，ESC 退出。";
        }

        private void UpdateSettings(KeyboardState keyboardState, MouseState mouseState)
        {
            // Exit settings
            if (WasKeyPressed(keyboardState, Keys.Escape))
            {
                mode = GameMode.Play;
                statusMessage = "已退出设置。按 G 开始游戏。";
                return;
            }

            // Save settings with Enter
            if (WasKeyPressed(keyboardState, Keys.Enter))
            {
                config.Gravity = settingsValues[0];
                config.JumpVelocity = settingsValues[1];
                config.Speed = settingsValues[2];
                config.Save(configFilePath);
                statusMessage = "设置已保存到 config.json。";
            }

            // Select setting with arrows / W S
            if (WasKeyPressed(keyboardState, Keys.Up) || WasKeyPressed(keyboardState, Keys.W))
            {
                settingsIndex = (settingsIndex + settingsNames.Length - 1) % settingsNames.Length;
            }
            if (WasKeyPressed(keyboardState, Keys.Down) || WasKeyPressed(keyboardState, Keys.S))
            {
                settingsIndex = (settingsIndex + 1) % settingsNames.Length;
            }

            // Adjust value
            if (WasKeyPressed(keyboardState, Keys.Left) || WasKeyPressed(keyboardState, Keys.A))
            {
                AdjustSetting(-1);
            }
            if (WasKeyPressed(keyboardState, Keys.Right) || WasKeyPressed(keyboardState, Keys.D))
            {
                AdjustSetting(1);
            }

            // Mouse interaction
            if (WasLeftClicked(mouseState))
            {
                for (int i = 0; i < 3; i++)
                {
                    // Click on +/- buttons adjusts and selects that row
                    if (settingsMinusButtons[i].Contains(mouseState.X, mouseState.Y))
                    {
                        settingsIndex = i;
                        AdjustSetting(-1);
                        return;
                    }
                    if (settingsPlusButtons[i].Contains(mouseState.X, mouseState.Y))
                    {
                        settingsIndex = i;
                        AdjustSetting(1);
                        return;
                    }
                    // Click on row selects it
                    if (settingsRowRects[i].Contains(mouseState.X, mouseState.Y))
                    {
                        settingsIndex = i;
                        return;
                    }
                }
            }
        }

        private void AdjustSetting(int direction)
        {
            settingsValues[settingsIndex] = Math.Clamp(
                settingsValues[settingsIndex] + direction * settingsStep[settingsIndex],
                settingsMin[settingsIndex],
                settingsMax[settingsIndex]);
        }

        // ==================== MAP FILE I/O ====================

        private void LoadMapsFile()
        {
            if (!File.Exists(mapsFilePath))
            {
                SaveMapsFile();
                return;
            }
            try
            {
                var json = File.ReadAllText(mapsFilePath);
                var loaded = JsonSerializer.Deserialize<MapCollection>(json);
                if (loaded != null && IsValidCollection(loaded))
                {
                    mapCollection = loaded;
                }
                else
                {
                    mapCollection = MapCollection.CreateDefault(MaxMapCount);
                }
            }
            catch
            {
                mapCollection = MapCollection.CreateDefault(MaxMapCount);
            }
        }

        /// <summary>
        /// Validates that a loaded collection has the correct number of maps and valid dimensions.
        /// </summary>
        private static bool IsValidCollection(MapCollection collection)
        {
            if (collection.Maps == null || collection.Maps.Count != MaxMapCount)
            {
                return false;
            }
            foreach (var map in collection.Maps)
            {
                if (map == null || map.Grid == null || map.Grid.Length != MapData.Rows)
                {
                    return false;
                }
                foreach (var row in map.Grid)
                {
                    if (row == null || row.Length != MapData.Columns)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void SaveMapsFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(mapCollection, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(mapsFilePath, json);
            }
            catch
            {
                // ignore write failures
            }
        }

        // ==================== DRAWING ====================

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // Draw map
            DrawMap();

            // Draw player (only in play mode)
            if (mode == GameMode.Play)
            {
                DrawPlayer();
            }

            // Draw mode-specific UI
            switch (mode)
            {
                case GameMode.Play:
                    DrawStatusBar();
                    break;
                case GameMode.Editor:
                    DrawEditorUI();
                    break;
                case GameMode.Settings:
                    DrawSettingsUI();
                    break;
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawStatusBar()
        {
            // Draw toolbar background
            var toolbarRect = new Rectangle(0, MapData.Rows * TileSize, MapData.Columns * TileSize, StatusBarHeight);
            DrawFilledRectangle(toolbarRect, new Color(30, 30, 40, 220));

            // Row 1: mode buttons
            var startRect = playStartButton;
            var editorBtnRect = playEditorButton;
            var settingsBtnRect = playSettingsButton;

            DrawFilledRectangle(startRect, isPlaying ? new Color(80, 120, 80) : new Color(50, 80, 50));
            DrawFilledRectangle(editorBtnRect, new Color(50, 50, 80));
            DrawFilledRectangle(settingsBtnRect, new Color(80, 60, 50));
            DrawRectangleOutline(startRect, isPlaying ? Color.GreenYellow : Color.Green);
            DrawRectangleOutline(editorBtnRect, Color.Blue);
            DrawRectangleOutline(settingsBtnRect, Color.Orange);
            DrawText(isPlaying ? "游戏中" : "开始游戏", 13, new Vector2(startRect.X + 14, startRect.Y + 3), Color.White);
            DrawText("地图编辑", 13, new Vector2(editorBtnRect.X + 14, editorBtnRect.Y + 3), Color.White);
            DrawText("设置", 13, new Vector2(settingsBtnRect.X + 28, settingsBtnRect.Y + 3), Color.White);

            // Row 2: map selection
            var prevRect = playPrevMapButton;
            var nextRect = playNextMapButton;
            DrawFilledRectangle(prevRect, new Color(50, 50, 70));
            DrawFilledRectangle(nextRect, new Color(50, 50, 70));
            DrawRectangleOutline(prevRect, Color.Gray);
            DrawRectangleOutline(nextRect, Color.Gray);
            DrawText("◀", 14, new Vector2(prevRect.X + 8, prevRect.Y + 2), Color.White);
            DrawText("▶", 14, new Vector2(nextRect.X + 8, nextRect.Y + 2), Color.White);

            // Map label (between prev and next buttons)
            DrawText($"地图 {currentMapIndex + 1}/{MaxMapCount}", 14,
                new Vector2(prevRect.X + 38, prevRect.Y + 2), Color.White);

            // Key hints on second row right side
            DrawText("G 开始/继续  E 编辑器  C 设置", 13,
                new Vector2(230, MapData.Rows * TileSize + 44), Color.LightGray);

            // Status message below
            DrawText(statusMessage, 14, new Vector2(10, MapData.Rows * TileSize + 68), Color.White);
        }

        private void DrawEditorUI()
        {
            // Draw editor toolbar background
            var toolbarRect = new Rectangle(0, MapData.Rows * TileSize, MapData.Columns * TileSize, StatusBarHeight);
            DrawFilledRectangle(toolbarRect, new Color(30, 30, 40, 220));

            // Row 1: tile palette buttons
            for (int i = 0; i < editorTileButtons.Length; i++)
            {
                bool selected = editorSelectedTile == EditorTileTypes[i];
                var btnRect = editorTileButtons[i];

                DrawFilledRectangle(btnRect, selected ? new Color(80, 80, 120) : new Color(50, 50, 70));
                DrawRectangleOutline(btnRect, selected ? Color.Yellow : Color.Gray);

                // Color swatch
                DrawFilledRectangle(new Rectangle(btnRect.X + 4, btnRect.Y + 5, 14, 14), EditorTileColors[i]);
                DrawRectangleOutline(new Rectangle(btnRect.X + 4, btnRect.Y + 5, 14, 14), Color.White);

                // Label
                DrawText(EditorTileNames[i], 12, new Vector2(btnRect.X + 24, btnRect.Y + 4), Color.White);
            }

            // Map navigation buttons (with label between them)
            // 1. 绘制左按钮
            var prevRect = editorPrevMapButton;
            DrawFilledRectangle(prevRect, new Color(50, 50, 70));
            DrawRectangleOutline(prevRect, Color.Gray);
            DrawText("◀", 14, new Vector2(prevRect.X + 8, prevRect.Y + 2), Color.White);

            // 2. 绘制中间文字 (在左按钮右侧，留出适量间距，如 38 像素)
            DrawText($"地图 {currentMapIndex + 1}/{MaxMapCount}", 14,
                new Vector2(prevRect.X + 38, prevRect.Y + 2), Color.White);

            // 3. 重新设置右按钮的位置：位于左按钮 + 间距（例如 110 像素，确保避开文字）
            // 如果你的 editorNextMapButton 本身就有独立 Rect，可以直接更新它的 X 坐标
            var nextRect = editorNextMapButton;
            nextRect.X = prevRect.X + 110; // 调整此数值以留出文本空间
            editorNextMapButton = nextRect; // 更新按钮碰撞/点击区域

            DrawFilledRectangle(nextRect, new Color(50, 50, 70));
            DrawRectangleOutline(nextRect, Color.Gray);
            DrawText("▶", 14, new Vector2(nextRect.X + 8, nextRect.Y + 2), Color.White);

            // Exit editor button (to the right of map navigation)
            var exitRect = editorExitButton;
            DrawFilledRectangle(exitRect, new Color(80, 40, 40));
            DrawRectangleOutline(exitRect, Color.Red);
            DrawText("退出编辑", 13, new Vector2(exitRect.X + 10, exitRect.Y + 3), Color.White);

            // Row 2: save / load buttons
            var saveRect = editorSaveButton;
            var loadRect = editorLoadButton;
            DrawFilledRectangle(saveRect, new Color(50, 80, 50));
            DrawFilledRectangle(loadRect, new Color(50, 50, 80));
            DrawRectangleOutline(saveRect, Color.Green);
            DrawRectangleOutline(loadRect, Color.Blue);
            DrawText("保存", 13, new Vector2(saveRect.X + 18, saveRect.Y + 3), Color.White);
            DrawText("读取", 13, new Vector2(loadRect.X + 18, loadRect.Y + 3), Color.White);

            // Status message (operation hints and feedback)
            string dirtyText = editorDirty ? "（未保存）" : "";
            DrawText(statusMessage, 13, new Vector2(170, MapData.Rows * TileSize + 46), editorDirty ? Color.Orange : Color.LightGreen);
        }

        private void DrawSettingsUI()
        {
            // Draw semi-transparent overlay
            var overlay = new Rectangle(0, 0, MapData.Columns * TileSize, MapData.Rows * TileSize + StatusBarHeight);
            DrawFilledRectangle(overlay, new Color(0, 0, 0, 180));

            // Draw settings panel
            int panelWidth = 460;
            int panelHeight = 250;
            int panelX = (MapData.Columns * TileSize - panelWidth) / 2;
            int panelY = (MapData.Rows * TileSize - panelHeight) / 2;

            var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);
            DrawFilledRectangle(panelRect, new Color(40, 40, 60));
            DrawRectangleOutline(panelRect, Color.White);

            // Title
            DrawText("游戏设置", 22, new Vector2(panelX + 20, panelY + 15), Color.White);

            // Draw settings rows
            for (int i = 0; i < settingsNames.Length; i++)
            {
                bool selected = i == settingsIndex;
                var rowRect = settingsRowRects[i];

                // Highlight selected row
                if (selected)
                {
                    DrawFilledRectangle(rowRect, new Color(70, 70, 110));
                }

                // Name and value
                DrawText(settingsNames[i], 16, new Vector2(panelX + 25, rowRect.Y + 10), selected ? Color.Yellow : Color.White);
                DrawText(settingsValues[i].ToString(), 16, new Vector2(panelX + 200, rowRect.Y + 10), selected ? Color.Yellow : Color.White);

                // - / + buttons (mouse clickable)
                var minusRect = settingsMinusButtons[i];
                var plusRect = settingsPlusButtons[i];
                DrawFilledRectangle(minusRect, new Color(60, 60, 90));
                DrawFilledRectangle(plusRect, new Color(60, 60, 90));
                DrawRectangleOutline(minusRect, selected ? Color.Yellow : Color.Gray);
                DrawRectangleOutline(plusRect, selected ? Color.Yellow : Color.Gray);
                DrawText("-", 16, new Vector2(minusRect.X + 9, minusRect.Y + 2), Color.White);
                DrawText("+", 16, new Vector2(plusRect.X + 9, plusRect.Y + 2), Color.White);
            }

            // Hint text
            DrawText(statusMessage, 12, new Vector2(panelX + 20, panelY + panelHeight - 35), Color.LightGray);
        }

        private void DrawMap()
        {
            for (int row = 0; row < MapData.Rows; row++)
            {
                for (int col = 0; col < MapData.Columns; col++)
                {
                    int type = CurrentMap.Grid[row][col];
                    if (type == MapData.TileEmpty) continue;

                    var rect = new Rectangle(col * TileSize, row * TileSize, TileSize, TileSize);
                    Color tileColor = type switch
                    {
                        MapData.TileSolid => Color.LightSlateGray,
                        MapData.TileEntrance => Color.LimeGreen,
                        MapData.TileExit => Color.Gold,
                        MapData.TileLava => Color.OrangeRed,
                        _ => Color.White
                    };

                    DrawFilledRectangle(rect, tileColor);
                    DrawRectangleOutline(rect, Color.White);
                }
            }

            // Draw grid
            Color gridColor = Color.White * 0.2f;
            for (int x = 0; x <= MapData.Columns * TileSize; x += TileSize)
            {
                DrawLine(new Vector2(x, 0), new Vector2(x, MapData.Rows * TileSize), gridColor);
            }
            for (int y = 0; y <= MapData.Rows * TileSize; y += TileSize)
            {
                DrawLine(new Vector2(0, y), new Vector2(MapData.Columns * TileSize, y), gridColor);
            }

            // In editor mode, draw a highlight on the hovered cell
            if (mode == GameMode.Editor)
            {
                var mouse = Mouse.GetState();
                if (mouse.X >= 0 && mouse.X < MapData.Columns * TileSize &&
                    mouse.Y >= 0 && mouse.Y < MapData.Rows * TileSize)
                {
                    int col = mouse.X / TileSize;
                    int row = mouse.Y / TileSize;
                    var highlight = new Rectangle(col * TileSize, row * TileSize, TileSize, TileSize);
                    DrawRectangleOutline(highlight, Color.Yellow);
                    DrawFilledRectangle(highlight, Color.White * 0.15f);
                }
            }
        }

        private void DrawPlayer()
        {
            var rect = new Rectangle((int)playerX, (int)playerY, PlayerWidth, PlayerHeight);
            DrawFilledRectangle(rect, Color.WhiteSmoke);
            DrawRectangleOutline(rect, Color.Black);

            // Draw simple face
            DrawFilledRectangle(new Rectangle((int)playerX + 5, (int)playerY + 4, 5, 5), Color.Black);
            DrawFilledRectangle(new Rectangle((int)playerX + 12, (int)playerY + 4, 5, 5), Color.Black);
        }

        private void DrawText(string text, int fontSize, Vector2 position, Color color)
        {
            var texture = TextRenderer.RenderText(GraphicsDevice, text, fontSize, color);
            spriteBatch.Draw(texture, position, Color.White);
        }

        private void DrawFilledRectangle(Rectangle rect, Color color)
        {
            spriteBatch.Draw(pixelTexture, rect, color);
        }

        private void DrawRectangleOutline(Rectangle rect, Color color)
        {
            // Top
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            // Bottom
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1), color);
            // Left
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            // Right
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X + rect.Width - 1, rect.Y, 1, rect.Height), color);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            var direction = end - start;
            var length = direction.Length();
            var angle = (float)Math.Atan2(direction.Y, direction.X);

            spriteBatch.Draw(pixelTexture, start, null, color, angle, Vector2.Zero, new Vector2(length, 1), SpriteEffects.None, 0);
        }
    }
}