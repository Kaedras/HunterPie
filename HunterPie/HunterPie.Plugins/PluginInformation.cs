﻿using System;


namespace HunterPie.Plugins
{
    public class PluginInformation
    {
        public string Name;
        public string EntryPoint;
        public string Description;
        public string Author;
        public string Version;
        public string[] Dependencies { get; set; } = Array.Empty<string>();
    }
}
