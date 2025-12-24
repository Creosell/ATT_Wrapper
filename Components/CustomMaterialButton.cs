using MaterialSkin;
using MaterialSkin.Controls;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ATT_Wrapper.Components
    {
    public class CustomMaterialButton : MaterialButton
        {
        // Доступ к приватному полю "text" базового класса, чтобы временно стирать его
        private static readonly FieldInfo _textField = typeof(Control).GetField("text", BindingFlags.Instance | BindingFlags.NonPublic);

        public CustomMaterialButton()
            {
            // Отключаем стандартный AutoSize, мы будем управлять размерами через FlowPanel
            this.AutoSize = false;
            this.Size = new Size(100, 50); // Дефолтный размер
            }

        protected override void OnPaint(PaintEventArgs pevent)
            {
            // 1. ХАК: Временно удаляем текст, чтобы базовый метод OnPaint нарисовал 
            // только фон и эффекты, но не нарисовал стандартный однострочный текст.
            string originalText = this.Text;
            _textField.SetValue(this, string.Empty);

            // Вызываем отрисовку фона MaterialButton
            base.OnPaint(pevent);

            // Возвращаем текст обратно
            _textField.SetValue(this, originalText);

            // 2. РИСУЕМ ТЕКСТ САМИ
            Graphics g = pevent.Graphics;

            // Настройки: перенос слов, левое выравнивание, центрирование по вертикали
            TextFormatFlags flags = TextFormatFlags.WordBreak |
                                    TextFormatFlags.Left |
                                    TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.TextBoxControl;

            // Отступы: 16px слева и справа (стандарт Material), 6px сверху/снизу
            Rectangle textRect = new Rectangle(
                16,
                6,
                this.Width - 32,
                this.Height - 12
            );

            // Определяем цвет текста. 
            // Если кнопка залитая (Contained) - текст обычно белый или HighEmphasis.
            // Если кнопка выключена - серый.
            Color textColor = !this.Enabled ? Color.Gray :
                              ( this.Type == MaterialButtonType.Contained ? Color.White : MaterialSkinManager.Instance.ColorScheme.PrimaryColor );

            // Рисуем
            TextRenderer.DrawText(g, originalText, this.Font, textRect, textColor, flags);
            }

        /// <summary>
        /// Этот метод позволяет системе узнать, какую высоту хочет кнопка
        /// при заданной ширине (proposedSize.Width).
        /// </summary>
        public override Size GetPreferredSize(Size proposedSize)
            {
            // Считаем высоту текста
            Size textSize = TextRenderer.MeasureText(
                this.Text,
                this.Font,
                new Size(proposedSize.Width - 32, 0), // Учитываем паддинги
                TextFormatFlags.WordBreak | TextFormatFlags.Left
            );

            // Возвращаем: Ширина как предложили, Высота = текст + 24px (отступы)
            // Минимальная высота 50px
            return new Size(proposedSize.Width, Math.Max(50, textSize.Height + 24));
            }
        }
    }