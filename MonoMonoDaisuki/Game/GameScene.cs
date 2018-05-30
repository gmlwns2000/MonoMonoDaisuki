using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMonoDaisuki.Engine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MonoMonoDaisuki.Game
{
    public class DirectionEnemyBullet : LinearEnemyBullet
    {
        public DirectionEnemyBullet(double x, double y, double angle, double force)
            : base(x, y, new Vector2(
                (float)(force * Math.Cos(angle / 180 * Math.PI)),
                (float)(force * Math.Sin(angle / 180 * Math.PI))))
        {

        }
    }

    public class LinearEnemyBullet : EnemyBullet
    {
        public Vector2 Force { get; set; }

        public LinearEnemyBullet(double x, double y, Vector2 force)
            : base(x, y)
        {
            Width = 5;
            Height = 5;
            Force = force;
            Sprite = new RectangleSprite(Color.Cyan);
        }

        public override void Update(GameTime time)
        {
            X += Force.X;
            Y += Force.Y;

            base.Update(time);
        }
    }

    public class EnemyBullet : GameObject
    {
        public EnemyBullet(double x, double y)
        {
            IsHittedVisible = true;
            IsHitVisible = false;

            X = x;
            Y = y;
        }

        public override void Update(GameTime time)
        {
            if (X > ParentScene.Width || X < -Width || Y < -Height || Y > ParentScene.Height)
                RemoveMe();
            base.Update(time);
        }
    }

    public class ActionEnemyBulletWave : EnemyBulletWave
    {
        public Action Action { get; set; }
        public bool IsAsync { get; set; } = true;

        public ActionEnemyBulletWave(Enemy Parent, TimeSpan Duration, Action action, bool isAsync = true)
            : base(Parent, Duration)
        {
            Action = action;
            IsAsync = isAsync;
        }

        public override void Start()
        {
            if (IsAsync)
            {
                Task.Factory.StartNew(Action);
                return;
            }
            Action.Invoke();
        }
    }

    public abstract class EnemyBulletWave
    {
        public TimeSpan Duration { get; set; }
        public Enemy Parent { get; set; }

        public EnemyBulletWave(Enemy parent, TimeSpan duration)
        {
            Duration = duration;
        }

        public abstract void Start();
    }

    public class AllDirectionEnemyBulletWave : ActionEnemyBulletWave
    {
        public AllDirectionEnemyBulletWave(Enemy parent, double bulletCount, double shootCount, double shootInterval, double bulletForce, Sprite sprite)
            : base(parent, TimeSpan.FromMilliseconds(shootCount * shootInterval), null)
        {
            Action = new Action(() =>
            {
                var scene = parent.ParentScene;
                for (int i = 0; i < shootCount; i++)
                {
                    var angleStep = 360 / bulletCount;
                    var angle = .0;
                    for (int ii = 0; ii < bulletCount; ii++)
                    {
                        scene.AddChild(new DirectionEnemyBullet(parent.Center.X, parent.Center.Y, angle, bulletForce)
                        {
                            Sprite = sprite
                        } );
                        angle += angleStep;
                    }
                    Core.Sleep(shootInterval);
                }
            });
        }
    }

    public class EnemyBulletWaveScheduler
    {
        public virtual bool IsLoop { get; set; } = false;
        public virtual int WaveIndex { get; set; } = 0;
        public virtual List<EnemyBulletWave> Waves { get; set; } = new List<EnemyBulletWave>();

        protected virtual EnemyBulletWave CurrentWave => (WaveIndex >= Waves.Count || WaveIndex < 0) ? null : Waves[WaveIndex];
        EnemyBulletWave lastwave;
        GameTimer timer;
        TimeSpan lastGen = new TimeSpan();

        public void Start()
        {
            if (timer == null)
            {
                timer = new GameTimer(TimeSpan.FromMilliseconds(1));
                timer.Tick += TickUpdate;
            }
            timer.Start();
        }

        private void TickUpdate(object sender, TimeSpan e)
        {
            if (WaveIndex >= Waves.Count)
            {
                if (IsLoop)
                    WaveIndex = -1;
                else
                    return;
            }

            if (e - lastGen > ((lastwave == null) ? new TimeSpan() : lastwave.Duration))
            {
                CurrentWave?.Start();
                lastGen = e;
                lastwave = CurrentWave;

                WaveIndex += 1;
            }
        }

        public void Stop()
        {
            timer.Stop();
        }
    }

    public class Enemy : GameObject
    {
        public virtual double BodyDamage { get; set; } = 20;
        public virtual Stage Stage { get; set; }
        public EnemyBulletWaveScheduler WaveScheduler { get; set; } = new EnemyBulletWaveScheduler();

        double targetX = 0;
        double speedX = 3;

        public Enemy(Stage stage)
        {
            Stage = stage;

            IsHittedVisible = true;
            IsHitVisible = true;

            Sprite = new RectangleSprite(Color.Lime);

            WaveScheduler.IsLoop = true;
            WaveScheduler.Waves = new List<EnemyBulletWave>()
            {
                new AllDirectionEnemyBulletWave(this, 20, 4, 300, 5, new RectangleSprite(Color.Cyan))
            };
        }

        public override void Load()
        {
            base.Load();

            Width = 100;
            Height = 64;
            X = ParentScene.Width / 2 - Width / 2;
            Y = 60;
            RandomTarget();

            WaveScheduler.Start();
        }

        public override void Unload()
        {
            base.Unload();

            WaveScheduler.Stop();
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
        }

        public override void Load()
        {
            base.Load();

            Width = 25;
            Height = 25;
            X = ParentScene.Width / 2 - Width / 2;
            Y = ParentScene.Height * 0.75 - Height / 2;
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
                OnFire();
        }

        public override void OnCollision(GameObject other)
        {
            if (other is Enemy enemy)
            {
                Stage.PlayerHitted(enemy.BodyDamage, 0);
                Logger.Log($"I hit to ENEMY : DMG {enemy.BodyDamage}");
            }
        }

        protected virtual void OnFire()
        {
            ParentScene.AddChild(new PlayerBullet(Center.X - 10, Y - 20));
            ParentScene.AddChild(new PlayerBullet(Center.X, Y - 20));
            ParentScene.AddChild(new PlayerBullet(Center.X + 10, Y - 20));
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

        public void Restart()
        {
            CurrentStage.Stop();
            CurrentStage.Start();
        }

        public void StartNext()
        {
            StageIndex++;
            CurrentStage.Start();
            CurrentStage.StageFinished += CurrentStage_StageFinished;
        }

        void CurrentStage_StageFinished(object sender, StageFinishedArgs e)
        {
            Logger.Log($"stage finished state:{e.State}");
        }
    }

    public class TestStage : Stage
    {
        Enemy enemy;
        Player player;
        GameScene scene;

        public TestStage(StageManager manager) : base(manager)
        {
            MaxEnemyHP = EnemyHP = 1000;
            MaxPlayerHP = PlayerHP = 200;
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
            scene.RemoveChild(player);
            for (int i = 0; i < scene.Children.Count; i++)
            {
                var c = scene.Children[i];
                if (c is EnemyBullet || c is PlayerBullet)
                    c.RemoveMe();
            }
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
            Console.WriteLine($"EnemyHP: {EnemyHP}/{MaxEnemyHP} PlayerHP: {PlayerHP}/{MaxPlayerHP} Combo: {Combo}");
            EnemyHP = Math.Max(0, EnemyHP);
            PlayerHP = Math.Max(0, PlayerHP);

            if(PlayerHP <= 0)
            {
                Stop();
                StageFinished?.Invoke(this, new StageFinishedArgs(FinishState.Failed));
            }

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
            Width = 385;
            Height = 600;

            manager = new StageManager(this);
            manager.StartNext();
        }

        public override void OnUpdate(GameTime time)
        {
            base.OnUpdate(time);
            if (Core.KeyState.IsKeyDown(Keys.R))
            {
                manager.Restart();
            }
        }
    }
}
