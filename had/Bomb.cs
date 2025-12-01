using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace had
{
    // Simple bomb entity (draws supplied circle texture)
    public class Bomb : IGameEntity
    {
        private Texture2D _texture;
        private Vector2 _position;
        private int _size;

        public Bomb(Texture2D texture, Vector2 position, int size)
        {
            _texture = texture;
            _position = position;
            _size = size;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // draw the bomb centered at its position (position already set to top-left in Game1.SpawnBomb)
            spriteBatch.Draw(_texture, new Rectangle((int)_position.X, (int)_position.Y, _size, _size), Color.White);

            // optional: small "spark" (fuse) - draw a small light pixel near top
            int sparkSize = Math.Max(2, _size / 8);
            Texture2D pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.OrangeRed });
            Rectangle sparkRect = new Rectangle((int)_position.X + _size - sparkSize - 2, (int)_position.Y + 2, sparkSize, sparkSize);
            spriteBatch.Draw(pixel, sparkRect, Color.OrangeRed);
        }

        public void SetPosition(Vector2 newPos)
        {
            _position = newPos;
        }

        public Vector2 GetPosition()
        {
            return _position;
        }

        public Rectangle GetBounds()
        {
            return new Rectangle((int)_position.X, (int)_position.Y, _size, _size);
        }

        public int GetSize()
        {
            return _size;
        }
    }
}