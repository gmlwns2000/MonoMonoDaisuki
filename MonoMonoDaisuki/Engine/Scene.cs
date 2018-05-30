using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace MonoMonoDaisuki.Engine
{
    public class HitTester
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHit(GameObject me, GameObject other)
        {
            if (me.Sprite != null && me.Sprite is RectangleSprite)
            {
                if (other.Sprite != null && other.Sprite is RectangleSprite)
                {
                    return HitRectRect(me, other);
                }
            }
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool HitRectRect(GameObject me, GameObject other)
        {
            if (me.X + me.Width >= other.X && me.X <= other.X + other.Width &&
                me.Y + me.Height >= other.Y && me.Y <= other.Y + other.Height)
            {
                return true;
            }
            return false;
        }
    }

    public class RectangleSprite : Sprite
    {
        Texture2D cache;
        Color color;
        public Color Color
        {
            get => color;
            set
            {
                if (color != value)
                {
                    color = value;
                    UpdateCache();
                }
            }
        }

        public RectangleSprite(Color color)
        {
            Color = color;
        }

        public override void Draw(GameTime time, SpriteBatch batch, Rectangle target)
        {
            batch.Draw(cache, target, Color.White);
        }

        bool loaded = false;
        public override void Load()
        {
            if (!loaded)
            {
                loaded = true;
                UpdateCache();
            }
        }

        public override void Unload()
        {
            cache?.Dispose();
            cache = null;
        }

        void UpdateCache()
        {
            if (!loaded)
                return;

            if (cache == null)
            {
                cache = new Texture2D(Core.GraphicsDevice, 1, 1);
            }
            cache.SetData(new[] { color });
        }
    }

    public abstract class Sprite
    {
        public abstract void Draw(GameTime time, SpriteBatch batch, Rectangle target);

        public virtual void Load() { }

        public virtual void Unload() { }
    }

    public abstract class GameObject
    {
        public string Name { get; set; }
        public string Tag { get; set; }

        public string HitTestGroup { get; set; } = "Any";
        public bool IsHitVisible = false;
        public bool IsHittedVisible = false;

        public virtual Sprite Sprite { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Vector2 Position => new Vector2((float)X, (float)Y);
        public Rectangle BoundBox => new Rectangle((int)X, (int)Y, (int)Width, (int)Height);
        public Scene ParentScene { get; set; }

        public virtual void OnCollision(GameObject other) { }

        public virtual void Load()
        {
            Sprite?.Load();
        }

        public virtual void Unload()
        {
            Sprite?.Unload();
        }

        public virtual void Update(GameTime time) { }

        public virtual void Draw(GameTime time, SpriteBatch batch)
        {
            Sprite.Draw(time, batch, BoundBox);
        }

        public virtual void RemoveMe()
        {
            ParentScene.RemoveChild(this);
        }
    }

    public abstract class Scene
    {
        public virtual List<GameObject> Children { get; set; } = new List<GameObject>();
        public bool MultiThreadCollision { get; set; } = false;
        public bool IsLoaded { get; protected set; } = false;

        List<GameObject> removePadding = new List<GameObject>();

        public virtual void OnLoad()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Load();
            }

            IsLoaded = true;
        }

        public virtual void OnUnload()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Unload();
            }

            IsLoaded = false;
        }

        public virtual void OnUpdate(GameTime time)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Update(time);
            }
        }

        public virtual void OnDraw(GameTime time, SpriteBatch batch)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Draw(time, batch);
            }
        }

        public void UpdateHitTest(GameTime time)
        {
            var tester = Core.HitTester;

            Profiler.Start("Scene.UpdateHitTest");

            var cores = Environment.ProcessorCount;

            if (MultiThreadCollision)
            {
                Parallel.For(0, cores, (threadId) =>
                {
                    var children = Children;
                    var count = children.Count * children.Count;
                    var block = Math.Max(1, count / cores);
                    var loopTo = Math.Min(threadId + block * (threadId + 1), count);
                    for (int i = threadId * block; i < loopTo; i++)
                    {
                        var me = children[i % children.Count];
                        var other = children[i / children.Count];

                        if (other != me && other.IsHittedVisible && me.IsHitVisible && me.HitTestGroup == other.HitTestGroup)
                        {
                            var result = tester.IsHit(me, other);
                            if (result)
                                me.OnCollision(other);
                        }
                    }
                });
            }
            else
            {
                var children = Children;
                for (int i = 0; i < children.Count * children.Count; i++)
                {
                    var me = children[i % children.Count];
                    var other = children[i / children.Count];

                    if (other != me && other.IsHittedVisible && me.IsHitVisible && me.HitTestGroup == other.HitTestGroup)
                    {
                        var result = tester.IsHit(me, other);
                        if (result)
                            me.OnCollision(other);
                    }
                }
            }

            Profiler.End("Scene.UpdateHitTest");
        }

        public void Update(GameTime time)
        {
            UpdateHitTest(time);
            OnUpdate(time);
            for (int i = 0; i < removePadding.Count; i++)
            {
                if (!Children.Remove(removePadding[i]))
                    Logger.Error("obj not found");
            }
            removePadding.Clear();
        }

        public void Draw(GameTime time, SpriteBatch batch)
        {
            OnDraw(time, batch);
        }

        public void AddChild(GameObject obj)
        {
            obj.ParentScene = this;
            if (IsLoaded)
                obj.Load();
            Children.Add(obj);
        }

        public void RemoveChild(GameObject obj)
        {
            removePadding.Add(obj);
        }
    }
}
