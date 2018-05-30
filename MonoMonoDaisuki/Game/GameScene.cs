using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMonoDaisuki.Engine;
using System;
using System.Collections.Generic;
using System.Text;

namespace MonoMonoDaisuki.Game
{
    public class EnemyBullet : GameObject
    {
        public EnemyBullet(double x, double y)
        {

        }
    }

    public class EnemyBulletWaveGenerator
    {

    }

    public class EnemyBulletSchedulerDefine
    {

    }

    public class EnemyBulletScheduler
    {

    }

    public class Enemy : GameObject
    {
        public virtual double BodyDamage { get; set; } = 20;
        public virtual Stage Stage { get; set; }

        double targetX = 0;
        double speedX = 3;

        public Enemy(Stage stage)
        {
            Stage = stage;

            IsHittedVisible = true;
            IsHitVisible = true;

            Sprite = new RectangleSprite(Color.Lime);

            Width = 100;
            Height = 64;
            X = Core.ScreenWidth / 2 - Width / 2;
            Y = 60;
            RandomTarget();
        }

        public override void Update(GameTime time)
        {
            if (targetX - X > 0)
            {
                X += speedX;
            }
            else
            {
                X -= speedX;
            }

            if (Math.Abs(targetX - X) < speedX)
                RandomTarget();
        }

        public override void OnCollision(GameObject other)
        {
            if (other is PlayerBullet bullet)
            {
                Stage.EnemyHitted(bullet.Damage, bullet.Point);
                bullet.RemoveMe();
            }
        }

        void RandomTarget()
        {
            targetX = RandomEx.NextRange(0, Core.ScreenWidth - Width);
        }
    }

    public class PlayerBullet : GameObject
    {
        public virtual double Point { get; set; } = 10;
        public virtual double Damage { get; set; } = 10;
        protected virtual double SpeedY { get; set; } = 10;

        public PlayerBullet(double x, double y)
        {
            Sprite = new RectangleSprite(Color.Magenta);

            IsHittedVisible = true;

            Width = 3;
            Height = 20;
            X = x - Width / 2;
            Y = y;
        }

        public override void Update(GameTime time)
        {
            Y -= SpeedY;
            if (Y < -Height)
                RemoveMe();
        }
    }

    public class Player : GameObject
    {
        public virtual Stage Stage { get; set; }

        protected virtual double SpeedX { get; set; } = 5;
        protected virtual double SpeedY { get; set; } = 3;
        protected virtual int FireFrame { get; set; } = 4;
        int fireTimer = 0;

        public Player(Stage stage)
        {
            Stage = stage;

            IsHittedVisible = true;
            IsHitVisible = true;

            Sprite = new RectangleSprite(Color.Red);

            Width = 25;
            Height = 25;
            X = Core.ScreenWidth / 2 - Width / 2;
            Y = Core.ScreenHeight * 0.75 - Height / 2;
        }

        public override void Update(GameTime time)
        {
            fireTimer++;
            var key = Core.KeyState;
            if (key.IsKeyDown(Keys.A))
                X -= SpeedX;
            if (key.IsKeyDown(Keys.D))
                X += SpeedX;
            if (key.IsKeyDown(Keys.W))
                Y -= SpeedY;
            if (key.IsKeyDown(Keys.S))
                Y += SpeedY;
            X = MathEx.Clamp(X, 0, Core.ScreenWidth - Width);
            Y = MathEx.Clamp(Y, 0, Core.ScreenHeight - Height);
            if (key.IsKeyDown(Keys.Space))
                Fire();
        }

        public virtual void Fire()
        {
            if (fireTimer > FireFrame)
                fireTimer = 0;
            if (fireTimer == 0)
                ParentScene.AddChild(GetNewBullet());
        }

        public override void OnCollision(GameObject other)
        {
            if (other is Enemy enemy)
            {
                Stage.PlayerHitted(enemy.BodyDamage, 0);
                Logger.Log($"I hit to ENEMY : DMG {enemy.BodyDamage}");
            }
        }

        protected virtual PlayerBullet GetNewBullet()
        {
            return new PlayerBullet(X + Width / 2, Y - 20);
        }
    }

    public class StageManager
    {
        public double Score { get; set; }
        public GameScene GameScene { get; set; }

        public List<Stage> Stages { get; set; } = new List<Stage>();
        public int StageIndex { get; set; } = -1;
        public Stage CurrentStage => Stages[StageIndex];

        public StageManager(GameScene game)
        {
            GameScene = game;
            Stages.Add(new TestStage(this));
        }

        public void StartNext()
        {
            StageIndex++;
            CurrentStage.Start();
            CurrentStage.StageFinished += CurrentStage_StageFinished;
        }

        void CurrentStage_StageFinished(object sender, StageFinishedArgs e)
        {
            Logger.Log("stage finished");
        }
    }

    public class TestStage : Stage
    {
        Enemy enemy;
        Player player;
        GameScene scene;

        public TestStage(StageManager manager) : base(manager)
        {
            scene = manager.GameScene;
            enemy = new Enemy(this);
            player = new Player(this);
        }

        public override void Start()
        {
            scene.AddChild(enemy);
            scene.AddChild(player);
        }

        public override void Stop()
        {
            scene.RemoveChild(enemy);
            scene.RemoveChild(enemy);
        }
    }

    public enum FinishState
    {
        Success,
        Failed,
        Stopped,
    }

    public class StageFinishedArgs : EventArgs
    {
        public FinishState State { get; set; }
        public StageFinishedArgs(FinishState state)
        {
            State = state;
        }
    }

    public abstract class Stage
    {
        public double MaxPlayerHP { get; set; }
        public double MaxEnemyHP { get; set; }
        public double PlayerHP { get; set; }
        public double EnemyHP { get; set; }

        public double Combo { get; set; }

        public virtual event EventHandler<StageFinishedArgs> StageFinished;
        public virtual StageManager Manager { get; set; }

        public Stage(StageManager manager)
        {
            Manager = manager;
        }

        public virtual void CheckStageValid()
        {
            EnemyHP = Math.Max(0, EnemyHP);
            if (EnemyHP <= 0)
            {
                Stop();
                StageFinished?.Invoke(this, new StageFinishedArgs(FinishState.Success));
            }
        }

        public virtual void AddScore(double score)
        {
            Manager.Score += score * Combo;
        }

        public abstract void Start();
        public abstract void Stop();

        public virtual void EnemyHitted(double dmg, double score)
        {
            Combo++;
            EnemyHP -= dmg;
            AddScore(score);
            CheckStageValid();
        }

        public virtual void PlayerHitted(double dmg, double score)
        {
            Combo = 0;
            PlayerHP -= dmg;
            AddScore(score);
            CheckStageValid();
        }
    }

    public class GameScene : Scene
    {
        StageManager manager;

        public GameScene()
        {
            Core.SetScreenSize(385, 600);

            manager = new StageManager(this);
            manager.StartNext();
        }
    }
}
