﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HunterPie.Core;
using Timer = System.Threading.Timer;

namespace HunterPie.GUI.Widgets.Monster_Widget.Parts
{
    /// <summary>
    /// Interaction logic for MonsterAilment.xaml
    /// </summary>
    public partial class MonsterAilment : UserControl
    {
        Ailment Context;
        Timer VisibilityTimer;
        bool IsAilmentGroupEnabled;



        public Brush AilmentGroupColor
        {
            get { return (Brush)GetValue(AilmentGroupColorProperty); }
            set { SetValue(AilmentGroupColorProperty, value); }
        }

        public static readonly DependencyProperty AilmentGroupColorProperty =
            DependencyProperty.Register("AilmentGroupColor", typeof(Brush), typeof(MonsterAilment));



        public MonsterAilment() => InitializeComponent();

        public void SetContext(Ailment ctx, double MaxBarSize)
        {
            Context = ctx;
            IsAilmentGroupEnabled = UserSettings.PlayerConfig.Overlay.MonstersComponent.EnabledAilmentGroups.Contains(Context.Group);
            AilmentGroupColor = FindResource($"MONSTER_AILMENT_COLOR_{Context.Group}") as Brush;
            SetAilmentInformation(MaxBarSize);
            HookEvents();
            StartVisibilityTimer();
        }

        private void HookEvents()
        {
            Context.OnBuildupChange += OnBuildupChange;
            Context.OnDurationChange += OnDurationChange;
            Context.OnCounterChange += OnCounterChange;
        }

        public void UnhookEvents()
        {
            Context.OnBuildupChange -= OnBuildupChange;
            Context.OnDurationChange -= OnDurationChange;
            Context.OnCounterChange -= OnCounterChange;
            VisibilityTimer?.Dispose();
            Context = null;
        }

        private void Dispatch(Action f) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, f);

        #region Settings
        public void ApplySettings()
        {

            System.Windows.Visibility visibility;
            if (UserSettings.PlayerConfig.Overlay.MonstersComponent.EnableMonsterAilments &&
                UserSettings.PlayerConfig.Overlay.MonstersComponent.EnabledAilmentGroups.Contains(Context.Group))
            {
                visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                visibility = System.Windows.Visibility.Collapsed;
            }
            if (UserSettings.PlayerConfig.Overlay.MonstersComponent.HidePartsAfterSeconds) visibility = System.Windows.Visibility.Collapsed;
            Dispatch(() => { Visibility = visibility; });
        }

        #endregion

        #region Visibility timer
        private void StartVisibilityTimer()
        {
            if (!UserSettings.PlayerConfig.Overlay.MonstersComponent.HidePartsAfterSeconds)
            {
                ApplySettings();
                return;
            }
            if (VisibilityTimer == null)
            {
                VisibilityTimer = new Timer(_ => HideUnactiveBar(), null, 10, 0);
            }
            else
            {
                VisibilityTimer.Change(UserSettings.PlayerConfig.Overlay.MonstersComponent.SecondsToHideParts * 1000, 0);
            }
        }

        private void HideUnactiveBar() => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            Visibility = System.Windows.Visibility.Collapsed;
        }));

        #endregion

        private void SetAilmentInformation(double NewSize)
        {
            AilmentName.Text = $"{Context.Name}";
            AilmentBar.MaxSize = NewSize - 37;
            AilmentCounter.Text = $"{Context.Counter}";
            if (Context.Duration > 0)
            {
                AilmentBar.MaxValue = Math.Max(1, Context.MaxDuration);
                AilmentBar.Value = Math.Max(0, Context.Duration);
            }
            else
            {
                AilmentBar.MaxValue = Math.Max(1, Context.MaxBuildup);
                AilmentBar.Value = Math.Max(0, Context.MaxBuildup - Context.Buildup);
            }
            AilmentText.Text = $"{AilmentBar.Value:0}/{AilmentBar.MaxValue:0}";
        }

        private void OnCounterChange(object source, MonsterAilmentEventArgs args)
        {
            System.Windows.Visibility visibility;
            if (UserSettings.PlayerConfig.Overlay.MonstersComponent.EnableMonsterAilments)
            {
                visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                visibility = System.Windows.Visibility.Collapsed;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                AilmentCounter.Text = args.Counter.ToString();
                Visibility = visibility;
                StartVisibilityTimer();
            }));
        }

        private void OnDurationChange(object source, MonsterAilmentEventArgs args)
        {
            if (args.MaxDuration <= 0) { return; }
            System.Windows.Visibility visibility;
            if (UserSettings.PlayerConfig.Overlay.MonstersComponent.EnableMonsterAilments && IsAilmentGroupEnabled)
            {
                visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                visibility = System.Windows.Visibility.Collapsed;
            }
            if (args.Duration > 0)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
                {
                    AilmentBar.MaxValue = args.MaxDuration;
                    AilmentBar.Value = Math.Max(0, args.MaxDuration - args.Duration);
                    AilmentText.Text = $"{AilmentBar.Value:0}/{AilmentBar.MaxValue:0}";
                    Visibility = visibility;
                    StartVisibilityTimer();
                }));
            }
            else { OnBuildupChange(source, args); } //ensures we switch back to showing buildup
        }

        private void OnBuildupChange(object source, MonsterAilmentEventArgs args)
        {
            if (args.MaxBuildup <= 0) { return; }
            System.Windows.Visibility visibility;
            if (UserSettings.PlayerConfig.Overlay.MonstersComponent.EnableMonsterAilments && IsAilmentGroupEnabled)
            {
                visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                visibility = System.Windows.Visibility.Collapsed;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                AilmentBar.MaxValue = Math.Max(1, args.MaxBuildup);
                // Get the min between them so the buildup doesnt overflow
                AilmentBar.Value = Math.Min(args.Buildup, args.MaxBuildup);
                AilmentText.Text = $"{AilmentBar.Value:0}/{AilmentBar.MaxValue:0}";
                Visibility = visibility;
                StartVisibilityTimer();
            }));
        }

        public void UpdateBarSize(double NewSize)
        {
            if (Context == null) return;
            AilmentBar.MaxSize = NewSize - 37;
            if (Context.Duration > 0)
            {
                AilmentBar.MaxValue = Math.Max(1, Context.MaxDuration);
                AilmentBar.Value = Math.Max(0, Context.Duration);
            }
            else
            {
                AilmentBar.MaxValue = Math.Max(1, Context.MaxBuildup);
                AilmentBar.Value = Math.Max(0, Context.MaxBuildup - Context.Buildup);
            }
            AilmentText.Text = $"{AilmentBar.Value:0}/{AilmentBar.MaxValue:0}";
        }

        public void UpdateSize(double NewSize)
        {
            AilmentBar.MaxSize = NewSize - 37;
            AilmentBar.MaxValue = AilmentBar.MaxValue;
            AilmentBar.Value = AilmentBar.Value;
        }
    }
}
