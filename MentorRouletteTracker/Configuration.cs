using System;
using Dalamud.Configuration;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker;

[Serializable]
public sealed class Configuration : Settings, IPluginConfiguration;
