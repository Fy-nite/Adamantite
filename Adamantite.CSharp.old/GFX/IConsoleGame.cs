using Adamantite.GFX;
using Microsoft.Xna.Framework.Graphics;

namespace Adamantite.GFX
{
    public interface IConsoleGame
    {
        void Init(Canvas surface);
        void Update(double deltaTime);
        void Draw(Canvas surface);
    }

    public interface IConsoleGameWithSpriteBatch : IConsoleGame
    {
        void Draw(SpriteBatch sb, SpriteFont font, float presentationScale);
    }
}
