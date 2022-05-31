﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RT.Serialization;
using RT.Util;

namespace Clowd
{
    public enum ToolType
    {
        None,
        Pointer,
        Rectangle,
        FilledRectangle,
        Ellipse,
        Line,
        Arrow,
        PolyLine,
        Text,
        //Pixelate,
        //Erase,
        Max
    };

    namespace Config
    {
        public class SavedToolSettings : SimpleNotifyObject
        {
            public bool TextObjectColorIsAuto
            {
                get => _textObjectColorIsAuto;
                set => Set(ref _textObjectColorIsAuto, value);
            }

            public Color ObjectColor
            {
                get => _objectColor;
                set => Set(ref _objectColor, value);
            }

            public double LineWidth
            {
                get => _lineWidth;
                set => Set(ref _lineWidth, value);
            }

            public string FontFamily
            {
                get => _fontFamily;
                set => Set(ref _fontFamily, value);
            }

            public double FontSize
            {
                get => _fontSize;
                set => Set(ref _fontSize, value);
            }

            public FontStyle FontStyle
            {
                get => _fontStyle;
                set => Set(ref _fontStyle, value);
            }

            public FontWeight FontWeight
            {
                get => _fontWeight;
                set => Set(ref _fontWeight, value);
            }

            public FontStretch FontStretch
            {
                get => _fontStretch;
                set => Set(ref _fontStretch, value);
            }

            private FontStretch _fontStretch = FontStretches.Normal;
            private FontWeight _fontWeight = FontWeights.Normal;
            private FontStyle _fontStyle = FontStyles.Normal;
            private double _fontSize = 12d;
            private string _fontFamily = "Segoe UI";
            private double _lineWidth = 2d;
            private Color _objectColor = Colors.Red;
            private bool _textObjectColorIsAuto = true;
        }

        public class SettingsEditor : CategoryBase
        {
            public Color CanvasBackground
            {
                get => _canvasBackground;
                set => Set(ref _canvasBackground, value);
            }

            [Browsable(false)]
            public bool TabsEnabled
            {
                get => _tabsEnabled;
                set => Set(ref _tabsEnabled, value);
            }

            [Browsable(false)]
            public bool AskBeforeClosingMultipleTabs
            {
                get => _askBeforeClosingMultipleTabs;
                set => Set(ref _askBeforeClosingMultipleTabs, value);
            }

            [Browsable(false)]
            public int StartupPadding
            {
                get => _startupPadding;
                set => Set(ref _startupPadding, value);
            }

            [DisplayName("Tool preferences")]
            public AutoDictionary<ToolType, SavedToolSettings> Tools
            {
                get => _tools;
                set => Set(ref _tools, value);
            }

            public TimeOption DeleteSessionsAfter
            {
                get => _deleteSessionsAfter;
                set => Set(ref _deleteSessionsAfter, value);
            }

            private Color _canvasBackground = Colors.Transparent;
            private bool _tabsEnabled = true;
            private bool _askBeforeClosingMultipleTabs = true;
            private int _startupPadding = 30;
            private TimeOption _deleteSessionsAfter = new TimeOption(30, TimeOptionUnit.Days);
            private AutoDictionary<ToolType, SavedToolSettings> _tools = new AutoDictionary<ToolType, SavedToolSettings>();

            public SettingsEditor()
            {
                Subscribe(Tools);
            }
        }
    }
}
