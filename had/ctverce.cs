using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace had
{
    public class ctverec
    {
        private Texture2D _texture;
        private Vector2 _position;
        private int _size;
        private Color _color;

        public ctverec(GraphicsDevice graphicsDevice, Vector2 position, int size, Color color)
        {
            _position = position;
            _size = size;
            _color = color;

            // create a white 1x1 texture and use _color as the tint when drawing
            _texture = new Texture2D(graphicsDevice, 1, 1);
            _texture.SetData(new[] { Color.White });
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(_texture, new Rectangle((int)_position.X, (int)_position.Y, _size, _size), _color);
        }

        public void SetPosition(Vector2 newPos)
        {
            _position = newPos;
        }

        public Vector2 GetPosition()
        {
            return _position;
        }

        // expose bounds and size so Game1 can detect collisions
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
