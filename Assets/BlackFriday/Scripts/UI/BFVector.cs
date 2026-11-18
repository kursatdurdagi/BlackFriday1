using UnityEngine;
using UnityEngine.UIElements;

namespace BlackFriday
{
    /// <summary>
    /// Vektörel arayüz parçaları (UI Toolkit Painter2D). Hiç bitmap yok: ikonlar, fiş kenarı, fiyat etiketi,
    /// barkod, süre halkası, yıldızlar ve nişangâh çözünürlükten bağımsız çizilir.
    /// Renkler USS'ten gelir: ikon/çizgi rengi = `color`, dolgu = `--bf-fill`, kenar = `--bf-stroke`.
    /// </summary>
    public enum IconKind
    {
        None, Cart, Tag, Clock, Bolt, Star, Check, Cross, Megaphone, Grab, Throw, Lock, Users, Gear, Play, Exit, Eye,
        Percent, Receipt, ChevronLeft, ChevronRight, Home, Push, Jump, Warning,
        // ürünler (ProductDef.id sırasıyla)
        TV, Console, Toaster, Sneakers, Headphones, Teddy, Blender, Phone, Bag, Perfume
    }

    public enum ShapeKind { Card, Tag, Receipt, Burst, Slant, Ticket }

    public static class BFDraw
    {
        public static IconKind ProductIcon(int productId)
        {
            int k = (int)IconKind.TV + productId;
            return k >= (int)IconKind.TV && k <= (int)IconKind.Perfume ? (IconKind)k : IconKind.Tag;
        }

        public static void RoundRect(Painter2D p, Rect r, float rad)
        {
            rad = Mathf.Min(rad, r.width * 0.5f, r.height * 0.5f);
            p.MoveTo(new Vector2(r.xMin + rad, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), rad);
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), rad);
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), rad);
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), rad);
            p.ClosePath();
        }

        public static void Circle(Painter2D p, Vector2 c, float rad)
        {
            p.MoveTo(c + new Vector2(rad, 0));
            p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
        }

        public static void StarPath(Painter2D p, Vector2 c, float rOut, float rIn, int points = 5, float rot = -90f)
        {
            for (int i = 0; i < points * 2; i++)
            {
                float a = (rot + i * 180f / points) * Mathf.Deg2Rad;
                float rr = (i & 1) == 0 ? rOut : rIn;
                var v = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (i == 0) p.MoveTo(v); else p.LineTo(v);
            }
            p.ClosePath();
        }

        /// <summary>İkonu r dikdörtgenine çizer (çizgi ikon stili, yuvarlak uçlar).</summary>
        public static void Icon(Painter2D p, IconKind kind, Rect r, Color c)
        {
            if (kind == IconKind.None) return;
            float s = Mathf.Min(r.width, r.height);
            var o = new Vector2(r.x + (r.width - s) * 0.5f, r.y + (r.height - s) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
            Rect R(float x, float y, float w, float h) => new Rect(o.x + x * s, o.y + y * s, w * s, h * s);
            void Line(params float[] v)
            {
                p.BeginPath();
                p.MoveTo(P(v[0], v[1]));
                for (int i = 2; i < v.Length; i += 2) p.LineTo(P(v[i], v[i + 1]));
                p.Stroke();
            }
            void Poly(bool fill, params float[] v)
            {
                p.BeginPath();
                p.MoveTo(P(v[0], v[1]));
                for (int i = 2; i < v.Length; i += 2) p.LineTo(P(v[i], v[i + 1]));
                p.ClosePath();
                if (fill) p.Fill(); else p.Stroke();
            }
            void Circ(float x, float y, float rad, bool fill)
            {
                p.BeginPath(); Circle(p, P(x, y), rad * s);
                if (fill) p.Fill(); else p.Stroke();
            }
            void RR(float x, float y, float w, float h, float rad, bool fill)
            {
                p.BeginPath(); RoundRect(p, R(x, y, w, h), rad * s);
                if (fill) p.Fill(); else p.Stroke();
            }
            void HalfArc(float x, float y, float rad)
            {
                p.BeginPath();
                p.Arc(P(x, y), rad * s, Angle.Degrees(180), Angle.Degrees(360));
                p.Stroke();
            }

            p.strokeColor = c; p.fillColor = c;
            p.lineWidth = Mathf.Max(1.5f, s * 0.085f);
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;

            switch (kind)
            {
                case IconKind.Cart:
                    Line(0.06f, 0.18f, 0.2f, 0.18f, 0.33f, 0.64f, 0.8f, 0.64f, 0.92f, 0.3f, 0.24f, 0.3f);
                    Circ(0.4f, 0.82f, 0.07f, true); Circ(0.74f, 0.82f, 0.07f, true);
                    break;
                case IconKind.Tag:
                    Poly(false, 0.1f, 0.1f, 0.55f, 0.1f, 0.92f, 0.47f, 0.5f, 0.9f, 0.1f, 0.5f);
                    Circ(0.3f, 0.3f, 0.06f, true);
                    break;
                case IconKind.Clock:
                    Circ(0.5f, 0.5f, 0.4f, false);
                    Line(0.5f, 0.26f, 0.5f, 0.5f, 0.68f, 0.6f);
                    break;
                case IconKind.Bolt:
                    Poly(true, 0.58f, 0.05f, 0.2f, 0.55f, 0.46f, 0.55f, 0.38f, 0.95f, 0.8f, 0.42f, 0.54f, 0.42f);
                    break;
                case IconKind.Star:
                    p.BeginPath(); StarPath(p, P(0.5f, 0.53f), 0.46f * s, 0.2f * s); p.Fill();
                    break;
                case IconKind.Check: Line(0.15f, 0.55f, 0.4f, 0.78f, 0.86f, 0.24f); break;
                case IconKind.Cross: Line(0.2f, 0.2f, 0.8f, 0.8f); Line(0.8f, 0.2f, 0.2f, 0.8f); break;
                case IconKind.Megaphone:
                    Poly(false, 0.1f, 0.4f, 0.28f, 0.4f, 0.66f, 0.18f, 0.66f, 0.82f, 0.28f, 0.6f, 0.1f, 0.6f);
                    Line(0.8f, 0.5f, 0.94f, 0.5f); Line(0.78f, 0.32f, 0.9f, 0.24f); Line(0.78f, 0.68f, 0.9f, 0.76f);
                    break;
                case IconKind.Grab:
                    Line(0.5f, 0.1f, 0.5f, 0.56f); Line(0.3f, 0.38f, 0.5f, 0.58f, 0.7f, 0.38f);
                    Line(0.15f, 0.62f, 0.15f, 0.88f, 0.85f, 0.88f, 0.85f, 0.62f);
                    break;
                case IconKind.Throw:
                    p.BeginPath(); p.MoveTo(P(0.12f, 0.82f)); p.QuadraticCurveTo(P(0.38f, 0.02f), P(0.84f, 0.5f)); p.Stroke();
                    Line(0.6f, 0.48f, 0.86f, 0.53f, 0.86f, 0.26f);
                    break;
                case IconKind.Lock:
                    RR(0.2f, 0.45f, 0.6f, 0.45f, 0.08f, false);
                    p.BeginPath(); p.MoveTo(P(0.32f, 0.45f)); p.LineTo(P(0.32f, 0.3f));
                    p.Arc(P(0.5f, 0.3f), 0.18f * s, Angle.Degrees(180), Angle.Degrees(360)); p.LineTo(P(0.68f, 0.45f)); p.Stroke();
                    break;
                case IconKind.Users:
                    Circ(0.5f, 0.32f, 0.16f, false);
                    p.BeginPath(); p.MoveTo(P(0.2f, 0.88f)); p.BezierCurveTo(P(0.2f, 0.52f), P(0.8f, 0.52f), P(0.8f, 0.88f)); p.Stroke();
                    break;
                case IconKind.Gear:
                    Circ(0.5f, 0.5f, 0.14f, false); Circ(0.5f, 0.5f, 0.3f, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45f * Mathf.Deg2Rad; float cx = Mathf.Cos(a), sy = Mathf.Sin(a);
                        Line(0.5f + cx * 0.32f, 0.5f + sy * 0.32f, 0.5f + cx * 0.43f, 0.5f + sy * 0.43f);
                    }
                    break;
                case IconKind.Play: Poly(true, 0.28f, 0.15f, 0.85f, 0.5f, 0.28f, 0.85f); break;
                case IconKind.Exit:
                    Line(0.55f, 0.15f, 0.15f, 0.15f, 0.15f, 0.85f, 0.55f, 0.85f);
                    Line(0.4f, 0.5f, 0.9f, 0.5f); Line(0.72f, 0.32f, 0.9f, 0.5f, 0.72f, 0.68f);
                    break;
                case IconKind.Eye:
                    p.BeginPath(); p.MoveTo(P(0.08f, 0.5f)); p.QuadraticCurveTo(P(0.5f, 0.06f), P(0.92f, 0.5f));
                    p.QuadraticCurveTo(P(0.5f, 0.94f), P(0.08f, 0.5f)); p.Stroke();
                    Circ(0.5f, 0.5f, 0.12f, true);
                    break;
                case IconKind.Percent:
                    Line(0.8f, 0.15f, 0.2f, 0.85f); Circ(0.27f, 0.27f, 0.12f, false); Circ(0.73f, 0.73f, 0.12f, false);
                    break;
                case IconKind.Receipt:
                    Poly(false, 0.22f, 0.1f, 0.78f, 0.1f, 0.78f, 0.9f, 0.64f, 0.8f, 0.5f, 0.9f, 0.36f, 0.8f, 0.22f, 0.9f);
                    Line(0.36f, 0.32f, 0.64f, 0.32f); Line(0.36f, 0.5f, 0.64f, 0.5f);
                    break;
                case IconKind.ChevronLeft: Line(0.62f, 0.18f, 0.32f, 0.5f, 0.62f, 0.82f); break;
                case IconKind.ChevronRight: Line(0.38f, 0.18f, 0.68f, 0.5f, 0.38f, 0.82f); break;
                case IconKind.Home:
                    Line(0.1f, 0.5f, 0.5f, 0.14f, 0.9f, 0.5f); Line(0.24f, 0.44f, 0.24f, 0.86f, 0.76f, 0.86f, 0.76f, 0.44f);
                    break;
                case IconKind.Push:
                    Line(0.12f, 0.5f, 0.6f, 0.5f); Line(0.42f, 0.3f, 0.62f, 0.5f, 0.42f, 0.7f); Line(0.82f, 0.18f, 0.82f, 0.82f);
                    break;
                case IconKind.Jump:
                    Line(0.5f, 0.78f, 0.5f, 0.16f); Line(0.28f, 0.38f, 0.5f, 0.16f, 0.72f, 0.38f); Line(0.22f, 0.9f, 0.78f, 0.9f);
                    break;
                case IconKind.Warning:
                    Poly(false, 0.5f, 0.1f, 0.92f, 0.86f, 0.08f, 0.86f);
                    Line(0.5f, 0.4f, 0.5f, 0.6f); Circ(0.5f, 0.73f, 0.035f, true);
                    break;

                case IconKind.TV:
                    RR(0.08f, 0.18f, 0.84f, 0.52f, 0.05f, false); Line(0.5f, 0.7f, 0.5f, 0.85f); Line(0.3f, 0.86f, 0.7f, 0.86f);
                    break;
                case IconKind.Console:
                    RR(0.08f, 0.3f, 0.84f, 0.42f, 0.2f, false);
                    Line(0.22f, 0.51f, 0.38f, 0.51f); Line(0.3f, 0.43f, 0.3f, 0.59f);
                    Circ(0.66f, 0.46f, 0.04f, true); Circ(0.76f, 0.57f, 0.04f, true);
                    break;
                case IconKind.Toaster:
                    RR(0.12f, 0.4f, 0.76f, 0.44f, 0.1f, false);
                    RR(0.26f, 0.2f, 0.2f, 0.2f, 0.05f, false); RR(0.54f, 0.2f, 0.2f, 0.2f, 0.05f, false);
                    Line(0.3f, 0.66f, 0.5f, 0.66f);
                    break;
                case IconKind.Sneakers:
                    p.BeginPath(); p.MoveTo(P(0.08f, 0.76f)); p.LineTo(P(0.08f, 0.36f)); p.LineTo(P(0.3f, 0.36f));
                    p.QuadraticCurveTo(P(0.4f, 0.58f), P(0.7f, 0.58f)); p.QuadraticCurveTo(P(0.92f, 0.6f), P(0.92f, 0.76f));
                    p.ClosePath(); p.Stroke();
                    Line(0.38f, 0.48f, 0.46f, 0.42f); Line(0.5f, 0.55f, 0.58f, 0.48f);
                    break;
                case IconKind.Headphones:
                    HalfArc(0.5f, 0.56f, 0.34f);
                    RR(0.08f, 0.52f, 0.17f, 0.32f, 0.06f, true); RR(0.75f, 0.52f, 0.17f, 0.32f, 0.06f, true);
                    break;
                case IconKind.Teddy:
                    Circ(0.27f, 0.24f, 0.11f, false); Circ(0.73f, 0.24f, 0.11f, false); Circ(0.5f, 0.52f, 0.32f, false);
                    Circ(0.39f, 0.46f, 0.035f, true); Circ(0.61f, 0.46f, 0.035f, true); Circ(0.5f, 0.62f, 0.08f, false);
                    break;
                case IconKind.Blender:
                    Poly(false, 0.28f, 0.1f, 0.72f, 0.1f, 0.64f, 0.6f, 0.36f, 0.6f);
                    RR(0.26f, 0.62f, 0.48f, 0.28f, 0.06f, false); Circ(0.5f, 0.76f, 0.04f, true);
                    break;
                case IconKind.Phone:
                    RR(0.3f, 0.08f, 0.4f, 0.84f, 0.07f, false); Line(0.44f, 0.8f, 0.56f, 0.8f);
                    break;
                case IconKind.Bag:
                    RR(0.16f, 0.38f, 0.68f, 0.5f, 0.06f, false); HalfArc(0.5f, 0.38f, 0.18f);
                    break;
                case IconKind.Perfume:
                    RR(0.24f, 0.44f, 0.52f, 0.46f, 0.08f, false); RR(0.42f, 0.27f, 0.16f, 0.17f, 0.02f, false);
                    Line(0.5f, 0.27f, 0.5f, 0.14f, 0.62f, 0.14f); Line(0.72f, 0.1f, 0.8f, 0.07f); Line(0.74f, 0.17f, 0.84f, 0.17f);
                    break;
            }
        }
    }

    /// <summary>Tek renk vektör ikon. Rengi USS `color` belirler.</summary>
    [UxmlElement]
    public partial class BFIcon : VisualElement
    {
        IconKind kind;
        [UxmlAttribute]
        public IconKind Kind { get => kind; set { if (kind == value) return; kind = value; MarkDirtyRepaint(); } }

        public BFIcon() { AddToClassList("bf-icon"); pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
        public BFIcon(IconKind k) : this() { kind = k; }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 2 || r.height < 2) return;
            BFDraw.Icon(ctx.painter2D, kind, r, resolvedStyle.color);
        }
    }

    /// <summary>
    /// Vektör arka planlı kutu: fiş (zikzak kenar), fiyat etiketi, yıldız patlaması, eğik bant, bilet.
    /// Çocuk elemanlar üstüne dizilir. USS: --bf-fill, --bf-stroke, --bf-stroke-width, --bf-shadow.
    /// </summary>
    [UxmlElement]
    public partial class BFShape : VisualElement
    {
        static readonly CustomStyleProperty<Color> FillProp = new CustomStyleProperty<Color>("--bf-fill");
        static readonly CustomStyleProperty<Color> StrokeProp = new CustomStyleProperty<Color>("--bf-stroke");
        static readonly CustomStyleProperty<Color> ShadowProp = new CustomStyleProperty<Color>("--bf-shadow");
        static readonly CustomStyleProperty<float> StrokeWProp = new CustomStyleProperty<float>("--bf-stroke-width");

        ShapeKind shape;
        Color fill = new Color(0.06f, 0.06f, 0.08f, 0.92f), stroke = Color.clear, shadow = Color.clear;
        float strokeW = 0f;

        [UxmlAttribute]
        public ShapeKind Shape { get => shape; set { shape = value; MarkDirtyRepaint(); } }

        public BFShape()
        {
            AddToClassList("bf-shape");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(FillProp, out var f)) fill = f;
                if (e.customStyle.TryGetValue(StrokeProp, out var s)) stroke = s;
                if (e.customStyle.TryGetValue(ShadowProp, out var sh)) shadow = sh;
                if (e.customStyle.TryGetValue(StrokeWProp, out var w)) strokeW = w;
                MarkDirtyRepaint();
            });
        }

        public void SetFill(Color c) { fill = c; MarkDirtyRepaint(); }

        void Path(Painter2D p, Rect r)
        {
            p.BeginPath();
            switch (shape)
            {
                case ShapeKind.Card:
                    BFDraw.RoundRect(p, r, 14f);
                    break;
                case ShapeKind.Tag:
                {
                    // sola bakan uçlu fiyat etiketi
                    float nose = Mathf.Min(r.height * 0.5f, r.width * 0.3f), rad = 8f;
                    p.MoveTo(new Vector2(r.xMin + nose, r.yMin));
                    p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), rad);
                    p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin + nose, r.yMax), rad);
                    p.LineTo(new Vector2(r.xMin + nose, r.yMax));
                    p.LineTo(new Vector2(r.xMin, r.center.y));
                    p.ClosePath();
                    break;
                }
                case ShapeKind.Receipt:
                {
                    // üstü düz, altı zikzak yırtık fiş
                    float tooth = 14f; int n = Mathf.Max(2, Mathf.RoundToInt(r.width / tooth)); float tw = r.width / n, th = 8f;
                    p.MoveTo(new Vector2(r.xMin, r.yMin));
                    p.LineTo(new Vector2(r.xMax, r.yMin));
                    p.LineTo(new Vector2(r.xMax, r.yMax - th));
                    for (int i = n - 1; i >= 0; i--)
                    {
                        p.LineTo(new Vector2(r.xMin + (i + 0.5f) * tw, r.yMax));
                        p.LineTo(new Vector2(r.xMin + i * tw, r.yMax - th));
                    }
                    p.ClosePath();
                    break;
                }
                case ShapeKind.Burst:
                    BFDraw.StarPath(p, r.center, Mathf.Min(r.width, r.height) * 0.5f, Mathf.Min(r.width, r.height) * 0.4f, 14, -90f);
                    break;
                case ShapeKind.Slant:
                {
                    float sk = Mathf.Min(r.height * 0.45f, r.width * 0.2f);
                    p.MoveTo(new Vector2(r.xMin + sk, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMin));
                    p.LineTo(new Vector2(r.xMax - sk, r.yMax)); p.LineTo(new Vector2(r.xMin, r.yMax));
                    p.ClosePath();
                    break;
                }
                case ShapeKind.Ticket:
                {
                    // iki yanı çentikli bilet / kupon
                    float notch = Mathf.Min(10f, r.height * 0.22f), rad = 10f;
                    p.MoveTo(new Vector2(r.xMin + rad, r.yMin));
                    p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.center.y), rad);
                    p.LineTo(new Vector2(r.xMax, r.center.y - notch));
                    p.Arc(new Vector2(r.xMax, r.center.y), notch, Angle.Degrees(270), Angle.Degrees(90), ArcDirection.CounterClockwise);
                    p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), rad);
                    p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.center.y), rad);
                    p.LineTo(new Vector2(r.xMin, r.center.y + notch));
                    p.Arc(new Vector2(r.xMin, r.center.y), notch, Angle.Degrees(90), Angle.Degrees(270), ArcDirection.CounterClockwise);
                    p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), rad);
                    p.ClosePath();
                    break;
                }
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = new Rect(0, 0, layout.width, layout.height);
            if (r.width < 4 || r.height < 4) return;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            float inset = strokeW * 0.5f + 1f;
            var body = new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f);
            if (shadow.a > 0.01f)
            {
                body.width -= 5f; body.height -= 5f;
                var sr = body; sr.position += new Vector2(5f, 5f);
                p.fillColor = shadow; Path(p, sr); p.Fill();
            }
            p.fillColor = fill; Path(p, body); p.Fill();
            if (strokeW > 0.01f && stroke.a > 0.01f)
            {
                p.strokeColor = stroke; p.lineWidth = strokeW; Path(p, body); p.Stroke();
            }
            if (shape == ShapeKind.Tag)
            {
                // etiket deliği
                float nose = Mathf.Min(body.height * 0.5f, body.width * 0.3f);
                p.fillColor = stroke.a > 0.01f ? stroke : new Color(0, 0, 0, 0.6f);
                p.BeginPath(); BFDraw.Circle(p, new Vector2(body.xMin + nose * 0.95f, body.center.y), Mathf.Min(6f, body.height * 0.09f)); p.Fill();
            }
        }
    }

    /// <summary>Dairesel ilerleme halkası (süre). USS: color = dolu kısım, --bf-stroke = iz, --bf-fill = iç dolgu.</summary>
    [UxmlElement]
    public partial class BFRing : VisualElement
    {
        static readonly CustomStyleProperty<Color> FillProp = new CustomStyleProperty<Color>("--bf-fill");
        static readonly CustomStyleProperty<Color> TrackProp = new CustomStyleProperty<Color>("--bf-stroke");
        float value = 1f; Color fill = new Color(0.06f, 0.06f, 0.08f, 0.92f), track = new Color(1, 1, 1, 0.18f);

        [UxmlAttribute]
        public float Value { get => value; set { float v = Mathf.Clamp01(value); if (Mathf.Abs(v - this.value) < 0.002f) return; this.value = v; MarkDirtyRepaint(); } }

        public BFRing()
        {
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(FillProp, out var f)) fill = f;
                if (e.customStyle.TryGetValue(TrackProp, out var t)) track = t;
                MarkDirtyRepaint();
            });
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 8 || h < 8) return;
            var p = ctx.painter2D;
            var c = new Vector2(w * 0.5f, h * 0.5f);
            float lw = Mathf.Min(w, h) * 0.085f, rad = Mathf.Min(w, h) * 0.5f - lw * 0.5f - 1f;
            p.fillColor = fill; p.BeginPath(); BFDraw.Circle(p, c, rad); p.Fill();
            p.lineWidth = lw; p.lineCap = LineCap.Butt;
            p.strokeColor = track; p.BeginPath(); BFDraw.Circle(p, c, rad); p.Stroke();
            if (value > 0.002f)
            {
                p.strokeColor = resolvedStyle.color; p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(c, rad, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * value));
                p.Stroke();
            }
        }
    }

    /// <summary>Barkod şeridi (dekor). Rengi USS `color`.</summary>
    [UxmlElement]
    public partial class BFBarcode : VisualElement
    {
        [UxmlAttribute] public int Seed { get; set; } = 7;
        public BFBarcode() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 8 || h < 4) return;
            var p = ctx.painter2D;
            p.fillColor = resolvedStyle.color;
            uint s = (uint)(Seed * 7919 + 13);
            float x = 0;
            while (x < w)
            {
                s = s * 1664525u + 1013904223u;
                float bw = 2f + (s >> 28) % 3 * 1.5f;
                s = s * 1664525u + 1013904223u;
                float gap = 2f + (s >> 29) % 3 * 1.5f;
                if (x + bw > w) break;
                p.BeginPath(); p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x + bw, 0));
                p.LineTo(new Vector2(x + bw, h)); p.LineTo(new Vector2(x, h)); p.ClosePath(); p.Fill();
                x += bw + gap;
            }
        }
    }

    /// <summary>Kesik çizgi (fişteki ayırıcı). Rengi USS `color`.</summary>
    [UxmlElement]
    public partial class BFDash : VisualElement
    {
        public BFDash() { AddToClassList("bf-dash"); pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, y = layout.height * 0.5f;
            if (w < 8) return;
            var p = ctx.painter2D;
            p.strokeColor = resolvedStyle.color; p.lineWidth = 2f; p.lineCap = LineCap.Butt;
            for (float x = 0; x < w; x += 12f)
            {
                p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(Mathf.Min(x + 7f, w), y)); p.Stroke();
            }
        }
    }

    /// <summary>Yıldız puanı (3 yıldız). color = dolu, --bf-stroke = boş.</summary>
    [UxmlElement]
    public partial class BFStars : VisualElement
    {
        static readonly CustomStyleProperty<Color> EmptyProp = new CustomStyleProperty<Color>("--bf-stroke");
        int filled; Color empty = new Color(0, 0, 0, 0.18f);
        [UxmlAttribute] public int Filled { get => filled; set { if (filled == value) return; filled = value; MarkDirtyRepaint(); } }
        [UxmlAttribute] public int Count { get; set; } = 3;

        public BFStars()
        {
            pickingMode = PickingMode.Ignore; generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e => { if (e.customStyle.TryGetValue(EmptyProp, out var c)) empty = c; MarkDirtyRepaint(); });
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 8 || h < 8) return;
            var p = ctx.painter2D;
            float cell = w / Count, rad = Mathf.Min(cell, h) * 0.46f;
            for (int i = 0; i < Count; i++)
            {
                // ortadaki yıldız biraz büyük ve yukarıda
                bool mid = Count == 3 && i == 1;
                var c = new Vector2(cell * (i + 0.5f), h * (mid ? 0.46f : 0.56f));
                p.fillColor = i < filled ? resolvedStyle.color : empty;
                p.BeginPath(); BFDraw.StarPath(p, c, rad * (mid ? 1f : 0.82f), rad * (mid ? 0.45f : 0.37f), 5, -90f + (i - 1) * 12f); p.Fill();
            }
        }
    }

    /// <summary>Nişangâh: 0 = nokta + halka, 1 = hedef (alınabilir ürün), 2 = fırlatma.</summary>
    [UxmlElement]
    public partial class BFCrosshair : VisualElement
    {
        int mode;
        [UxmlAttribute] public int Mode { get => mode; set { if (mode == value) return; mode = value; MarkDirtyRepaint(); } }
        public BFCrosshair() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 8) return;
            var p = ctx.painter2D; var c = new Vector2(w * 0.5f, h * 0.5f);
            Color main = mode == 1 ? new Color(0.35f, 1f, 0.55f) : (mode == 2 ? new Color(1f, 0.82f, 0.12f) : Color.white);
            var dark = new Color(0, 0, 0, 0.55f);
            void Ring(float rad, float lw, Color col, float a0, float a1)
            {
                p.strokeColor = col; p.lineWidth = lw; p.lineCap = LineCap.Round;
                p.BeginPath(); p.Arc(c, rad, Angle.Degrees(a0), Angle.Degrees(a1)); p.Stroke();
            }
            p.fillColor = dark; p.BeginPath(); BFDraw.Circle(p, c, 4.5f); p.Fill();
            p.fillColor = main; p.BeginPath(); BFDraw.Circle(p, c, 2.6f); p.Fill();
            float rad0 = mode == 0 ? 9f : 13f;
            if (mode == 2)
            {
                // dört köşe yayı: fırlatma
                for (int i = 0; i < 4; i++) { Ring(rad0, 5f, dark, i * 90f + 20f, i * 90f + 70f); }
                for (int i = 0; i < 4; i++) { Ring(rad0, 2.5f, main, i * 90f + 20f, i * 90f + 70f); }
            }
            else
            {
                Ring(rad0, 4.5f, dark, 0, 360); Ring(rad0, 2f, main, 0, 360);
            }
        }
    }

    /// <summary>Yazar kasa müşteri ekranı gibi 7 segmentli rakamlar ("3:35"). Rengi USS `color`,
    /// sönük segmentler `--bf-stroke` (gerçek LCD'deki gibi "8" gölgesi).</summary>
    [UxmlElement]
    public partial class BFSeg : VisualElement
    {
        static readonly CustomStyleProperty<Color> GhostProp = new CustomStyleProperty<Color>("--bf-stroke");
        string text = "0:00";
        Color ghost = new Color(1, 1, 1, 0.06f);

        [UxmlAttribute]
        public string Text { get => text; set { if (value == text) return; text = value ?? ""; MarkDirtyRepaint(); } }

        // a b c d e f g
        static readonly byte[] Digits = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

        public BFSeg()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e => { if (e.customStyle.TryGetValue(GhostProp, out var g)) ghost = g; MarkDirtyRepaint(); });
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 8 || h < 8 || string.IsNullOrEmpty(text)) return;
            var p = ctx.painter2D;
            // genişlik birimleri: rakam 1, iki nokta 0.35, boşluk 0.25
            float units = 0;
            foreach (char ch in text) units += ch == ':' || ch == '.' ? 0.35f : (ch == ' ' ? 0.3f : 1f);
            float gap = 0.18f;
            float dh = h * 0.92f;
            float dw = Mathf.Min(dh * 0.55f, w / (units + gap * text.Length));
            float t = dw * 0.2f;
            float x = (w - (units * dw + gap * dw * (text.Length - 1))) * 0.5f;
            float y0 = (h - dh) * 0.5f;
            Color on = resolvedStyle.color;
            foreach (char ch in text)
            {
                if (ch == ':' || ch == '.')
                {
                    float cw = dw * 0.35f;
                    p.fillColor = on;
                    if (ch == ':') { Dot(p, x + cw * 0.5f, y0 + dh * 0.32f, t * 0.62f); Dot(p, x + cw * 0.5f, y0 + dh * 0.68f, t * 0.62f); }
                    else Dot(p, x + cw * 0.5f, y0 + dh - t * 0.6f, t * 0.62f);
                    x += cw + gap * dw; continue;
                }
                if (ch == ' ') { x += dw * 0.3f + gap * dw; continue; }
                int mask = ch >= '0' && ch <= '9' ? Digits[ch - '0'] : (ch == '-' ? 0x40 : 0);
                for (int sgm = 0; sgm < 7; sgm++)
                {
                    p.fillColor = (mask & (1 << sgm)) != 0 ? on : ghost;
                    Segment(p, sgm, x, y0, dw, dh, t);
                }
                x += dw + gap * dw;
            }
        }

        static void Dot(Painter2D p, float cx, float cy, float r)
        {
            p.BeginPath(); p.MoveTo(new Vector2(cx - r, cy - r)); p.LineTo(new Vector2(cx + r, cy - r)); p.LineTo(new Vector2(cx + r, cy + r)); p.LineTo(new Vector2(cx - r, cy + r)); p.ClosePath(); p.Fill();
        }

        // eğik segment (italik LCD)
        static Vector2 K(float x, float y, float y0, float dh) => new Vector2(x + (y0 + dh - y) * 0.1f, y);

        static void Segment(Painter2D p, int s, float x, float y0, float w, float h, float t)
        {
            float g = t * 0.12f, ht = t * 0.5f;
            float l = x, r = x + w, top = y0, mid = y0 + h * 0.5f, bot = y0 + h;
            p.BeginPath();
            if (s == 0 || s == 3 || s == 6)
            {
                float cy = s == 0 ? top + ht : (s == 3 ? bot - ht : mid);
                p.MoveTo(K(l + g, cy, y0, h)); p.LineTo(K(l + ht + g, cy - ht, y0, h)); p.LineTo(K(r - ht - g, cy - ht, y0, h));
                p.LineTo(K(r - g, cy, y0, h)); p.LineTo(K(r - ht - g, cy + ht, y0, h)); p.LineTo(K(l + ht + g, cy + ht, y0, h));
            }
            else
            {
                bool right = s == 1 || s == 2; bool upper = s == 1 || s == 5;
                float cx = right ? r - ht : l + ht;
                float a = upper ? top + ht : mid, b = upper ? mid : bot - ht;
                p.MoveTo(K(cx, a + g, y0, h)); p.LineTo(K(cx + ht, a + ht + g, y0, h)); p.LineTo(K(cx + ht, b - ht - g, y0, h));
                p.LineTo(K(cx, b - g, y0, h)); p.LineTo(K(cx - ht, b - ht - g, y0, h)); p.LineTo(K(cx - ht, a + ht + g, y0, h));
            }
            p.ClosePath(); p.Fill();
        }
    }
}
