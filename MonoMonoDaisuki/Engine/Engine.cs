using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace MonoMonoDaisuki.Engine
{
    public static class Random
    {
        public static System.Random Instance = new System.Random((int)DateTime.Now.Ticks);

        public static double NextRange(double min, double max)
        {
            return Instance.NextDouble() * (max - min) + min;
        }
    }

    public static class Engine
    {
        public static GraphicsDeviceManager GraphicsDeviceManager { get; private set; }
        public static GraphicsDevice GraphicsDevice => GraphicsDeviceManager.GraphicsDevice;
        public static HitTester HitTester { get; private set; } = new HitTester();
        public static Scene Scene { get; private set; }

        public static double ScreenWidth => GraphicsDeviceManager.PreferredBackBufferWidth;
        public static double ScreenHeight => GraphicsDeviceManager.PreferredBackBufferHeight;

        public static void Initialize(GraphicsDeviceManager manager)
        {
            GraphicsDeviceManager = manager;
        }

        public static void SetScene(Scene newScene)
        {
            Scene?.OnUnload();
            newScene.OnLoad();
            Scene = newScene;
        }

        public static void SetScreenSize(double w, double h)
        {
            var manager = GraphicsDeviceManager;
            manager.PreferredBackBufferWidth = (int)w;
            manager.PreferredBackBufferHeight = (int)h;
            manager.ApplyChanges();
        }

        public static void Update(GameTime time)
        {
            Scene.Update(time);
        }

        public static void Draw(GameTime time, SpriteBatch batch)
        {
            batch.Begin();
            Scene.Draw(time, batch);
            batch.End();
        }
    }
}
