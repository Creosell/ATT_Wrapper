using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATT_Wrapper.UI.Controls
    {

    /// <summary>
    /// Кастомный элемент статус-бара, рисующий круглую анимацию загрузки
    /// </summary>
    public class ToolStripLoadingSpinner : System.Windows.Forms.ToolStripStatusLabel
        {
        private readonly Timer _animTimer;
        private int _currentAngle = 0;
        private readonly int _sweepAngle = 280; // Длина дуги
        private readonly int _rotationSpeed = 15;

        // Цвет спиннера (можно менять)
        public Color SpinnerColor { get; set; } = Color.FromArgb(63, 81, 181); // Material Blue

        public ToolStripLoadingSpinner()
            {
            // Настройка таймера анимации
            _animTimer = new Timer
                {
                Interval = 30 // 30мс = плавная анимация
                };
            _animTimer.Tick += (s, e) =>
            {
                _currentAngle = ( _currentAngle + _rotationSpeed ) % 360;
                this.Invalidate(); // Перерисовка
            };

            this.Size = new Size(24, 24); // Размер спиннера
            this.AutoSize = false;
            }

        // Управление видимостью автоматически включает/выключает таймер
        protected override void OnVisibleChanged(EventArgs e)
            {
            base.OnVisibleChanged(e);
            if (this.Visible && !this.DesignMode)
                _animTimer.Start();
            else
                _animTimer.Stop();
            }

        protected override void OnPaint(PaintEventArgs e)
            {
            // Если скрыт — не рисуем
            if (!this.Visible) return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            // Вычисляем размеры круга с отступами
            int padding = 4;
            Rectangle rect = new Rectangle(
                this.ContentRectangle.X + padding,
                this.ContentRectangle.Y + padding,
                this.ContentRectangle.Height - ( padding * 2 ),
                this.ContentRectangle.Height - ( padding * 2 ));

            // Рисуем дугу
            using (Pen pen = new Pen(SpinnerColor, 2.5f))
                {
                // Pen.StartCap/EndCap делают концы линии закругленными
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                e.Graphics.DrawArc(pen, rect, _currentAngle, _sweepAngle);
                }
            }

        // Очистка ресурсов
        protected override void Dispose(bool disposing)
            {
            if (disposing) _animTimer?.Dispose();
            base.Dispose(disposing);
            }
        }
    }
