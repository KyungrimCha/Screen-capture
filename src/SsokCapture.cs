// ==========================================================
// 쏙캡처 (SsokCapture) - 개인용 화면 캡처 + 주석 도구
// 영역/전체 캡처, 선택/이동, 박스, 원, 화살표, 말풍선(인라인 편집), 복사, 저장
// 아이콘 : Phosphor Icons (MIT) - https://phosphoricons.com
// 색상   : Adobe Color 테마 (소울곰 지정)
// 빌드   : 빌드.bat 실행 (윈도우 내장 csc.exe 사용, 설치 불필요)
// ==========================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("쏙캡처")]
[assembly: System.Reflection.AssemblyProduct("SsokCapture")]
[assembly: System.Reflection.AssemblyVersion(SsokCapture.App.Version + ".0")]
[assembly: System.Reflection.AssemblyFileVersion(SsokCapture.App.Version + ".0")]

namespace SsokCapture
{
    // ---------------- 버전 ----------------
    // 버전은 여기 한 곳만 고치면 된다. 고치면 변경기록.md 에도 한 줄 남기기.
    public static class App
    {
        public const string Version = "1.3.0";
    }

    // ---------------- 테마 ----------------

    public static class Theme
    {
        public static float Scale = 1f;

        public static readonly Color Bar = Color.White;
        public static readonly Color Line = Color.FromArgb(228, 231, 236);
        public static readonly Color Ink = Color.FromArgb(34, 38, 46);
        public static readonly Color Muted = Color.FromArgb(128, 135, 147);
        public static readonly Color Accent = Color.FromArgb(0, 110, 207);      // #006ECF
        public static readonly Color AccentDark = Color.FromArgb(0, 76, 148);   // #004C94
        public static readonly Color AccentSoft = Color.FromArgb(230, 240, 251);
        public static readonly Color Hover = Color.FromArgb(240, 242, 246);
        public static readonly Color Press = Color.FromArgb(228, 232, 238);
        public static readonly Color Board = Color.FromArgb(217, 219, 224);     // 위니브 W-Gray-Lv2 #D9DBE0

        // 주석 색상 - Adobe Color 테마 15색 + 흰색
        public static readonly Color[] Palette = new Color[] {
            Color.FromArgb(0, 76, 148),     Color.FromArgb(0, 110, 207),   Color.FromArgb(60, 147, 250),
            Color.FromArgb(0, 212, 198),    Color.FromArgb(15, 166, 157),  Color.FromArgb(2, 129, 131),
            Color.FromArgb(29, 189, 142),   Color.FromArgb(115, 204, 128),
            Color.FromArgb(242, 79, 79),    Color.FromArgb(255, 143, 92),  Color.FromArgb(238, 183, 43),
            Color.FromArgb(255, 87, 176),   Color.FromArgb(145, 81, 184),  Color.FromArgb(45, 63, 84),
            Color.FromArgb(82, 100, 122),   Color.Black,                   Color.White
        };
        public const int PaletteColumns = 9;      // 팔레트 16색 + 아웃라인 스와치 1칸 = 9 x 2 줄
        public const int DefaultColorIndex = 8;   // #F24F4F

        public static string UiFamily;
        public static string BubbleFamily;

        public static Font Ui;
        public static Font Overlay;

        private static readonly Dictionary<int, Font> bubbleFonts = new Dictionary<int, Font>();

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        // 주석(선 굵기, 글자, 모자이크)은 캡처 이미지의 픽셀 단위다.
        // 고배율 화면에서는 캡처 이미지도 그만큼 조밀하므로 같은 비율로 키워야 눈에 보이는 크기가 같아진다.
        public static float A(float v) { return v * Scale; }

        // 말풍선 글꼴은 픽셀 단위 - 화면 미리보기와 저장 결과가 같아진다
        public static Font Bubble(float px)
        {
            int key = (int)Math.Round(px);
            if (key < 8) key = 8;
            Font f;
            if (!bubbleFonts.TryGetValue(key, out f))
            {
                f = new Font(BubbleFamily, key, FontStyle.Regular, GraphicsUnit.Pixel);
                bubbleFonts[key] = f;
            }
            return f;
        }

        private static string Resolve(string[] names)
        {
            foreach (string n in names)
            {
                try
                {
                    using (FontFamily ff = new FontFamily(n))
                        if (string.Equals(ff.Name, n, StringComparison.OrdinalIgnoreCase)) return n;
                }
                catch { }
            }
            return FontFamily.GenericSansSerif.Name;
        }

        public static void Init()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                Scale = g.DpiX / 96f;

            UiFamily = Resolve(new string[] { "Pretendard", "Malgun Gothic" });
            BubbleFamily = Resolve(new string[] { "Pretendard SemiBold", "Pretendard", "Malgun Gothic" });

            Ui = new Font(UiFamily, 10.5f);
            Overlay = new Font(UiFamily, 17f * Scale, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            if (d <= 0 || d > r.Width || d > r.Height) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------------- SVG 패스 파서 (Phosphor 아이콘용) ----------------

    public static class SvgPath
    {
        public static GraphicsPath Parse(string d)
        {
            GraphicsPath gp = new GraphicsPath(FillMode.Winding);
            int i = 0;
            char cmd = ' ';
            PointF cur = PointF.Empty, start = PointF.Empty, lastCtrl = PointF.Empty;

            while (true)
            {
                Skip(d, ref i);
                if (i >= d.Length) break;
                if (char.IsLetter(d[i])) { cmd = d[i]; i++; Skip(d, ref i); }
                if (cmd == 'Z' || cmd == 'z') { gp.CloseFigure(); cur = start; continue; }
                if (i >= d.Length) break;

                bool rel = char.IsLower(cmd);
                char c = char.ToUpper(cmd);
                PointF p;

                switch (c)
                {
                    case 'M':
                        p = ReadPoint(d, ref i, rel, cur);
                        gp.StartFigure();
                        cur = p; start = p; lastCtrl = p;
                        cmd = rel ? 'l' : 'L';     // 이어지는 좌표쌍은 L 로 해석
                        break;

                    case 'L':
                        p = ReadPoint(d, ref i, rel, cur);
                        if (p != cur) gp.AddLine(cur, p);
                        cur = p; lastCtrl = p;
                        break;

                    case 'H':
                        p = new PointF(Num(d, ref i) + (rel ? cur.X : 0f), cur.Y);
                        if (p != cur) gp.AddLine(cur, p);
                        cur = p; lastCtrl = p;
                        break;

                    case 'V':
                        p = new PointF(cur.X, Num(d, ref i) + (rel ? cur.Y : 0f));
                        if (p != cur) gp.AddLine(cur, p);
                        cur = p; lastCtrl = p;
                        break;

                    case 'C':
                        {
                            PointF c1 = ReadPoint(d, ref i, rel, cur);
                            PointF c2 = ReadPoint(d, ref i, rel, cur);
                            p = ReadPoint(d, ref i, rel, cur);
                            gp.AddBezier(cur, c1, c2, p);
                            lastCtrl = c2; cur = p;
                        }
                        break;

                    case 'S':
                        {
                            PointF c1 = new PointF(2 * cur.X - lastCtrl.X, 2 * cur.Y - lastCtrl.Y);
                            PointF c2 = ReadPoint(d, ref i, rel, cur);
                            p = ReadPoint(d, ref i, rel, cur);
                            gp.AddBezier(cur, c1, c2, p);
                            lastCtrl = c2; cur = p;
                        }
                        break;

                    case 'A':
                        {
                            float rx = Num(d, ref i), ry = Num(d, ref i), rot = Num(d, ref i);
                            bool large = Num(d, ref i) != 0f;
                            bool sweep = Num(d, ref i) != 0f;
                            p = ReadPoint(d, ref i, rel, cur);
                            Arc(gp, cur, rx, ry, rot, large, sweep, p);
                            cur = p; lastCtrl = p;
                        }
                        break;

                    default:
                        return gp;   // 지원하지 않는 명령이면 중단
                }
            }
            return gp;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ',' || s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
        }

        private static float Num(string s, ref int i)
        {
            Skip(s, ref i);
            int st = i;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
            bool dot = false;
            while (i < s.Length)
            {
                if (char.IsDigit(s[i])) i++;
                else if (s[i] == '.' && !dot) { dot = true; i++; }
                else break;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            if (i == st) { i++; return 0f; }
            return float.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        private static PointF ReadPoint(string s, ref int i, bool rel, PointF cur)
        {
            float x = Num(s, ref i), y = Num(s, ref i);
            return rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
        }

        // 타원 호를 베지어로 변환 (SVG 사양의 endpoint -> center 매개변수화)
        private static void Arc(GraphicsPath gp, PointF p0, float rx, float ry, float rotDeg,
                                bool large, bool sweep, PointF p1)
        {
            if (rx == 0f || ry == 0f) { if (p0 != p1) gp.AddLine(p0, p1); return; }
            rx = Math.Abs(rx); ry = Math.Abs(ry);

            double phi = rotDeg * Math.PI / 180.0;
            double cosP = Math.Cos(phi), sinP = Math.Sin(phi);

            double dx2 = (p0.X - p1.X) / 2.0, dy2 = (p0.Y - p1.Y) / 2.0;
            double x1p = cosP * dx2 + sinP * dy2;
            double y1p = -sinP * dx2 + cosP * dy2;

            double rxs = (double)rx * rx, rys = (double)ry * ry;
            double x1ps = x1p * x1p, y1ps = y1p * y1p;
            double lambda = x1ps / rxs + y1ps / rys;
            if (lambda > 1.0)
            {
                double k = Math.Sqrt(lambda);
                rx = (float)(rx * k); ry = (float)(ry * k);
                rxs = (double)rx * rx; rys = (double)ry * ry;
            }

            double denom = rxs * y1ps + rys * x1ps;
            double numer = rxs * rys - rxs * y1ps - rys * x1ps;
            if (numer < 0) numer = 0;
            double co = (denom <= 0) ? 0 : Math.Sqrt(numer / denom);
            if (large == sweep) co = -co;

            double cxp = co * rx * y1p / ry;
            double cyp = -co * ry * x1p / rx;
            double cx = cosP * cxp - sinP * cyp + (p0.X + p1.X) / 2.0;
            double cy = sinP * cxp + cosP * cyp + (p0.Y + p1.Y) / 2.0;

            double ux = (x1p - cxp) / rx, uy = (y1p - cyp) / ry;
            double vx = (-x1p - cxp) / rx, vy = (-y1p - cyp) / ry;

            double theta = Angle(1, 0, ux, uy);
            double delta = Angle(ux, uy, vx, vy);
            if (!sweep && delta > 0) delta -= 2 * Math.PI;
            else if (sweep && delta < 0) delta += 2 * Math.PI;

            int segs = (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2.0));
            if (segs < 1) segs = 1;
            double step = delta / segs;
            double k4 = 4.0 / 3.0 * Math.Tan(step / 4.0);

            PointF from = p0;
            for (int s = 0; s < segs; s++)
            {
                double t1 = theta + s * step, t2 = t1 + step;
                PointF e2 = OnArc(cx, cy, rx, ry, cosP, sinP, t2);
                PointF d1 = ArcDir(rx, ry, cosP, sinP, t1);
                PointF d2 = ArcDir(rx, ry, cosP, sinP, t2);
                PointF c1 = new PointF((float)(from.X + k4 * d1.X), (float)(from.Y + k4 * d1.Y));
                PointF c2 = new PointF((float)(e2.X - k4 * d2.X), (float)(e2.Y - k4 * d2.Y));
                gp.AddBezier(from, c1, c2, e2);
                from = e2;
            }
        }

        private static PointF OnArc(double cx, double cy, float rx, float ry, double cosP, double sinP, double t)
        {
            double ct = Math.Cos(t), st = Math.Sin(t);
            return new PointF((float)(cx + rx * ct * cosP - ry * st * sinP),
                              (float)(cy + rx * ct * sinP + ry * st * cosP));
        }

        private static PointF ArcDir(float rx, float ry, double cosP, double sinP, double t)
        {
            double ct = Math.Cos(t), st = Math.Sin(t);
            return new PointF((float)(-rx * st * cosP - ry * ct * sinP),
                              (float)(-rx * st * sinP + ry * ct * cosP));
        }

        private static double Angle(double ux, double uy, double vx, double vy)
        {
            double len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
            if (len <= 0) return 0;
            double cosA = (ux * vx + uy * vy) / len;
            if (cosA > 1) cosA = 1; else if (cosA < -1) cosA = -1;
            double a = Math.Acos(cosA);
            return (ux * vy - uy * vx < 0) ? -a : a;
        }
    }

    // ---------------- Phosphor 아이콘 ----------------

    public static class Icons
    {
        // phosphor-icons/core, regular weight, 256x256 viewBox
        private static readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        private static readonly Dictionary<string, GraphicsPath> cache = new Dictionary<string, GraphicsPath>();

        static Icons()
        {
            Data["cursor"] = "M168,132.69,214.08,115l.33-.13A16,16,0,0,0,213,85.07L52.92,32.8A15.95,15.95,0,0,0,32.8,52.92L85.07,213a15.82,15.82,0,0,0,14.41,11l.78,0a15.84,15.84,0,0,0,14.61-9.59l.13-.33L132.69,168,184,219.31a16,16,0,0,0,22.63,0l12.68-12.68a16,16,0,0,0,0-22.63ZM195.31,208,144,156.69a16,16,0,0,0-26,4.93c0,.11-.09.22-.13.32l-17.65,46L48,48l159.85,52.2-45.95,17.64-.32.13a16,16,0,0,0-4.93,26h0L208,195.31Z";
            Data["selection"] = "M152,40a8,8,0,0,1-8,8H112a8,8,0,0,1,0-16h32A8,8,0,0,1,152,40Zm-8,168H112a8,8,0,0,0,0,16h32a8,8,0,0,0,0-16ZM208,32H184a8,8,0,0,0,0,16h24V72a8,8,0,0,0,16,0V48A16,16,0,0,0,208,32Zm8,72a8,8,0,0,0-8,8v32a8,8,0,0,0,16,0V112A8,8,0,0,0,216,104Zm0,72a8,8,0,0,0-8,8v24H184a8,8,0,0,0,0,16h24a16,16,0,0,0,16-16V184A8,8,0,0,0,216,176ZM40,152a8,8,0,0,0,8-8V112a8,8,0,0,0-16,0v32A8,8,0,0,0,40,152Zm32,56H48V184a8,8,0,0,0-16,0v24a16,16,0,0,0,16,16H72a8,8,0,0,0,0-16ZM72,32H48A16,16,0,0,0,32,48V72a8,8,0,0,0,16,0V48H72a8,8,0,0,0,0-16Z";
            Data["frame-corners"] = "M200,80v32a8,8,0,0,1-16,0V88H160a8,8,0,0,1,0-16h32A8,8,0,0,1,200,80ZM96,168H72V144a8,8,0,0,0-16,0v32a8,8,0,0,0,8,8H96a8,8,0,0,0,0-16ZM232,56V200a16,16,0,0,1-16,16H40a16,16,0,0,1-16-16V56A16,16,0,0,1,40,40H216A16,16,0,0,1,232,56ZM216,200V56H40V200H216Z";
            Data["square"] = "M208,32H48A16,16,0,0,0,32,48V208a16,16,0,0,0,16,16H208a16,16,0,0,0,16-16V48A16,16,0,0,0,208,32Zm0,176H48V48H208V208Z";
            Data["circle"] = "M128,24A104,104,0,1,0,232,128,104.11,104.11,0,0,0,128,24Zm0,192a88,88,0,1,1,88-88A88.1,88.1,0,0,1,128,216Z";
            Data["arrow-up-right"] = "M200,64V168a8,8,0,0,1-16,0V83.31L69.66,197.66a8,8,0,0,1-11.32-11.32L172.69,72H88a8,8,0,0,1,0-16H192A8,8,0,0,1,200,64Z";
            Data["tree-structure"] = "M160,112h48a16,16,0,0,0,16-16V48a16,16,0,0,0-16-16H160a16,16,0,0,0-16,16V64H128a24,24,0,0,0-24,24v32H72v-8A16,16,0,0,0,56,96H24A16,16,0,0,0,8,112v32a16,16,0,0,0,16,16H56a16,16,0,0,0,16-16v-8h32v32a24,24,0,0,0,24,24h16v16a16,16,0,0,0,16,16h48a16,16,0,0,0,16-16V160a16,16,0,0,0-16-16H160a16,16,0,0,0-16,16v16H128a8,8,0,0,1-8-8V88a8,8,0,0,1,8-8h16V96A16,16,0,0,0,160,112ZM56,144H24V112H56v32Zm104,16h48v48H160Zm0-112h48V96H160Z";
            Data["flow-arrow"] ="M245.66,74.34l-32-32a8,8,0,0,0-11.32,11.32L220.69,72H208c-49.33,0-61.05,28.12-71.38,52.92-9.38,22.51-16.92,40.59-49.48,42.84a40,40,0,1,0,.1,16c43.26-2.65,54.34-29.15,64.14-52.69C161.41,107,169.33,88,208,88h12.69l-18.35,18.34a8,8,0,0,0,11.32,11.32l32-32A8,8,0,0,0,245.66,74.34ZM48,200a24,24,0,1,1,24-24A24,24,0,0,1,48,200Z";
            Data["arrow-up-right-fill"] ="M200,64V168a8,8,0,0,1-13.66,5.66L140,127.31,69.66,197.66a8,8,0,0,1-11.32-11.32L128.69,116,82.34,69.66A8,8,0,0,1,88,56H192A8,8,0,0,1,200,64Z";
            Data["chat-teardrop"] ="M132,24A100.11,100.11,0,0,0,32,124v84a16,16,0,0,0,16,16h84a100,100,0,0,0,0-200Zm0,184H48V124a84,84,0,1,1,84,84Z";
            Data["arrow-counter-clockwise"] = "M224,128a96,96,0,0,1-94.71,96H128A95.38,95.38,0,0,1,62.1,197.8a8,8,0,0,1,11-11.63A80,80,0,1,0,71.43,71.39a3.07,3.07,0,0,1-.26.25L44.59,96H72a8,8,0,0,1,0,16H24a8,8,0,0,1-8-8V56a8,8,0,0,1,16,0V85.8L60.25,60A96,96,0,0,1,224,128Z";
            Data["trash"] = "M216,48H176V40a24,24,0,0,0-24-24H104A24,24,0,0,0,80,40v8H40a8,8,0,0,0,0,16h8V208a16,16,0,0,0,16,16H192a16,16,0,0,0,16-16V64h8a8,8,0,0,0,0-16ZM96,40a8,8,0,0,1,8-8h48a8,8,0,0,1,8,8v8H96Zm96,168H64V64H192ZM112,104v64a8,8,0,0,1-16,0V104a8,8,0,0,1,16,0Zm48,0v64a8,8,0,0,1-16,0V104a8,8,0,0,1,16,0Z";
            Data["copy"] = "M216,32H88a8,8,0,0,0-8,8V80H40a8,8,0,0,0-8,8V216a8,8,0,0,0,8,8H168a8,8,0,0,0,8-8V176h40a8,8,0,0,0,8-8V40A8,8,0,0,0,216,32ZM160,208H48V96H160Zm48-48H176V88a8,8,0,0,0-8-8H96V48H208Z";
            Data["download-simple"] = "M224,144v64a8,8,0,0,1-8,8H40a8,8,0,0,1-8-8V144a8,8,0,0,1,16,0v56H208V144a8,8,0,0,1,16,0Zm-101.66,5.66a8,8,0,0,0,11.32,0l40-40a8,8,0,0,0-11.32-11.32L136,124.69V32a8,8,0,0,0-16,0v92.69L93.66,98.34a8,8,0,0,0-11.32,11.32Z";
            Data["grid-four"] = "M200,40H56A16,16,0,0,0,40,56V200a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V56A16,16,0,0,0,200,40Zm0,80H136V56h64ZM120,56v64H56V56ZM56,136h64v64H56Zm144,64H136V136h64v64Z";
            Data["text-align-left"] = "M32,64a8,8,0,0,1,8-8H216a8,8,0,0,1,0,16H40A8,8,0,0,1,32,64Zm8,48H168a8,8,0,0,0,0-16H40a8,8,0,0,0,0,16Zm176,24H40a8,8,0,0,0,0,16H216a8,8,0,0,0,0-16Zm-48,40H40a8,8,0,0,0,0,16H168a8,8,0,0,0,0-16Z";
            Data["text-align-center"] = "M32,64a8,8,0,0,1,8-8H216a8,8,0,0,1,0,16H40A8,8,0,0,1,32,64ZM64,96a8,8,0,0,0,0,16H192a8,8,0,0,0,0-16Zm152,40H40a8,8,0,0,0,0,16H216a8,8,0,0,0,0-16Zm-24,40H64a8,8,0,0,0,0,16H192a8,8,0,0,0,0-16Z";
            Data["text-align-right"] = "M32,64a8,8,0,0,1,8-8H216a8,8,0,0,1,0,16H40A8,8,0,0,1,32,64ZM216,96H88a8,8,0,0,0,0,16H216a8,8,0,0,0,0-16Zm0,40H40a8,8,0,0,0,0,16H216a8,8,0,0,0,0-16Zm0,40H88a8,8,0,0,0,0,16H216a8,8,0,0,0,0-16Z";
            Data["crop"] ="M240,192a8,8,0,0,1-8,8H200v32a8,8,0,0,1-16,0V200H64a8,8,0,0,1-8-8V72H24a8,8,0,0,1,0-16H56V24a8,8,0,0,1,16,0V184H232A8,8,0,0,1,240,192ZM96,72h88v88a8,8,0,0,0,16,0V64a8,8,0,0,0-8-8H96a8,8,0,0,0,0,16Z";
            Data["text-t"] ="M208,56V88a8,8,0,0,1-16,0V64H136V192h24a8,8,0,0,1,0,16H96a8,8,0,0,1,0-16h24V64H64V88a8,8,0,0,1-16,0V56a8,8,0,0,1,8-8H200A8,8,0,0,1,208,56Z";
            Data["textbox"] = "M112,40a8,8,0,0,0-8,8V64H24A16,16,0,0,0,8,80v96a16,16,0,0,0,16,16h80v16a8,8,0,0,0,16,0V48A8,8,0,0,0,112,40ZM24,176V80h80v96ZM248,80v96a16,16,0,0,1-16,16H144a8,8,0,0,1,0-16h88V80H144a8,8,0,0,1,0-16h88A16,16,0,0,1,248,80ZM88,112a8,8,0,0,1-8,8H72v24a8,8,0,0,1-16,0V120H48a8,8,0,0,1,0-16H80A8,8,0,0,1,88,112Z";
            Data["number-circle-one"] ="M128,24A104,104,0,1,0,232,128,104.11,104.11,0,0,0,128,24Zm0,192a88,88,0,1,1,88-88A88.1,88.1,0,0,1,128,216ZM140,80v96a8,8,0,0,1-16,0V95l-11.56,7.71a8,8,0,1,1-8.88-13.32l24-16A8,8,0,0,1,140,80Z";
            Data["magnifying-glass-plus"] ="M152,112a8,8,0,0,1-8,8H120v24a8,8,0,0,1-16,0V120H80a8,8,0,0,1,0-16h24V80a8,8,0,0,1,16,0v24h24A8,8,0,0,1,152,112Zm77.66,117.66a8,8,0,0,1-11.32,0l-50.06-50.07a88.11,88.11,0,1,1,11.31-11.31l50.07,50.06A8,8,0,0,1,229.66,229.66ZM112,184a72,72,0,1,0-72-72A72.08,72.08,0,0,0,112,184Z";
        }

        private static GraphicsPath Get(string name)
        {
            GraphicsPath p;
            if (cache.TryGetValue(name, out p)) return p;
            string d;
            if (!Data.TryGetValue(name, out d)) return null;
            p = SvgPath.Parse(d);
            cache[name] = p;
            return p;
        }

        public static void Draw(Graphics g, string name, RectangleF area, Color c)
        {
            GraphicsPath p = Get(name);
            if (p == null) return;
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(area.X, area.Y);
            g.ScaleTransform(area.Width / 256f, area.Height / 256f);
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
            g.Restore(st);
        }
    }

    // ---------------- 호버 툴팁 오버레이 ----------------

    // 포커스를 뺏지 않는 작은 설명 창
    public class TipWindow : Form
    {
        private string text = "";

        public TipWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(30, 33, 40);
            Font = Theme.Ui;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;   // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080;   // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public void Present(string t, Control anchor)
        {
            text = t;
            Size ts = TextRenderer.MeasureText(t, Font);
            Size = new Size(ts.Width + Theme.S(20), ts.Height + Theme.S(13));

            Point below = anchor.PointToScreen(new Point(0, anchor.Height + Theme.S(6)));
            Rectangle wa = Screen.FromControl(anchor).WorkingArea;
            int x = below.X;
            if (x + Width > wa.Right - Theme.S(8)) x = wa.Right - Theme.S(8) - Width;
            int y = below.Y;
            if (y + Height > wa.Bottom) y = below.Y - anchor.Height - Height - Theme.S(12);
            Location = new Point(x, y);

            using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.S(6)))
                Region = new Region(p);

            if (!Visible) Show();
            Invalidate();
        }

        public void Dismiss() { if (Visible) Hide(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, text, Font, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // 컨트롤에 마우스를 올리면 잠깐 뒤에 설명이 떠오른다
    public static class Tip
    {
        private static TipWindow win;
        private static Timer timer;
        private static Control pending;
        private static readonly Dictionary<Control, string> texts = new Dictionary<Control, string>();

        public static void Attach(Control c, string text)
        {
            bool first = !texts.ContainsKey(c);
            texts[c] = text;
            if (first)
            {
                c.MouseEnter += OnEnter;
                c.MouseLeave += OnLeave;
                c.MouseDown += OnDown;
                c.Disposed += delegate { texts.Remove(c); if (pending == c) HideTip(); };
            }
        }

        public static string TextOf(Control c)
        {
            string s;
            return texts.TryGetValue(c, out s) ? s : null;
        }

        private static void OnEnter(object s, EventArgs e)
        {
            pending = (Control)s;
            if (timer == null)
            {
                timer = new Timer();
                timer.Interval = 380;
                timer.Tick += Fire;
            }
            timer.Stop();
            timer.Start();
        }

        private static void Fire(object s, EventArgs e)
        {
            timer.Stop();
            Control c = pending;
            if (c == null || c.IsDisposed || !c.Visible || !c.IsHandleCreated) return;
            if (!c.ClientRectangle.Contains(c.PointToClient(Cursor.Position))) return;
            string t = TextOf(c);
            if (string.IsNullOrEmpty(t)) return;
            if (win == null || win.IsDisposed) win = new TipWindow();
            win.Present(t, c);
        }

        private static void OnLeave(object s, EventArgs e) { HideTip(); }
        private static void OnDown(object s, MouseEventArgs e) { HideTip(); }

        public static void HideTip()
        {
            if (timer != null) timer.Stop();
            pending = null;
            if (win != null && !win.IsDisposed) win.Dismiss();
        }
    }

    // ---------------- 버튼 ----------------

    public class FlatButton : Control
    {
        public string Icon;
        public string Caption;
        public bool Checked;
        public bool Primary;
        public bool Vertical;

        private bool hover, down;
        private int iconPx = Theme.S(19);

        public event EventHandler Activated;

        public FlatButton(string icon, string caption)
        {
            Icon = icon;
            Caption = caption;
            Font = Theme.Ui;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public void Fit(int iconSize, int padH, int height)
        {
            iconPx = Theme.S(iconSize);
            Size ts = (Caption != null) ? TextRenderer.MeasureText(Caption, Font) : Size.Empty;
            int w;
            if (Vertical)
                w = Math.Max(ts.Width + Theme.S(padH) * 2, iconPx + Theme.S(padH) * 2);
            else
            {
                int gap = (Icon != null && Caption != null) ? Theme.S(7) : 0;
                w = Theme.S(padH) * 2 + (Icon != null ? iconPx : 0) + gap + ts.Width;
            }
            Size = new Size(w, Theme.S(height));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool was = down;
            down = false;
            Invalidate();
            base.OnMouseUp(e);
            if (was && ClientRectangle.Contains(e.Location) && Activated != null)
                Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
            Color fg = Theme.Ink;
            Color bgPaint = Color.Empty;

            if (Primary)
            {
                bgPaint = (down || hover) ? Theme.AccentDark : Theme.Accent;
                fg = Color.White;
            }
            else if (Checked) { bgPaint = Theme.AccentSoft; fg = Theme.Accent; }
            else if (down) bgPaint = Theme.Press;
            else if (hover) bgPaint = Theme.Hover;

            if (bgPaint != Color.Empty)
                using (GraphicsPath p = Theme.Round(r, Theme.S(8)))
                using (SolidBrush b = new SolidBrush(bgPaint))
                    g.FillPath(b, p);

            Size ts = (Caption != null) ? TextRenderer.MeasureText(Caption, Font) : Size.Empty;

            if (Vertical)
            {
                int totalH = (Icon != null ? iconPx : 0) + (Caption != null ? ts.Height + Theme.S(6) : 0);
                int y = (Height - totalH) / 2;
                if (Icon != null)
                {
                    Icons.Draw(g, Icon, new RectangleF((Width - iconPx) / 2f, y, iconPx, iconPx), fg);
                    y += iconPx + Theme.S(6);
                }
                if (Caption != null)
                    TextRenderer.DrawText(g, Caption, Font, new Point((Width - ts.Width) / 2, y), fg);
            }
            else
            {
                int gap = (Icon != null && Caption != null) ? Theme.S(7) : 0;
                int contentW = (Icon != null ? iconPx : 0) + gap + ts.Width;
                int x = (Width - contentW) / 2;
                if (Icon != null)
                {
                    Icons.Draw(g, Icon, new RectangleF(x, (Height - iconPx) / 2f, iconPx, iconPx), fg);
                    x += iconPx + gap;
                }
                if (Caption != null)
                    TextRenderer.DrawText(g, Caption, Font, new Point(x, (Height - ts.Height) / 2), fg);
            }
        }
    }

    // 색상 팔레트 - 여러 줄 격자. 마지막 칸은 "흰색 + 검정 아웃라인" 스타일 스와치
    public class SwatchGrid : Control
    {
        public Color[] Values;
        public int SelectedIndex;
        private int columns, cell, hoverIndex = -1;

        public event EventHandler Activated;

        private int Total { get { return Values.Length + 1; } }
        public bool SelectedIsOutline { get { return SelectedIndex == Values.Length; } }
        public Color Selected { get { return SelectedIsOutline ? Color.White : Values[SelectedIndex]; } }

        public SwatchGrid(Color[] values, int columns, int selected)
        {
            Values = values;
            this.columns = columns;
            SelectedIndex = selected;
            cell = Theme.S(25);
            int rows = (Total + columns - 1) / columns;
            Size = new Size(cell * columns, cell * rows);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public void SelectColor(Color c, bool outlined)
        {
            if (outlined) { SelectedIndex = Values.Length; Invalidate(); return; }
            for (int i = 0; i < Values.Length; i++)
                if (Values[i].ToArgb() == c.ToArgb()) { SelectedIndex = i; Invalidate(); return; }
        }

        private int IndexAt(Point p)
        {
            int col = p.X / cell, row = p.Y / cell;
            if (col < 0 || col >= columns || row < 0) return -1;
            int idx = row * columns + col;
            return (idx >= 0 && idx < Total) ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx != hoverIndex) { hoverIndex = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx >= 0)
            {
                SelectedIndex = idx;
                Invalidate();
                if (Activated != null) Activated(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 쓸 수 없는 상태(모자이크 도구)에서는 흐리게 보여준다
            int alpha = Enabled ? 255 : 60;

            for (int i = 0; i < Total; i++)
            {
                int col = i % columns, row = i / columns;
                float cxp = col * cell, cyp = row * cell;
                bool sel = (i == SelectedIndex) && Enabled;
                bool outlineCell = (i == Values.Length);
                Color v = Color.FromArgb(alpha, outlineCell ? Color.White : Values[i]);
                Color ringColor = outlineCell ? Color.FromArgb(alpha, 22, 24, 30) : v;

                if (sel)
                {
                    float rd = cell - Theme.S(3);
                    using (Pen ring = new Pen(ringColor, Theme.S(2)))
                        g.DrawEllipse(ring, cxp + (cell - rd) / 2f, cyp + (cell - rd) / 2f, rd, rd);
                }
                else if (i == hoverIndex && Enabled)
                {
                    using (SolidBrush b = new SolidBrush(Theme.Hover))
                        g.FillEllipse(b, cxp + Theme.S(1), cyp + Theme.S(1), cell - Theme.S(2), cell - Theme.S(2));
                }

                float d = Theme.S(sel ? 14 : 16);
                RectangleF dot = new RectangleF(cxp + (cell - d) / 2f, cyp + (cell - d) / 2f, d, d);
                using (SolidBrush b = new SolidBrush(v))
                    g.FillEllipse(b, dot);

                if (outlineCell)
                    using (Pen p = new Pen(Color.FromArgb(alpha, 22, 24, 30), Theme.S(1.8f)))
                        g.DrawEllipse(p, dot);
                else
                    using (Pen p = new Pen(Color.FromArgb(Enabled ? 40 : 14, 0, 0, 0), 1f))
                        g.DrawEllipse(p, dot);
            }
        }
    }

    public class WidthButton : Control
    {
        public float Value;
        public bool Checked;
        private bool hover;
        public event EventHandler Activated;

        public WidthButton(float value)
        {
            Value = value;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(40), Theme.S(36));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (ClientRectangle.Contains(e.Location) && Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = Checked ? Theme.AccentSoft : (hover ? Theme.Hover : Color.Empty);
            if (bg != Color.Empty)
                using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), Theme.S(8)))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, p);

            // Value 는 이미 이미지 픽셀 단위(배율 반영)라 그대로 그리면 실제 굵기와 같아 보인다
            Color fg = Checked ? Theme.Accent : Theme.Ink;
            using (Pen pen = new Pen(fg, Value))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float y = Height / 2f;
                g.DrawLine(pen, Theme.S(9), y, Width - Theme.S(9), y);
            }
        }
    }

    // 글자 크기 선택 - '가' 를 실제 비율로 보여준다
    public class TextSizeButton : Control
    {
        public float Value;
        public bool Checked;
        private bool hover;
        private int glyphPx;
        public event EventHandler Activated;

        public TextSizeButton(float value, int glyph)
        {
            Value = value;
            glyphPx = Theme.S(glyph);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(35), Theme.S(36));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (ClientRectangle.Contains(e.Location) && Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = Checked ? Theme.AccentSoft : (hover ? Theme.Hover : Color.Empty);
            if (bg != Color.Empty)
                using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), Theme.S(8)))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, p);

            Color fg = Checked ? Theme.Accent : Theme.Ink;
            Font f = Theme.Bubble(glyphPx);
            Size ts = TextRenderer.MeasureText("가", f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "가", f,
                new Point((Width - ts.Width) / 2, (Height - ts.Height) / 2), fg, TextFormatFlags.NoPadding);
        }
    }

    // 모자이크 거칠기 선택 - 격자 칸 크기를 그대로 보여준다
    public class BlockButton : Control
    {
        public float Value;
        public bool Checked;
        private int cells;
        private bool hover;
        public event EventHandler Activated;

        public BlockButton(float value, int cells)
        {
            Value = value;
            this.cells = cells;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(40), Theme.S(36));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (ClientRectangle.Contains(e.Location) && Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = Checked ? Theme.AccentSoft : (hover ? Theme.Hover : Color.Empty);
            if (bg != Color.Empty)
                using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), Theme.S(8)))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, p);

            Color fg = Checked ? Theme.Accent : Theme.Ink;
            int side = Theme.S(18);
            float cell = side / (float)cells;
            float ox = (Width - side) / 2f, oy = (Height - side) / 2f;
            float gap = Math.Max(1f, Theme.S(1));

            g.SmoothingMode = SmoothingMode.None;
            using (SolidBrush b = new SolidBrush(fg))
                for (int r = 0; r < cells; r++)
                    for (int c = 0; c < cells; c++)
                        if (((r + c) % 2) == 0)
                            g.FillRectangle(b, ox + c * cell, oy + r * cell,
                                            Math.Max(1f, cell - gap), Math.Max(1f, cell - gap));
        }
    }

    // 짧은 글자를 보여주는 선택 버튼 (돋보기 배율 등)
    public class LabelButton : Control
    {
        public float Value;
        public string Caption;
        public bool Checked;
        private bool hover;
        public event EventHandler Activated;

        public LabelButton(float value, string caption)
        {
            Value = value;
            Caption = caption;
            Font = Theme.Ui;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(40), Theme.S(36));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (ClientRectangle.Contains(e.Location) && Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = Checked ? Theme.AccentSoft : (hover ? Theme.Hover : Color.Empty);
            if (bg != Color.Empty)
                using (GraphicsPath p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), Theme.S(8)))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, p);

            Color fg = Checked ? Theme.Accent : Theme.Ink;
            Size ts = TextRenderer.MeasureText(Caption, Font);
            TextRenderer.DrawText(g, Caption, Font,
                new Point((Width - ts.Width) / 2, (Height - ts.Height) / 2), fg);
        }
    }

    // 체크 표시가 있는 한 줄짜리 설정 항목
    public class CheckRow : Control
    {
        public bool Checked;
        public string Caption;
        private bool hover;
        public event EventHandler Toggled;

        public CheckRow(string caption)
        {
            Caption = caption;
            Font = Theme.Ui;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size ts = TextRenderer.MeasureText(caption, Font);
            Size = new Size(Theme.S(16) + Theme.S(9) + ts.Width + Theme.S(4), Theme.S(28));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!ClientRectangle.Contains(e.Location)) return;
            Checked = !Checked;
            Invalidate();
            if (Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float box = Theme.S(16);
            RectangleF r = new RectangleF(0, (Height - box) / 2f, box, box);

            using (GraphicsPath p = Theme.Round(r, Theme.S(4)))
            {
                if (Checked)
                    using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
                else
                {
                    using (SolidBrush b = new SolidBrush(hover ? Theme.Hover : Color.White)) g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(190, 196, 206), Theme.S(1.4f))) g.DrawPath(pen, p);
                }
            }

            if (Checked)
                using (Pen pen = new Pen(Color.White, Theme.S(2)))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new PointF[] {
                        new PointF(r.X + box * 0.24f, r.Y + box * 0.52f),
                        new PointF(r.X + box * 0.42f, r.Y + box * 0.71f),
                        new PointF(r.X + box * 0.76f, r.Y + box * 0.31f) });
                }

            Size ts = TextRenderer.MeasureText(Caption, Font);
            TextRenderer.DrawText(g, Caption, Font,
                new Point((int)(box + Theme.S(9)), (Height - ts.Height) / 2),
                Checked ? Theme.Ink : Theme.Muted);
        }
    }

    // ---------------- 주석 데이터 ----------------

    public class Annotation
    {
        public string Kind;      // box, ellipse, arrow, text, textbox, bubble, mosaic, zoom, numbox
        public int Number = 1;   // 숫자 박스의 순번 (편집기가 다시 매긴다)
        public bool Outlined;    // 흰색 + 검정 아웃라인 스타일
        public int X1, Y1, X2, Y2;   // 말풍선은 (X1,Y1)=가리킬 지점, (X2,Y2)=말풍선 중심
        public Color Color;
        public float Thickness;
        public string Text = "";
        public float FontSize = 19f;
        public float Block = 14f;    // 모자이크 한 칸 크기
        public float Zoom = 2f;      // 돋보기 배율
        public int Align = 1;        // 일반 텍스트 정렬 : 0 좌, 1 중앙, 2 우
        public bool Editing;

        public Point P1 { get { return new Point(X1, Y1); } }
        public Point P2 { get { return new Point(X2, Y2); } }

        // 연결선(connector)의 양 끝이 붙어 있는 도형. 도형이 움직이면 곡선도 따라간다
        public Annotation RefA, RefB;

        // 모자이크는 매번 다시 계산하면 느려서 결과를 들고 있는다
        public Bitmap Cache;
        public Rectangle CacheRect;
        public float CacheBlock;

        public void DropCache()
        {
            if (Cache != null) { Cache.Dispose(); Cache = null; }
            CacheRect = Rectangle.Empty;
        }
    }

    // ---------------- Win32 ----------------

    public static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        // 이미 떠 있는 쏙캡처에게 "창을 보여라" 라고 알리는 메시지
        private static uint showMsg;
        public static uint WM_SHOW_SSOK
        {
            get
            {
                if (showMsg == 0) showMsg = RegisterWindowMessage("SsokCapture_ShowWindow_v1");
                return showMsg;
            }
        }

        public const int WM_HOTKEY = 0x0312;
        public const uint VK_SNAPSHOT = 0x2C;
        public const uint VK_F8 = 0x77;
    }

    // ---------------- 윈도우 시작 시 자동 실행 ----------------

    public static class AutoRun
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Name = "SsokCapture";

        public static bool Enabled
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key))
                        return k != null && k.GetValue(Name) != null;
                }
                catch { return false; }
            }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(Name, "\"" + Application.ExecutablePath + "\" -tray");
                    else if (k.GetValue(Name) != null) k.DeleteValue(Name, false);
                }
            }
            catch { }
        }
    }

    // ---------------- 주석 그리기 / 히트테스트 ----------------

    public static class Painter
    {
        public static int BubbleMaxWidth { get { return (int)Theme.A(460f); } }

        private const TextFormatFlags MeasureFlags =
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        public static Rectangle Norm(int x1, int y1, int x2, int y2)
        {
            return new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2),
                                 Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        public static Color InkFor(Color fill)
        {
            double lum = (0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B) / 255.0;
            return (lum > 0.68) ? Color.FromArgb(26, 28, 34) : Color.White;
        }

        public static int PadX(float fontSize) { return (int)Math.Round(fontSize * 0.85f); }
        public static int PadY(float fontSize) { return (int)Math.Round(fontSize * 0.55f); }

        public static Size MeasureText(string text, Font f)
        {
            if (string.IsNullOrEmpty(text)) text = " ";
            Size s = TextRenderer.MeasureText(text, f, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
            if (s.Width > BubbleMaxWidth)
                s = TextRenderer.MeasureText(text, f, new Size(BubbleMaxWidth, int.MaxValue),
                                             MeasureFlags | TextFormatFlags.WordBreak);
            if (s.Height < f.Height) s.Height = f.Height;
            if (s.Width < f.Height) s.Width = f.Height;
            return s;
        }

        public static Rectangle BubbleTextRect(Annotation a)
        {
            Size ts = MeasureText(a.Text, Theme.Bubble(a.FontSize));
            return new Rectangle(a.X2 - ts.Width / 2, a.Y2 - ts.Height / 2, ts.Width, ts.Height);
        }

        public static Rectangle BubbleRect(Annotation a)
        {
            Rectangle t = BubbleTextRect(a);
            int px = PadX(a.FontSize), py = PadY(a.FontSize);
            return new Rectangle(t.X - px, t.Y - py, t.Width + px * 2, t.Height + py * 2);
        }

        // 꼬리 삼각형 (없으면 null)
        public static PointF[] BubbleTail(Annotation a)
        {
            Rectangle box = BubbleRect(a);
            RectangleF boxF = box;
            float cx = a.X2, cy = a.Y2, tipX = a.X1, tipY = a.Y1;
            float dx = tipX - cx, dy = tipY - cy;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len <= Theme.A(8f) || boxF.Contains(tipX, tipY)) return null;

            float hw = box.Width / 2f, hh = box.Height / 2f;
            float tx = (Math.Abs(dx) > 0.001f) ? hw / Math.Abs(dx) : float.MaxValue;
            float ty = (Math.Abs(dy) > 0.001f) ? hh / Math.Abs(dy) : float.MaxValue;
            bool sideExit = tx <= ty;
            float t = Math.Min(tx, ty);

            float ux = (float)(dx / len), uy = (float)(dy / len);
            float exX = cx + dx * t - ux * 1.5f;
            float exY = cy + dy * t - uy * 1.5f;
            // 꼬리 밑변은 글자 크기에 비례 - 몸통에 비해 삼각형이 커 보이지 않게 한다
            float cap = a.FontSize * 0.55f;
            float half = sideExit ? Math.Min(cap, hh * 0.45f) : Math.Min(cap, hw * 0.45f);
            float bx = sideExit ? 0f : 1f;
            float by = sideExit ? 1f : 0f;

            return new PointF[] {
                new PointF(tipX, tipY),
                new PointF(exX + bx * half, exY + by * half),
                new PointF(exX - bx * half, exY - by * half) };
        }

        // 꼬리 끝을 반지름 r 만큼 둥글린 삼각형
        // 꼭짓점이 뾰족할수록 모서리를 더 길게 잘라내야 실제 반지름 r 이 나온다 (d = r / tan(θ/2))
        private static GraphicsPath TailPath(PointF[] t, float r)
        {
            PointF tip = t[0], b1 = t[1], b2 = t[2];
            GraphicsPath p = new GraphicsPath();

            float l1 = (float)Math.Sqrt((b1.X - tip.X) * (b1.X - tip.X) + (b1.Y - tip.Y) * (b1.Y - tip.Y));
            float l2 = (float)Math.Sqrt((b2.X - tip.X) * (b2.X - tip.X) + (b2.Y - tip.Y) * (b2.Y - tip.Y));
            if (l1 < 2f || l2 < 2f) { p.AddPolygon(t); return p; }

            float u1x = (b1.X - tip.X) / l1, u1y = (b1.Y - tip.Y) / l1;
            float u2x = (b2.X - tip.X) / l2, u2y = (b2.Y - tip.Y) / l2;

            double dot = u1x * u2x + u1y * u2y;
            if (dot > 1) dot = 1; else if (dot < -1) dot = -1;
            double tanHalf = Math.Tan(Math.Acos(dot) / 2.0);

            float d = (tanHalf > 0.001) ? (float)(r / tanHalf) : r;
            float maxD = Math.Min(l1, l2) * 0.45f;
            if (d > maxD) d = maxD;
            if (d < 0.8f) { p.AddPolygon(t); return p; }

            PointF c1 = new PointF(tip.X + u1x * d, tip.Y + u1y * d);
            PointF c2 = new PointF(tip.X + u2x * d, tip.Y + u2y * d);

            p.AddLine(b1, c1);
            p.AddBezier(c1, tip, tip, c2);   // 제어점을 꼭짓점에 두면 자연스러운 라운드가 나온다
            p.AddLine(c2, b2);
            p.CloseFigure();
            return p;
        }

        // 흰색 + 검정 아웃라인 스타일에서 쓰는 외곽선 색
        public static readonly Color OutlineInk = Color.FromArgb(22, 24, 30);

        private static Pen MakeStrokePen(Color c, float width)
        {
            Pen pen = new Pen(c, width);
            pen.LineJoin = LineJoin.Round;
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            return pen;
        }

        public static void Draw(Graphics g, Annotation a, Bitmap source)
        {
            if (a.Kind == "connector") { DrawConnector(g, a); return; }
            if (a.Kind == "arrowfill") { DrawArrowFill(g, a); return; }
            if (a.Kind == "text") { DrawPlainText(g, a); return; }
            if (a.Kind == "textbox") { DrawTextBox(g, a); return; }
            if (a.Kind == "bubble") { DrawBubble(g, a); return; }
            if (a.Kind == "mosaic") { DrawMosaic(g, a, source); return; }
            if (a.Kind == "zoom") { DrawZoom(g, a, source); return; }
            if (a.Kind == "numbox") { DrawNumBox(g, a); return; }

            // 선으로 그리는 도형 : 아웃라인 스타일이면 검정을 한 번 더 두껍게 깔고 그 위에 흰 선
            if (a.Outlined)
                using (Pen halo = MakeStrokePen(OutlineInk, a.Thickness + Theme.A(2.8f)))
                    DrawStroke(g, halo, a);
            using (Pen pen = MakeStrokePen(a.Color, a.Thickness))
                DrawStroke(g, pen, a);
        }

        private static void DrawStroke(Graphics g, Pen pen, Annotation a)
        {
            if (a.Kind == "box")
            {
                Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
                if (r.Width > 0 && r.Height > 0) g.DrawRectangle(pen, r);
            }
            else if (a.Kind == "ellipse")
            {
                Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
                if (r.Width > 0 && r.Height > 0) g.DrawEllipse(pen, r);
            }
            else if (a.Kind == "arrow")
            {
                DrawArrow(g, pen, a);
            }
        }

        // 채움 화살표 : 몸통 선 + 꽉 찬 삼각형 머리
        private static void DrawArrowFill(Graphics g, Annotation a)
        {
            float dx = a.X2 - a.X1, dy = a.Y2 - a.Y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;

            float head = Math.Max(a.Thickness * 5.5f, Theme.A(16f));
            head = (float)Math.Min(head, len * 0.4);
            if (head < Theme.A(6f)) return;

            double ang = Math.Atan2(dy, dx);
            double spread = 26.0 * Math.PI / 180.0;

            PointF tip = new PointF(a.X2, a.Y2);
            PointF w1 = new PointF((float)(a.X2 - head * Math.Cos(ang - spread)),
                                   (float)(a.Y2 - head * Math.Sin(ang - spread)));
            PointF w2 = new PointF((float)(a.X2 - head * Math.Cos(ang + spread)),
                                   (float)(a.Y2 - head * Math.Sin(ang + spread)));
            PointF[] tri = new PointF[] { tip, w1, w2 };

            // 몸통은 머리 밑변까지만 (끝이 삼각형을 뚫고 나오지 않게)
            float bodyLen = (float)(len - head * Math.Cos(spread) * 0.85);
            PointF bodyEnd = new PointF((float)(a.X1 + bodyLen * Math.Cos(ang)),
                                        (float)(a.Y1 + bodyLen * Math.Sin(ang)));

            if (a.Outlined)
            {
                using (Pen halo = MakeStrokePen(OutlineInk, a.Thickness + Theme.A(2.8f)))
                    g.DrawLine(halo, new PointF(a.X1, a.Y1), bodyEnd);
                using (Pen haloTri = MakeStrokePen(OutlineInk, Theme.A(2.8f)))
                    g.DrawPolygon(haloTri, tri);
            }

            using (Pen pen = MakeStrokePen(a.Color, a.Thickness))
                g.DrawLine(pen, new PointF(a.X1, a.Y1), bodyEnd);
            using (SolidBrush fill = new SolidBrush(a.Color))
                g.FillPolygon(fill, tri);
        }

        private const TextFormatFlags DrawFlags =
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl |
            TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter;

        // 일반 텍스트 : 배경 없음. 아웃라인 스타일이면 8방향으로 검정을 깔아 테두리를 만든다
        private static void DrawPlainText(Graphics g, Annotation a)
        {
            if (a.Editing || string.IsNullOrEmpty(a.Text)) return;
            Rectangle tr = BubbleTextRect(a);
            Font f = Theme.Bubble(a.FontSize);

            // 정렬은 여러 줄일 때 줄이 맞춰지는 기준이다
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                                    TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak;
            if (a.Align == 0) flags |= TextFormatFlags.Left;
            else if (a.Align == 2) flags |= TextFormatFlags.Right;
            else flags |= TextFormatFlags.HorizontalCenter;

            if (a.Outlined)
            {
                int o = Math.Max(1, (int)Math.Round(Theme.A(1.3f)));
                for (int dx = -o; dx <= o; dx += o)
                    for (int dy = -o; dy <= o; dy += o)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Rectangle rr = tr;
                        rr.Offset(dx, dy);
                        TextRenderer.DrawText(g, a.Text, f, rr, OutlineInk, flags);
                    }
            }
            TextRenderer.DrawText(g, a.Text, f, tr, a.Color, flags);
        }

        // 배경 있는 텍스트 : 둥근 사각형 채움 (말풍선에서 꼬리만 뺀 것)
        private static void DrawTextBox(Graphics g, Annotation a)
        {
            Rectangle box = BubbleRect(a);
            float rad = Math.Min(a.FontSize * 0.7f, box.Height / 2.6f);

            using (GraphicsPath body = Theme.Round(box, rad))
            {
                using (SolidBrush fill = new SolidBrush(a.Color))
                    g.FillPath(fill, body);
                if (a.Outlined)
                    using (Pen edge = MakeStrokePen(OutlineInk, Theme.A(1.8f)))
                        g.DrawPath(edge, body);
            }

            if (!a.Editing && !string.IsNullOrEmpty(a.Text))
                TextRenderer.DrawText(g, a.Text, Theme.Bubble(a.FontSize), BubbleTextRect(a),
                                      InkFor(a.Color), DrawFlags);
        }

        // 열린 갈매기 머리
        private static void DrawChevronHead(Graphics g, Pen pen, PointF tip, double ang, float head)
        {
            double spread = 29.0 * Math.PI / 180.0;
            PointF w1 = new PointF((float)(tip.X - head * Math.Cos(ang - spread)),
                                   (float)(tip.Y - head * Math.Sin(ang - spread)));
            PointF w2 = new PointF((float)(tip.X - head * Math.Cos(ang + spread)),
                                   (float)(tip.Y - head * Math.Sin(ang + spread)));
            g.DrawLines(pen, new PointF[] { w1, tip, w2 });
        }

        // 화살표 : 채운 삼각형이 아니라 <- 처럼 열린 갈매기 모양
        private static void DrawArrow(Graphics g, Pen pen, Annotation a)
        {
            float dx = a.X2 - a.X1, dy = a.Y2 - a.Y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;

            g.DrawLine(pen, a.X1, a.Y1, a.X2, a.Y2);

            // 머리 크기는 선 굵기에 비례하되, 짧은 화살표에서는 몸통 길이에 맞춰 줄인다
            float head = Math.Max(a.Thickness * 6.5f, Theme.A(17f));
            head = (float)Math.Min(head, len * 0.32);
            if (head < Theme.A(6f)) return;

            DrawChevronHead(g, pen, new PointF(a.X2, a.Y2), Math.Atan2(dy, dx), head);
        }

        // ---------------- 연결선 (도형과 도형을 잇는 곡선 화살표) ----------------

        private static PointF CenterOf(Annotation s)
        {
            RectangleF r = BoundsOf(s);
            return new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
        }

        private static RectangleF BoundsOf(Annotation s)
        {
            if (s.Kind == "bubble" || s.Kind == "textbox") return BubbleRect(s);
            if (s.Kind == "text") return BubbleTextRect(s);
            return Norm(s.X1, s.Y1, s.X2, s.Y2);
        }

        // 도형의 상하좌우 변 가운데에서 나가는 지점과 방향
        // 상대가 있는 쪽 변을 고르고, 곡선은 그 변에서 수직으로 출발한다 (노드 에디터 방식)
        private static void AnchorFor(Annotation shape, PointF toward, out PointF anchor, out PointF dir)
        {
            RectangleF r = BoundsOf(shape);
            PointF c = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            float dx = toward.X - c.X, dy = toward.Y - c.Y;
            float gap = Theme.A(6f);

            // 박스 비율을 감안해 가로로 나갈지 세로로 나갈지 정한다
            bool horizontal = Math.Abs(dx) * r.Height >= Math.Abs(dy) * r.Width;
            if (horizontal)
            {
                if (dx >= 0) { anchor = new PointF(r.Right + gap, c.Y); dir = new PointF(1f, 0f); }
                else { anchor = new PointF(r.Left - gap, c.Y); dir = new PointF(-1f, 0f); }
            }
            else
            {
                if (dy >= 0) { anchor = new PointF(c.X, r.Bottom + gap); dir = new PointF(0f, 1f); }
                else { anchor = new PointF(c.X, r.Top - gap); dir = new PointF(0f, -1f); }
            }
        }

        // 연결선의 실제 곡선 좌표. 도형이 움직이면 매번 새로 계산된다
        public static bool ConnectorCurve(Annotation a, out PointF s, out PointF c1, out PointF c2, out PointF e)
        {
            PointF cA = (a.RefA != null) ? CenterOf(a.RefA) : new PointF(a.X1, a.Y1);
            PointF cB = (a.RefB != null) ? CenterOf(a.RefB) : new PointF(a.X2, a.Y2);

            PointF dirA, dirB;
            if (a.RefA != null) AnchorFor(a.RefA, cB, out s, out dirA);
            else { s = cA; dirA = PointF.Empty; }
            if (a.RefB != null) AnchorFor(a.RefB, cA, out e, out dirB);
            else { e = cB; dirB = PointF.Empty; }

            float dx = e.X - s.X, dy = e.Y - s.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 4) { c1 = s; c2 = e; return false; }

            // 변에서 수직으로 뻗어 나가다가 S 자로 상대에게 흘러간다
            float k = Math.Max(Theme.A(30f), Math.Min((float)len * 0.38f, Theme.A(150f)));

            if (dirA == PointF.Empty)
            {
                float ux = (float)(dx / len), uy = (float)(dy / len);
                dirA = new PointF(ux, uy);
            }
            if (dirB == PointF.Empty)
            {
                float ux = (float)(dx / len), uy = (float)(dy / len);
                dirB = new PointF(-ux, -uy);
            }

            c1 = new PointF(s.X + dirA.X * k, s.Y + dirA.Y * k);
            c2 = new PointF(e.X + dirB.X * k, e.Y + dirB.Y * k);
            return true;
        }

        private static void DrawConnector(Graphics g, Annotation a)
        {
            PointF s, c1, c2, e;
            if (!ConnectorCurve(a, out s, out c1, out c2, out e)) return;

            // 머리는 꽉 찬 삼각형
            double tang = Math.Atan2(e.Y - c2.Y, e.X - c2.X);
            float head = Math.Max(a.Thickness * 5.5f, Theme.A(16f));
            double spread = 26.0 * Math.PI / 180.0;
            PointF w1 = new PointF((float)(e.X - head * Math.Cos(tang - spread)),
                                   (float)(e.Y - head * Math.Sin(tang - spread)));
            PointF w2 = new PointF((float)(e.X - head * Math.Cos(tang + spread)),
                                   (float)(e.Y - head * Math.Sin(tang + spread)));
            PointF[] tri = new PointF[] { e, w1, w2 };

            // 곡선은 삼각형 밑변 밑에서 멈춘다 - 끝까지 그리면 둥근 끝단이 꼭짓점 밖으로 삐져나온다
            float kLen = (float)Math.Sqrt((c2.X - e.X) * (c2.X - e.X) + (c2.Y - e.Y) * (c2.Y - e.Y));
            float back = head * (float)Math.Cos(spread) * 0.8f;
            float t = (kLen > 1f) ? Math.Min(0.9f, back / kLen) : 0f;
            PointF stop = new PointF(e.X + (c2.X - e.X) * t, e.Y + (c2.Y - e.Y) * t);

            if (a.Outlined)
            {
                using (Pen halo = MakeStrokePen(OutlineInk, a.Thickness + Theme.A(2.8f)))
                    g.DrawBezier(halo, s, c1, c2, stop);
                using (Pen haloTri = MakeStrokePen(OutlineInk, Theme.A(2.8f)))
                    g.DrawPolygon(haloTri, tri);
            }
            using (Pen pen = MakeStrokePen(a.Color, a.Thickness))
                g.DrawBezier(pen, s, c1, c2, stop);
            using (SolidBrush fill = new SolidBrush(a.Color))
                g.FillPolygon(fill, tri);
        }

        // 숫자 박스 : 사각형 + 왼쪽 위 모서리에 순번 배지. 배지는 항상 정원이다
        public static RectangleF BadgeRect(Annotation a)
        {
            float d = a.FontSize * 1.8f;
            Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
            return new RectangleF(r.Left - d / 2f, r.Top - d / 2f, d, d);
        }

        // 자릿수가 늘어도 원은 그대로 두고 숫자를 줄여서 안에 맞춘다
        private static Font BadgeFont(Annotation a)
        {
            float limit = a.FontSize * 1.8f * 0.70f;
            Font f = Theme.Bubble(a.FontSize);
            Size ts = TextRenderer.MeasureText(a.Number.ToString(), f,
                                               new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            if (ts.Width > limit && ts.Width > 0)
                f = Theme.Bubble(a.FontSize * limit / ts.Width);
            return f;
        }

        private static void DrawNumBox(Graphics g, Annotation a)
        {
            Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
            if (r.Width > 0 && r.Height > 0)
            {
                if (a.Outlined)
                    using (Pen halo = MakeStrokePen(OutlineInk, a.Thickness + Theme.A(2.8f)))
                        g.DrawRectangle(halo, r);
                using (Pen pen = MakeStrokePen(a.Color, a.Thickness))
                    g.DrawRectangle(pen, r);
            }

            RectangleF badge = BadgeRect(a);
            using (SolidBrush b = new SolidBrush(a.Color))
                g.FillEllipse(b, badge);
            if (a.Outlined)
                using (Pen edge = MakeStrokePen(OutlineInk, Theme.A(1.8f)))
                    g.DrawEllipse(edge, badge);

            TextRenderer.DrawText(g, a.Number.ToString(), BadgeFont(a),
                Rectangle.Round(badge), InkFor(a.Color),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        // 돋보기 : 원 안쪽을 같은 중심 기준으로 확대해서 보여준다
        private static void DrawZoom(Graphics g, Annotation a, Bitmap source)
        {
            if (source == null) return;
            Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
            if (r.Width < 12 || r.Height < 12) return;

            float z = Math.Max(1.2f, a.Zoom);
            float sw = r.Width / z, sh = r.Height / z;
            float sx = r.X + (r.Width - sw) / 2f;
            float sy = r.Y + (r.Height - sh) / 2f;

            // 원본 밖을 읽지 않도록 가져올 영역을 이미지 안으로 밀어 넣는다
            if (sw > source.Width) sw = source.Width;
            if (sh > source.Height) sh = source.Height;
            if (sx < 0) sx = 0;
            if (sy < 0) sy = 0;
            if (sx + sw > source.Width) sx = source.Width - sw;
            if (sy + sh > source.Height) sy = source.Height - sh;

            GraphicsState st = g.Save();
            using (GraphicsPath clip = new GraphicsPath())
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                clip.AddEllipse(r);
                g.SetClip(clip, CombineMode.Intersect);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, r, sx, sy, sw, sh, GraphicsUnit.Pixel, ia);
            }
            g.Restore(st);

            if (a.Thickness > 0)
            {
                if (a.Outlined)
                    using (Pen halo = new Pen(OutlineInk, a.Thickness + Theme.A(2.8f)))
                        g.DrawEllipse(halo, r);
                using (Pen pen = new Pen(a.Color, a.Thickness))
                    g.DrawEllipse(pen, r);
            }
        }

        // 모자이크 : 영역을 축소했다가 최근접 이웃으로 다시 키워 픽셀을 뭉갠다
        private static void DrawMosaic(Graphics g, Annotation a, Bitmap source)
        {
            if (source == null) return;
            Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
            r.Intersect(new Rectangle(0, 0, source.Width, source.Height));
            if (r.Width < 2 || r.Height < 2) return;

            int block = Math.Max(3, (int)Math.Round(a.Block));

            if (a.Cache == null || a.CacheRect != r || a.CacheBlock != block)
            {
                a.DropCache();
                a.Cache = BuildMosaic(source, r, block);
                a.CacheRect = r;
                a.CacheBlock = block;
            }
            if (a.Cache != null) g.DrawImageUnscaled(a.Cache, r.X, r.Y);
        }

        private static Bitmap BuildMosaic(Bitmap source, Rectangle r, int block)
        {
            int cols = Math.Max(1, r.Width / block);
            int rows = Math.Max(1, r.Height / block);

            Bitmap outp = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
            using (Bitmap region = source.Clone(r, PixelFormat.Format24bppRgb))
            using (Bitmap small = new Bitmap(cols, rows, PixelFormat.Format24bppRgb))
            // WrapMode 를 지정하지 않으면 축소, 확대할 때 이미지 바깥을 함께 샘플링해서
            // 가장자리가 어두워진다 (모자이크 테두리에 그림자처럼 보이는 원인)
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);

                using (Graphics sg = Graphics.FromImage(small))
                {
                    sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    sg.DrawImage(region, new Rectangle(0, 0, cols, rows),
                                 0, 0, region.Width, region.Height, GraphicsUnit.Pixel, ia);
                }
                using (Graphics og = Graphics.FromImage(outp))
                {
                    og.InterpolationMode = InterpolationMode.NearestNeighbor;
                    og.PixelOffsetMode = PixelOffsetMode.Half;
                    og.SmoothingMode = SmoothingMode.None;
                    og.DrawImage(small, new Rectangle(0, 0, r.Width, r.Height),
                                 0, 0, small.Width, small.Height, GraphicsUnit.Pixel, ia);
                }
            }
            return outp;
        }

        // 말풍선 : 선택한 색으로 채운다. 아웃라인 스타일이면 몸통과 꼬리 둘레에 검정 테두리
        private static void DrawBubble(Graphics g, Annotation a)
        {
            Rectangle box = BubbleRect(a);
            PointF[] tail = BubbleTail(a);
            float rad = Math.Min(a.FontSize * 0.7f, box.Height / 2.6f);
            float tailRad = Math.Max(Theme.A(2.5f), Math.Min(Theme.A(4.5f), a.FontSize * 0.14f));

            using (GraphicsPath body = Theme.Round(box, rad))
            using (GraphicsPath tp = (tail != null) ? TailPath(tail, tailRad) : null)
            {
                using (SolidBrush fill = new SolidBrush(a.Color))
                {
                    if (tp != null) g.FillPath(fill, tp);
                    g.FillPath(fill, body);
                }

                if (a.Outlined)
                    using (Pen edge = MakeStrokePen(OutlineInk, Theme.A(1.8f)))
                    {
                        // 몸통과 꼬리가 만나는 자리는 서로의 안쪽을 잘라내고 그려야 이음선이 안 생긴다
                        Region keep = g.Clip;
                        if (tp != null)
                        {
                            g.SetClip(body, CombineMode.Exclude);
                            g.DrawPath(edge, tp);
                            g.Clip = keep;
                            g.SetClip(tp, CombineMode.Exclude);
                            g.DrawPath(edge, body);
                            g.Clip = keep;
                        }
                        else g.DrawPath(edge, body);
                        keep.Dispose();
                    }
            }

            if (!a.Editing && !string.IsNullOrEmpty(a.Text))
                TextRenderer.DrawText(g, a.Text, Theme.Bubble(a.FontSize), BubbleTextRect(a),
                                      InkFor(a.Color), DrawFlags);
        }

        // --- 히트테스트 ---

        public static bool NearSegment(PointF a, PointF b, PointF p, float tol)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len2 = dx * dx + dy * dy;
            float t = (len2 <= 0f) ? 0f : ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float qx = a.X + t * dx - p.X, qy = a.Y + t * dy - p.Y;
            return qx * qx + qy * qy <= tol * tol;
        }

        private static bool InTriangle(PointF p, PointF a, PointF b, PointF c)
        {
            float d1 = (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
            float d2 = (p.X - c.X) * (b.Y - c.Y) - (b.X - c.X) * (p.Y - c.Y);
            float d3 = (p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y);
            bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(neg && pos);
        }

        public static bool Hit(Annotation a, Point p, float slack)
        {
            switch (a.Kind)
            {
                case "text":
                    {
                        Rectangle r = BubbleTextRect(a);
                        r.Inflate((int)slack, (int)slack);
                        return r.Contains(p);
                    }
                case "textbox":
                    {
                        Rectangle r = BubbleRect(a);
                        r.Inflate((int)slack, (int)slack);
                        return r.Contains(p);
                    }
                case "bubble":
                    {
                        Rectangle r = BubbleRect(a);
                        r.Inflate((int)slack, (int)slack);
                        if (r.Contains(p)) return true;
                        PointF[] tail = BubbleTail(a);
                        return tail != null && InTriangle(p, tail[0], tail[1], tail[2]);
                    }
                case "arrow":
                case "arrowfill":
                    return NearSegment(a.P1, a.P2, p, slack + a.Thickness);
                case "connector":
                    {
                        PointF s, c1, c2, e;
                        if (!ConnectorCurve(a, out s, out c1, out c2, out e)) return false;
                        using (GraphicsPath gp = new GraphicsPath())
                        using (Pen wide = new Pen(Color.Black, (slack + a.Thickness) * 2f))
                        {
                            gp.AddBezier(s, c1, c2, e);
                            return gp.IsOutlineVisible(p, wide);
                        }
                    }
                case "mosaic":
                    {
                        Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
                        r.Inflate((int)slack, (int)slack);
                        return r.Contains(p);
                    }
                case "zoom":
                    {
                        Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
                        float rx = r.Width / 2f + slack, ry = r.Height / 2f + slack;
                        if (rx < 1 || ry < 1) return false;
                        float nx = (p.X - (r.Left + r.Width / 2f)) / rx;
                        float ny = (p.Y - (r.Top + r.Height / 2f)) / ry;
                        return nx * nx + ny * ny <= 1f;
                    }
                case "numbox":
                    if (BadgeRect(a).Contains(p)) return true;
                    goto case "box";
                case "box":
                    {
                        Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
                        float tol = slack + a.Thickness;
                        return NearSegment(new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), p, tol)
                            || NearSegment(new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom), p, tol)
                            || NearSegment(new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom), p, tol)
                            || NearSegment(new PointF(r.Left, r.Bottom), new PointF(r.Left, r.Top), p, tol);
                    }
                case "ellipse":
                    {
                        Rectangle r = Norm(a.X1, a.Y1, a.X2, a.Y2);
                        float rx = r.Width / 2f, ry = r.Height / 2f;
                        if (rx < 1 || ry < 1) return false;
                        float nx = (p.X - (r.Left + rx)) / rx, ny = (p.Y - (r.Top + ry)) / ry;
                        double dist = Math.Abs(Math.Sqrt(nx * nx + ny * ny) - 1.0) * Math.Min(rx, ry);
                        return dist <= slack + a.Thickness;
                    }
            }
            return false;
        }

        // 선택했을 때 잡을 수 있는 점들
        public static Point[] Handles(Annotation a)
        {
            if (a.Kind == "bubble") return new Point[] { a.P1 };
            if (a.Kind == "text" || a.Kind == "textbox") return new Point[0];   // 이동만 가능
            if (a.Kind == "connector") return new Point[0];                     // 도형을 따라다닌다
            return new Point[] { a.P1, a.P2 };
        }

        public static bool IsTextKind(string kind)
        {
            return kind == "text" || kind == "textbox" || kind == "bubble";
        }
    }

    // ---------------- 캔버스 ----------------

    public class CanvasControl : Control
    {
        public CanvasControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    // ---------------- 영역 선택 오버레이 ----------------

    public class RegionForm : Form
    {
        private Bitmap screen;
        private Rectangle area;
        private Point? start;
        private Point cur;

        public Bitmap Result;

        public RegionForm()
        {
            area = SystemInformation.VirtualScreen;
            screen = new Bitmap(area.Width, area.Height);
            using (Graphics g = Graphics.FromImage(screen))
                g.CopyFromScreen(area.X, area.Y, 0, 0, screen.Size);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = area;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImage(screen, 0, 0, screen.Width, screen.Height);

            int W = ClientSize.Width, H = ClientSize.Height;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(125, 12, 14, 18)))
            {
                if (start.HasValue)
                {
                    Rectangle r = Painter.Norm(start.Value.X, start.Value.Y, cur.X, cur.Y);
                    g.FillRectangle(dim, 0, 0, W, r.Y);
                    g.FillRectangle(dim, 0, r.Y, r.X, r.Height);
                    g.FillRectangle(dim, r.Right, r.Y, W - r.Right, r.Height);
                    g.FillRectangle(dim, 0, r.Bottom, W, H - r.Bottom);

                    g.SmoothingMode = SmoothingMode.None;
                    using (Pen pen = new Pen(Theme.Accent, Theme.S(2)))
                        g.DrawRectangle(pen, r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height));

                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    DrawHandles(g, r);
                    DrawChip(g, r.Width + " x " + r.Height, r.X, r.Y - Theme.S(38), r);
                }
                else
                {
                    g.FillRectangle(dim, 0, 0, W, H);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    string guide = "드래그해서 캡처할 영역을 선택하세요      Esc 취소";
                    SizeF ts = g.MeasureString(guide, Theme.Overlay);
                    float bw = ts.Width + Theme.S(44), bh = ts.Height + Theme.S(28);
                    float gx = (W - bw) / 2f, gy = H * 0.12f;
                    using (GraphicsPath p = Theme.Round(new RectangleF(gx, gy, bw, bh), Theme.S(12)))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(238, 22, 25, 32)))
                        g.FillPath(b, p);
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(guide, Theme.Overlay, Brushes.White, new RectangleF(gx, gy, bw, bh), sf);
                    }
                }
            }
        }

        private void DrawHandles(Graphics g, Rectangle r)
        {
            float s = Theme.S(8);
            PointF[] pts = new PointF[] {
                new PointF(r.Left, r.Top), new PointF(r.Right, r.Top),
                new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom) };
            using (SolidBrush b = new SolidBrush(Color.White))
            using (Pen p = new Pen(Theme.Accent, Theme.S(2)))
                foreach (PointF pt in pts)
                {
                    RectangleF h = new RectangleF(pt.X - s / 2f, pt.Y - s / 2f, s, s);
                    g.FillEllipse(b, h);
                    g.DrawEllipse(p, h);
                }
        }

        private void DrawChip(Graphics g, string txt, int x, int y, Rectangle sel)
        {
            SizeF ts = g.MeasureString(txt, Theme.Overlay);
            float bw = ts.Width + Theme.S(20), bh = ts.Height + Theme.S(11);
            if (y < 0) y = sel.Y + Theme.S(8);
            using (GraphicsPath p = Theme.Round(new RectangleF(x, y, bw, bh), Theme.S(7)))
            using (SolidBrush b = new SolidBrush(Theme.Accent))
                g.FillPath(b, p);
            g.DrawString(txt, Theme.Overlay, Brushes.White, x + Theme.S(10), y + Theme.S(5));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { start = e.Location; cur = e.Location; Invalidate(); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (start.HasValue) { cur = e.Location; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!start.HasValue) return;
            Rectangle r = Painter.Norm(start.Value.X, start.Value.Y, e.X, e.Y);
            start = null;
            r.Intersect(new Rectangle(0, 0, screen.Width, screen.Height));
            if (r.Width >= 5 && r.Height >= 5)
            {
                Result = screen.Clone(r, screen.PixelFormat);
                Close();
                return;
            }
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && screen != null) { screen.Dispose(); screen = null; }
            base.Dispose(disposing);
        }
    }

    // ---------------- 편집기 ----------------

    public class EditorForm : Form
    {
        private Bitmap image;
        private List<Annotation> shapes = new List<Annotation>();
        private string tool = "box";
        private Color color = Theme.Palette[Theme.DefaultColorIndex];
        private bool outlined;
        private float thickness = Theme.A(3f);
        private float fontSize = Theme.A(19f);
        private float blockSize = Theme.A(14f);
        private float zoomFactor = 2f;
        private int textAlign = 1;   // 일반 텍스트 정렬 : 0 좌, 1 중앙, 2 우
        private FlatButton alignBtn;

        private static readonly string[] AlignIcons = new string[] { "text-align-left", "text-align-center", "text-align-right" };
        private static readonly string[] AlignNames = new string[] { "왼쪽", "중앙", "오른쪽" };

        private static HorizontalAlignment AlignOf(int align)
        {
            if (align == 0) return HorizontalAlignment.Left;
            if (align == 2) return HorizontalAlignment.Right;
            return HorizontalAlignment.Center;
        }

        // 그리기 드래그
        private Point? dragStart;
        private Point dragCur;

        // 자르기 한 단계 되돌리기
        private Bitmap prevImage;
        private Point prevOffset;
        private bool lastActionWasCrop;

        // 배경 프레임 : 0 없음, 1 라이트, 2 다크, 3 선택한 색
        private int frameMode;

        private Color FrameColor()
        {
            if (frameMode == 1) return Color.FromArgb(238, 240, 244);
            if (frameMode == 2) return Color.FromArgb(31, 33, 35);   // 위니브 D-Background #1F2123
            return color;
        }

        private int FramePad()
        {
            return Math.Max(Theme.S(28), (int)Math.Round(Math.Min(image.Width, image.Height) * 0.06));
        }

        private int FrameRadius()
        {
            int r = (int)Math.Round(Math.Min(image.Width, image.Height) * 0.025);
            return Math.Max(Theme.S(7), Math.Min(r, Theme.S(14)));
        }
        private Annotation preview = new Annotation();   // 미리보기는 한 개를 재사용한다 (모자이크 캐시 유지)

        // 선택 / 이동
        private Annotation selected;
        private string grab;            // null, "move", "p1", "p2"
        private Point grabOrigin;
        private Point origP1, origP2;
        private bool grabAll;

        // 연결선 드래그 중 상태
        private Annotation connStart;
        private Annotation connHover;

        // 흐름 박스 : 직전에 그린 박스 (다음 박스와 자동으로 이어진다)
        private Annotation lastFlowBox;
        private Annotation previewConn = new Annotation();

        // 화면 확대 축소 (보기 전용)
        private float viewZoom = 1f;
        private Bitmap viewBuffer;

        // 말풍선 인라인 편집
        private TextBox inlineBox;
        private Annotation editing;
        private bool editingIsNew;
        private string editingBackup;

        private bool centering;
        private Panel board;
        private CanvasControl canvas;
        private Label status;
        private List<FlatButton> toolButtons = new List<FlatButton>();
        private SwatchGrid swatches;
        private List<WidthButton> widths = new List<WidthButton>();
        private List<TextSizeButton> sizes = new List<TextSizeButton>();
        private List<BlockButton> blocks = new List<BlockButton>();
        private List<LabelButton> zooms = new List<LabelButton>();

        private int BarH { get { return Theme.S(62); } }
        private int HandleR { get { return Theme.S(7); } }

        public EditorForm(Bitmap img)
        {
            image = img;
            Text = "쏙캡처 편집  ·  " + img.Width + " x " + img.Height;
            KeyPreview = true;
            Font = Theme.Ui;
            BackColor = Theme.Bar;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            if (Program.Host != null) Icon = Program.Host.Icon;

            BuildCanvas();
            BuildStatus();
            Control bar = BuildToolbar();

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int minW = Math.Min((int)bar.Tag + Theme.S(26), wa.Width - Theme.S(40));
            MinimumSize = new Size(minW, Theme.S(380));

            int cw = Math.Max(minW, Math.Min(img.Width + Theme.S(60), wa.Width - Theme.S(80)));
            int ch = Math.Min(img.Height + Theme.S(160), wa.Height - Theme.S(80));
            ClientSize = new Size(cw, Math.Max(Theme.S(380), ch));

            SyncToolbar();
        }

        // --- 툴바 ---

        private void CenterInBar(Control c)
        {
            int m = Math.Max(0, (BarH - c.Height) / 2);
            c.Margin = new Padding(Theme.S(2), m, Theme.S(2), m);
        }

        private Control MakeSeparator()
        {
            Panel p = new Panel();
            p.Size = new Size(1, Theme.S(24));
            p.BackColor = Theme.Line;
            int m = (BarH - Theme.S(24)) / 2;
            p.Margin = new Padding(Theme.S(6), m, Theme.S(6), m);
            return p;
        }

        private FlatButton MakeTool(string icon, string caption, string kind, string tip)
        {
            FlatButton b = new FlatButton(icon, caption);
            b.Fit(19, 11, 38);
            b.Tag = kind;
            CenterInBar(b);
            b.Activated += delegate { SetTool(kind); };
            if (tip != null) Tip.Attach(b, tip);
            toolButtons.Add(b);
            return b;
        }

        private void SetTool(string kind)
        {
            CommitEdit();
            tool = kind;
            if (kind != "select") selected = null;
            SyncToolbar();
            canvas.Invalidate();

            if (kind == "select") status.Text = "주석을 눌러 고른 뒤 끌어서 옮기세요. 말풍선은 꼬리 끝점을 끌면 방향이 바뀌고, 글자는 두 번 누르면 고칠 수 있어요";
            else if (kind == "crop") status.Text = "자르기 : 남길 영역을 드래그하세요. 자른 뒤 Ctrl+Z 로 되돌릴 수 있어요";
            else if (kind == "text") status.Text = "텍스트 : 넣을 자리를 클릭하면 바로 입력할 수 있어요";
            else if (kind == "textbox") status.Text = "박스 글자 : 넣을 자리를 클릭하면 바로 입력할 수 있어요";
            else if (kind == "bubble") status.Text = "말풍선 : 가리킬 지점을 누른 채, 말풍선 놓을 자리까지 드래그하세요";
            else if (kind == "mosaic") status.Text = "모자이크 : 가릴 영역을 드래그하세요. 거칠기는 오른쪽 격자 버튼으로 고르면 돼요";
            else if (kind == "zoom") status.Text = "돋보기 : 확대할 영역을 드래그하세요. 배율은 오른쪽 1.5x / 2x / 3x 로 고르면 돼요";
            else if (kind == "numbox") status.Text = "숫자 박스 : 드래그할 때마다 1, 2, 3 번호가 차례로 붙어요. 중간을 지우면 뒤 번호가 자동으로 당겨져요";
            else if (kind == "connector") status.Text = "연결선 : 도형 안에서 시작해 다른 도형 안에서 끝내세요. 나중에 도형을 옮겨도 곡선이 따라와요";
            else if (kind == "flowbox") status.Text = "흐름 박스 : 박스를 그릴 때마다 이전 박스와 자동으로 이어져요.    Esc = 새 흐름 시작";
            else status.Text = "이미지 위에 드래그해서 그리세요";
        }

        private Control BuildToolbar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = BarH;
            bar.BackColor = Theme.Bar;
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Line))
                    e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            FlowLayoutPanel left = new FlowLayoutPanel();
            left.Dock = DockStyle.Left;
            left.AutoSize = true;
            left.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            left.WrapContents = false;
            left.Padding = new Padding(Theme.S(8), 0, 0, 0);
            left.BackColor = Theme.Bar;

            FlatButton bNew = new FlatButton("selection", "새 캡처");
            bNew.Fit(19, 11, 38);
            CenterInBar(bNew);
            bNew.Activated += delegate { CommitEdit(); Shooter.Region(this); };
            Tip.Attach(bNew, "이 창을 숨기고 새로 캡처해요");
            left.Controls.Add(bNew);
            left.Controls.Add(MakeTool("crop", "자르기", "crop", "남길 영역을 드래그하면 이미지가 잘려요"));
            left.Controls.Add(MakeSeparator());

            left.Controls.Add(MakeTool("cursor", "선택", "select", "주석을 고르고 옮겨요"));
            left.Controls.Add(MakeTool("square", "박스", "box", "사각형으로 강조"));
            left.Controls.Add(MakeTool("number-circle-one", "숫자 박스", "numbox", "그리는 순서대로 1, 2, 3 번호가 붙어요"));
            left.Controls.Add(MakeTool("circle", "원", "ellipse", "원, 타원으로 강조"));
            left.Controls.Add(MakeTool("arrow-up-right", "화살표", "arrow", "열린 갈매기 머리 화살표"));
            left.Controls.Add(MakeTool("arrow-up-right-fill", "채움 화살표", "arrowfill", "삼각형이 꽉 찬 머리 화살표"));
            left.Controls.Add(MakeTool("flow-arrow", "연결선", "connector", "도형에서 도형으로 드래그하면 곡선 화살표로 이어져요"));
            left.Controls.Add(MakeTool("tree-structure", "흐름 박스", "flowbox", "박스를 그릴 때마다 이전 박스와 곡선 화살표로 자동 연결"));
            left.Controls.Add(MakeTool("text-t", "텍스트", "text", "배경 없는 글자. 클릭한 자리에서 바로 입력"));
            left.Controls.Add(MakeTool("textbox", "박스 글자", "textbox", "색이 깔린 글자. 클릭한 자리에서 바로 입력"));
            left.Controls.Add(MakeTool("chat-teardrop", "말풍선", "bubble", "가리킬 지점에서 말풍선 자리까지 드래그"));
            left.Controls.Add(MakeTool("grid-four", "모자이크", "mosaic", "가릴 영역을 드래그해서 뭉개요"));
            left.Controls.Add(MakeTool("magnifying-glass-plus", "돋보기", "zoom", "동그란 영역을 드래그하면 그 안이 확대돼요"));
            left.Controls.Add(MakeSeparator());

            swatches = new SwatchGrid(Theme.Palette, Theme.PaletteColumns, Theme.DefaultColorIndex);
            CenterInBar(swatches);
            swatches.Activated += delegate
            {
                color = swatches.Selected;
                outlined = swatches.SelectedIsOutline;
                Annotation target = (editing != null) ? editing : selected;
                if (target != null)
                {
                    target.Color = color;
                    target.Outlined = outlined;
                    if (inlineBox != null && editing != null) SyncInlineColors(editing);
                    canvas.Invalidate();
                }
                if (frameMode == 3) { board.Invalidate(); canvas.Invalidate(); }
            };
            left.Controls.Add(swatches);
            left.Controls.Add(MakeSeparator());

            float[] widthValues = new float[] { Theme.A(2f), Theme.A(3f), Theme.A(5f) };
            string[] widthTips = new string[] { "가는 선", "보통 선", "굵은 선" };
            for (int i = 0; i < widthValues.Length; i++)
            {
                WidthButton wb = new WidthButton(widthValues[i]);
                CenterInBar(wb);
                wb.Activated += delegate(object s, EventArgs e)
                {
                    WidthButton hit = (WidthButton)s;
                    thickness = hit.Value;
                    if (selected != null && selected.Kind != "bubble") { selected.Thickness = thickness; canvas.Invalidate(); }
                    SyncToolbar();
                };
                Tip.Attach(wb, widthTips[i]);
                left.Controls.Add(wb);
                widths.Add(wb);
            }

            float[] sizeValues = new float[] { Theme.A(14f), Theme.A(19f), Theme.A(27f) };
            int[] glyphPx = new int[] { 11, 14, 18 };
            string[] sizeTips = new string[] { "작은 글자", "보통 글자", "큰 글자" };
            for (int i = 0; i < sizeValues.Length; i++)
            {
                TextSizeButton sb = new TextSizeButton(sizeValues[i], glyphPx[i]);
                sb.Visible = false;
                CenterInBar(sb);
                sb.Activated += delegate(object s, EventArgs e)
                {
                    TextSizeButton hit = (TextSizeButton)s;
                    fontSize = hit.Value;
                    Annotation target = (editing != null) ? editing : selected;
                    if (target != null && (Painter.IsTextKind(target.Kind) || target.Kind == "numbox"))
                    {
                        target.FontSize = fontSize;
                        if (inlineBox != null && editing != null) { inlineBox.Font = Theme.Bubble(fontSize * viewZoom); LayoutInline(); }
                        canvas.Invalidate();
                    }
                    SyncToolbar();
                };
                Tip.Attach(sb, sizeTips[i]);
                left.Controls.Add(sb);
                sizes.Add(sb);
            }

            // 일반 텍스트 정렬 : 누를 때마다 좌 -> 중앙 -> 우 순환
            alignBtn = new FlatButton(AlignIcons[textAlign], null);
            alignBtn.Fit(19, 11, 38);
            alignBtn.Visible = false;
            CenterInBar(alignBtn);
            alignBtn.Activated += delegate
            {
                textAlign = (textAlign + 1) % 3;
                alignBtn.Icon = AlignIcons[textAlign];
                alignBtn.Invalidate();

                Annotation target = (editing != null) ? editing : selected;
                if (target != null && target.Kind == "text")
                {
                    target.Align = textAlign;
                    if (inlineBox != null && editing != null) inlineBox.TextAlign = AlignOf(textAlign);
                    canvas.Invalidate();
                }
                status.Text = "텍스트 정렬 : " + AlignNames[textAlign] + "  (여러 줄일 때 줄이 맞춰지는 기준이에요)";
            };
            Tip.Attach(alignBtn, "텍스트 정렬 (누를 때마다 왼쪽, 중앙, 오른쪽 순환)");
            left.Controls.Add(alignBtn);

            float[] blockValues = new float[] { Theme.A(8f), Theme.A(14f), Theme.A(24f) };
            int[] blockCells = new int[] { 6, 4, 3 };
            string[] blockTips = new string[] { "곱게", "보통", "굵게" };
            for (int i = 0; i < blockValues.Length; i++)
            {
                BlockButton bb = new BlockButton(blockValues[i], blockCells[i]);
                bb.Visible = false;
                CenterInBar(bb);
                bb.Activated += delegate(object s, EventArgs e)
                {
                    BlockButton hit = (BlockButton)s;
                    blockSize = hit.Value;
                    Annotation target = selected;
                    if (target != null && target.Kind == "mosaic")
                    {
                        target.Block = blockSize;
                        canvas.Invalidate();
                    }
                    SyncToolbar();
                };
                Tip.Attach(bb, "모자이크 " + blockTips[i]);
                left.Controls.Add(bb);
                blocks.Add(bb);
            }

            float[] zoomValues = new float[] { 1.5f, 2f, 3f };
            string[] zoomLabels = new string[] { "1.5x", "2x", "3x" };
            for (int i = 0; i < zoomValues.Length; i++)
            {
                LabelButton zb = new LabelButton(zoomValues[i], zoomLabels[i]);
                zb.Visible = false;
                CenterInBar(zb);
                zb.Activated += delegate(object s, EventArgs e)
                {
                    LabelButton hit = (LabelButton)s;
                    zoomFactor = hit.Value;
                    if (selected != null && selected.Kind == "zoom")
                    {
                        selected.Zoom = zoomFactor;
                        canvas.Invalidate();
                    }
                    SyncToolbar();
                };
                Tip.Attach(zb, "돋보기 배율 " + zoomLabels[i]);
                left.Controls.Add(zb);
                zooms.Add(zb);
            }
            left.Controls.Add(MakeSeparator());

            FlatButton bUndo = new FlatButton("arrow-counter-clockwise", null);
            bUndo.Fit(19, 11, 38);
            CenterInBar(bUndo);
            bUndo.Activated += delegate { Undo(); };
            Tip.Attach(bUndo, "되돌리기 (Ctrl+Z)");
            left.Controls.Add(bUndo);

            FlatButton bClear = new FlatButton("trash", null);
            bClear.Fit(19, 11, 38);
            CenterInBar(bClear);
            bClear.Activated += delegate { ClearAll(); };
            Tip.Attach(bClear, "주석 모두 지우기");
            left.Controls.Add(bClear);

            FlowLayoutPanel right = new FlowLayoutPanel();
            right.Dock = DockStyle.Right;
            right.AutoSize = true;
            right.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            right.WrapContents = false;
            right.Padding = new Padding(0, 0, Theme.S(8), 0);
            right.BackColor = Theme.Bar;

            FlatButton bFrame = new FlatButton("frame-corners", "프레임");
            bFrame.Fit(19, 12, 38);
            CenterInBar(bFrame);
            bFrame.Activated += delegate(object s, EventArgs e)
            {
                frameMode = (frameMode + 1) % 4;
                FlatButton fb = (FlatButton)s;
                fb.Checked = frameMode > 0;
                fb.Invalidate();
                CenterCanvas();
                board.Invalidate();
                canvas.Invalidate();
                string[] names = new string[] { "없음", "라이트", "다크", "선택한 색" };
                status.Text = "배경 프레임 : " + names[frameMode] +
                              (frameMode > 0 ? "    복사, 저장할 때 여백과 둥근 모서리가 함께 나가요" : "");
            };
            Tip.Attach(bFrame, "캡처 둘레에 배경을 둘러요 (누를 때마다 없음, 라이트, 다크, 선택한 색 순환)");
            right.Controls.Add(bFrame);

            FlatButton bCopy = new FlatButton("copy", "복사");
            bCopy.Fit(19, 12, 38);
            CenterInBar(bCopy);
            bCopy.Activated += delegate { CopyToClipboard(); };
            Tip.Attach(bCopy, "클립보드로 복사 (Ctrl+C)");
            right.Controls.Add(bCopy);

            FlatButton bSave = new FlatButton("download-simple", "저장");
            bSave.Primary = true;
            bSave.Fit(19, 12, 38);
            CenterInBar(bSave);
            bSave.Activated += delegate { SaveToFile(); };
            Tip.Attach(bSave, "PNG, JPG로 저장 (Ctrl+S)");
            right.Controls.Add(bSave);

            bar.Controls.Add(left);
            bar.Controls.Add(right);
            Controls.Add(bar);

            // 고배율 화면에서 툴바가 화면보다 넓어지면 버튼 이름을 떼고 아이콘만 남긴다
            int room = Screen.PrimaryScreen.WorkingArea.Width - Theme.S(60);
            if (left.PreferredSize.Width + right.PreferredSize.Width > room)
            {
                left.SuspendLayout();
                right.SuspendLayout();
                List<FlatButton> all = new List<FlatButton>(toolButtons);
                all.Add(bNew); all.Add(bUndo); all.Add(bClear);
                all.Add(bFrame); all.Add(bCopy); all.Add(bSave);
                foreach (FlatButton b in all)
                {
                    if (b.Caption == null) continue;
                    // 아이콘만 남기는 대신, 툴팁 앞에 버튼 이름을 붙여준다
                    string cur = Tip.TextOf(b);
                    Tip.Attach(b, (cur != null && cur.Length > 0) ? (b.Caption + "  ·  " + cur) : b.Caption);
                    b.Caption = null;
                    b.Fit(19, 10, 38);
                    CenterInBar(b);
                }
                left.ResumeLayout(true);
                right.ResumeLayout(true);
            }

            // 최소 폭은 가장 넓은 모드(텍스트 = 크기 3개 + 정렬 버튼) 기준으로 잡는다
            bar.Tag = left.PreferredSize.Width + right.PreferredSize.Width
                      + alignBtn.Width + alignBtn.Margin.Horizontal;
            return bar;
        }

        private void SyncToolbar()
        {
            // 선택 도구일 때는 고른 주석의 종류를 따라간다
            string mode = tool;
            if (editing != null) mode = "bubble";
            else if (tool == "select" && selected != null) mode = selected.Kind;

            bool bubbleMode = Painter.IsTextKind(mode) || (mode == "numbox");   // 글자 크기를 쓰는 도구들
            bool mosaicMode = (mode == "mosaic");
            bool zoomMode = (mode == "zoom");

            foreach (FlatButton b in toolButtons) { b.Checked = ((string)b.Tag == tool); b.Invalidate(); }
            swatches.SelectColor(color, outlined);
            swatches.Enabled = !mosaicMode;
            // 어느 모드에서든 이 자리에는 3개짜리 그룹 하나만 보인다 (툴바 너비를 일정하게 유지)
            foreach (WidthButton w in widths) { w.Checked = (w.Value == thickness); w.Visible = !bubbleMode && !mosaicMode && !zoomMode; w.Invalidate(); }
            foreach (TextSizeButton t in sizes) { t.Checked = (t.Value == fontSize); t.Visible = bubbleMode; t.Invalidate(); }
            if (alignBtn != null)
            {
                alignBtn.Visible = (mode == "text");
                alignBtn.Icon = AlignIcons[textAlign];
                alignBtn.Invalidate();
            }
            foreach (BlockButton b in blocks) { b.Checked = (b.Value == blockSize); b.Visible = mosaicMode; b.Invalidate(); }
            foreach (LabelButton z in zooms) { z.Checked = (z.Value == zoomFactor); z.Visible = zoomMode; z.Invalidate(); }
        }

        // --- 상태 표시줄 ---

        private void BuildStatus()
        {
            status = new Label();
            status.Dock = DockStyle.Bottom;
            status.Height = Theme.S(32);
            status.Padding = new Padding(Theme.S(14), Theme.S(8), 0, 0);
            status.BackColor = Theme.Bar;
            status.ForeColor = Theme.Muted;
            status.Text = "도구를 고르고 이미지 위에 드래그하세요.    Ctrl+Z 되돌리기 · Ctrl+C 복사 · Ctrl+S 저장";
            Controls.Add(status);
        }

        // --- 캔버스 ---

        private void BuildCanvas()
        {
            board = new Panel();
            board.Dock = DockStyle.Fill;
            board.AutoScroll = true;
            board.BackColor = Theme.Board;
            board.Resize += delegate { CenterCanvas(); };
            board.Paint += delegate(object s, PaintEventArgs e)
            {
                Rectangle rb = canvas.Bounds;
                if (frameMode > 0)
                {
                    // 프레임 여백 미리보기
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle fr = rb;
                    fr.Inflate(FramePad(), FramePad());
                    using (GraphicsPath p = Theme.Round(fr, Theme.S(10)))
                    using (SolidBrush b = new SolidBrush(FrameColor()))
                        e.Graphics.FillPath(b, p);
                    rb = fr;
                }
                rb.Inflate(1, 1);
                using (Pen p = new Pen(Color.FromArgb(120, 0, 0, 0)))
                    e.Graphics.DrawRectangle(p, rb);
            };

            canvas = new CanvasControl();
            canvas.Size = new Size(image.Width, image.Height);
            canvas.Cursor = Cursors.Cross;
            canvas.Paint += CanvasPaint;
            canvas.MouseDown += CanvasMouseDown;
            canvas.MouseMove += CanvasMouseMove;
            canvas.MouseUp += CanvasMouseUp;
            canvas.MouseDoubleClick += CanvasDoubleClick;
            canvas.MouseWheel += CanvasWheel;
            board.MouseWheel += CanvasWheel;

            board.Controls.Add(canvas);
            Controls.Add(board);
        }

        private void CenterCanvas()
        {
            if (centering || canvas == null) return;
            centering = true;
            try
            {
                board.AutoScrollPosition = new Point(0, 0);
                int pad = Theme.S(20) + (frameMode > 0 ? FramePad() : 0);
                int mx = Math.Max(pad, (board.ClientSize.Width - canvas.Width) / 2);
                int my = Math.Max(pad, (board.ClientSize.Height - canvas.Height) / 2);
                Point want = new Point(mx, my);
                if (canvas.Location != want) canvas.Location = want;
                board.Invalidate();
            }
            finally { centering = false; }
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); CenterCanvas(); }

        private void CanvasPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            bool hasPreview = PreparePreview();

            if (viewZoom == 1f)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(image, 0, 0, image.Width, image.Height);
                foreach (Annotation a in shapes) Painter.Draw(g, a, image);
                if (hasPreview) DrawPreview(g);
            }
            else
            {
                // 글자(TextRenderer)는 좌표 변환을 무시하므로, 장면을 원본 배율 버퍼에 그린 뒤 통째로 확대 축소한다
                // 드래그 미리보기도 버퍼 안에서 그려야 어느 배율에서든 배지 숫자, 말풍선 글자가 온전히 보인다
                EnsureViewBuffer();
                using (Graphics pg = Graphics.FromImage(viewBuffer))
                {
                    pg.SmoothingMode = SmoothingMode.AntiAlias;
                    pg.DrawImage(image, 0, 0, image.Width, image.Height);
                    foreach (Annotation a in shapes) Painter.Draw(pg, a, image);
                    if (hasPreview) DrawPreview(pg);
                }
                Rectangle clip = e.ClipRectangle;
                if (clip.Width <= 0 || clip.Height <= 0) return;
                g.InterpolationMode = (viewZoom >= 2f) ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(viewBuffer,
                    new RectangleF(clip.X, clip.Y, clip.Width, clip.Height),
                    new RectangleF(clip.X / viewZoom, clip.Y / viewZoom, clip.Width / viewZoom, clip.Height / viewZoom),
                    GraphicsUnit.Pixel);
            }

            // 오버레이(자르기, 미리보기, 선택 표시)는 이미지 좌표로 그리고 배율만 걸어준다
            GraphicsState ost = g.Save();
            if (viewZoom != 1f) g.ScaleTransform(viewZoom, viewZoom);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 자르기 드래그 중에는 남길 영역만 밝게 보여준다
            if (dragStart.HasValue && tool == "crop")
            {
                Rectangle cr = Painter.Norm(dragStart.Value.X, dragStart.Value.Y, dragCur.X, dragCur.Y);
                using (SolidBrush dim = new SolidBrush(Color.FromArgb(115, 12, 14, 18)))
                {
                    int W = image.Width, H = image.Height;
                    g.FillRectangle(dim, 0, 0, W, cr.Y);
                    g.FillRectangle(dim, 0, cr.Y, cr.X, cr.Height);
                    g.FillRectangle(dim, cr.Right, cr.Y, W - cr.Right, cr.Height);
                    g.FillRectangle(dim, 0, cr.Bottom, W, H - cr.Bottom);
                }
                using (Pen pen = new Pen(Theme.Accent, Theme.S(2)))
                    g.DrawRectangle(pen, cr.X, cr.Y, Math.Max(1, cr.Width), Math.Max(1, cr.Height));

                string txt = cr.Width + " x " + cr.Height;
                SizeF ts = g.MeasureString(txt, Theme.Overlay);
                float ly = cr.Y - ts.Height - Theme.S(12);
                if (ly < 0) ly = cr.Y + Theme.S(8);
                using (GraphicsPath chip = Theme.Round(new RectangleF(cr.X, ly, ts.Width + Theme.S(18), ts.Height + Theme.S(10)), Theme.S(6)))
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, chip);
                g.DrawString(txt, Theme.Overlay, Brushes.White, cr.X + Theme.S(9), ly + Theme.S(5));
                g.Restore(ost);
                return;
            }

            if (selected != null && editing == null && shapes.Contains(selected))
                DrawSelection(g, selected);

            // 프레임이 켜져 있으면 이미지 모서리를 프레임 색으로 둥글게 깎아 보여준다
            if (frameMode > 0)
            {
                using (GraphicsPath rp = Theme.Round(new RectangleF(0, 0, image.Width, image.Height), FrameRadius()))
                using (Region rg = new Region(new Rectangle(0, 0, image.Width, image.Height)))
                using (SolidBrush b = new SolidBrush(FrameColor()))
                using (Pen smooth = new Pen(FrameColor(), 2f))
                {
                    rg.Exclude(rp);
                    g.FillRegion(b, rg);
                    g.DrawPath(smooth, rp);   // 계단 현상을 눌러준다
                }
            }

            g.Restore(ost);
        }

        // 드래그 중인 도형의 미리보기 준비. 그릴 게 있으면 true
        private bool PreparePreview()
        {
            if (!dragStart.HasValue) return false;
            if (tool == "select" || tool == "crop" || tool == "text" || tool == "textbox") return false;

            Point end = dragCur;
            if (SquareConstrain(tool)) end = Squared(dragStart.Value, dragCur);
            else if (AxisConstrain(tool)) end = AxisSnap(dragStart.Value, dragCur);
            preview.Kind = (tool == "flowbox") ? "box" : tool;
            preview.Editing = false;
            preview.Outlined = outlined;
            preview.RefA = (tool == "connector") ? connStart : null;
            preview.RefB = (tool == "connector" && connHover != connStart) ? connHover : null;
            preview.X1 = dragStart.Value.X; preview.Y1 = dragStart.Value.Y;
            preview.X2 = end.X; preview.Y2 = end.Y;
            preview.Color = color;
            preview.Thickness = thickness;
            preview.FontSize = fontSize;
            preview.Block = blockSize;
            preview.Zoom = zoomFactor;
            preview.Text = (tool == "bubble") ? "텍스트" : "";
            if (tool == "numbox")
            {
                int n = 0;
                foreach (Annotation a in shapes) if (a.Kind == "numbox") n++;
                preview.Number = n + 1;
            }
            return true;
        }

        private void DrawPreview(Graphics g)
        {
            Painter.Draw(g, preview, image);

            // 흐름 박스 : 이전 박스에서 그리는 중인 박스까지 이어질 곡선도 미리 보여준다
            if (tool == "flowbox" && lastFlowBox != null && shapes.Contains(lastFlowBox))
            {
                previewConn.Kind = "connector";
                previewConn.RefA = lastFlowBox;
                previewConn.RefB = preview;
                previewConn.Color = color;
                previewConn.Thickness = thickness;
                previewConn.Outlined = outlined;
                Painter.Draw(g, previewConn, image);
            }
        }

        // ---------------- 화면 확대 축소 (보기 전용, 저장 결과와 무관) ----------------

        private void EnsureViewBuffer()
        {
            if (viewBuffer == null || viewBuffer.Width != image.Width || viewBuffer.Height != image.Height)
            {
                if (viewBuffer != null) viewBuffer.Dispose();
                viewBuffer = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
            }
        }

        // 캔버스 좌표 -> 이미지 좌표
        private Point ToImg(Point p)
        {
            if (viewZoom == 1f) return p;
            return new Point((int)Math.Round(p.X / viewZoom), (int)Math.Round(p.Y / viewZoom));
        }

        private void SetViewZoom(float z, Point? anchorImg)
        {
            z = Math.Max(0.2f, Math.Min(4f, z));
            if (Math.Abs(z - 1f) < 0.05f) z = 1f;
            if (Math.Abs(z - viewZoom) < 0.001f) return;
            CommitEdit();

            // 앵커(이미지 좌표)가 화면에서 같은 자리에 머물도록 스크롤을 맞춘다
            PointF anchor = anchorImg.HasValue
                ? new PointF(anchorImg.Value.X, anchorImg.Value.Y)
                : new PointF((board.ClientSize.Width / 2f - canvas.Location.X) / viewZoom,
                             (board.ClientSize.Height / 2f - canvas.Location.Y) / viewZoom);
            PointF anchorBoard = new PointF(canvas.Location.X + anchor.X * viewZoom,
                                            canvas.Location.Y + anchor.Y * viewZoom);

            viewZoom = z;
            canvas.Size = new Size((int)Math.Round(image.Width * z), (int)Math.Round(image.Height * z));
            CenterCanvas();

            int ox = (int)Math.Round(canvas.Location.X + anchor.X * z - anchorBoard.X);
            int oy = (int)Math.Round(canvas.Location.Y + anchor.Y * z - anchorBoard.Y);
            board.AutoScrollPosition = new Point(Math.Max(0, ox), Math.Max(0, oy));

            board.Invalidate();
            canvas.Invalidate();
            status.Text = "화면 배율 " + (int)Math.Round(viewZoom * 100) + "%    Ctrl+휠, +/- 로 조절 · Ctrl+0 은 100% (저장 결과에는 영향 없어요)";
        }

        private void CanvasWheel(object sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) != Keys.Control) return;
            HandledMouseEventArgs h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;

            Point cp = canvas.PointToClient(Cursor.Position);
            Point ip = ToImg(cp);
            SetViewZoom(viewZoom * ((e.Delta > 0) ? 1.15f : 1f / 1.15f), ip);
        }

        private void DrawSelection(Graphics g, Annotation a)
        {
            Rectangle box;
            if (a.Kind == "bubble" || a.Kind == "textbox") box = Painter.BubbleRect(a);
            else if (a.Kind == "text") box = Painter.BubbleTextRect(a);
            else box = Painter.Norm(a.X1, a.Y1, a.X2, a.Y2);
            box.Inflate(Theme.S(5), Theme.S(5));

            using (Pen dash = new Pen(Theme.Accent, Theme.S(1.4f)))
            {
                dash.DashStyle = DashStyle.Dash;
                dash.DashPattern = new float[] { 4f, 3f };
                if (a.Kind == "arrow" || a.Kind == "arrowfill") g.DrawLine(dash, a.P1, a.P2);
                else if (a.Kind == "connector")
                {
                    PointF cs, cc1, cc2, ce;
                    if (Painter.ConnectorCurve(a, out cs, out cc1, out cc2, out ce))
                        g.DrawBezier(dash, cs, cc1, cc2, ce);
                }
                else g.DrawRectangle(dash, box);
            }

            float r = HandleR;
            using (SolidBrush b = new SolidBrush(Color.White))
            using (Pen p = new Pen(Theme.Accent, Theme.S(2)))
                foreach (Point h in Painter.Handles(a))
                {
                    RectangleF hr = new RectangleF(h.X - r, h.Y - r, r * 2, r * 2);
                    g.FillEllipse(b, hr);
                    g.DrawEllipse(p, hr);
                }
        }

        // 돋보기는 항상, 박스 원 숫자박스는 Shift 를 누른 동안 정비율로 그린다
        private static bool SquareConstrain(string kind)
        {
            if (kind == "zoom") return true;
            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                return kind == "box" || kind == "ellipse" || kind == "numbox" || kind == "flowbox";
            return false;
        }

        // 화살표는 Shift 를 누른 동안 수평 또는 수직으로만 그려진다
        private static bool AxisConstrain(string kind)
        {
            if ((Control.ModifierKeys & Keys.Shift) != Keys.Shift) return false;
            return kind == "arrow" || kind == "arrowfill";
        }

        // 더 많이 움직인 축으로 스냅한다
        private static Point AxisSnap(Point fixedPt, Point moving)
        {
            int dx = Math.Abs(moving.X - fixedPt.X), dy = Math.Abs(moving.Y - fixedPt.Y);
            return (dx >= dy) ? new Point(moving.X, fixedPt.Y) : new Point(fixedPt.X, moving.Y);
        }

        // 끄는 방향은 유지한 채 가로세로를 같게 맞춘다
        private static Point Squared(Point fixedPt, Point moving)
        {
            int dx = moving.X - fixedPt.X, dy = moving.Y - fixedPt.Y;
            int side = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new Point(fixedPt.X + (dx < 0 ? -side : side),
                             fixedPt.Y + (dy < 0 ? -side : side));
        }

        private Annotation HitAt(Point p)
        {
            for (int i = shapes.Count - 1; i >= 0; i--)
                if (Painter.Hit(shapes[i], p, Theme.S(6))) return shapes[i];
            return null;
        }

        // 연결선을 붙일 수 있는 도형 찾기 (안쪽을 눌러도 잡힌다)
        private Annotation ConnectableAt(Point p)
        {
            for (int i = shapes.Count - 1; i >= 0; i--)
            {
                Annotation a = shapes[i];
                Rectangle r;
                if (a.Kind == "box" || a.Kind == "numbox" || a.Kind == "ellipse")
                    r = Painter.Norm(a.X1, a.Y1, a.X2, a.Y2);
                else if (a.Kind == "textbox" || a.Kind == "bubble")
                    r = Painter.BubbleRect(a);
                else continue;
                r.Inflate(Theme.S(4), Theme.S(4));
                if (r.Contains(p)) return a;
            }
            return null;
        }

        private string HandleAt(Annotation a, Point p)
        {
            if (a == null) return null;
            float r = HandleR + Theme.S(3);
            Point[] hs = Painter.Handles(a);
            for (int i = 0; i < hs.Length; i++)
            {
                float dx = p.X - hs[i].X, dy = p.Y - hs[i].Y;
                if (dx * dx + dy * dy <= r * r)
                    return (a.Kind == "bubble") ? "p1" : (i == 0 ? "p1" : "p2");
            }
            return null;
        }

        // --- 말풍선 인라인 편집 ---

        private void BeginEdit(Annotation a, bool isNew)
        {
            CommitEdit();

            editing = a;
            editingIsNew = isNew;
            editingBackup = a.Text;
            a.Editing = true;
            selected = a;

            color = a.Color;
            outlined = a.Outlined;
            fontSize = a.FontSize;
            SyncToolbar();

            inlineBox = new TextBox();
            inlineBox.Multiline = true;
            inlineBox.BorderStyle = BorderStyle.None;
            inlineBox.WordWrap = true;
            inlineBox.ScrollBars = ScrollBars.None;
            if (a.Kind == "text") textAlign = a.Align;
            inlineBox.TextAlign = (a.Kind == "text") ? AlignOf(a.Align) : HorizontalAlignment.Center;
            inlineBox.AcceptsReturn = true;
            inlineBox.Font = Theme.Bubble(a.FontSize * viewZoom);
            SyncInlineColors(a);
            inlineBox.Text = a.Text;
            inlineBox.TextChanged += delegate { LayoutInline(); };
            inlineBox.KeyDown += InlineKeyDown;

            canvas.Controls.Add(inlineBox);
            LayoutInline();
            inlineBox.Focus();
            inlineBox.SelectionStart = inlineBox.TextLength;

            status.Text = "말풍선에 문구를 입력하세요.    Esc 취소 · 빈 곳을 클릭하거나 Ctrl+Enter 로 완료";
        }

        // 인라인 입력 상자의 색을 주석 종류에 맞춘다
        private void SyncInlineColors(Annotation a)
        {
            if (inlineBox == null) return;
            if (a.Kind == "text")
            {
                // 배경 없는 텍스트는 입력 중에만 흰 상자가 보인다
                inlineBox.BackColor = Color.White;
                double lum = (0.299 * a.Color.R + 0.587 * a.Color.G + 0.114 * a.Color.B) / 255.0;
                inlineBox.ForeColor = (lum > 0.75) ? Painter.OutlineInk : a.Color;
            }
            else
            {
                inlineBox.BackColor = a.Color;
                inlineBox.ForeColor = Painter.InkFor(a.Color);
            }
        }

        private void LayoutInline()
        {
            if (editing == null || inlineBox == null) return;
            editing.Text = inlineBox.Text;
            Rectangle t = Painter.BubbleTextRect(editing);
            // 확대 축소 상태에서는 입력 상자도 화면 배율에 맞춘다
            inlineBox.SetBounds(
                (int)Math.Round(t.X * viewZoom) - 2,
                (int)Math.Round(t.Y * viewZoom) - 1,
                (int)Math.Round(t.Width * viewZoom) + 4,
                (int)Math.Round(t.Height * viewZoom) + 2);
            canvas.Invalidate();
        }

        private void InlineKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { CancelEdit(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Enter) { CommitEdit(); e.SuppressKeyPress = true; }
        }

        private void DropInline()
        {
            if (inlineBox == null) return;
            canvas.Controls.Remove(inlineBox);
            inlineBox.Dispose();
            inlineBox = null;
        }

        private void CommitEdit()
        {
            if (editing == null) return;
            Annotation a = editing;
            if (inlineBox != null) a.Text = inlineBox.Text.TrimEnd();
            DropInline();
            a.Editing = false;
            editing = null;

            if (a.Text.Trim().Length == 0) { shapes.Remove(a); selected = null; }
            else
            {
                // 바로 위치를 다듬을 수 있게 선택 도구로 넘어간다
                selected = a;
                tool = "select";
            }
            SyncToolbar();
            canvas.Invalidate();
            canvas.Focus();
            if (selected != null)
                status.Text = (selected.Kind == "bubble")
                    ? "말풍선을 끌어 옮기거나, 꼬리 끝점을 끌어 방향을 바꿔보세요. 두 번 누르면 글자 수정"
                    : "끌어서 옮길 수 있어요. 두 번 누르면 글자 수정";
        }

        private void CancelEdit()
        {
            if (editing == null) return;
            Annotation a = editing;
            DropInline();
            a.Editing = false;
            a.Text = editingBackup;
            editing = null;
            if (editingIsNew || a.Text.Trim().Length == 0) { shapes.Remove(a); selected = null; }
            SyncToolbar();
            canvas.Invalidate();
            canvas.Focus();
            status.Text = "말풍선 편집을 취소했어요";
        }

        // --- 마우스 ---

        private void CanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            canvas.Focus();
            Point ip = ToImg(e.Location);

            if (editing != null)
            {
                CommitEdit();
                if (tool != "select") return;
            }

            if (tool == "select")
            {
                string h = HandleAt(selected, ip);
                if (h == null)
                {
                    Annotation hit = HitAt(ip);
                    if (hit != selected) { selected = hit; SyncToolbar(); }
                    if (selected == null) { canvas.Invalidate(); return; }
                    h = "move";
                    if (selected != null)
                    {
                        color = selected.Color;
                        outlined = selected.Outlined;
                        if (selected.Kind == "text") textAlign = selected.Align;
                        if (Painter.IsTextKind(selected.Kind) || selected.Kind == "numbox") { fontSize = selected.FontSize; thickness = selected.Thickness; }
                        else if (selected.Kind == "mosaic") blockSize = selected.Block;
                        else if (selected.Kind == "zoom") { zoomFactor = selected.Zoom; thickness = selected.Thickness; }
                        else thickness = selected.Thickness;
                        SyncToolbar();
                    }
                }
                grab = h;
                grabAll = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                grabOrigin = ip;
                origP1 = selected.P1;
                origP2 = selected.P2;
                canvas.Invalidate();
                return;
            }

            if (Painter.IsTextKind(tool))
            {
                Annotation hit = HitAt(ip);
                if (hit != null && Painter.IsTextKind(hit.Kind)) { BeginEdit(hit, false); return; }
            }

            if (tool == "connector")
            {
                connStart = ConnectableAt(ip);
                connHover = null;
            }

            dragStart = ip;
            dragCur = ip;
            canvas.Invalidate();
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            Point ip = ToImg(e.Location);

            if (tool == "select")
            {
                if (grab != null && selected != null)
                {
                    int dx = ip.X - grabOrigin.X, dy = ip.Y - grabOrigin.Y;
                    bool round = SquareConstrain(selected.Kind);
                    bool axis = AxisConstrain(selected.Kind);
                    if (grab == "p1")
                    {
                        Point np = new Point(origP1.X + dx, origP1.Y + dy);
                        if (round) np = Squared(selected.P2, np);
                        else if (axis) np = AxisSnap(selected.P2, np);
                        selected.X1 = np.X; selected.Y1 = np.Y;
                    }
                    else if (grab == "p2")
                    {
                        Point np = new Point(origP2.X + dx, origP2.Y + dy);
                        if (round) np = Squared(selected.P1, np);
                        else if (axis) np = AxisSnap(selected.P1, np);
                        selected.X2 = np.X; selected.Y2 = np.Y;
                    }
                    else
                    {
                        // 말풍선은 본체만 옮기고 꼬리 끝은 가리키던 곳에 남는다 (Shift 로 함께 이동)
                        bool moveBoth = (selected.Kind != "bubble") || grabAll;
                        selected.X2 = origP2.X + dx; selected.Y2 = origP2.Y + dy;
                        if (moveBoth) { selected.X1 = origP1.X + dx; selected.Y1 = origP1.Y + dy; }
                    }
                    canvas.Invalidate();
                }
                else
                {
                    Cursor want = Cursors.Default;
                    if (HandleAt(selected, ip) != null) want = Cursors.SizeAll;
                    else if (HitAt(ip) != null) want = Cursors.SizeAll;
                    if (canvas.Cursor != want) canvas.Cursor = want;
                }
                return;
            }

            if (canvas.Cursor != Cursors.Cross) canvas.Cursor = Cursors.Cross;
            if (dragStart.HasValue)
            {
                dragCur = ip;
                if (tool == "connector") connHover = ConnectableAt(ip);
                canvas.Invalidate();
            }
        }

        private void CanvasMouseUp(object sender, MouseEventArgs e)
        {
            if (tool == "select") { grab = null; return; }
            if (!dragStart.HasValue) return;

            Point s = dragStart.Value;
            dragStart = null;

            Point ip = ToImg(e.Location);
            int ex = ip.X, ey = ip.Y;
            int dx = Math.Abs(ex - s.X), dy = Math.Abs(ey - s.Y);

            if (tool == "crop")
            {
                Rectangle cr = Painter.Norm(s.X, s.Y, ex, ey);
                if (cr.Width >= Theme.S(15) && cr.Height >= Theme.S(15)) ApplyCrop(cr);
                else status.Text = "자르기 취소 : 영역이 너무 작아요";
                canvas.Invalidate();
                return;
            }

            if (tool == "connector")
            {
                Annotation refB = ConnectableAt(new Point(ex, ey));
                if (refB == connStart) refB = null;
                if (dx + dy >= Theme.S(12) || (connStart != null && refB != null))
                {
                    Annotation a = Add("connector", s.X, s.Y, ex, ey);
                    a.RefA = connStart;
                    a.RefB = refB;
                    selected = a;
                    SyncToolbar();
                    status.Text = (a.RefA != null && a.RefB != null)
                        ? "연결했어요. 도형을 옮기면 곡선이 따라와요"
                        : "연결선을 그렸어요. 도형 위에서 시작하고 끝내면 도형을 따라다녀요";
                }
                connStart = null; connHover = null;
                canvas.Invalidate();
                return;
            }

            if (tool == "text" || tool == "textbox")
            {
                // 클릭(또는 드래그가 끝난) 지점이 글 상자의 중심이 된다
                Annotation a = Add(tool, ex, ey, ex, ey);
                BeginEdit(a, true);
            }
            else if (tool == "bubble")
            {
                if (dx + dy < Theme.S(12)) { ex = s.X + Theme.S(130); ey = s.Y - Theme.S(95); }
                Annotation a = Add("bubble", s.X, s.Y, ex, ey);
                BeginEdit(a, true);
            }
            else if (dx >= Theme.S(4) || dy >= Theme.S(4))
            {
                if (SquareConstrain(tool))
                {
                    Point q = Squared(s, new Point(ex, ey));
                    ex = q.X; ey = q.Y;
                }
                else if (AxisConstrain(tool))
                {
                    Point q = AxisSnap(s, new Point(ex, ey));
                    ex = q.X; ey = q.Y;
                }

                if (tool == "flowbox")
                {
                    // 박스를 만들고, 직전 흐름 박스가 있으면 곡선으로 자동 연결한다
                    Annotation nb = Add("box", s.X, s.Y, ex, ey);
                    if (lastFlowBox != null && shapes.Contains(lastFlowBox))
                    {
                        Annotation cn = Add("connector", lastFlowBox.X2, lastFlowBox.Y2, nb.X1, nb.Y1);
                        cn.RefA = lastFlowBox;
                        cn.RefB = nb;
                        status.Text = "이전 박스와 이어졌어요.    계속 그리면 흐름이 이어지고, Esc 를 누르면 새 흐름을 시작해요";
                    }
                    lastFlowBox = nb;
                    selected = nb;
                    SyncToolbar();
                    canvas.Invalidate();
                    return;
                }

                selected = Add(tool, s.X, s.Y, ex, ey);

                // 돋보기는 만들자마자 선택 도구로 넘어가서 바로 위치를 다듬을 수 있다
                if (tool == "zoom")
                {
                    tool = "select";
                    canvas.Cursor = Cursors.Default;
                    status.Text = "돋보기를 끌어서 옮기거나, 모서리 점을 끌어 크기를 바꿔보세요";
                }
                SyncToolbar();
            }
            else if (selected != null)
            {
                // 그리기 도구로 빈 곳을 한 번 누르면 선택만 해제한다
                selected = null;
                SyncToolbar();
                status.Text = "선택을 해제했어요.    선택 도구로 바꾸면 다시 고를 수 있어요";
            }
            canvas.Invalidate();
        }

        private void CanvasDoubleClick(object sender, MouseEventArgs e)
        {
            if (tool != "select") return;
            Annotation hit = HitAt(ToImg(e.Location));
            if (hit != null && Painter.IsTextKind(hit.Kind)) BeginEdit(hit, false);
        }

        private Annotation Add(string kind, int x1, int y1, int x2, int y2)
        {
            Annotation a = new Annotation();
            a.Kind = kind;
            a.X1 = x1; a.Y1 = y1; a.X2 = x2; a.Y2 = y2;
            a.Color = color;
            a.Thickness = thickness;
            a.FontSize = fontSize;
            a.Block = blockSize;
            a.Zoom = zoomFactor;
            a.Outlined = outlined;
            a.Align = textAlign;
            a.Text = "";
            shapes.Add(a);
            Renumber();
            lastActionWasCrop = false;
            return a;
        }

        // --- 자르기 ---

        private void ApplyCrop(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, image.Width, image.Height));
            if (r.Width < 8 || r.Height < 8) return;

            CommitEdit();
            if (prevImage != null) prevImage.Dispose();
            prevImage = image;
            prevOffset = r.Location;
            lastActionWasCrop = true;

            image = (Bitmap)prevImage.Clone(r, prevImage.PixelFormat);
            foreach (Annotation a in shapes)
            {
                a.X1 -= r.X; a.Y1 -= r.Y;
                a.X2 -= r.X; a.Y2 -= r.Y;
                a.DropCache();
            }
            AfterImageChange();
            status.Text = "잘랐어요 (" + image.Width + " x " + image.Height + ").    Ctrl+Z 로 되돌릴 수 있어요";
        }

        private void UndoCrop()
        {
            if (prevImage == null) return;
            Bitmap cur = image;
            image = prevImage;
            prevImage = null;
            cur.Dispose();
            foreach (Annotation a in shapes)
            {
                a.X1 += prevOffset.X; a.Y1 += prevOffset.Y;
                a.X2 += prevOffset.X; a.Y2 += prevOffset.Y;
                a.DropCache();
            }
            lastActionWasCrop = false;
            AfterImageChange();
            status.Text = "자르기를 되돌렸어요";
        }

        private void AfterImageChange()
        {
            Text = "쏙캡처 편집  ·  " + image.Width + " x " + image.Height;
            if (viewBuffer != null) { viewBuffer.Dispose(); viewBuffer = null; }
            canvas.Size = new Size((int)Math.Round(image.Width * viewZoom),
                                   (int)Math.Round(image.Height * viewZoom));
            CenterCanvas();
            canvas.Invalidate();
        }

        // 숫자 박스는 목록 순서대로 1부터 다시 매긴다 (중간을 지우면 뒤가 당겨진다)
        private void Renumber()
        {
            int n = 0;
            foreach (Annotation a in shapes)
                if (a.Kind == "numbox") a.Number = ++n;
        }

        private void Drop(Annotation a)
        {
            if (a == null) return;
            a.DropCache();
            shapes.Remove(a);
            if (selected == a) selected = null;

            // 이 도형에 붙어 있던 연결선도 함께 지운다
            for (int i = shapes.Count - 1; i >= 0; i--)
                if (shapes[i].Kind == "connector" && (shapes[i].RefA == a || shapes[i].RefB == a))
                {
                    if (selected == shapes[i]) selected = null;
                    shapes.RemoveAt(i);
                }
            Renumber();
        }

        private void DeleteSelected()
        {
            if (selected == null) return;
            Drop(selected);
            SyncToolbar();
            canvas.Invalidate();
            status.Text = "선택한 주석을 지웠어요";
        }

        private void Undo()
        {
            if (editing != null) { CancelEdit(); return; }
            if (lastActionWasCrop && prevImage != null) { UndoCrop(); return; }
            if (shapes.Count == 0) { status.Text = "되돌릴 주석이 없어요"; return; }
            Drop(shapes[shapes.Count - 1]);
            SyncToolbar();
            canvas.Invalidate();
            status.Text = "마지막 주석을 지웠어요";
        }

        private void ClearAll()
        {
            CommitEdit();
            if (shapes.Count == 0) return;
            if (MessageBox.Show("주석을 모두 지울까요?", "쏙캡처",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (Annotation a in shapes) a.DropCache();
            shapes.Clear();
            selected = null;
            SyncToolbar();
            canvas.Invalidate();
            status.Text = "주석을 모두 지웠어요";
        }

        // --- 내보내기 ---

        private Bitmap Composite()
        {
            Bitmap inner = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(inner))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(image, 0, 0, image.Width, image.Height);
                foreach (Annotation a in shapes) Painter.Draw(g, a, image);
            }
            if (frameMode == 0) return inner;

            // 배경 프레임 : 솔리드 배경 여백 + 둥근 모서리 (그림자 없음)
            int pad = FramePad();
            int rad = FrameRadius();
            Bitmap outp = new Bitmap(inner.Width + pad * 2, inner.Height + pad * 2, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(outp))
            {
                g.Clear(FrameColor());
                g.SmoothingMode = SmoothingMode.AntiAlias;

                RectangleF ir = new RectangleF(pad, pad, inner.Width, inner.Height);
                using (TextureBrush tb = new TextureBrush(inner))
                {
                    tb.TranslateTransform(pad, pad);
                    using (GraphicsPath ip = Theme.Round(ir, rad))
                        g.FillPath(tb, ip);
                }
            }
            inner.Dispose();
            return outp;
        }

        private void CopyToClipboard()
        {
            CommitEdit();
            using (Bitmap bmp = Composite())
                Clipboard.SetDataObject(bmp, true);
            status.Text = "클립보드에 복사했어요  (" + DateTime.Now.ToString("HH:mm:ss") + ")";
        }

        private void SaveToFile()
        {
            CommitEdit();
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "PNG 이미지|*.png|JPEG 이미지|*.jpg";
                dlg.FileName = "캡처_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                using (Bitmap bmp = Composite())
                {
                    if (dlg.FileName.ToLower().EndsWith(".jpg"))
                        bmp.Save(dlg.FileName, ImageFormat.Jpeg);
                    else
                        bmp.Save(dlg.FileName, ImageFormat.Png);
                }
                status.Text = "저장했어요 : " + dlg.FileName;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (editing != null)
            {
                if (keyData == (Keys.Control | Keys.S)) { SaveToFile(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (keyData == (Keys.Control | Keys.Z)) { Undo(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { CopyToClipboard(); return true; }
            if (keyData == (Keys.Control | Keys.S)) { SaveToFile(); return true; }

            // 화면 확대 축소 : +/- (Ctrl 유무 무관), Ctrl+0 = 100%
            if (keyData == Keys.Oemplus || keyData == Keys.Add ||
                keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Add))
            { SetViewZoom(viewZoom * 1.25f, null); return true; }
            if (keyData == Keys.OemMinus || keyData == Keys.Subtract ||
                keyData == (Keys.Control | Keys.OemMinus) || keyData == (Keys.Control | Keys.Subtract))
            { SetViewZoom(viewZoom / 1.25f, null); return true; }
            if (keyData == (Keys.Control | Keys.D0) || keyData == (Keys.Control | Keys.NumPad0))
            { SetViewZoom(1f, null); return true; }
            if ((keyData == Keys.Delete || keyData == Keys.Back) && selected != null) { DeleteSelected(); return true; }
            if (keyData == Keys.Escape && tool == "flowbox" && lastFlowBox != null)
            {
                lastFlowBox = null;
                selected = null;
                SyncToolbar();
                canvas.Invalidate();
                status.Text = "새 흐름을 시작해요. 다음 박스는 앞의 것과 연결되지 않아요";
                return true;
            }
            if (keyData == Keys.Escape && selected != null) { selected = null; SyncToolbar(); canvas.Invalidate(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitEdit();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Annotation a in shapes) a.DropCache();
                preview.DropCache();
                if (image != null) { image.Dispose(); image = null; }
                if (prevImage != null) { prevImage.Dispose(); prevImage = null; }
                if (viewBuffer != null) { viewBuffer.Dispose(); viewBuffer = null; }
            }
            base.Dispose(disposing);
        }
    }

    // ---------------- 캡처 진행 ----------------

    public static class Shooter
    {
        private static bool busy;

        private static void Run(bool fullScreen, Form extraHide)
        {
            if (busy) return;
            busy = true;
            try
            {
                bool hostVisible = Program.Host != null && Program.Host.Visible;
                bool extraVisible = extraHide != null && extraHide.Visible;

                if (hostVisible) Program.Host.Hide();
                if (extraVisible) extraHide.Hide();
                if (hostVisible || extraVisible)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(220);
                }

                Bitmap shot = null;
                if (fullScreen)
                {
                    Rectangle vs = SystemInformation.VirtualScreen;
                    shot = new Bitmap(vs.Width, vs.Height);
                    using (Graphics g = Graphics.FromImage(shot))
                        g.CopyFromScreen(vs.X, vs.Y, 0, 0, shot.Size);
                }
                else
                {
                    using (RegionForm rf = new RegionForm())
                    {
                        rf.ShowDialog();
                        shot = rf.Result;
                    }
                }

                if (shot != null)
                {
                    // 캡처에 성공하면 런처 창은 트레이로 내려가고 편집 창만 남는다
                    if (extraVisible) extraHide.Show();
                    new EditorForm(shot).Show();
                }
                else
                {
                    // 캡처를 취소했으면 숨겼던 창을 원래대로 되돌린다
                    if (hostVisible) Program.Host.Show();
                    if (extraVisible) extraHide.Show();
                }
            }
            finally { busy = false; }
        }

        public static void Region(Form extraHide) { Run(false, extraHide); }
        public static void Full(Form extraHide) { Run(true, extraHide); }
    }

    // ---------------- 메인 창 ----------------

    public class MainForm : Form
    {
        private const int HotkeyId = 1;
        private bool hotkeyRegistered;
        private bool exitRequested;
        private bool startHidden;
        private bool toldAboutTray;
        private NotifyIcon tray;
        private Icon appIcon;
        private string hotkeyName = "PrtScn";

        // 트레이에서 시작하면 창을 띄우지 않는다
        public void StartInTray() { startHidden = true; }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden) { startHidden = false; value = false; }
            base.SetVisibleCore(value);
        }

        private static Icon MakeAppIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Icons.Draw(g, "selection", new RectangleF(0.5f, 0.5f, 31f, 31f), Theme.Accent);
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                        return (Icon)tmp.Clone();
                }
                finally { Native.DestroyIcon(h); }
            }
        }

        private void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = Theme.Ui;
            menu.Items.Add("영역 캡처  (" + hotkeyName + ")", null, delegate { Shooter.Region(null); });
            menu.Items.Add("전체 화면", null, delegate { Shooter.Full(null); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("쏙캡처 창 보이기", null, delegate { ShowWindow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("종료", null, delegate { exitRequested = true; Close(); });

            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Text = "쏙캡처 v" + App.Version + "  ·  " + hotkeyName + " 로 캡처";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowWindow(); };
            tray.Visible = true;
        }

        public MainForm()
        {
            Text = "쏙캡처";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            TopMost = true;
            BackColor = Theme.Bar;
            Font = Theme.Ui;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;

            FlatButton b1 = new FlatButton("selection", "영역 캡처");
            b1.Vertical = true;
            b1.Fit(28, 16, 82);
            b1.Location = new Point(Theme.S(12), Theme.S(12));
            b1.Activated += delegate { Shooter.Region(null); };
            Controls.Add(b1);

            FlatButton b2 = new FlatButton("frame-corners", "전체 화면");
            b2.Vertical = true;
            b2.Fit(28, 16, 82);
            b2.Location = new Point(b1.Right + Theme.S(8), Theme.S(12));
            b2.Activated += delegate { Shooter.Full(null); };
            Controls.Add(b2);

            Panel divider = new Panel();
            divider.SetBounds(Theme.S(12), Theme.S(103), b2.Right + Theme.S(12) - Theme.S(24), 1);
            divider.BackColor = Theme.Line;
            Controls.Add(divider);

            Label hint = new Label();
            hint.AutoSize = true;
            hint.ForeColor = Theme.Muted;
            hint.Location = new Point(Theme.S(14), Theme.S(114));
            Controls.Add(hint);

            if (Native.RegisterHotKey(Handle, HotkeyId, 0, Native.VK_SNAPSHOT))
            { hotkeyRegistered = true; hotkeyName = "PrtScn"; }
            else if (Native.RegisterHotKey(Handle, HotkeyId, 0, Native.VK_F8))
            { hotkeyRegistered = true; hotkeyName = "F8"; }
            else
            { hotkeyName = null; }
            hint.Text = (hotkeyName != null) ? ("단축키   " + hotkeyName) : "단축키 등록 실패 · 버튼으로 캡처";

            CheckRow auto = new CheckRow("윈도우 시작할 때 자동 실행");
            auto.Checked = AutoRun.Enabled;
            auto.Location = new Point(Theme.S(12), Theme.S(142));
            auto.Toggled += delegate(object s, EventArgs e)
            {
                CheckRow c = (CheckRow)s;
                AutoRun.Set(c.Checked);
            };
            Controls.Add(auto);

            ClientSize = new Size(Math.Max(b2.Right + Theme.S(12), auto.Right + Theme.S(12)), Theme.S(180));

            Label ver = new Label();
            ver.AutoSize = true;
            ver.Text = "v" + App.Version;
            ver.ForeColor = Color.FromArgb(178, 184, 194);
            Size vs = TextRenderer.MeasureText(ver.Text, Theme.Ui);
            ver.Location = new Point(ClientSize.Width - vs.Width - Theme.S(14), Theme.S(114));
            Controls.Add(ver);

            appIcon = MakeAppIcon();
            Icon = appIcon;
            BuildTray();

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - Theme.S(24), wa.Top + Theme.S(20));
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
                Shooter.Region(null);
            else if (m.Msg == (int)Native.WM_SHOW_SSOK)
                ShowWindow();   // 두 번째로 실행된 쏙캡처가 보낸 신호
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 창을 닫으면 종료가 아니라 트레이로 내려간다 (단축키를 계속 쓰기 위해)
            if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!toldAboutTray)
                {
                    toldAboutTray = true;
                    tray.BalloonTipTitle = "쏙캡처는 계속 켜져 있어요";
                    tray.BalloonTipText = (hotkeyName != null)
                        ? (hotkeyName + " 를 누르면 바로 캡처돼요. 완전히 끄려면 트레이 아이콘 우클릭 > 종료.")
                        : "트레이 아이콘을 눌러 캡처하세요. 완전히 끄려면 우클릭 > 종료.";
                    tray.ShowBalloonTip(4000);
                }
                return;
            }

            int open = 0;
            foreach (Form f in Application.OpenForms)
                if (f is EditorForm) open++;

            if (open > 0)
            {
                DialogResult r = MessageBox.Show(
                    "편집 중인 창이 " + open + "개 있어요. 쏙캡처를 종료할까요?",
                    "쏙캡처", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) { e.Cancel = true; exitRequested = false; return; }
            }

            if (hotkeyRegistered) Native.UnregisterHotKey(Handle, HotkeyId);
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            base.OnFormClosing(e);
        }
    }

    // ---------------- 진입점 ----------------

    public static class Program
    {
        public static MainForm Host;

        [STAThread]
        public static void Main(string[] args)
        {
            // 이미 떠 있으면 그쪽 창을 띄우고 조용히 물러난다
            // (두 개가 뜨면 두 번째는 PrtScn 을 못 잡고 F8 로 밀린다)
            bool first;
            using (System.Threading.Mutex mtx = new System.Threading.Mutex(true, "SsokCapture_SingleInstance_v1", out first))
            {
                if (!first)
                {
                    Native.PostMessage(Native.HWND_BROADCAST, Native.WM_SHOW_SSOK, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.Init();

                Host = new MainForm();
                foreach (string a in args)
                    if (string.Equals(a, "-tray", StringComparison.OrdinalIgnoreCase))
                        Host.StartInTray();

                Application.Run(Host);
                GC.KeepAlive(mtx);
            }
        }
    }
}
