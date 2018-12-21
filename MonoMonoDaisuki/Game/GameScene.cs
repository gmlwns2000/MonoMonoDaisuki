using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMonoDaisuki.Engine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

    public class EnemyBulletWaveLoader
    {
        static List<Type> WaveDefines = new List<Type>();
        static Dictionary<string, Color> ColorDict = new Dictionary<string, Color>();
        static EnemyBulletWaveLoader()
        {
            var asm = Assembly.GetAssembly(typeof(EnemyBulletWaveLoader));
            var types = asm.GetTypes();
            foreach (var type in types)
            {
                if (type.IsSubclassOf(typeof(EnemyBulletWave)))
                {
                    WaveDefines.Add(type);
                }
            }

            var colorProp = typeof(Color).GetProperties();
            foreach (var prop in colorProp)
            {
                if (prop.PropertyType != typeof(Color))
                    continue;
                var c = (Color)prop.GetValue(null, null);
                ColorDict.Add(prop.Name, c);
            }
        }

        public string Path { get; set; }

        public EnemyBulletWaveLoader(string path)
        {
            Path = path;
        }

        Color ParseHexColor(string colorcode)
        {
            colorcode = colorcode.TrimStart('#');

            Color col;
            if (colorcode.Length == 6)
                col = new Color(
                            int.Parse(colorcode.Substring(0, 2), NumberStyles.HexNumber),
                            int.Parse(colorcode.Substring(2, 2), NumberStyles.HexNumber),
                            int.Parse(colorcode.Substring(4, 2), NumberStyles.HexNumber), 255);
            else // assuming length of 8
                col = new Color(
                            int.Parse(colorcode.Substring(2, 2), NumberStyles.HexNumber),
                            int.Parse(colorcode.Substring(4, 2), NumberStyles.HexNumber),
                            int.Parse(colorcode.Substring(6, 2), NumberStyles.HexNumber),
                            int.Parse(colorcode.Substring(0, 2), NumberStyles.HexNumber));

            return col;
        }

        string[] ParamParse(string content, char split = ',')
        {
            var ret = new List<string>();

            var groupChar = new List<char> { '\'', '"', '(' };
            var groupEndChar = new List<char> { '\'', '"', ')' };
            var builder = new StringBuilder();
            int grouping = 0;
            foreach (var c in content)
            {
                if (c == split && grouping == 0)
                {
                    ret.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }
                else if(groupChar.Contains(c))
                {
                    grouping++;
                }
                else if (groupEndChar.Contains(c))
                {
                    grouping--;
                }
                builder.Append(c);
            }
            ret.Add(builder.ToString());
            if (grouping != 0)
                throw new Exception("Uncorrect grouping");

            return ret.ToArray();
        }

        public List<EnemyBulletWave> Load(Enemy enemy)
        {
            var list = new List<EnemyBulletWave>();

            var lines = System.IO.File.ReadAllLines(Path);

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("//"))
                    continue;

                var spl = line.Split(new[] { ' ' }, 2);
                if (spl.Length > 0 && !string.IsNullOrWhiteSpace(line))
                {
                    var param = new List<object>();
                    param.Add(enemy);
                    var paramSpl = ParamParse(spl[1]);

                    foreach(var raw in paramSpl)
                    {
                        var trim = raw.Trim();

                        if ((trim.StartsWith("\"") && trim.StartsWith("\"")) || (trim.StartsWith("'") && trim.EndsWith("'")))
                            param.Add(trim.Trim(new[] { '\'', '"' }));
                        else if (trim.ToLower() == "true")
                            param.Add(true);
                        else if (trim.ToLower() == "false")
                            param.Add(false);
                        else
                        {
                            var funcMatch = Regex.Match(trim, @"((.{1,})\((.*)\))");
                            if (funcMatch.Success)
                            {
                                var funcName = funcMatch.Groups[2].Value.ToLower().Trim();
                                var argsContent = funcMatch.Groups[3].Value.Trim();
                                switch (funcName)
                                {
                                    case "colorrect":
                                        if (ColorDict.ContainsKey(argsContent))
                                        {
                                            param.Add(new RectangleSprite(ColorDict[argsContent]));
                                        }
                                        else
                                        {
                                            param.Add(new RectangleSprite(ParseHexColor(argsContent)));
                                        }
                                        break;
                                    default:
                                        throw new NotImplementedException("Unknown Function");
                                }
                            }
                            else
                            {
                                param.Add(Convert.ToDouble(raw));
                            }
                        }
                    }

                    foreach (var type in WaveDefines)
                    {
                        if (type.Name == spl[0].Trim())
                        {
                            var caa = type.GetConstructors();
                            list.Add((EnemyBulletWave)Activator.CreateInstance(type, param.ToArray()));
                            break;
                        }
                    }
                }
            }

            return list;
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

            var t = new EnemyBulletWaveLoader("Game\\wave.wvsc").Load(this);

            WaveScheduler = new EnemyBulletWaveScheduler();
            WaveScheduler.IsLoop = true;
            WaveScheduler.Waves = t;
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
                for (int i = 0; i < 10; i++)
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
