using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MonoMonoDaisuki.Engine
{
    public class HitTester
    {
        public bool IsHit(GameObject me, GameObject other)
        {
            if(me.Sprite != null && me.Sprite is RectangleSprite)
            {
                if(other.Sprite != null && other.Sprite is RectangleSprite)
                {
                    return hitRectRect(me, other);
                }
            }
            return false;
        }

        bool hitRectRect(GameObject me, GameObject other)
        {
            if (me.X + me.Sprite.Width >= other.X && me.X <= other.X + other.Sprite.Width &&
                me.Y + me.Sprite.Height >= other.Y && me.Y <= other.Y + other.Sprite.Height)
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
                    if(loaded)
                        UpdateCache();
                }
            }
        }

        public RectangleSprite(double w, double h, Color color)
        {
            Width = w;
            Height = h;
            Color = color;
        }

        public override void Draw(GameTime time, SpriteBatch batch, Vector2 pos)
        {
            batch.Draw(cache, new Rectangle(pos.ToPoint(), Size.ToPoint()), Color.White);
        }

        bool loaded = false;
        public override void Load()
        {
            UpdateCache();
            loaded = true;
        }

        public override void Unload()
        {
            cache?.Dispose();
            cache = null;
        }

        void UpdateCache()
        {
            if (cache == null)
            {
                cache = new Texture2D(Engine.GraphicsDevice, 1, 1);
            }
            cache.SetData(new[] { color });
        }
    }

    public abstract class Sprite
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public virtual Vector2 Size => new Vector2((float)Width, (float)Height);

        public abstract void Draw(GameTime time, SpriteBatch batch, Vector2 pos);
        
        public virtual void Load() { }
        
        public virtual void Unload() { }
    }

    public abstract class GameObject
    {
        public string Name { get; set; }
        public string Tag { get; set; }

        public string HitTestGroup { get; set; } = "Any";
        public bool IsHitVisible { get; set; } = false;
        public bool IsHittedVisible { get; set; } = false;

        public virtual Sprite Sprite { get; set; }

        public double X {get;set; }
        public double Y {get; set; }
        public Vector2 Position => new Vector2((float)X, (float)Y);

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
            Sprite.Draw(time, batch, Position);
        }
    }

    public abstract class Scene
    {
        public virtual List<GameObject> Children { get; set; } = new List<GameObject>();

        public virtual void OnLoad()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Load();
            }
        }

        public virtual void OnUnload()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Unload();
            }
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
            var tester = Engine.HitTester;
            Parallel.For(0, Children.Count * Children.Count, (i)=>
            {
                var me = Children[i % Children.Count];
                var other = Children[i / Children.Count];

                if (other != me && other.IsHittedVisible && me.IsHitVisible && me.HitTestGroup == other.HitTestGroup)
                {
                    var result = tester.IsHit(me, other);
                    if (result)
                        me.OnCollision(other);
                }
            });

            //foreach (var me in Children)
            //{
            //    if (me.IsHitVisible)
            //    {
            //        foreach (var other in Children)
            //        {
            //            if (other != me && other.IsHittedVisible && me.HitTestGroup == other.HitTestGroup)
            //            {
            //                var result = tester.IsHit(me, other);
            //                if (result)
            //                    me.OnCollision(other);
            //            }
            //        }
            //    }
            //}
        }

        public void Update(GameTime time)
        {
            UpdateHitTest(time);
            OnUpdate(time);
        }

        public void Draw(GameTime time, SpriteBatch batch)
        {
            OnDraw(time, batch);
        }
    }
}
