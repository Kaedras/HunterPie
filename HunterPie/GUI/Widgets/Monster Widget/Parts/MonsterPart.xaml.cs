﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HunterPie.Core;
using HunterPie.Core.Events;
using Timer = System.Threading.Timer;

namespace HunterPie.GUI.Widgets.Monster_Widget.Parts
{
    /// <summary>
    /// Interaction logic for MonsterPart.xaml
    /// </summary>
    public partial class MonsterPart : UserControl
    {
        private Part context;
        private Timer visibilityTimer;

        public MonsterPart() => InitializeComponent();

        private static UserSettings.Config.Monsterscomponent ComponentSettings =>
            UserSettings.PlayerConfig.Overlay.MonstersComponent;

        public void SetContext(Part ctx, double MaxHealthBarSize)
        {
            context = ctx;
            SetPartInformation(MaxHealthBarSize);
            HookEvents();
            StartVisibilityTimer();
        }

        private void HookEvents()
        {
            context.OnHealthChange += OnHealthChange;
            context.OnBrokenCounterChange += OnBrokenCounterChange;
            context.OnTenderizeStateChange += OnTenderizeStateChange;
        }

        public void UnhookEvents()
        {
            context.OnHealthChange -= OnHealthChange;
            context.OnBrokenCounterChange -= OnBrokenCounterChange;
            context.OnTenderizeStateChange -= OnTenderizeStateChange;
            visibilityTimer?.Dispose();
            context = null;
        }

        private void Dispatch(Action f) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, f);

        #region Visibility timer
        private void StartVisibilityTimer()
        {
            if (!ComponentSettings.HidePartsAfterSeconds)
            {
                ApplySettings();
                return;
            }
            if (visibilityTimer == null)
            {
                visibilityTimer = new Timer(_ => HideInactiveBar(), null, 10, 0);
            }
            else
            {
                visibilityTimer.Change(ComponentSettings.SecondsToHideParts * 1000, 0);
            }
        }

        private void HideInactiveBar() => Dispatch(() =>
        {
            Visibility = Visibility.Collapsed;
        });

        #endregion

        #region Settings
        public void ApplySettings()
        {
            Visibility visibility = GetVisibility();
            if (ComponentSettings.HidePartsAfterSeconds) visibility = Visibility.Collapsed;
            Dispatch(() => { Visibility = visibility; UpdateHealthText(); });
        }

        private Visibility GetVisibility()
        {
            // Hide invalid parts, like the Vaal Hazaak unknown ones
            if (float.IsNaN(context.TotalHealth)) return Visibility.Collapsed;

            if (context.IsRemovable)
            {
                return ComponentSettings.EnableRemovableParts ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ComponentSettings.EnableMonsterParts && ComponentSettings.EnabledPartGroups.Contains(context.Group))
            {
                return Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        #endregion

        #region Events

        private void SetPartInformation(double newSize)
        {
            PartName.Text = $"{context.Name}";
            UpdatePartBrokenCounter();
            UpdateHealthSize(newSize);
            UpdateHealthText();
            ApplySettings();
        }

        private void OnBrokenCounterChange(object source, MonsterPartEventArgs args)
        {
            Visibility visibility = GetVisibility();
            Dispatch(() =>
            {
                UpdatePartBrokenCounter();
                Visibility = visibility;
                StartVisibilityTimer();
            });
        }

        private void OnTenderizeStateChange(object source, MonsterPartEventArgs args)
        {
            Visibility visibility = args.Duration > 0 ? Visibility.Visible : Visibility.Collapsed;
            Dispatch(() =>
            {
                TenderizeBar.Value = args.MaxDuration - args.Duration;
                TenderizeBar.MaxValue = args.MaxDuration;
                TenderizeBar.Visibility = visibility;
                Visibility = GetVisibility();
                StartVisibilityTimer();
            });
        }

        private void OnHealthChange(object source, MonsterPartEventArgs args)
        {
            Visibility visibility = GetVisibility();
            Dispatch(() =>
            {
                UpdateHealthText();
                Visibility = visibility;
                StartVisibilityTimer();
            });
        }

        public void UpdateHealthBarSize(double newSize)
        {
            if (context == null) return;
            UpdateHealthSize(newSize);
            UpdateHealthText();
            ApplySettings();
        }

        private void UpdatePartBrokenCounter()
        {
            string suffix = "";
            for (int i = context.BreakThresholds.Length - 1; i >= 0; i--)
            {
                int threshold = context.BreakThresholds[i];
                if (context.BrokenCounter < threshold || i == context.BreakThresholds.Length - 1)
                {
                    suffix = $"/{threshold}";
                    if (i < context.BreakThresholds.Length - 1)
                    {
                        suffix += "+";
                    }
                }
            }
            PartBrokenCounter.Text = $"{context.BrokenCounter}{suffix}";
        }

        public void UpdateHealthSize(double newSize)
        {
            PartHealth.MaxSize = newSize - 37;
            TenderizeBar.MaxSize = newSize - 37;
            PartHealth.MaxValue = context.TotalHealth;
            PartHealth.Value = context.Health;
        }

        private void UpdateHealthText()
        {
            PartHealth.MaxValue = context.TotalHealth;
            PartHealth.Value = context.Health;
            double percentage = PartHealth.Value / Math.Max(1, PartHealth.MaxValue);
            string format = UserSettings.PlayerConfig.Overlay.MonstersComponent.PartTextFormat;
            PartHealthText.Text = format.Replace("{Current}", $"{PartHealth.Value:0}")
                .Replace("{Max}", $"{PartHealth.MaxValue:0}")
                .Replace("{Percentage}", $"{percentage * 100:0}");
        }

        #endregion
    }
}
