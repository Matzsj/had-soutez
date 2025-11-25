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
        private ctverec _ctverec;
        private ctverec food;
        private Random _rnd = new Random();

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

            // place the square on a grid intersection and make it slightly smaller than a cell
            _ctverec = new ctverec(GraphicsDevice, new Vector2(gridSize, gridSize), gridSize, Color.Red);

            // create food (color is used as tint)
            food = new ctverec(GraphicsDevice, Vector2.Zero, gridSize - 1, Color.Yellow);

            // ensure the player starts exactly on a grid intersection
            _ctverec.SetPosition(SnapToGrid(_ctverec.GetPosition()));

            // initialize snake state: head at the starting snapped position
            _lastSnappedHead = SnapToGrid(_ctverec.GetPosition());
            _snakeTiles.Clear();
            _snakeTiles.Add(_lastSnappedHead);
            _snakeLength = 1;

            // place food on a (random) grid cell not overlapping the player
            RespawnFood();

            // initialize previous keyboard state so first Update has a baseline
            _previousKeyboardState = Keyboard.GetState();
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

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
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
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

                    // trim tail unless we've grown
                    if (_snakeTiles.Count > _snakeLength)
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

            // store this frame's keyboard for edge detection next frame
            _previousKeyboardState = keyboard;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.LightGreen);

            base.Draw(gameTime);

            _spriteBatch.Begin();

            DrawGrid();

            // draw snake tiles (index 0 = head)
            for (int i = 0; i < _snakeTiles.Count; i++)
            {
                Rectangle r = new Rectangle((int)_snakeTiles[i].X + 1, (int)_snakeTiles[i].Y + 1, gridSize - 2, gridSize - 2);
                _spriteBatch.Draw(_whitePixel, r, i == 0 ? Color.Red : Color.OrangeRed);
            }

            // draw food
            food.Draw(_spriteBatch);

            _spriteBatch.End();
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

        // Respawn food on a random grid cell that does not overlap the player
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
                    food.SetPosition(newPos);
                    break;
                }
            } while (true);
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
    }
}
