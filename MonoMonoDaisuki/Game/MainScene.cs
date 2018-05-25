using Microsoft.Xna.Framework;
using MonoMonoDaisuki.Engine;
using System;
using System.Collections.Generic;
using System.Text;

namespace MonoMonoDaisuki.Game
{
    public class Player : GameObject
    {
        public Player()
        {
            IsHittedVisible = IsHitVisible = true;
            Sprite = new RectangleSprite(100, 100, Color.Lime);
            X = 100;
            Y = 100;
        }

        public override void Update(GameTime time)
        {
            X = (X + 2) % 100;
        }
    }

    public class HitTest : GameObject
    {
        RectangleSprite s = new RectangleSprite(10, 10, Color.Blue);
        double speedX, speedY;

        public HitTest()
        {
            Sprite = s;

            IsHittedVisible = IsHitVisible = true;
            HitTestGroup = "test";

            rndMe();
        }

        public override void Update(GameTime time)
        {
            X += speedX; Y += speedY;
            if (X > Engine.Engine.ScreenWidth || Y < 0)
                rndMe();
            if (Y > Engine.Engine.ScreenHeight || Y < 0)
                rndMe();
        }

        public override void OnCollision(GameObject other)
        {
            rndMe();
        }

        void rndMe()
        {
            speedX = Engine.Random.NextRange(-7, 7);
            speedY = Engine.Random.NextRange(-7, 7);
            X = Engine.Random.NextRange(0, Engine.Engine.ScreenWidth);
            Y = Engine.Random.NextRange(0, Engine.Engine.ScreenHeight);
            s.Color = new Color((uint)Engine.Random.Instance.Next());
        }
    }

    public class MainScene : Scene
    {
        Player player = new Player();

        public MainScene()
        {
            Engine.Engine.SetScreenSize(1280, 720);
            Children.Add(player);
            for (int i = 0; i < 1000; i++)
            {
                Children.Add(new HitTest());
            }
        }
    }
}
