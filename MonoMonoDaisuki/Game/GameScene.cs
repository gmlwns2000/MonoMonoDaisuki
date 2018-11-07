using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMonoDaisuki.Engine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
            Width = 7;
            Height = 7;
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
        public double Damage { get; set; } = 5;

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

    public abstract class EnemyBulletWave
    {
        public TimeSpan Duration { get; set; }
        public Enemy Parent { get; set; }

        public EnemyBulletWave(Enemy parent, TimeSpan duration)
        {
            Duration = duration;
        }

        public abstract void Start();
        public abstract void Stop();
    }

    public class ActionEnemyBulletWave : EnemyBulletWave
    {
        public Action Action { get; set; }
        public bool IsAsync { get; set; } = true;
        protected CancellationTokenSource cancelSource;
        protected Task task;

        public ActionEnemyBulletWave(Enemy Parent, TimeSpan Duration, Action action, bool isAsync = true)
            : base(Parent, Duration)
        {
            Action = action;
            IsAsync = isAsync;
        }

        public override void Start()
        {
            Stop();

            if (IsAsync)
            {
                cancelSource = new CancellationTokenSource();
                task = Task.Factory.StartNew(Action, TaskCreationOptions.LongRunning);
                return;
            }
            Action.Invoke();
        }

        public override void Stop()
        {
            if (task != null)
            {
                if(!task.IsCompleted)
                    cancelSource.Cancel(true);
                cancelSource.Dispose();
                cancelSource = null;

                task.Wait();
                task.Dispose();
                task = null;
            }
        }
    }

    public class SleepBulletWave : EnemyBulletWave
    {
        public SleepBulletWave(Enemy parent, double ms): base(parent, TimeSpan.FromMilliseconds(ms))
        {

        }

        public override void Start()
        {

        }

        public override void Stop()
        {

        }
    }

    public class EnemyToggleRandomTargetWave : ActionEnemyBulletWave
    {
        public EnemyToggleRandomTargetWave(Enemy parent, bool on): base(parent, TimeSpan.FromMilliseconds(1), null, false)
        {
            Action = new Action(() =>
            {
                parent.ToggleRandomTarget = on;
            });
        }
    }

    public class EnemySetXTargetWave : ActionEnemyBulletWave
    {
        public EnemySetXTargetWave(Enemy parent, double x) :  base (parent, TimeSpan.FromMilliseconds(1), null, false)
        {
            Action = new Action(() =>
            {
                parent.SetXTarget(x);
            });
        }
    }

    public class EnemySpeedSetWave : ActionEnemyBulletWave
    {
        public EnemySpeedSetWave(Enemy parent, double speed) : base(parent, TimeSpan.FromMilliseconds(1), null, false)
        {
            Action = new Action(() =>
            {
                parent.Speed = speed;
            });
        }
    }

    public class AllDirectionEnemyBulletWave : ActionEnemyBulletWave
    {
        public AllDirectionEnemyBulletWave(Enemy parent, double bulletCount, double shootCount, double shootInterval, double bulletForce, Sprite sprite, double angleOffset = 0, double bulletDamage = 10, double bulletSize = 7)
            : base(parent, TimeSpan.FromMilliseconds(shootCount * shootInterval), null)
        {
            Action = new Action(() =>
            {
                if (cancelSource == null)
                    return;
                var tk = cancelSource.Token;
                var scene = parent.ParentScene;
                Logger.Log($"thread id: {Thread.CurrentThread.ManagedThreadId}");
                Thread.CurrentThread.Name = Thread.CurrentThread.ManagedThreadId.ToString();
                for (int i = 0; i < shootCount; i++)
                {
                    if (tk.IsCancellationRequested)
                        return;
                    Logger.Log($"thread id: {Thread.CurrentThread.ManagedThreadId} gen bullet");
                    var angleStep = 360 / bulletCount;
                    var angle = angleOffset;
                    for (int ii = 0; ii < bulletCount; ii++)
                    {
                        scene.AddChild(new DirectionEnemyBullet(parent.Center.X-bulletSize / 2, parent.Center.Y, angle, bulletForce)
                        {
                            Sprite = sprite,
                            Damage = bulletDamage,
                            Width = bulletSize,
                            Height = bulletSize,
                        } );
                        angle += angleStep;
                    }
                    Core.Sleep(shootInterval, tk);
                }
            });
        }
    }

    public class EnemyBulletWaveScheduler
    {
        public virtual bool IsLoop { get; set; } = false;
        public virtual int NextWaveIndex { get; set; } = 0;
        public virtual List<EnemyBulletWave> Waves { get; set; } = new List<EnemyBulletWave>();

        protected virtual EnemyBulletWave NextWave => (NextWaveIndex >= Waves.Count || NextWaveIndex < 0) ? null : Waves[NextWaveIndex];
        protected EnemyBulletWave CurrentWave;
        protected TimeSpan CurrentGen = new TimeSpan();
        GameTimer timer;

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
            if (NextWaveIndex >= Waves.Count)
            {
                if (IsLoop)
                    NextWaveIndex = 0;
                else
                    return;
            }

            if (e - CurrentGen > ((CurrentWave == null) ? new TimeSpan() : CurrentWave.Duration))
            {
                NextWave?.Start();
                CurrentGen = e;
                CurrentWave = NextWave;

                NextWaveIndex += 1;
            }
        }

        public void Stop()
        {
            CurrentWave?.Stop();
            timer.Stop();
        }
    }

    public class Enemy : GameObject
    {
        public virtual double BodyDamage { get; set; } = 20;
        public virtual Stage Stage { get; set; }
        public EnemyBulletWaveScheduler WaveScheduler { get; set; }

        double targetX = 0;
        double speedX = 3;
        public double Speed { get => speedX; set => speedX = value; }

        public bool ToggleRandomTarget { get; set; } = true;

        public Enemy(Stage stage)
        {
            Stage = stage;

            IsHittedVisible = true;
            IsHitVisible = true;

            Sprite = new RectangleSprite(Color.Lime);

            WaveScheduler = new EnemyBulletWaveScheduler();
            WaveScheduler.IsLoop = true;
            WaveScheduler.Waves = new List<EnemyBulletWave>()
            {
                new AllDirectionEnemyBulletWave(this, 20, 4, 300, 3.5, new RectangleSprite(Color.Red), 3),
                new AllDirectionEnemyBulletWave(this, 10, 2, 50, 3.5, new RectangleSprite(Color.Green), 23),
                new AllDirectionEnemyBulletWave(this, 40, 2, 600, 3.5, new RectangleSprite(Color.MediumPurple), 11),
                new AllDirectionEnemyBulletWave(this, 20, 4, 300, 3.5, new RectangleSprite(Color.Blue)),
                new AllDirectionEnemyBulletWave(this, 20, 1, 0, 3.5, new RectangleSprite(Color.Red), 3),
                new AllDirectionEnemyBulletWave(this, 17, 1, 0, 1.88, new RectangleSprite(Color.Orange), 10),
                new AllDirectionEnemyBulletWave(this, 5, 1, 0, 3.01, new RectangleSprite(Color.Pink), 7),
                new AllDirectionEnemyBulletWave(this, 20, 2, 300, 3.5, new RectangleSprite(Color.Red), 20),

                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 0),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 10),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 20),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 30),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 40),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 50),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 60),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 70),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 80),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 90),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 100),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 110),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 120),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 130),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 140),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 150),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 160),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 170),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 180),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 190),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 200),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 210),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 220),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 230),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 240),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 250),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 260),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 270),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 280),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 290),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 300),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 310),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 320),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 330),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 340),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 350),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 360),

                new AllDirectionEnemyBulletWave(this, 17, 1, 0, 1.88, new RectangleSprite(Color.Orange), 10),

                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 0),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 10),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 20),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 30),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 40),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 50),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 60),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 70),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 80),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 90),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 100),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 110),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 120),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 130),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 140),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 150),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 160),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 170),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 180),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 190),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 200),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 210),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 220),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 230),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 240),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 250),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 260),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 270),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 280),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 290),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 300),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 310),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 320),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 330),
                new AllDirectionEnemyBulletWave(this, 3, 1, 25, 3.01, new RectangleSprite(Color.Pink), 340),
                new AllDirectionEnemyBulletWave(this, 3, 1, 1000, 3.01, new RectangleSprite(Color.Pink), 350),

                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.Black), 0, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.White), 12, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.Black), 22, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.White), 44, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.Black), 0, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.White), 11, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 80, 1, 4200, 2.5, new RectangleSprite(Color.Black), 7, bulletSize:8),

                new EnemySpeedSetWave(this, 1.8),
                new AllDirectionEnemyBulletWave(this, 1, 400, 12, 10.5, new RectangleSprite(new Color(253, 72, 157)), 90, bulletSize:25, bulletDamage:0.5),
                new EnemySpeedSetWave(this, 3),
                new SleepBulletWave(this, 300),

                new AllDirectionEnemyBulletWave(this, 40, 3, 500, 2.5, new RectangleSprite(Color.LavenderBlush), 7, bulletSize:8),
                new SleepBulletWave(this, 300),
                
                new EnemyToggleRandomTargetWave(this, false),
                new EnemySpeedSetWave(this, 4.5),
                new EnemySetXTargetWave(this, 0.5),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.75),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.25),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.25),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.89),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.57),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.21),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.74),
                new SleepBulletWave(this, 400),
                new AllDirectionEnemyBulletWave(this, 1, 25, 50, 10, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:50, bulletDamage:0.5),
                new EnemySetXTargetWave(this, 0.0),
                new SleepBulletWave(this, 100),
                new EnemySpeedSetWave(this, 2),
                new AllDirectionEnemyBulletWave(this, 1, 6, 350, 5, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:40),
                new SleepBulletWave(this, 100),
                new EnemySetXTargetWave(this, 1.0),
                new AllDirectionEnemyBulletWave(this, 1, 8, 350, 5, new RectangleSprite(new Color(0, 255, 122, 170)), 90, bulletSize:40),
                new EnemySpeedSetWave(this, 3),
                new EnemyToggleRandomTargetWave(this, true),

                new SleepBulletWave(this, 750),
                new AllDirectionEnemyBulletWave(this, 55, 1, 600, 2.5, new RectangleSprite(Color.Black), 0, bulletSize:25),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.White), 12, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 55, 1, 600, 2.5, new RectangleSprite(Color.Black), 22, bulletSize:25),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.White), 44, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 55, 1, 600, 2.5, new RectangleSprite(Color.Black), 0, bulletSize:25),
                new AllDirectionEnemyBulletWave(this, 80, 1, 600, 2.5, new RectangleSprite(Color.White), 11, bulletSize:8),
                new AllDirectionEnemyBulletWave(this, 55, 1, 4200, 2.5, new RectangleSprite(Color.Black), 7, bulletSize:25),
            };
        }

        public override void Load()
        {
            Logger.Log("OnLoad");
            base.Load();

            Width = 48;
            Height = 48;
            X = ParentScene.Width / 2 - Width / 2;
            Y = 60;
            RandomTarget();

            WaveScheduler.Start();
        }

        public override void Unload()
        {
            Logger.Log("Unloaded");
            base.Unload();

            WaveScheduler.Stop();
        }

        public override void Update(GameTime time)
        {
            if (targetX - X > 0)
            {
                X += speedX;
                X = Math.Min(targetX, X);
            }
            else
            {
                X -= speedX;
                X = Math.Max(targetX, X);
            }

            if (Math.Abs(targetX - X) < speedX)
            {
                X = targetX;
                if(ToggleRandomTarget)
                    RandomTarget();
            }
        }

        public override void OnCollision(GameObject other)
        {
            if (other is PlayerBullet bullet)
            {
                Stage.EnemyHitted(bullet.Damage, bullet.Point);
                bullet.RemoveMe();
            }
        }

        public void SetXTarget(double percent)
        {
            targetX = Core.ScreenWidth * percent;
        }

        void RandomTarget()
        {
            SetXTarget(RandomEx.NextRange(0.01, 0.99));
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

        protected virtual double SpeedX { get; set; } = 3;
        protected virtual double SpeedY { get; set; } = 2;
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

            Width = 10;
            Height = 10;
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
            if (key.IsKeyDown(Keys.J))
                Fire();
        }

        public virtual void Fire()
        {
            if (fireTimer > FireFrame)
                fireTimer = 0;
            if (fireTimer == 0)
                OnFire();
        }

        public virtual void Skill()
        {

        }

        public override void OnCollision(GameObject other)
        {
            if (other is Enemy enemy)
            {
                Stage.PlayerHitted(enemy.BodyDamage, 0);
                Logger.Log($"I hit to ENEMY : DMG {enemy.BodyDamage}");
            }
            else if(other is EnemyBullet bullet)
            {
                Stage.PlayerHitted(bullet.Damage, 0);
                bullet.RemoveMe();
            }
        }

        public override void Draw(GameTime time, RenderContext batch)
        {
            base.Draw(time, batch);

            var barWidth = 50.0;
            var barHeight = 3.0;
            batch.DrawRectangle(Color.White, X - barWidth * 0.5 + Width / 2, Y + Height + 4, barWidth, barHeight);
            batch.DrawRectangle(Color.Red, X - barWidth * 0.5 + Width / 2, Y + Height + 4, barWidth * ((double)Stage.PlayerHP/Stage.MaxPlayerHP), barHeight);
        }

        protected virtual void OnFire()
        {
            ParentScene.AddChild(new PlayerBullet(Center.X, Y - 20));
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
            Stop();
            StageIndex = -1;
            StartNext();

            Stop();
            StageIndex = -1;
            StartNext();

            Stop();
            StageIndex = -1;
            StartNext();

            Stop();
            StageIndex = -1;
            StartNext();

            Stop();
            StageIndex = -1;
            StartNext();
        }

        public void Stop()
        {
            CurrentStage.StageFinished -= CurrentStage_StageFinished;
            CurrentStage.Stop();
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
            scene = manager.GameScene;
        }

        public override void Start()
        {
            MaxEnemyHP = EnemyHP = 1000;
#if DEBUG
            PlayerHP = 5000;
#else
            PlayerHP = 30;
#endif

            MaxPlayerHP = PlayerHP;
            enemy = new Enemy(this);
            player = new Player(this);
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
                for (int i = 0; i < 123; i++)
                {
                    Console.WriteLine("AYYYY Congratulation, Success");
                }
                Console.ReadLine();
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
