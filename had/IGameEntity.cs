using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace had
{
    // Single interface for simple game entities used by Game1
    public interface IGameEntity
    {
        void Draw(SpriteBatch spriteBatch);
        void SetPosition(Vector2 newPos);
        Vector2 GetPosition();
        Rectangle GetBounds();
        int GetSize();
    }
}