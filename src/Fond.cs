// Fonds decoratifs "coucher de soleil retro annees 80" (violet, magenta, orange, rose).
// Chaque decor est dessine une seule fois en 640x480, puis reutilise a chaque image.
// Styles : palmiers, ville, jetee, voiture, route, neon, aucun.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

static class Decor
{
    public static readonly string[] Styles = { "palmiers", "ville", "jetee", "voiture", "route", "neon", "aucun" };
    public static readonly string[] Noms = { "Palmiers", "Ville", "Jetée", "Voiture rétro", "Route côtière", "Néon", "Aucun (fond uni)" };

    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
    static readonly Color Ombre = Color.FromArgb(14, 6, 24);

    public static Bitmap Image(string style)
    {
        if (Array.IndexOf(Styles, style) < 0) style = "palmiers";
        Bitmap b;
        if (!cache.TryGetValue(style, out b)) { b = Dessiner(style, 640, 480); cache[style] = b; }
        return b;
    }

    static Bitmap Dessiner(string style, int W, int H)
    {
        var bmp = new Bitmap(W, H);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            switch (style)
            {
                case "ville": Ville(g, W, H); break;
                case "jetee": Jetee(g, W, H); break;
                case "voiture": Voiture(g, W, H); break;
                case "route": Route(g, W, H); break;
                case "neon": Neon(g, W, H); break;
                case "aucun": using (var b = new SolidBrush(Color.FromArgb(14, 10, 26))) g.FillRectangle(b, 0, 0, W, H); break;
                default: Palmiers(g, W, H); break;
            }
        }
        return bmp;
    }

    // ---------------------------------------------------------------- elements communs

    static void Ciel(Graphics g, int W, float horizon)
    {
        using (var b = new LinearGradientBrush(new RectangleF(0, 0, W, horizon + 1), Color.Black, Color.Black, LinearGradientMode.Vertical))
        {
            var cb = new ColorBlend();
            cb.Colors = new[] { Color.FromArgb(12, 8, 24), Color.FromArgb(28, 12, 52), Color.FromArgb(70, 20, 90), Color.FromArgb(150, 40, 120) };
            cb.Positions = new[] { 0f, 0.45f, 0.8f, 1f };
            b.InterpolationColors = cb;
            g.FillRectangle(b, 0, 0, W, horizon + 1);
        }
        var r = new Random(7);
        for (int i = 0; i < 60; i++)
        {
            float x = (float)r.NextDouble() * W, y = (float)r.NextDouble() * horizon * 0.6f;
            using (var b = new SolidBrush(Color.FromArgb(40 + r.Next(90), 255, 230, 255))) g.FillEllipse(b, x, y, 1.6f, 1.6f);
        }
    }

    // Soleil a bandes, coupe a l'horizon, avec halo
    static void Soleil(Graphics g, int W, float horizon, float sx, float sy, float sr)
    {
        var disque = new RectangleF(sx - sr, sy - sr, 2 * sr, 2 * sr);
        using (var p = new GraphicsPath())
        {
            p.AddEllipse(disque);
            var clip = new Region(p);
            for (int i = 0; i < 6; i++)
                clip.Exclude(new RectangleF(sx - sr, sy + 4 + i * sr * 0.133f, 2 * sr, 3 + i * 1.6f));
            clip.Intersect(new RectangleF(0, 0, W, horizon));
            g.SetClip(clip, CombineMode.Replace);
            using (var b = new LinearGradientBrush(disque, Color.FromArgb(255, 214, 120), Color.FromArgb(236, 72, 153), LinearGradientMode.Vertical))
                g.FillEllipse(b, disque);
            g.ResetClip();
            clip.Dispose();
        }
        using (var halo = new GraphicsPath())
        {
            halo.AddEllipse(sx - sr * 1.7f, sy - sr * 1.7f, sr * 3.4f, sr * 3.4f);
            using (var b = new PathGradientBrush(halo) { CenterColor = Color.FromArgb(70, 255, 120, 200), SurroundColors = new[] { Color.FromArgb(0, 255, 120, 200) } })
            {
                g.SetClip(new RectangleF(0, 0, W, horizon));
                g.FillPath(b, halo);
                g.ResetClip();
            }
        }
    }

    static void Mer(Graphics g, int W, int H, float horizon, float sx, float largeurReflet)
    {
        using (var b = new LinearGradientBrush(new RectangleF(0, horizon, W, H - horizon), Color.FromArgb(60, 14, 70), Color.FromArgb(14, 8, 26), LinearGradientMode.Vertical))
            g.FillRectangle(b, 0, horizon, W, H - horizon);
        for (int i = 0; i < 9; i++)
        {
            float y = horizon + 5 + i * 9, l = largeurReflet - i * largeurReflet / 12f;
            using (var pen = new Pen(Color.FromArgb(110 - i * 10, 255, 140, 200), 2))
                g.DrawLine(pen, sx - l / 2 + (i % 2) * 10, y, sx + l / 2 - (i % 2) * 6, y);
        }
        LigneHorizon(g, W, horizon);
    }

    static void LigneHorizon(Graphics g, int W, float horizon)
    {
        using (var pen = new Pen(Color.FromArgb(160, 255, 150, 210), 1.2f)) g.DrawLine(pen, 0, horizon, W, horizon);
    }

    // Ombre chinoise avec un leger contour lumineux rose (style "neon")
    static void Silhouette(Graphics g, Color c, PointF[] pts)
    {
        using (var halo = new Pen(Color.FromArgb(45, 236, 72, 153), 5) { LineJoin = LineJoin.Round }) g.DrawPolygon(halo, pts);
        using (var bord = new Pen(Color.FromArgb(120, 244, 114, 182), 1.4f) { LineJoin = LineJoin.Round }) g.DrawPolygon(bord, pts);
        using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
    }

    // ---------------------------------------------------------------- 1. Palmiers

    static void Palmiers(Graphics g, int W, int H)
    {
        float horizon = H * 0.80f, sx = W * 0.62f;
        Ciel(g, W, horizon);
        Soleil(g, W, horizon, sx, horizon - 40, 120);
        Mer(g, W, H, horizon, sx, 150);
        Palmier(g, Ombre, 70, H + 10, 360, 0.30f, 1.0f);
        Palmier(g, Ombre, 18, H + 10, 250, -0.20f, 0.75f);
        Palmier(g, Ombre, 600, H + 10, 300, -0.35f, 0.9f);
    }

    // Tronc courbe qui s'affine + couronne de palmes retombantes
    static void Palmier(Graphics g, Color c, float x, float y, float hauteur, float penche, float taille)
    {
        var gauche = new List<PointF>(); var droite = new List<PointF>();
        PointF sommet = PointF.Empty;
        for (int i = 0; i <= 20; i++)
        {
            float t = i / 20f;
            float px = x + penche * hauteur * t * t, py = y - hauteur * t;
            float largeur = (16 - 9 * t) * taille;
            gauche.Add(new PointF(px - largeur / 2, py)); droite.Add(new PointF(px + largeur / 2, py));
            if (i == 20) sommet = new PointF(px, py);
        }
        droite.Reverse(); gauche.AddRange(droite);
        Silhouette(g, c, gauche.ToArray());

        float[] angles = { 200, 225, 250, 290, 315, 340, 165, 15 };
        foreach (float ang in angles)
        {
            double a = ang * Math.PI / 180;
            float longueur = (110 + (ang % 50)) * taille;
            var dir = new PointF((float)Math.Cos(a), (float)Math.Sin(a));
            var p1 = new PointF(sommet.X + dir.X * longueur * 0.55f, sommet.Y + dir.Y * longueur * 0.55f - 28 * taille);
            var p2 = new PointF(sommet.X + dir.X * longueur, sommet.Y + dir.Y * longueur * 0.6f + 45 * taille);
            Palme(g, c, sommet, p1, p2, 13 * taille);
        }
        using (var b = new SolidBrush(c)) g.FillEllipse(b, sommet.X - 9 * taille, sommet.Y - 7 * taille, 18 * taille, 14 * taille);
    }

    // Feuille effilee le long d'une courbe, bord dentele (folioles)
    static void Palme(Graphics g, Color c, PointF p0, PointF p1, PointF p2, float largeurMax)
    {
        const int N = 24;
        var haut = new List<PointF>(); var bas = new List<PointF>();
        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N, u = 1 - t;
            var p = new PointF(u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X, u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y);
            var d = new PointF(2 * u * (p1.X - p0.X) + 2 * t * (p2.X - p1.X), 2 * u * (p1.Y - p0.Y) + 2 * t * (p2.Y - p1.Y));
            float n = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y); if (n < 0.001f) n = 1;
            var perp = new PointF(-d.Y / n, d.X / n);
            float w = largeurMax * (float)Math.Sin(Math.PI * Math.Min(1, t * 1.15f)) * (i % 2 == 0 ? 1f : 0.45f);
            haut.Add(new PointF(p.X + perp.X * w, p.Y + perp.Y * w));
            bas.Add(new PointF(p.X - perp.X * w * 0.8f, p.Y - perp.Y * w * 0.8f));
        }
        bas.Reverse(); haut.AddRange(bas);
        Silhouette(g, c, haut.ToArray());
    }

    // ---------------------------------------------------------------- 2. Ville

    static void Ville(Graphics g, int W, int H)
    {
        float horizon = H * 0.78f, sx = W * 0.5f;
        Ciel(g, W, horizon);
        Soleil(g, W, horizon, sx, horizon - 70, 130);

        var r = new Random(11);
        // deux rangees d'immeubles : lointaine (plus claire) puis proche (ombre)
        for (int rangee = 0; rangee < 2; rangee++)
        {
            Color c = rangee == 0 ? Color.FromArgb(45, 16, 62) : Ombre;
            float x = -10;
            while (x < W + 10)
            {
                float l = 28 + r.Next(36);
                bool devantSoleil = Math.Abs(x + l / 2 - sx) < 150;   // plus bas devant le soleil pour le laisser voir
                float h = rangee == 0 ? (devantSoleil ? 30 + r.Next(40) : 60 + r.Next(80))
                                      : (devantSoleil ? 15 + r.Next(35) : 30 + r.Next(110));
                var pts = new List<PointF> { new PointF(x, H), new PointF(x, horizon - h) };
                if (r.Next(4) == 0) { pts.Add(new PointF(x + l * 0.5f - 1.5f, horizon - h)); pts.Add(new PointF(x + l * 0.5f, horizon - h - 22)); pts.Add(new PointF(x + l * 0.5f + 1.5f, horizon - h)); } // antenne
                pts.Add(new PointF(x + l, horizon - h)); pts.Add(new PointF(x + l, H));
                if (rangee == 1) Silhouette(g, c, pts.ToArray());
                else using (var b = new SolidBrush(c)) g.FillPolygon(b, pts.ToArray());
                // fenetres allumees (orange / rose), surtout sur la rangee proche
                if (rangee == 1)
                    for (float wy = horizon - h + 8; wy < horizon + 20; wy += 9)
                        for (float wx = x + 5; wx < x + l - 6; wx += 7)
                            if (r.Next(10) == 0)
                                using (var b = new SolidBrush(r.Next(2) == 0 ? Color.FromArgb(110, 255, 180, 90) : Color.FromArgb(100, 244, 114, 182)))
                                    g.FillRectangle(b, wx, wy, 3, 4);
                x += l + (rangee == 0 ? -6 : 2);
            }
        }
    }

    // ---------------------------------------------------------------- 4. Jetee

    static void Jetee(Graphics g, int W, int H)
    {
        float horizon = H * 0.70f, sx = W * 0.34f;
        Ciel(g, W, horizon);
        Soleil(g, W, horizon, sx, horizon - 25, 105);
        Mer(g, W, H, horizon, sx, 140);

        // jetee en perspective : part du bas a droite vers l'horizon
        var pont = new[] { new PointF(W + 40, H - 30), new PointF(W * 0.55f, horizon + 14), new PointF(W * 0.55f, horizon + 18), new PointF(W + 40, H + 10) };
        Silhouette(g, Ombre, pont);
        // poteaux + lampadaires
        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f, k = 1 - t * 0.8f;
            float x = W + 30 - (W * 0.45f + 30) * t, y = (H - 30) - (H - 30 - horizon - 14) * t;
            using (var b = new SolidBrush(Ombre)) g.FillRectangle(b, x, y, 4 * k, 40 * k);       // pilier sous le pont
            if (i % 2 == 0)
            {
                float hl = 70 * k;
                Silhouette(g, Ombre, new[] { new PointF(x, y), new PointF(x, y - hl), new PointF(x + 2.5f * k, y - hl), new PointF(x + 2.5f * k, y) });
                using (var b = new SolidBrush(Color.FromArgb(200, 255, 200, 120))) g.FillEllipse(b, x - 3 * k, y - hl - 4 * k, 8 * k, 8 * k);
                using (var b = new SolidBrush(Color.FromArgb(50, 255, 200, 120))) g.FillEllipse(b, x - 10 * k, y - hl - 11 * k, 22 * k, 22 * k);
            }
        }
        // petit palmier a gauche et quelques oiseaux
        Palmier(g, Ombre, 40, H + 10, 230, 0.25f, 0.7f);
        using (var pen = new Pen(Color.FromArgb(150, 20, 8, 30), 1.6f))
            foreach (var o in new[] { new PointF(300, 120), new PointF(325, 105), new PointF(350, 128), new PointF(470, 90) })
                g.DrawLines(pen, new[] { new PointF(o.X - 7, o.Y - 3), new PointF(o.X, o.Y + 2), new PointF(o.X + 7, o.Y - 3) });
    }

    // ---------------------------------------------------------------- 4. Voiture retro

    static void Voiture(Graphics g, int W, int H)
    {
        float horizon = H * 0.62f, sx = W * 0.55f;
        Ciel(g, W, horizon);
        Soleil(g, W, horizon, sx, horizon - 20, 110);
        Mer(g, W, H, horizon, sx, 130);

        // muret + route vue de cote
        float yRoute = H * 0.80f;
        using (var b = new SolidBrush(Color.FromArgb(22, 9, 34))) g.FillRectangle(b, 0, yRoute - 6, W, H - yRoute + 6);
        using (var pen = new Pen(Color.FromArgb(150, 244, 114, 182), 1.4f)) g.DrawLine(pen, 0, yRoute - 6, W, yRoute - 6);
        using (var pen = new Pen(Color.FromArgb(150, 255, 190, 100), 3) { DashPattern = new[] { 6f, 5f } }) g.DrawLine(pen, 0, yRoute + 30, W, yRoute + 30);

        // petits palmiers au loin
        Palmier(g, Ombre, 560, yRoute - 4, 130, -0.15f, 0.42f);
        Palmier(g, Ombre, 600, yRoute - 4, 100, 0.10f, 0.34f);

        // coupe sportif generique (vue de cote, vers la droite)
        float x0 = 90, y0 = yRoute + 18, k = 1.25f;
        Func<float, float, PointF> P = (x, y) => new PointF(x0 + x * k, y0 - y * k);
        var carrosserie = new[]
        {
            P(0, 6), P(0, 24), P(6, 27), P(40, 29), P(78, 46), P(132, 47), P(168, 31), P(232, 25), P(244, 19), P(244, 8), P(238, 4),
            P(212, 4), P(208, 14), P(198, 20), P(184, 20), P(174, 14), P(170, 4),   // passage de roue avant
            P(72, 4), P(68, 14), P(58, 20), P(44, 20), P(34, 14), P(30, 4), P(4, 4)  // passage de roue arriere
        };
        Silhouette(g, Ombre, carrosserie);
        // vitres : on voit le coucher de soleil a travers
        using (var b = new LinearGradientBrush(new RectangleF(P(80, 45).X, P(80, 45).Y, 90 * k, 16 * k), Color.FromArgb(150, 120, 40, 120), Color.FromArgb(150, 236, 110, 150), LinearGradientMode.Vertical))
            g.FillPolygon(b, new[] { P(84, 43), P(130, 44), P(158, 32), P(84, 31) });
        // roues
        foreach (float cx in new[] { 51f, 191f })
        {
            var c = P(cx, 4);
            float rr = 13 * k;
            using (var b = new SolidBrush(Color.FromArgb(8, 4, 14))) g.FillEllipse(b, c.X - rr, c.Y - rr, 2 * rr, 2 * rr);
            using (var pen = new Pen(Color.FromArgb(130, 244, 114, 182), 1.6f)) g.DrawEllipse(pen, c.X - rr * 0.6f, c.Y - rr * 0.6f, rr * 1.2f, rr * 1.2f);
        }
        // feux arriere qui brillent + ligne neon le long de la caisse
        var feu = P(0, 22);
        using (var b = new SolidBrush(Color.FromArgb(60, 255, 60, 120))) g.FillEllipse(b, feu.X - 18, feu.Y - 10, 30, 20);
        using (var b = new SolidBrush(Color.FromArgb(230, 255, 70, 130))) g.FillRectangle(b, feu.X - 1, feu.Y - 2, 9, 5);
        using (var pen = new Pen(Color.FromArgb(160, 236, 72, 153), 1.5f)) g.DrawLine(pen, P(6, 17), P(236, 13));
    }

    // ---------------------------------------------------------------- 5. Route cotiere

    static void Route(Graphics g, int W, int H)
    {
        float horizon = H * 0.62f, sx = W * 0.60f, vx = W * 0.40f;   // vx : point de fuite de la route
        Ciel(g, W, horizon);
        Soleil(g, W, horizon, sx, horizon - 15, 100);
        Mer(g, W, H, horizon, sx, 120);

        // terre a gauche de la route
        Silhouette(g, Color.FromArgb(22, 9, 34), new[] { new PointF(-10, horizon), new PointF(vx - 4, horizon), new PointF(W * 0.10f, H + 10), new PointF(-10, H + 10) });
        // route en perspective
        var route = new[] { new PointF(vx - 3, horizon), new PointF(vx + 3, horizon), new PointF(W * 0.78f, H + 10), new PointF(W * 0.10f, H + 10) };
        using (var b = new LinearGradientBrush(new RectangleF(0, horizon, W, H - horizon), Color.FromArgb(40, 14, 56), Color.FromArgb(18, 8, 30), LinearGradientMode.Vertical))
            g.FillPolygon(b, route);
        using (var pen = new Pen(Color.FromArgb(170, 244, 114, 182), 1.6f))
        {
            g.DrawLine(pen, route[0], route[3]);
            g.DrawLine(pen, route[1], route[2]);
        }
        // pointilles centraux qui grossissent en approchant
        for (int i = 0; i < 9; i++)
        {
            float t0 = (float)Math.Pow(i / 9f, 1.8), t1 = (float)Math.Pow((i + 0.5f) / 9f, 1.8);
            float cxb = W * 0.44f;
            var a = new PointF(vx + (cxb - vx) * t0, horizon + (H + 10 - horizon) * t0);
            var b2 = new PointF(vx + (cxb - vx) * t1, horizon + (H + 10 - horizon) * t1);
            using (var pen = new Pen(Color.FromArgb(200, 255, 190, 100), 1 + 6 * t1)) g.DrawLine(pen, a, b2);
        }
        // palmiers le long de la route, de plus en plus petits vers l'horizon
        foreach (float t in new[] { 0.12f, 0.28f, 0.55f, 1f })
        {
            // le long du bord gauche de la route, un peu en retrait
            float y = horizon + (H + 10 - horizon) * t;
            float bord = vx - 3 + (W * 0.10f - vx + 3) * t;
            float x = bord - 18 - 40 * t;
            Palmier(g, Ombre, x, y, 30 + 230 * t, 0.12f, 0.2f + 0.6f * t);
        }
    }

    // ---------------------------------------------------------------- 6. Neon

    // Tube neon : plusieurs traits superposes, du halo large et transparent au coeur presque blanc
    static void Tube(Graphics g, GraphicsPath p, Color c)
    {
        int[] largeurs = { 16, 9, 5 };
        int[] alphas = { 22, 55, 140 };
        for (int i = 0; i < 3; i++)
            using (var pen = new Pen(Color.FromArgb(alphas[i], c), largeurs[i]) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawPath(pen, p);
        using (var pen = new Pen(Color.FromArgb(235, 255, 235, 245), 1.8f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawPath(pen, p);
    }

    static void Neon(Graphics g, int W, int H)
    {
        float sol = H * 0.74f;
        // mur sombre violet, leger degrade
        using (var b = new LinearGradientBrush(new RectangleF(0, 0, W, sol + 1), Color.FromArgb(16, 8, 30), Color.FromArgb(34, 12, 50), LinearGradientMode.Vertical))
            g.FillRectangle(b, 0, 0, W, sol + 1);
        var rose = Color.FromArgb(244, 80, 170);
        var orange = Color.FromArgb(255, 150, 70);
        var violet = Color.FromArgb(180, 110, 255);

        // enseigne 1 : soleil a bandes (cercle + 3 traits)
        var soleil = new GraphicsPath();
        float cx = W * 0.64f, cy = H * 0.36f, r = 88;
        soleil.AddArc(cx - r, cy - r, 2 * r, 2 * r, 180, 180);
        soleil.StartFigure(); soleil.AddLine(cx - r, cy, cx + r, cy);
        for (int i = 1; i <= 3; i++)
        {
            float y = cy + i * 16, d = (float)Math.Sqrt(Math.Max(0, r * r - (i * 16) * (i * 16)));
            soleil.StartFigure(); soleil.AddLine(cx - d, y, cx + d, y);
        }
        Tube(g, soleil, orange);

        // enseigne 2 : palmier (tronc courbe + palmes en arcs)
        var palmier = new GraphicsPath();
        float px = W * 0.22f, pyb = H * 0.66f, pyh = H * 0.20f;
        palmier.AddBezier(px, pyb, px + 6, pyb - 80, px + 22, pyh + 60, px + 34, pyh);
        foreach (var f in new[] { new[] { -70f, 20f }, new[] { -55f, -18f }, new[] { 15f, -30f }, new[] { 65f, -10f }, new[] { 80f, 28f } })
        {
            palmier.StartFigure();
            palmier.AddBezier(px + 34, pyh, px + 34 + f[0] * 0.4f, pyh + f[1] - 22, px + 34 + f[0] * 0.8f, pyh + f[1] - 8, px + 34 + f[0], pyh + f[1] + 22);
        }
        Tube(g, palmier, rose);

        // cadre neon en haut et petites etoiles
        var cadre = new GraphicsPath();
        cadre.AddLine(W * 0.06f, H * 0.08f, W * 0.94f, H * 0.08f);
        Tube(g, cadre, violet);
        foreach (var e in new[] { new PointF(W * 0.42f, H * 0.2f), new PointF(W * 0.88f, H * 0.56f), new PointF(W * 0.08f, H * 0.42f) })
        {
            var et = new GraphicsPath();
            et.AddLine(e.X - 7, e.Y, e.X + 7, e.Y); et.StartFigure(); et.AddLine(e.X, e.Y - 7, e.X, e.Y + 7);
            Tube(g, et, rose);
        }

        // rue mouillee : reflets des neons etires et brouilles
        using (var b = new LinearGradientBrush(new RectangleF(0, sol, W, H - sol), Color.FromArgb(28, 10, 42), Color.FromArgb(10, 5, 18), LinearGradientMode.Vertical))
            g.FillRectangle(b, 0, sol, W, H - sol);
        using (var pen = new Pen(Color.FromArgb(150, 244, 114, 182), 1.4f)) g.DrawLine(pen, 0, sol, W, sol);
        var rr = new Random(5);
        foreach (var refl in new[] { new { X = cx, L = 2 * r, C = orange }, new { X = px + 20, L = 90f, C = rose } })
            for (int i = 0; i < 9; i++)
            {
                float y = sol + 6 + i * 11, l = refl.L * (1 - i * 0.08f) * (0.7f + (float)rr.NextDouble() * 0.3f);
                using (var pen = new Pen(Color.FromArgb(90 - i * 9, refl.C), 3))
                    g.DrawLine(pen, refl.X - l / 2, y, refl.X + l / 2, y);
            }
    }
}
