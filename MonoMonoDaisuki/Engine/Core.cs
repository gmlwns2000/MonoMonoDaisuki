using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace MonoMonoDaisuki.Engine
{
    public static class MathEx
    {
        public static double Clamp(double var, double min, double max)
        {
            return Math.Max(min, Math.Min(max, var));
        }
    }
    public static class RandomEx
    {
        public static System.Random Instance = new System.Random((int)DateTime.Now.Ticks);

        public static double NextRange(double min, double max)
        {
            return Instance.NextDouble() * (max - min) + min;
        }
    }

    public static class Core
    {
        public static GraphicsDeviceManager GraphicsDeviceManager { get; private set; }
        public static GraphicsDevice GraphicsDevice => GraphicsDeviceManager.GraphicsDevice;

        public static Scene Scene { get; private set; }
        public static HitTester HitTester { get; private set; } = new HitTester();

        public static KeyboardState KeyState { get; private set; }
        public static MouseState MouseState { get; private set; }

        public static double ScreenWidth {get; private set; }
        public static double ScreenHeight {get; private set; }

        public static void Initialize(GraphicsDeviceManager manager)
        {
            GraphicsDeviceManager = manager;
            UpdateScreenSize();
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
            ScreenWidth = w;
            ScreenHeight = h;
            manager.ApplyChanges();
        }

        public static void UpdateScreenSize()
        {
            ScreenWidth = GraphicsDeviceManager.PreferredBackBufferWidth;
            ScreenHeight = GraphicsDeviceManager.PreferredBackBufferHeight;
        }

        public static void Update(GameTime time)
        {
            KeyState = Keyboard.GetState();
            MouseState = Mouse.GetState();
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
