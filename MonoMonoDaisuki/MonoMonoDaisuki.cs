#region Using Statements
using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMonoDaisuki.Engine;

#if !WINDOWS
//using Microsoft.Xna.Framework.Storage;
#endif

#endregion

namespace MonoMonoDaisuki
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class MonoMonoDaisuki : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        RenderContext renderContext;

        public MonoMonoDaisuki()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.IsFullScreen = false;
            IsMouseVisible = true;
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            Engine.Core.Initialize(graphics);
            Engine.Core.SetScene(new Game.GameScene());

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            renderContext = new RenderContext(spriteBatch);
            
            //TODO: use this.Content to load your game content here 
        }

        protected override void Update(GameTime gameTime)
        {
            #if !__IOS__ &&  !__TVOS__
            if (GamePad.GetState (PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit ();
            }
            #endif

            Engine.Core.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(54,54,54));

            Engine.Core.Draw(gameTime, renderContext);
            
            base.Draw(gameTime);
        }
    }
}
