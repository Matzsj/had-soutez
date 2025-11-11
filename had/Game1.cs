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
        private ctverec _ctverec;


        private int gridSize = 80;
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
           
            _graphics.PreferredBackBufferWidth = 960;
            _graphics.PreferredBackBufferHeight = 960;
            _graphics.ApplyChanges();

            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);


            // TODO: use this.Content to load your game content here
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.DarkGreen });


            _ctverec = new ctverec(GraphicsDevice, new Vector2(80, 80), gridSize, Color.Red);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.LightGreen);

            // TODO: Add your drawing code here

            base.Draw(gameTime);

            _spriteBatch.Begin();
          
            DrawGrid();
            _ctverec.Draw(_spriteBatch);

            _spriteBatch.End();
        }
        private void DrawGrid()
        {
            int width = _graphics.PreferredBackBufferWidth;
            int height = _graphics.PreferredBackBufferHeight;

            // vertikální čáry
            for (int x = 0; x <= width; x += gridSize)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x, 0, 3, height), Color.DarkGreen);
            }

            // horizontální čáry
            for (int y = 0; y <= height; y += gridSize)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, y, width, 3), Color.DarkGreen);
            }
        }
    }
}
