using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace had
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private Texture2D _whitePixel;
        private Texture2D _roundedTile;
        private Texture2D _bombTexture;
        private IGameEntity _ctverec; // use interface type
        private IGameEntity food;     // use interface type
        private Bomb _bomb;
        private Random _rnd = new Random();
        private SpriteFont _font; // <-- added SpriteFont for button text

        // snake body as grid-aligned tiles (index 0 = head)
        private List<Vector2> _snakeTiles = new List<Vector2>();
        private int _snakeLength = 1;
        private Vector2 _lastSnappedHead;

        // smaller cells -> more fields, faster opportunity to turn
        private int gridSize = 40;
        private float speed = 400f; // pixels per second

        // current movement direction (unit vector). starts moving right
        private Vector2 _direction = Vector2.UnitX;

        // queued direction when a key is pressed between grid cells
        private Vector2 _pendingDirection = Vector2.Zero;
        private bool _hasPendingDirection;

        // keep previous keyboard state to detect key presses (edges)
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;

        // game state
        private bool _isGameOver;

        // bomb spawn/timing
        private bool _bombActive = false;
        private float _bombHiddenTimer = 0f; // time since last hide
        private float _bombVisibleTimer = 0f; // time bomb has been visible
        private const float BombVisibleDuration = 3f; // seconds visible
        private const float BombHiddenInterval = 0f; // seconds hidden between visible windows

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);

            _graphics.PreferredBackBufferWidth = 720;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();

            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // pixel used for grid lines
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.DarkGreen });

            // white pixel used to draw snake tiles (tintable)
            _whitePixel = new Texture2D(GraphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });

            // create a reusable rounded-tile texture sized to the snake tile (will be tinted at draw time)
            int roundedSize = Math.Max(1, gridSize - 2);
            int roundedRadius = Math.Max(2, gridSize / 6); // small radius relative to tile
            _roundedTile = CreateRoundedRectTexture(GraphicsDevice, roundedSize, roundedSize, roundedRadius, Color.White);

            // create bomb texture (filled circle) sized similarly to a tile
            int bombSize = Math.Max(1, gridSize - 4);
            _bombTexture = CreateCircleTexture(GraphicsDevice, bombSize, Color.Black);

            // load SpriteFont; fail early with a helpful message if missing
            try
            {
                _font = Content.Load<SpriteFont>("DefaultFont");
            }
            catch
            {
                // quietly ignore font here; drawing text is optional
                _font = null;
            }
            
            // place the head (typed as IGameEntity)
            _ctverec = new ctverec(GraphicsDevice, new Vector2(gridSize, gridSize), gridSize, Color.Red);

            // create food
            food = new ctverec(GraphicsDevice, Vector2.Zero, gridSize - 1, Color.Yellow);

            // create bomb entity (starts hidden)
            _bomb = new Bomb(_bombTexture, Vector2.Zero, bombSize);

            // ensure the player starts exactly on a grid intersection
            _ctverec.SetPosition(SnapToGrid(_ctverec.GetPosition()));

            // initialize snake state: head at the starting snapped position
            _lastSnappedHead = SnapToGrid(_ctverec.GetPosition());
            _snakeTiles.Clear();
            _snakeTiles.Add(_lastSnappedHead);
            _snakeLength = 1;

            // place food on a (random) grid cell not overlapping the player
            RespawnFood();

            // initialize previous input states so first Update has a baseline
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _isGameOver = false;

            // start bomb hidden timer so it will appear after BombHiddenInterval
            _bombHiddenTimer = 0f;
            _bombActive = false;
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            var mouse = Mouse.GetState();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Bomb lifecycle: hidden -> visible (10s) -> hidden -> ...
            if (_bombActive)
            {
                _bombVisibleTimer += dt;
                if (_bombVisibleTimer >= BombVisibleDuration)
                {
                    // hide bomb and start hidden timer
                    _bombActive = false;
                    _bombHiddenTimer = 0f;
                }
            }
            else
            {
                _bombHiddenTimer += dt;
                if (_bombHiddenTimer >= BombHiddenInterval)
                {
                    // show bomb at a new random grid cell not overlapping player or food
                    SpawnBomb();
                    _bombActive = true;
                    _bombVisibleTimer = 0f;
                }
            }

            // If game over: check for restart input only
            if (_isGameOver)
            {
                // restart by pressing R (edge) or clicking restart button
                if (IsKeyPressed(keyboard, _previousKeyboardState, Keys.R))
                {
                    ResetGame();
                }
                else if (mouse.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
                {
                    if (GetRestartButtonRect().Contains(mouse.Position))
                        ResetGame();
                }

                // store input states and skip game update while game is over
                _previousKeyboardState = keyboard;
                _previousMouseState = mouse;
                base.Update(gameTime);
                return;
            }

            if (keyboard.IsKeyDown(Keys.Escape))
                Exit();

            // current position (top-left assumed)
            Vector2 pos = _ctverec.GetPosition();

            // read a desired direction on key press (edge)
            Vector2 desired = Vector2.Zero;
            if (IsKeyPressed(keyboard, _previousKeyboardState, Keys.Left) || IsKeyPressed(keyboard, _previousKeyboardState, Keys.A))
                desired = -Vector2.UnitX;
            else if (IsKeyPressed(keyboard, _previousKeyboardState, Keys.Right) || IsKeyPressed(keyboard, _previousKeyboardState, Keys.D))
                desired = Vector2.UnitX;
            else if (IsKeyPressed(keyboard, _previousKeyboardState, Keys.Up) || IsKeyPressed(keyboard, _previousKeyboardState, Keys.W))
                desired = -Vector2.UnitY;
            else if (IsKeyPressed(keyboard, _previousKeyboardState, Keys.Down) || IsKeyPressed(keyboard, _previousKeyboardState, Keys.S))
                desired = Vector2.UnitY;

            // if user pressed a direction:
            if (desired != Vector2.Zero)
            {
                if (IsAlignedToGrid(pos))
                {
                    // change direction immediately and snap exactly to grid
                    _direction = desired;
                    pos = SnapToGrid(pos);
                    _hasPendingDirection = false;
                    _pendingDirection = Vector2.Zero;
                }
                else
                {
                    // queue the direction to apply when we next hit a grid cell
                    _pendingDirection = desired;
                    _hasPendingDirection = true;
                }
            }

            // move automatically each frame along current _direction
            pos += _direction * speed * dt;

            // after moving, if we have a pending direction and are now aligned, apply it
            if (_hasPendingDirection && IsAlignedToGrid(pos))
            {
                _direction = _pendingDirection;
                pos = SnapToGrid(pos);
                _hasPendingDirection = false;
                _pendingDirection = Vector2.Zero;
            }

            // clamp so the square stays inside the visible grid area (avoid going off-screen)
            pos.X = MathHelper.Clamp(pos.X, 0, _graphics.PreferredBackBufferWidth - gridSize);
            pos.Y = MathHelper.Clamp(pos.Y, 0, _graphics.PreferredBackBufferHeight - gridSize);

            _ctverec.SetPosition(pos);

            // when head reaches a new grid cell, push that tile into the snake list
            if (IsAlignedToGrid(pos))
            {
                Vector2 snapped = SnapToGrid(pos);
                if (snapped != _lastSnappedHead)
                {
                    _snakeTiles.Insert(0, snapped);
                    _lastSnappedHead = snapped;

                    // check self-collision: head moved into a tile already occupied by the body
                    for (int i = 1; i < _snakeTiles.Count; i++)
                    {
                        if (_snakeTiles[i] == snapped)
                        {
                            _isGameOver = true;
                            break;
                        }
                    }

                    // trim tail unless we've grown (do not trim after collision)
                    if (!_isGameOver && _snakeTiles.Count > _snakeLength)
                        _snakeTiles.RemoveAt(_snakeTiles.Count - 1);
                }
            }

            // --- detect collision (eating) between player head and food ---
            if (_ctverec.GetBounds().Intersects(food.GetBounds()))
            {
                // increase snake length by one grid tile
                _snakeLength += 1;

                // respawn food somewhere else on grid
                RespawnFood();
            }

            // check bomb collision if active
            if (_bombActive && _ctverec.GetBounds().Intersects(_bomb.GetBounds()))
            {
                // eating bomb removes two tiles from length
                _snakeLength = Math.Max(1, _snakeLength - 2);

                // trim snakeTiles to new length immediately
                while (_snakeTiles.Count > _snakeLength)
                    _snakeTiles.RemoveAt(_snakeTiles.Count - 1);

                // hide bomb and start hidden timer
                _bombActive = false;
                _bombHiddenTimer = 0f;
            }

            // store this frame's keyboard and mouse for edge detection next frame
            _previousKeyboardState = keyboard;
            _previousMouseState = mouse;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.LightGreen);

            base.Draw(gameTime);

            _spriteBatch.Begin();

            DrawGrid();

            // draw snake tiles (index 0 = head) with rainbow colors
            int total = Math.Max(1, _snakeTiles.Count);
            for (int i = 0; i < _snakeTiles.Count; i++)
            {
                Rectangle r = new Rectangle((int)_snakeTiles[i].X + 1, (int)_snakeTiles[i].Y + 1, gridSize - 2, gridSize - 2);
                Color tileColor = GetRainbowColor(i, total);
                // draw rounded tile texture tinted to tileColor
                _spriteBatch.Draw(_roundedTile, r, tileColor);
            }

            // draw food via interface
            food.Draw(_spriteBatch);

            // draw bomb if active
            if (_bombActive)
                _bomb.Draw(_spriteBatch);

            // if game over: draw overlay + restart button
            if (_isGameOver)
            {
                // semi-transparent overlay
                _spriteBatch.Draw(_whitePixel, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), new Color(0, 0, 0, 150));

                // draw restart button
                Rectangle btn = GetRestartButtonRect();
                _spriteBatch.Draw(_whitePixel, btn, Color.Gray);

                // small inner rect to indicate clickable area
                Rectangle inner = new Rectangle(btn.X + 4, btn.Y + 4, btn.Width - 8, btn.Height - 8);
                _spriteBatch.Draw(_whitePixel, inner, Color.LightGray);

                // draw button text if font available
                if (_font != null)
                {
                    string title = "Restart";
                    Vector2 titleSize = _font.MeasureString(title);
                    Vector2 titlePos = new Vector2(btn.Center.X - titleSize.X / 2, btn.Center.Y - titleSize.Y / 2 - 8);
                    _spriteBatch.DrawString(_font, title, titlePos, Color.Black);

                    string hint = "Press R or click";
                    Vector2 hintSize = _font.MeasureString(hint);
                    Vector2 hintPos = new Vector2(btn.Center.X - hintSize.X / 2, titlePos.Y + titleSize.Y + 6);
                    _spriteBatch.DrawString(_font, hint, hintPos, Color.Black);
                }
            }

            _spriteBatch.End();
        }

        private Rectangle GetRestartButtonRect()
        {
            int w = 220;
            int h = 64;
            int x = (_graphics.PreferredBackBufferWidth - w) / 2;
            int y = (_graphics.PreferredBackBufferHeight - h) / 2;
            return new Rectangle(x, y, w, h);
        }

        private Color GetRainbowColor(int index, int total)
        {
            // map index to hue [0..360)
            float hue = (index * 360f) / total;
            // use high saturation and value for vivid colors
            return ColorFromHSV(hue, 0.9f, 0.95f);
        }

        // convert HSV to XNA Color
        private Color ColorFromHSV(float hue, float saturation, float value)
        {
            // hue in degrees [0,360)
            int hi = (int)MathF.Floor(hue / 60f) % 6;
            float f = (hue / 60f) - MathF.Floor(hue / 60f);
            value = MathHelper.Clamp(value, 0f, 1f);
            saturation = MathHelper.Clamp(saturation, 0f, 1f);

            float v = value;
            float p = value * (1f - saturation);
            float q = value * (1f - f * saturation);
            float t = value * (1f - (1f - f) * saturation);

            float r = 0, g = 0, b = 0;
            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }

            return new Color((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f));
        }

        private void DrawGrid()
        {
            int width = _graphics.PreferredBackBufferWidth;
            int height = _graphics.PreferredBackBufferHeight;

            // thinner grid lines (1 px) and more cells due to smaller gridSize
            for (int x = 0; x <= width; x += gridSize)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, 0, 1, height), Color.DarkGreen);
            }

            for (int y = 0; y <= height; y += gridSize)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, y, width, 1), Color.DarkGreen);
            }
        }

        // Respawn food on a random grid cell that does not overlap the player or active bomb
        private void RespawnFood()
        {
            int cols = _graphics.PreferredBackBufferWidth / gridSize;
            int rows = _graphics.PreferredBackBufferHeight / gridSize;

            Rectangle playerBounds = _ctverec.GetBounds();
            Rectangle candidate;
            do
            {
                int cx = _rnd.Next(cols);
                int cy = _rnd.Next(rows);
                Vector2 newPos = new Vector2((cx * gridSize) + 1, (cy * gridSize) + 1);
                candidate = new Rectangle((int)newPos.X, (int)newPos.Y, food.GetSize(), food.GetSize());
                if (!candidate.Intersects(playerBounds))
                {
                    // avoid bomb if active
                    if (_bombActive && candidate.Intersects(_bomb.GetBounds()))
                        continue;

                    food.SetPosition(newPos);
                    break;
                }
            } while (true);
        }

        // spawn bomb at a random grid cell not overlapping player or food
        private void SpawnBomb()
        {
            int cols = _graphics.PreferredBackBufferWidth / gridSize;
            int rows = _graphics.PreferredBackBufferHeight / gridSize;

            Rectangle playerBounds = _ctverec.GetBounds();
            Rectangle candidate;
            do
            {
                int cx = _rnd.Next(cols);
                int cy = _rnd.Next(rows);
                Vector2 newPos = new Vector2((cx * gridSize) + ((gridSize - _bomb.GetSize()) / 2), (cy * gridSize) + ((gridSize - _bomb.GetSize()) / 2));
                candidate = new Rectangle((int)newPos.X, (int)newPos.Y, _bomb.GetSize(), _bomb.GetSize());
                if (!candidate.Intersects(playerBounds) && !candidate.Intersects(food.GetBounds()))
                {
                    _bomb.SetPosition(newPos);
                    break;
                }
            } while (true);
        }

        private void ResetGame()
        {
            // reset movement and length
            _direction = Vector2.UnitX;
            _snakeLength = 1;

            // reset head position to initial cell
            Vector2 start = new Vector2(gridSize, gridSize);
            start = SnapToGrid(start);
            _ctverec.SetPosition(start);

            // reset snake tiles
            _snakeTiles.Clear();
            _snakeTiles.Add(start);
            _lastSnappedHead = start;

            // place food and clear game over flag
            RespawnFood();
            _isGameOver = false;

            // reset bomb cycle
            _bombActive = false;
            _bombHiddenTimer = 0f;
            _bombVisibleTimer = 0f;
        }

        // helper to detect key press (went from up -> down)
        private bool IsKeyPressed(KeyboardState current, KeyboardState previous, Keys key)
        {
            return current.IsKeyDown(key) && previous.IsKeyUp(key);
        }

        // true when position is (almost) exactly on grid intersection
        private bool IsAlignedToGrid(Vector2 pos)
        {
            float snappedX = MathF.Round(pos.X / gridSize) * gridSize;
            float snappedY = MathF.Round(pos.Y / gridSize) * gridSize;
            // adaptive tolerance so fast movement / small cells still allow alignment detection
            float epsilon = MathF.Max(1.5f, gridSize * 0.05f);
            return MathF.Abs(pos.X - snappedX) <= epsilon && MathF.Abs(pos.Y - snappedY) <= epsilon;
        }

        // snap position to exact grid coordinates
        private Vector2 SnapToGrid(Vector2 pos)
        {
            pos.X = MathF.Round(pos.X / gridSize) * gridSize;
            pos.Y = MathF.Round(pos.Y / gridSize) * gridSize;
            return pos;
        }

        // create a rounded-corner white texture which can be tinted when drawing
        private Texture2D CreateRoundedRectTexture(GraphicsDevice graphicsDevice, int width, int height, int radius, Color color)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            radius = Math.Max(0, Math.Min(Math.Min(width, height) / 2, radius));

            var tex = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];

            float rr = radius * radius;
            float cxOffset = radius - 0.5f;
            float cyOffset = radius - 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = false;

                    // central rectangles (fast path)
                    if (x >= radius && x < width - radius)
                        inside = true;
                    if (y >= radius && y < height - radius)
                        inside = true;

                    // corners: if not already inside, check quarter-circles
                    if (!inside && radius > 0)
                    {
                        // top-left
                        if (x < radius && y < radius)
                        {
                            float dx = x - cxOffset;
                            float dy = y - cyOffset;
                            if (dx * dx + dy * dy <= rr) inside = true;
                        }
                        // top-right
                        else if (x >= width - radius && y < radius)
                        {
                            float dx = x - (width - 1 - cxOffset);
                            float dy = y - cyOffset;
                            if (dx * dx + dy * dy <= rr) inside = true;
                        }
                        // bottom-left
                        else if (x < radius && y >= height - radius)
                        {
                            float dx = x - cxOffset;
                            float dy = y - (height - 1 - cyOffset);
                            if (dx * dx + dy * dy <= rr) inside = true;
                        }
                        // bottom-right
                        else if (x >= width - radius && y >= height - radius)
                        {
                            float dx = x - (width - 1 - cxOffset);
                            float dy = y - (height - 1 - cyOffset);
                            if (dx * dx + dy * dy <= rr) inside = true;
                        }
                    }

                    data[y * width + x] = inside ? color : Color.Transparent;
                }
            }

            tex.SetData(data);
            return tex;
        }

        // create a filled circle texture (for bomb) which can be tinted when drawing
        private Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, int diameter, Color color)
        {
            int w = Math.Max(1, diameter);
            int h = Math.Max(1, diameter);
            var tex = new Texture2D(graphicsDevice, w, h);
            Color[] data = new Color[w * h];

            float radius = w / 2f;
            float cx = radius - 0.5f;
            float cy = radius - 0.5f;
            float rr = radius * radius;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    bool inside = dx * dx + dy * dy <= rr;
                    data[y * w + x] = inside ? color : Color.Transparent;
                }
            }

            tex.SetData(data);
            return tex;
        }
    }
}
