using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;

namespace PokemonRebornLauncher.UI;

/// <summary>
/// Adds brief pointer, press and keyboard feedback without changing layout or replacing
/// a control's existing transform. Enable from a style with
/// <c>ui:ReactiveFeedback.Enabled="True"</c>.
/// </summary>
public static class ReactiveFeedback
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ReactiveFeedback), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element || element is not ButtonBase and not Slider)
            return;

        if ((bool)args.NewValue)
        {
            var state = new FeedbackState(element);
            element.SetValue(StatePropertyKey, state);
            element.Loaded += state.OnLoaded;
            element.Unloaded += state.OnUnloaded;
            if (element.IsLoaded)
                state.Attach();
        }
        else if (element.GetValue(StatePropertyKey.DependencyProperty) is FeedbackState state)
        {
            element.Loaded -= state.OnLoaded;
            element.Unloaded -= state.OnUnloaded;
            state.Detach();
            element.ClearValue(StatePropertyKey);
        }
    }

    private static readonly DependencyPropertyKey StatePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "State", typeof(FeedbackState), typeof(ReactiveFeedback), new PropertyMetadata(null));

    private sealed class FeedbackState(FrameworkElement element)
    {
        private readonly FrameworkElement _element = element;
        private Transform? _originalTransform;
        private TransformGroup? _group;
        private TranslateTransform? _lift;
        private ScaleTransform? _scale;
        private Window? _window;
        private bool _attached;
        private bool _pointerDown;
        private bool _keyboardDown;

        public void OnLoaded(object sender, RoutedEventArgs e) => Attach();

        public void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

        public void Attach()
        {
            if (_attached)
                return;

            _attached = true;
            _originalTransform = _element.RenderTransform;
            _lift = new TranslateTransform();
            _scale = new ScaleTransform(1, 1);
            _group = new TransformGroup();
            if (_originalTransform is not null && !_originalTransform.Value.IsIdentity)
                _group.Children.Add(_originalTransform);
            _group.Children.Add(_lift);
            _group.Children.Add(_scale);
            _element.SetCurrentValue(UIElement.RenderTransformProperty, _group);

            _window = Window.GetWindow(_element);
            if (_window is not null)
                TagDescriptor.AddValueChanged(_window, OnWindowTagChanged);

            _element.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseDown), true);
            _element.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseUp), true);
            _element.AddHandler(UIElement.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture), true);
            _element.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown), true);
            _element.AddHandler(UIElement.PreviewKeyUpEvent, new KeyEventHandler(OnKeyUp), true);
            _element.MouseEnter += OnPointerChanged;
            _element.MouseLeave += OnPointerChanged;
            _element.GotKeyboardFocus += OnFocusChanged;
            _element.LostKeyboardFocus += OnLostKeyboardFocus;
            _element.IsEnabledChanged += OnEnabledChanged;
            if (_element is ButtonBase) PressedDescriptor.AddValueChanged(_element, OnPressedChanged);
            Update();
        }

        public void Detach()
        {
            if (!_attached)
                return;

            _attached = false;
            _element.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseDown));
            _element.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseUp));
            _element.RemoveHandler(UIElement.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture));
            _element.RemoveHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown));
            _element.RemoveHandler(UIElement.PreviewKeyUpEvent, new KeyEventHandler(OnKeyUp));
            _element.MouseEnter -= OnPointerChanged;
            _element.MouseLeave -= OnPointerChanged;
            _element.GotKeyboardFocus -= OnFocusChanged;
            _element.LostKeyboardFocus -= OnLostKeyboardFocus;
            _element.IsEnabledChanged -= OnEnabledChanged;
            if (_element is ButtonBase) PressedDescriptor.RemoveValueChanged(_element, OnPressedChanged);
            if (_window is not null)
                TagDescriptor.RemoveValueChanged(_window, OnWindowTagChanged);
            _window = null;
            _pointerDown = false;
            _keyboardDown = false;

            if (_lift is not null)
            {
                _lift.BeginAnimation(TranslateTransform.YProperty, null);
                _lift.Y = 0;
            }
            if (_scale is not null)
            {
                _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _scale.ScaleX = _scale.ScaleY = 1;
            }

            // SetCurrentValue preserves a binding/style expression on RenderTransform.
            if (_group is not null && ReferenceEquals(_element.RenderTransform, _group))
                _element.SetCurrentValue(UIElement.RenderTransformProperty, _originalTransform ?? Transform.Identity);

            _originalTransform = null;
            _group = null;
            _lift = null;
            _scale = null;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _pointerDown = true;
            Update();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _pointerDown = false;
            Update();
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            _pointerDown = false;
            Update();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Space or Key.Enter)
            {
                _keyboardDown = true;
                Update();
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Space or Key.Enter)
            {
                _keyboardDown = false;
                Update();
            }
        }

        private void OnPointerChanged(object sender, MouseEventArgs e) => Update();

        private void OnFocusChanged(object sender, KeyboardFocusChangedEventArgs e) => Update();

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _keyboardDown = false;
            Update();
        }

        private void OnEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                _pointerDown = false;
                _keyboardDown = false;
            }
            Update();
        }

        private void OnPressedChanged(object? sender, EventArgs e) => Update();

        private void OnWindowTagChanged(object? sender, EventArgs e) => Update();

        private void Update()
        {
            if (!_attached || _lift is null || _scale is null)
                return;

            bool enabled = _element.IsEnabled;
            bool pressed = enabled && (_pointerDown || _keyboardDown || _element is ButtonBase { IsPressed: true } || _element is Slider { IsMouseCaptureWithin: true });
            bool hovered = enabled && _element.IsMouseOver;
            bool focused = enabled && _element.IsKeyboardFocused;
            double y = pressed ? 0.5 : hovered ? -2 : 0;
            double scale = pressed ? 0.965 : hovered ? 1.025 : focused ? 1.01 : 1;
            bool reduceMotion = Window.GetWindow(_element)?.Tag is true || !SystemParameters.ClientAreaAnimation;

            Animate(_lift, TranslateTransform.YProperty, y, reduceMotion);
            Animate(_scale, ScaleTransform.ScaleXProperty, scale, reduceMotion);
            Animate(_scale, ScaleTransform.ScaleYProperty, scale, reduceMotion);
        }

        private static void Animate(Animatable target, DependencyProperty property, double value, bool immediate)
        {
            var animation = immediate
                ? null
                : new DoubleAnimation(value, TimeSpan.FromMilliseconds(135))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                };
            target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
            if (immediate)
                target.SetValue(property, value);
        }

        private static readonly DependencyPropertyDescriptor PressedDescriptor =
            DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase))!;

        private static readonly DependencyPropertyDescriptor TagDescriptor =
            DependencyPropertyDescriptor.FromProperty(FrameworkElement.TagProperty, typeof(Window))!;
    }
}
