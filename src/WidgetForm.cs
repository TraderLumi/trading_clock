using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TradingClock
{
    /// <summary>
    /// 窗口外壳：置顶、无边框、拖动、托盘、右键菜单、位置记忆、日/周视图切换。
    /// 具体画面全部交给 WidgetRenderer。
    /// </summary>
    public class WidgetForm : Form
    {
        Config _cfg;
        Timer _timer;
        Snapshot _snap;
        float _scale = 1f;
        bool _dragging;
        Point _dragScreen;
        Point _formAtDrag;
        bool _exit;
        NotifyIcon _tray;
        ContextMenuStrip _menu;
        Icon _icon;
        bool _lastWasButton;

        // 视图状态
        bool _weekView;
        int _dayShift;
        int _weekShift;

        public WidgetForm(string title)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = WidgetRenderer.C_Bg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _cfg = Config.Load();
            TopMost = _cfg.AlwaysOnTop;

            BuildMenu();
            BuildTray();

            _timer = new Timer();
            _timer.Interval = 200;
            _timer.Tick += new EventHandler(OnTick);
            _timer.Start();

            UpdateSnapshot();
        }

        void BuildMenu()
        {
            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;

            ToolStripMenuItem top = new ToolStripMenuItem("窗口置顶");
            top.CheckOnClick = true;
            top.Click += delegate
            {
                _cfg.AlwaysOnTop = top.Checked;
                TopMost = top.Checked;
                _cfg.Save();
            };
            _menu.Items.Add(top);

            ToolStripMenuItem op = new ToolStripMenuItem("不透明度");
            int[] vals = new int[] { 100, 96, 90, 80 };
            for (int i = 0; i < vals.Length; i++)
            {
                int v = vals[i];
                ToolStripMenuItem it = new ToolStripMenuItem(v + "%");
                it.Click += delegate
                {
                    _cfg.Opacity = v;
                    Opacity = v / 100.0;
                    _cfg.Save();
                };
                op.DropDownItems.Add(it);
            }
            _menu.Items.Add(op);

            ToolStripMenuItem auto = new ToolStripMenuItem("开机自启");
            auto.CheckOnClick = true;
            auto.Click += delegate { Config.SetAutostart(auto.Checked); };
            _menu.Items.Add(auto);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem today = new ToolStripMenuItem("回到今天 / 本周");
            today.Click += delegate { GoToday(); };
            _menu.Items.Add(today);

            ToolStripMenuItem toggle = new ToolStripMenuItem("切换 日视图 / 周视图");
            toggle.Click += delegate { SetWeek(!_weekView); };
            _menu.Items.Add(toggle);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem home = new ToolStripMenuItem("回到右上角");
            home.Click += delegate { MoveToDefault(); };
            _menu.Items.Add(home);

            ToolStripMenuItem refresh = new ToolStripMenuItem("立即刷新");
            refresh.Click += delegate { UpdateSnapshot(); Invalidate(); };
            _menu.Items.Add(refresh);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem quit = new ToolStripMenuItem("退出");
            quit.Click += delegate { _exit = true; Close(); };
            _menu.Items.Add(quit);

            _menu.Opening += delegate
            {
                top.Checked = _cfg.AlwaysOnTop;
                auto.Checked = Config.IsAutostartEnabled();
                for (int i = 0; i < op.DropDownItems.Count; i++)
                {
                    ToolStripMenuItem it = op.DropDownItems[i] as ToolStripMenuItem;
                    if (it != null) it.Checked = it.Text == (_cfg.Opacity + "%");
                }
            };

            ContextMenuStrip = _menu;
        }

        void BuildTray()
        {
            _icon = MakeTrayIcon();
            _tray = new NotifyIcon();
            _tray.Icon = _icon;
            _tray.Text = "交易时钟 · SMC";
            _tray.ContextMenuStrip = _menu;
            _tray.DoubleClick += delegate { Visible = !Visible; };
            _tray.Visible = true;
        }

        static Icon MakeTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 197, 94)))
                    g.FillEllipse(b, 1, 1, 30, 30);
                using (Pen p = new Pen(Color.FromArgb(20, 22, 28), 3.2f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, 16, 9, 16, 17);
                    g.DrawLine(p, 16, 17, 22, 20);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        void MoveToDefault()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 40, wa.Top + 60);
            RememberPos();
        }

        void RememberPos()
        {
            _cfg.X = Location.X;
            _cfg.Y = Location.Y;
            _cfg.HasPos = true;
            _cfg.Save();
        }

        // ---------------- 视图操作 ----------------

        void SetWeek(bool week)
        {
            _weekView = week;
            UpdateSnapshot();
            Invalidate();
        }

        void GoToday()
        {
            _dayShift = 0;
            _weekShift = 0;
            UpdateSnapshot();
            Invalidate();
        }

        void Step(int delta)
        {
            if (_weekView)
            {
                _weekShift += delta;
                if (_weekShift > 52) _weekShift = 52;
                if (_weekShift < -52) _weekShift = -52;
            }
            else
            {
                _dayShift += delta;
                if (_dayShift > 365) _dayShift = 365;
                if (_dayShift < -365) _dayShift = -365;
            }
            UpdateSnapshot();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics g = CreateGraphics())
            {
                _scale = g.DpiX / 96f;
                if (_scale < 0.5f) _scale = 1f;
            }
            ClientSize = new Size((int)Math.Round(WidgetRenderer.W * _scale),
                                  (int)Math.Round(WidgetRenderer.H * _scale));
            Opacity = _cfg.Opacity / 100.0;
            ApplyRegion();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_cfg.HasPos)
            {
                Location = new Point(_cfg.X, _cfg.Y);
                EnsureOnScreen();
            }
            else
            {
                MoveToDefault();
            }
        }

        void EnsureOnScreen()
        {
            Rectangle probe = new Rectangle(Location, new Size(60, 60));
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                if (Screen.AllScreens[i].WorkingArea.IntersectsWith(probe)) return;
            }
            MoveToDefault();
        }

        void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath p = WidgetRenderer.RoundedPath(
                new RectangleF(0, 0, Width, Height), 12f * _scale))
            {
                Region old = Region;
                Region = new Region(p);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (!Visible) return;
            UpdateSnapshot();
            Invalidate();
        }

        void UpdateSnapshot()
        {
            _snap = TimeEngine.Compute(DateTime.UtcNow, _weekView, _dayShift, _weekShift);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_snap == null) return;
            WidgetRenderer.Render(e.Graphics, _snap, _scale);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Visible = false;
                return;
            }
            RememberPos();
            _cfg.AlwaysOnTop = TopMost;
            _cfg.Save();
            _timer.Stop();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null) _timer.Dispose();
                if (_menu != null) _menu.Dispose();
                if (_icon != null) _icon.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---------------- 交互 ----------------

        PointF ToLogical(Point p)
        {
            return new PointF(p.X / _scale, p.Y / _scale);
        }

        static bool InRect(PointF p, RectangleF r)
        {
            return p.X >= r.X && p.X <= r.Right && p.Y >= r.Y && p.Y <= r.Bottom;
        }

        /// <summary>命中按钮就返回 true，并完成动作</summary>
        bool HandleButton(PointF p)
        {
            if (InRect(p, WidgetRenderer.BtnDay)) { SetWeek(false); return true; }
            if (InRect(p, WidgetRenderer.BtnWeek)) { SetWeek(true); return true; }
            if (InRect(p, WidgetRenderer.BtnPrev)) { Step(-1); return true; }
            if (InRect(p, WidgetRenderer.BtnNext)) { Step(1); return true; }
            if (InRect(p, WidgetRenderer.BtnToday)) { GoToday(); return true; }
            return false;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _lastWasButton = false;
            if (e.Button == MouseButtons.Left)
            {
                if (HandleButton(ToLogical(e.Location)))
                {
                    _lastWasButton = true;
                }
                else
                {
                    _dragging = true;
                    _dragScreen = Cursor.Position;
                    _formAtDrag = Location;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                Point now = Cursor.Position;
                Location = new Point(_formAtDrag.X + (now.X - _dragScreen.X),
                                     _formAtDrag.Y + (now.Y - _dragScreen.Y));
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                RememberPos();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Step(e.Delta > 0 ? -1 : 1);
            base.OnMouseWheel(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            if (_lastWasButton) return;   // 双击按钮不要顺带切置顶
            TopMost = !TopMost;
            _cfg.AlwaysOnTop = TopMost;
            _cfg.Save();
            base.OnDoubleClick(e);
        }
    }
}
