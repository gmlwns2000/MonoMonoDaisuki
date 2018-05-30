using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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

    public class GameTimer
    {
        bool isEnable = false;
        public bool IsEnable
        {
            get => isEnable;
            set
            {
                if (isEnable != value)
                {
                    isEnable = value;
                    if (value)
                    {
                        Start();
                    }
                    else
                    {
                        Stop();
                    }
                }
            }
        }
        public TimeSpan Interval { get; set; }
        public event EventHandler<TimeSpan> Tick;

        TimeSpan lastTick;

        public GameTimer(TimeSpan interval)
        {
            Interval = interval;
        }

        public void Start()
        {
            lastTick = Core.LastGameTimeFrame.TotalGameTime;
            Core.TimerScheduler.Register(this);
            isEnable = true;
        }

        public void Stop()
        {
            Core.TimerScheduler.Unregister(this);
            isEnable = false;
        }

        internal void CheckTick(TimeSpan elapsed)
        {
            if(elapsed - lastTick > Interval)
            {
                lastTick = elapsed;
                Tick?.Invoke(this, elapsed);
            }
        }
    }

    public class GameTimerScheduler
    {
        List<GameTimer> Managed = new List<GameTimer>();

        public void Update(GameTime gametime)
        {
            var temp = new List<GameTimer>(Managed);
            foreach (var item in temp)
            {
                item.CheckTick(gametime.TotalGameTime);
            }
        }

        public void Register(GameTimer timer)
        {
            Managed.Add(timer);
        }

        public void Unregister(GameTimer timer)
        {
            Managed.Remove(timer);
        }
    }

    public static class Core
    {
        public static GraphicsDeviceManager GraphicsDeviceManager { get; private set; }
        public static GraphicsDevice GraphicsDevice => GraphicsDeviceManager.GraphicsDevice;
        public static GameTime LastGameTimeFrame { get; private set; } = new GameTime();

        public static Scene Scene { get; private set; }
        public static HitTester HitTester { get; private set; } = new HitTester();

        public static KeyboardState KeyState { get; private set; }
        public static MouseState MouseState { get; private set; }

        public static double ScreenWidth {get; private set; }
        public static double ScreenHeight {get; private set; }

        public static GameTimerScheduler TimerScheduler { get; private set; } = new GameTimerScheduler();

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
            LastGameTimeFrame = time;
            KeyState = Keyboard.GetState();
            MouseState = Mouse.GetState();
            TimerScheduler.Update(time);
            Scene.Update(time);
        }

        public static void Draw(GameTime time, SpriteBatch batch)
        {
            batch.Begin();
            Scene.Draw(time, batch);
            batch.End();
        }

        public static void Sleep(double durationMs) 
        {
            if (durationMs <= 5)
            {
                Thread.Sleep((int)Math.Round(durationMs));
                return;
            }
            var start = Logger.Stopwatch.Elapsed;
            while (true)
            {
                Thread.Sleep(1);
                if(Logger.Stopwatch.Elapsed.TotalMilliseconds - start.TotalMilliseconds > durationMs)
                {
                    return;
                }
            }
        }
    }
}
